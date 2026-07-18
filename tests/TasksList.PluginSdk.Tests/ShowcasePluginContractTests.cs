using TasksList.Core.Models;
using TasksList.Plugin.BrowserContext;
using TasksList.Plugin.CaptureWorkflows;
using TasksList.Plugin.DeveloperWorkspace;
using TasksList.PluginSdk;

namespace TasksList.PluginSdk.Tests;

public sealed class ShowcasePluginContractTests
{
    [Fact]
    public void BrowserContextTurnsEveryNonPrivateTabIntoAContextualPlace()
    {
        var browser = Place.Create(PlaceKind.Browser, "Edge", null, "edge");
        var tabs = new[]
        {
            new BrowserTabInput("1", "10", "https://chatgpt.com/c/abc", "Task'sList chat", false),
            new BrowserTabInput("1", "11", "https://docs.docker.com", "Docker docs", false),
            new BrowserTabInput("2", "12", "https://bank.example", "Bank", true),
        };

        var places = BrowserContextPlugin.CreateTabPlaces(browser.Id, tabs);

        Assert.Equal(2, places.Count);
        Assert.Contains(places, place => place.Kind == PlaceKind.Conversation && place.Name == "Task'sList chat");
        Assert.DoesNotContain(places, place => place.StableIdentity.Contains("bank", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeveloperWorkspaceCreatesMarkdownHandoffWithoutExecutingACommand()
    {
        var operation = DeveloperWorkspacePlugin.CreateHandoff(
            "D:\\git\\taskslist",
            "main",
            ["src/TasksList.App/MainWindow.xaml"],
            "Wire the browser session tree");

        Assert.IsType<CreateNoteOperation>(operation);
        Assert.Contains("## Next step", operation.Markdown, StringComparison.Ordinal);
        Assert.Contains("Wire the browser session tree", operation.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("process.execute", DeveloperWorkspacePlugin.Manifest.Capabilities.Select(item => item.ToManifestName()));
    }

    [Fact]
    public void SensitiveCaptureWorkflowBlocksNetworkAndSetsExpiry()
    {
        var now = DateTimeOffset.Parse("2026-07-18T20:00:00-04:00");

        var result = CaptureWorkflowsPlugin.ProtectSensitiveCapture(
            "OPENAI_API_KEY=sk-example-secret-value",
            now);

        Assert.False(result.NetworkAllowed);
        Assert.True(result.IsSensitive);
        Assert.Equal(now.AddMinutes(5), result.ExpiresAt);
    }

    [Fact]
    public void EveryShowcaseManifestPassesTheSameHostValidation()
    {
        var manifests = new[]
        {
            BrowserContextPlugin.Manifest,
            DeveloperWorkspacePlugin.Manifest,
            CaptureWorkflowsPlugin.Manifest,
        };

        foreach (var manifest in manifests)
        {
            PluginManifestValidator.Validate(manifest, supportedApiVersion: 1);
        }

        Assert.Equal(3, manifests.Select(manifest => manifest.Id).Distinct().Count());
    }
}
