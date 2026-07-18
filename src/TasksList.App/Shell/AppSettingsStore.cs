using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TasksList.App.Shell;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public AppSettingsStore(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return AppSettings.Default;
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(_path),
                JsonOptions);
            return Normalize(settings ?? AppSettings.Default);
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
        catch (IOException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var defaults = AppSettings.Default;
        var hotkeys = settings.Hotkeys is { Count: > 0 }
            ? new Dictionary<AppHotkeyAction, HotkeyGesture>(settings.Hotkeys)
            : new Dictionary<AppHotkeyAction, HotkeyGesture>(defaults.Hotkeys);
        if (!hotkeys.ContainsKey(AppHotkeyAction.DisableGhostMode))
        {
            hotkeys[AppHotkeyAction.DisableGhostMode] =
                defaults.Hotkeys[AppHotkeyAction.DisableGhostMode];
        }

        return settings with
        {
            SnapTolerance = Math.Clamp(settings.SnapTolerance, 0, 40),
            ExcludedClipboardApplications = (settings.ExcludedClipboardApplications ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Hotkeys = hotkeys,
        };
    }
}
