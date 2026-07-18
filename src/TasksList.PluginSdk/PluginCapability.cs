namespace TasksList.PluginSdk;

public enum PluginCapability
{
    BrowserTabsIdentity,
    BrowserPageContent,
    PlacesWrite,
    NotesWrite,
    ClipboardRead,
    ScreenCapture,
    FilesReadSelected,
    ProcessRead,
    ProcessExecute,
    Network,
}

public static class PluginCapabilityNames
{
    private static readonly IReadOnlyDictionary<PluginCapability, string> Names =
        new Dictionary<PluginCapability, string>
        {
            [PluginCapability.BrowserTabsIdentity] = "browser.tabs.identity",
            [PluginCapability.BrowserPageContent] = "browser.page.content",
            [PluginCapability.PlacesWrite] = "places.write",
            [PluginCapability.NotesWrite] = "notes.write",
            [PluginCapability.ClipboardRead] = "clipboard.read",
            [PluginCapability.ScreenCapture] = "screen.capture",
            [PluginCapability.FilesReadSelected] = "files.read.selected",
            [PluginCapability.ProcessRead] = "process.read",
            [PluginCapability.ProcessExecute] = "process.execute",
            [PluginCapability.Network] = "network",
        };

    public static string ToManifestName(this PluginCapability capability) => Names[capability];

    public static PluginCapability Parse(string value) =>
        Names.FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase)) is var match &&
        !string.IsNullOrEmpty(match.Value)
            ? match.Key
            : throw new InvalidDataException($"Unknown plugin capability '{value}'.");
}

