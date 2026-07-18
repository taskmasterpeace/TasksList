using TasksList.Core.Models;

namespace TasksList.Core.Places;

public sealed record BrowserTabSnapshot(
    string WindowId,
    string TabId,
    string Url,
    string Title,
    int WindowIndex,
    int TabIndex,
    bool IsPrivate);

public sealed record BrowserSession(Place Place, IReadOnlyList<SavedTab> Tabs);

public sealed record BrowserRestorePlan(
    IReadOnlyList<string> UrlsToOpen,
    IReadOnlyList<string> TabIdsToClose,
    bool RequiresLargeSessionConfirmation);

public static class BrowserSessionService
{
    public static BrowserSession SaveSession(
        PlaceId browserPlaceId,
        string name,
        IEnumerable<BrowserTabSnapshot> openTabs)
    {
        var sessionPlace = Place.Create(
            PlaceKind.BrowserSession,
            name.Trim(),
            browserPlaceId,
            $"browser-session:{Guid.NewGuid():N}");
        var tabs = openTabs
            .Where(tab => !tab.IsPrivate)
            .OrderBy(tab => tab.WindowIndex)
            .ThenBy(tab => tab.TabIndex)
            .Select(tab => SavedTab.Create(
                sessionPlace.Id,
                NormalizeUrl(tab.Url),
                tab.Title,
                tab.WindowIndex,
                tab.TabIndex))
            .ToArray();
        return new BrowserSession(sessionPlace, tabs);
    }

    public static BrowserRestorePlan PlanRestore(
        BrowserSession session,
        IEnumerable<BrowserTabSnapshot> currentlyOpen,
        int largeSessionThreshold = 20)
    {
        var openCounts = currentlyOpen
            .Where(tab => !tab.IsPrivate)
            .GroupBy(tab => NormalizeUrl(tab.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var urlsToOpen = new List<string>();

        foreach (var saved in session.Tabs.OrderBy(tab => tab.WindowIndex).ThenBy(tab => tab.TabIndex))
        {
            var normalized = NormalizeUrl(saved.Url);
            if (openCounts.TryGetValue(normalized, out var count) && count > 0)
            {
                openCounts[normalized] = count - 1;
            }
            else
            {
                urlsToOpen.Add(saved.Url);
            }
        }

        return new BrowserRestorePlan(
            urlsToOpen,
            [],
            session.Tabs.Count > largeSessionThreshold);
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Trim();
        }

        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}
