using TasksList.Core.Models;
using TasksList.Core.Places;

namespace TasksList.Core.Tests.Places;

public sealed class BrowserSessionServiceTests
{
    [Fact]
    public void SaveSessionPreservesEveryTabAndOriginalOrdering()
    {
        var browser = Place.Create(PlaceKind.Browser, "Edge", null, "edge");
        var tabs = new[]
        {
            new BrowserTabSnapshot("window-a", "tab-1", "https://chatgpt.com/c/123", "ChatGPT", 0, 0, false),
            new BrowserTabSnapshot("window-a", "tab-2", "https://docs.docker.com", "Docker docs", 0, 1, false),
            new BrowserTabSnapshot("window-b", "tab-3", "https://docs.docker.com", "Docker docs copy", 1, 0, false),
        };

        var session = BrowserSessionService.SaveSession(browser.Id, "Release research", tabs);

        Assert.Equal(3, session.Tabs.Count);
        Assert.Equal(new[] { 0, 1, 0 }, session.Tabs.Select(tab => tab.TabIndex));
        Assert.Equal(2, session.Tabs.Count(tab => tab.Url == "https://docs.docker.com"));
    }

    [Fact]
    public void RestorePlanKeepsUnrelatedTabsAndOpensOnlyMissingSavedInstances()
    {
        var browser = Place.Create(PlaceKind.Browser, "Edge", null, "edge");
        var saved = BrowserSessionService.SaveSession(
            browser.Id,
            "Research",
            [
                new BrowserTabSnapshot("saved", "1", "https://a.example", "A", 0, 0, false),
                new BrowserTabSnapshot("saved", "2", "https://a.example", "A copy", 0, 1, false),
                new BrowserTabSnapshot("saved", "3", "https://b.example", "B", 0, 2, false),
            ]);
        var open = new[]
        {
            new BrowserTabSnapshot("current", "9", "https://a.example", "A", 0, 0, false),
            new BrowserTabSnapshot("current", "10", "https://unrelated.example", "Unrelated", 0, 1, false),
        };

        var plan = BrowserSessionService.PlanRestore(saved, open, largeSessionThreshold: 2);

        Assert.Equal(new[] { "https://a.example", "https://b.example" }, plan.UrlsToOpen);
        Assert.True(plan.RequiresLargeSessionConfirmation);
        Assert.Empty(plan.TabIdsToClose);
    }

    [Fact]
    public void PrivateTabsAreNeverSaved()
    {
        var session = BrowserSessionService.SaveSession(
            PlaceId.New(),
            "Safe session",
            [new BrowserTabSnapshot("private", "1", "https://bank.example", "Bank", 0, 0, true)]);

        Assert.Empty(session.Tabs);
    }
}
