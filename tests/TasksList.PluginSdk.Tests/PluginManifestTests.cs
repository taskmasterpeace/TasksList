using TasksList.PluginSdk;

namespace TasksList.PluginSdk.Tests;

public sealed class PluginManifestTests
{
    [Fact]
    public void ValidManifestDeclaresApiEntrypointAndCapabilities()
    {
        const string json = """
            {
              "id": "taskslist.browser-context",
              "name": "Browser Context",
              "version": "1.0.0",
              "apiVersion": 1,
              "entryPoint": "BrowserContext.exe",
              "capabilities": ["browser.tabs.identity", "places.write"]
            }
            """;

        var manifest = PluginManifest.Parse(json);

        PluginManifestValidator.Validate(manifest, supportedApiVersion: 1);
        Assert.Equal("taskslist.browser-context", manifest.Id);
        Assert.Contains(PluginCapability.BrowserTabsIdentity, manifest.Capabilities);
    }

    [Fact]
    public void EntrypointPathTraversalIsRejected()
    {
        var manifest = new PluginManifest(
            "malicious",
            "Malicious",
            "1.0.0",
            1,
            "..\\outside.exe",
            [PluginCapability.PlacesWrite]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            PluginManifestValidator.Validate(manifest, supportedApiVersion: 1));

        Assert.Contains("entry point", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedApiVersionIsRejected()
    {
        var manifest = new PluginManifest("future", "Future", "1.0.0", 99, "Future.exe", []);

        Assert.Throws<InvalidDataException>(() =>
            PluginManifestValidator.Validate(manifest, supportedApiVersion: 1));
    }
}

