using System.Xml.Linq;

namespace TasksList.App.Tests.Shell;

public sealed class SecondaryWindowNativeChromeContractTests
{
    [Theory]
    [InlineData("Plugins/PluginManagerWindow.xaml", "CanResize")]
    [InlineData("Places/NewPlaceDialog.xaml", "NoResize")]
    public void DialogWindowsUseStandardNonTransparentFrames(string relativePath, string resizeMode)
    {
        var root = Load(relativePath).Root!;
        Assert.Equal("SingleBorderWindow", root.Attribute("WindowStyle")?.Value);
        Assert.Equal("False", root.Attribute("AllowsTransparency")?.Value);
        Assert.Equal(resizeMode, root.Attribute("ResizeMode")?.Value);
    }

    [Fact]
    public void PluginManagerContainsNoCustomCaptionHandlers()
    {
        var repository = FindRepositoryRoot();
        var xaml = Load("Plugins/PluginManagerWindow.xaml");
        var source = File.ReadAllText(Path.Combine(
            repository, "src", "TasksList.App", "Plugins", "PluginManagerWindow.xaml.cs"));

        Assert.DoesNotContain(xaml.Root!.DescendantsAndSelf().Attributes(), attribute =>
            attribute.Value is "TitleBarMouseDown" or "CloseClick");
        Assert.DoesNotContain("DragMove", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TitleBarMouseDown", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ClipboardPaletteUsesNonLayeredNativeWindowChrome()
    {
        var document = Load("Clipboard/ClipboardPaletteWindow.xaml");
        Assert.Equal("False", document.Root?.Attribute("AllowsTransparency")?.Value);
        var chrome = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "WindowChrome"));
        Assert.Equal("64", chrome.Attribute("CaptionHeight")?.Value);
        Assert.Equal("6", chrome.Attribute("ResizeBorderThickness")?.Value);
    }

    private static XDocument Load(string relativePath) => XDocument.Load(Path.Combine(
        FindRepositoryRoot(), "src", "TasksList.App", relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
