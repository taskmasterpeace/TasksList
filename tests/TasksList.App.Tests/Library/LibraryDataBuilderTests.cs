using TasksList.App.Library;
using TasksList.App.Places;
using TasksList.App.Plugins;
using TasksList.Core.Models;
using TasksList.PluginSdk;

namespace TasksList.App.Tests.Library;

public sealed class LibraryDataBuilderTests
{
    [Fact]
    public void Build_UsesOnlyPersistedAndObservedData()
    {
        var context = ContextRef.Create(
            ContextKind.Application,
            "win32",
            "C:/Tools/RealApp.exe",
            "Real App");
        var tab = new LiveBrowserTab(
            "tab-7",
            "window-2",
            0,
            "Research page",
            "https://example.test/research",
            true,
            false,
            PlaceKind.BrowserTab);
        var plugin = new PluginCatalogEntry(
            new PluginManifest(
                "real.plugin",
                "Real Plugin",
                "1.0.0",
                1,
                "plugin.dll",
                [PluginCapability.NotesWrite]),
            "C:/plugins/real.plugin");

        var result = LibraryDataBuilder.Build([context], [tab], [plugin]);

        Assert.Collection(result.Contexts,
            row => Assert.Equal("Real App", row.Name),
            row => Assert.Equal("Research page", row.Name));
        Assert.Single(result.Extensions);
        Assert.Equal("Real Plugin", result.Extensions[0].Name);
        var rendered = string.Join('|', result.Contexts.Select(row => row.Name)
            .Concat(result.Extensions.Select(row => row.Name)));
        Assert.DoesNotContain("Microsoft Edge", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Docker", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Developer workspace", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ProvidesExplanatoryEmptyStates()
    {
        var result = LibraryDataBuilder.Build([], [], []);

        Assert.Empty(result.Contexts);
        Assert.Empty(result.Extensions);
        Assert.Contains("application", result.ContextsEmptyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plugin", result.ExtensionsEmptyMessage, StringComparison.OrdinalIgnoreCase);
    }
}
