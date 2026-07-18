using TasksList.App.Plugins;

namespace TasksList.App.Tests.Plugins;

public sealed class PluginCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"taskslist-plugin-catalog-{Guid.NewGuid():N}");

    public PluginCatalogTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void CatalogLoadsValidPluginsAndQuarantinesInvalidManifests()
    {
        var validDirectory = Directory.CreateDirectory(Path.Combine(_directory, "valid"));
        File.WriteAllText(Path.Combine(validDirectory.FullName, "plugin.json"), """
            { "id": "valid", "name": "Valid", "version": "1.0.0", "apiVersion": 1, "entryPoint": "Valid.exe", "capabilities": ["notes.write"] }
            """);
        var invalidDirectory = Directory.CreateDirectory(Path.Combine(_directory, "invalid"));
        File.WriteAllText(Path.Combine(invalidDirectory.FullName, "plugin.json"), "{ " +
            "\"id\": \"bad\", \"name\": \"Bad\", \"version\": \"1.0.0\", \"apiVersion\": 1, " +
            "\"entryPoint\": \"..\\\\bad.exe\", \"capabilities\": [] }");

        var catalog = PluginCatalog.Load(_directory);

        Assert.Single(catalog.Plugins);
        Assert.Equal("valid", catalog.Plugins[0].Manifest.Id);
        Assert.Single(catalog.Errors);
    }
}
