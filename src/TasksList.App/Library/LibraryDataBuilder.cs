using TasksList.App.Places;
using TasksList.App.Plugins;
using TasksList.Core.Models;

namespace TasksList.App.Library;

public sealed record ContextLibraryRow(
    string Name,
    string KindLabel,
    string Detail,
    bool IsLive,
    ContextRef? Context,
    LiveBrowserTab? BrowserTab)
{
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1].ToUpperInvariant();
}

public sealed record ExtensionLibraryRow(
    string Name,
    string VersionLabel,
    string CapabilityLabel,
    PluginCatalogEntry Plugin)
{
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1].ToUpperInvariant();
}

public sealed record LibraryData(
    IReadOnlyList<ContextLibraryRow> Contexts,
    IReadOnlyList<ExtensionLibraryRow> Extensions,
    string ContextsEmptyMessage,
    string ExtensionsEmptyMessage);

public static class LibraryDataBuilder
{
    public static LibraryData Build(
        IEnumerable<ContextRef> contexts,
        IEnumerable<LiveBrowserTab> browserTabs,
        IEnumerable<PluginCatalogEntry> plugins)
    {
        var contextRows = contexts
            .OrderBy(context => context.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(context => new ContextLibraryRow(
                context.DisplayName,
                FriendlyKind(context.Kind),
                context.Provider,
                false,
                context,
                null))
            .Concat(browserTabs
                .OrderBy(tab => tab.WindowId, StringComparer.Ordinal)
                .ThenBy(tab => tab.Index)
                .Select(tab => new ContextLibraryRow(
                    tab.Title,
                    tab.Kind == PlaceKind.Conversation ? "Conversation" : "Browser tab",
                    tab.Url,
                    true,
                    null,
                    tab)))
            .ToArray();

        var extensionRows = plugins
            .OrderBy(plugin => plugin.Manifest.Name, StringComparer.OrdinalIgnoreCase)
            .Select(plugin => new ExtensionLibraryRow(
                plugin.Manifest.Name,
                $"v{plugin.Manifest.Version}",
                plugin.Manifest.Capabilities.Count == 0
                    ? "No permissions requested"
                    : string.Join(" · ", plugin.Manifest.Capabilities.Select(FriendlyCapability)),
                plugin))
            .ToArray();

        return new LibraryData(
            contextRows,
            extensionRows,
            "No application contexts yet. Attach a sticky or copy from an application to add one.",
            "No plugins installed. Open Plugins & permissions to install a local package.");
    }

    private static string FriendlyKind(ContextKind kind) => kind switch
    {
        ContextKind.BrowserTab => "Browser tab",
        ContextKind.BrowserWindow => "Browser window",
        _ => kind.ToString(),
    };

    private static string FriendlyCapability(TasksList.PluginSdk.PluginCapability capability) =>
        capability.ToString()
            .Replace("Browser", "Browser ", StringComparison.Ordinal)
            .Replace("Notes", "Notes ", StringComparison.Ordinal)
            .Replace("Clipboard", "Clipboard ", StringComparison.Ordinal)
            .Replace("Screen", "Screen ", StringComparison.Ordinal)
            .Replace("Files", "Files ", StringComparison.Ordinal)
            .Replace("Process", "Process ", StringComparison.Ordinal)
            .Trim();
}
