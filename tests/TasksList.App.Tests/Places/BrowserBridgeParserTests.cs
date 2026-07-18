using TasksList.App.Places;
using TasksList.Core.Models;

namespace TasksList.App.Tests.Places;

public sealed class BrowserBridgeParserTests
{
    [Fact]
    public void ParseReturnsEverySafeTabAndRecognizesConversations()
    {
        const string json = """
            {
              "type": "snapshot",
              "browser": "chromium",
              "capturedAt": "2026-07-18T20:00:00Z",
              "windows": [
                {
                  "id": "1",
                  "focused": true,
                  "tabs": [
                    { "id": "10", "windowId": "1", "index": 0, "title": "Task'sList", "url": "https://chatgpt.com/c/abc", "active": true, "pinned": false, "incognito": false },
                    { "id": "11", "windowId": "1", "index": 1, "title": "Docker", "url": "https://docs.docker.com", "active": false, "pinned": false, "incognito": false },
                    { "id": "12", "windowId": "1", "index": 2, "title": "Private", "url": "https://bank.example", "active": false, "pinned": false, "incognito": true }
                  ]
                }
              ]
            }
            """;

        var snapshot = BrowserBridgeMonitor.Parse(json);

        Assert.Equal(2, snapshot.Tabs.Count);
        Assert.Contains(snapshot.Tabs, tab => tab.Kind == PlaceKind.Conversation);
        Assert.DoesNotContain(snapshot.Tabs, tab => tab.Url.Contains("bank", StringComparison.OrdinalIgnoreCase));
    }
}
