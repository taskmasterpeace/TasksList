using System.Security.Cryptography;
using System.Text.Json;
using TasksList.Core.Notes;
using TasksList.Infrastructure.Storage;
using TasksList.PluginSdk;

var options = ParseArguments(args);
var installRoot = FullPath(options, "install-root");
var dataRoot = options.TryGetValue("data-root", out var configuredDataRoot)
    ? Path.GetFullPath(configuredDataRoot)
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TasksList");
var failures = new List<string>();

var executable = Path.Combine(installRoot, "TasksList.App.exe");
RequireFile(executable, "installed executable", failures);
RequireFile(Path.Combine(installRoot, "themes", "default", "theme.json"), "default theme", failures);
RequireFile(Path.Combine(installRoot, "browser-extension", "manifest.json"), "browser companion", failures);

var pluginRoot = Path.Combine(installRoot, "plugins");
var plugins = new List<PluginManifest>();
if (Directory.Exists(pluginRoot))
{
    foreach (var manifestPath in Directory.EnumerateFiles(pluginRoot, "plugin.json", SearchOption.AllDirectories))
    {
        try
        {
            var manifest = PluginManifest.Parse(await File.ReadAllTextAsync(manifestPath));
            PluginManifestValidator.Validate(manifest, 1);
            plugins.Add(manifest);
            RequireFile(
                Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.EntryPoint),
                $"plugin entry point for {manifest.Name}",
                failures);
        }
        catch (Exception exception)
        {
            failures.Add($"Invalid plugin manifest {manifestPath}: {exception.Message}");
        }
    }
}
var minimumPlugins = IntOption(options, "minimum-plugins", 3);
if (plugins.Count < minimumPlugins)
{
    failures.Add($"Expected at least {minimumPlugins} valid installed plugins; found {plugins.Count}.");
}

var databasePath = Path.Combine(dataRoot, "taskslist.db");
RequireFile(databasePath, "installed data database", failures);
var noteCount = 0;
var captureCount = 0;
var favoriteCaptureCount = 0;
InstalledNoteResult? expectedNote = null;
if (File.Exists(databasePath))
{
    var database = new TasksListDatabase(databasePath);
    await database.InitializeAsync();
    var notes = await database.ListNotesAsync();
    var captures = await database.ListCapturesAsync();
    noteCount = notes.Count;
    captureCount = captures.Count;
    favoriteCaptureCount = captures.Count(capture => capture.IsFavorite);

    if (options.TryGetValue("expect-title", out var expectedTitle))
    {
        var note = notes.FirstOrDefault(item => string.Equals(item.Title, expectedTitle, StringComparison.Ordinal));
        if (note is null)
        {
            failures.Add($"Expected installed note '{expectedTitle}' was not found.");
        }
        else
        {
            var presentation = await database.GetNotePresentationAsync(note.Id);
            expectedNote = new InstalledNoteResult(
                note.Title,
                presentation.Bounds.Left,
                presentation.Bounds.Top,
                presentation.Bounds.Width,
                presentation.Bounds.Height,
                presentation.Preset.ToString(),
                presentation.ActiveOpacity,
                presentation.Topmost,
                presentation.Rolled,
                presentation.Locked,
                presentation.Ghost,
                presentation.EditorMode.ToString());
            if (options.TryGetValue("expect-preset", out var preset) &&
                !string.Equals(expectedNote.Preset, preset, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Expected preset {preset}; found {expectedNote.Preset}.");
            }
            if (options.TryGetValue("expect-opacity", out var opacityText) &&
                double.TryParse(opacityText, out var opacity) &&
                Math.Abs(expectedNote.ActiveOpacity - opacity) > 0.001)
            {
                failures.Add($"Expected opacity {opacity:0.00}; found {expectedNote.ActiveOpacity:0.00}.");
            }
            ValidateBoolean(options, "expect-topmost", expectedNote.Topmost, failures);
            ValidateBoolean(options, "expect-rolled", expectedNote.Rolled, failures);
        }
    }
    if (options.ContainsKey("require-favorite-capture") && favoriteCaptureCount == 0)
    {
        failures.Add("Expected at least one favorite clipboard capture.");
    }
}

var report = new InstalledProbeReport(
    installRoot,
    dataRoot,
    File.Exists(executable) ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(executable))).ToLowerInvariant() : string.Empty,
    plugins.Select(plugin => $"{plugin.Name} {plugin.Version}").Order().ToArray(),
    noteCount,
    captureCount,
    favoriteCaptureCount,
    expectedNote,
    failures);
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return failures.Count == 0 ? 0 : 1;

static Dictionary<string, string> ParseArguments(string[] arguments)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        if (!argument.StartsWith("--", StringComparison.Ordinal)) continue;
        var key = argument[2..];
        var value = index + 1 < arguments.Length && !arguments[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? arguments[++index]
            : "true";
        result[key] = value;
    }
    return result;
}

static string FullPath(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? Path.GetFullPath(value)
        : throw new ArgumentException($"Missing required --{key} argument.");

static int IntOption(IReadOnlyDictionary<string, string> options, string key, int fallback) =>
    options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

static void RequireFile(string path, string label, ICollection<string> failures)
{
    if (!File.Exists(path)) failures.Add($"Missing {label}: {path}");
}

static void ValidateBoolean(
    IReadOnlyDictionary<string, string> options,
    string key,
    bool actual,
    ICollection<string> failures)
{
    if (options.TryGetValue(key, out var text) && bool.TryParse(text, out var expected) && actual != expected)
    {
        failures.Add($"Expected {key[7..]} {expected}; found {actual}.");
    }
}

internal sealed record InstalledNoteResult(
    string Title,
    double Left,
    double Top,
    double Width,
    double Height,
    string Preset,
    double ActiveOpacity,
    bool Topmost,
    bool Rolled,
    bool Locked,
    bool Ghost,
    string EditorMode);

internal sealed record InstalledProbeReport(
    string InstallRoot,
    string DataRoot,
    string ExecutableSha256,
    IReadOnlyList<string> Plugins,
    int NoteCount,
    int CaptureCount,
    int FavoriteCaptureCount,
    InstalledNoteResult? ExpectedNote,
    IReadOnlyList<string> Failures);
