using System.IO;
using TasksList.PluginSdk;

namespace TasksList.App.Plugins;

public sealed record PluginCatalogEntry(PluginManifest Manifest, string DirectoryPath);

public sealed record PluginCatalogError(string ManifestPath, string Message);

public sealed record PluginCatalogSnapshot(
    IReadOnlyList<PluginCatalogEntry> Plugins,
    IReadOnlyList<PluginCatalogError> Errors);

public static class PluginCatalog
{
    public static PluginCatalogSnapshot Load(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return new PluginCatalogSnapshot([], []);
        }

        var plugins = new List<PluginCatalogEntry>();
        var errors = new List<PluginCatalogError>();
        foreach (var manifestPath in Directory.EnumerateFiles(rootDirectory, "plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                var manifest = PluginManifest.Parse(File.ReadAllText(manifestPath));
                PluginManifestValidator.Validate(manifest, supportedApiVersion: 1);
                plugins.Add(new PluginCatalogEntry(
                    manifest,
                    Path.GetDirectoryName(manifestPath) ?? rootDirectory));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
            {
                errors.Add(new PluginCatalogError(manifestPath, exception.Message));
            }
        }

        return new PluginCatalogSnapshot(
            plugins.OrderBy(plugin => plugin.Manifest.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            errors);
    }
}
