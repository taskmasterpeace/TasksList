using TasksList.Core.Models;
using TasksList.PluginSdk;

namespace TasksList.Plugin.BrowserContext;

public sealed record BrowserTabInput(
    string WindowId,
    string TabId,
    string Url,
    string Title,
    bool IsPrivate);

public static class BrowserContextPlugin
{
    public static PluginManifest Manifest { get; } = new(
        "taskslist.browser-context",
        "Browser Context",
        "1.0.0",
        1,
        "TasksList.Plugin.BrowserContext.exe",
        [PluginCapability.BrowserTabsIdentity, PluginCapability.PlacesWrite]);

    public static IReadOnlyList<Place> CreateTabPlaces(
        PlaceId browserPlaceId,
        IEnumerable<BrowserTabInput> tabs) =>
        tabs
            .Where(tab => !tab.IsPrivate)
            .Select(tab => Place.Create(
                IsConversation(tab.Url) ? PlaceKind.Conversation : PlaceKind.BrowserTab,
                string.IsNullOrWhiteSpace(tab.Title) ? tab.Url : tab.Title,
                browserPlaceId,
                $"browser-tab:{tab.WindowId}:{tab.TabId}:{NormalizeUrl(tab.Url)}"))
            .ToArray();

    private static bool IsConversation(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Host.EndsWith("chatgpt.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith("claude.ai", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/chat/", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? new UriBuilder(uri) { Fragment = string.Empty }.Uri.AbsoluteUri.TrimEnd('/')
            : url.Trim();
}

