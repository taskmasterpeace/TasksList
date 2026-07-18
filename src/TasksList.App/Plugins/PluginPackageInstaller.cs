using System.IO;
using System.IO.Compression;
using TasksList.PluginSdk;

namespace TasksList.App.Plugins;

public static class PluginPackageInstaller
{
    public static PluginCatalogEntry Install(string packagePath, string pluginsRoot)
    {
        pluginsRoot = Path.GetFullPath(pluginsRoot);
        Directory.CreateDirectory(pluginsRoot);
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.Entries.SingleOrDefault(entry =>
            string.Equals(entry.FullName, "plugin.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The package has no root plugin.json manifest.");
        string manifestJson;
        using (var reader = new StreamReader(manifestEntry.Open()))
        {
            manifestJson = reader.ReadToEnd();
        }

        var manifest = PluginManifest.Parse(manifestJson);
        PluginManifestValidator.Validate(manifest, 1);
        var temporaryRoot = Path.Combine(pluginsRoot, $".installing-{Guid.NewGuid():N}");
        var targetRoot = Path.GetFullPath(Path.Combine(pluginsRoot, manifest.Id, manifest.Version));
        EnsureInsideRoot(targetRoot, pluginsRoot);

        foreach (var entry in archive.Entries)
        {
            var prospective = Path.GetFullPath(Path.Combine(temporaryRoot, entry.FullName));
            EnsureInsideRoot(prospective, temporaryRoot);
        }

        try
        {
            Directory.CreateDirectory(temporaryRoot);
            archive.ExtractToDirectory(temporaryRoot);
            var entryPointPath = Path.GetFullPath(Path.Combine(temporaryRoot, manifest.EntryPoint));
            EnsureInsideRoot(entryPointPath, temporaryRoot);
            if (!File.Exists(entryPointPath))
            {
                throw new InvalidDataException("The declared plugin entry point is missing from the package.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)
                ?? throw new InvalidOperationException("Plugin target has no parent directory."));
            if (Directory.Exists(targetRoot))
            {
                Directory.Delete(targetRoot, true);
            }
            Directory.Move(temporaryRoot, targetRoot);
            return new PluginCatalogEntry(manifest, targetRoot);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }
        }
    }

    private static void EnsureInsideRoot(string candidate, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The plugin package contains a path outside its installation directory.");
        }
    }
}
