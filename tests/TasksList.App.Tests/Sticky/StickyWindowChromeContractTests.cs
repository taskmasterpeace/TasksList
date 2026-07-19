using System.Xml.Linq;

namespace TasksList.App.Tests.Sticky;

public sealed class StickyWindowChromeContractTests
{
    [Fact]
    public void StickyHeaderUsesTheNativeWindowChromeCaptionSurface()
    {
        var stickyWindow = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TasksList.App",
            "Sticky",
            "StickyWindow.xaml"));

        var chrome = Assert.Single(stickyWindow.Descendants().Where(element =>
            element.Name.LocalName == "WindowChrome"));
        Assert.Equal("48", chrome.Attribute("CaptionHeight")?.Value);

        var header = Assert.Single(stickyWindow.Descendants().Where(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "Header")));

        Assert.Equal("Transparent", header.Attribute("Background")?.Value);
        Assert.Null(header.Attribute("Cursor"));
        Assert.Null(header.Attribute("MouseLeftButtonDown"));
        Assert.Equal("HeaderMouseEnter", header.Attribute("MouseEnter")?.Value);
        Assert.Equal("HeaderMouseLeave", header.Attribute("MouseLeave")?.Value);
    }

    [Fact]
    public void HiddenToolbarDoesNotInterceptHeaderAndButtonsUseStandardCursor()
    {
        var stickyWindow = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TasksList.App",
            "Sticky",
            "StickyWindow.xaml"));

        var toolbar = Assert.Single(stickyWindow.Descendants().Where(element =>
            element.Name.LocalName == "StackPanel" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "ChromePanel")));
        Assert.Equal("0", toolbar.Attribute("Opacity")?.Value);
        Assert.Equal("False", toolbar.Attribute("IsHitTestVisible")?.Value);

        var buttonStyle = Assert.Single(stickyWindow.Descendants().Where(element =>
            element.Name.LocalName == "Style" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value == "StickyIconButton")));
        Assert.DoesNotContain(buttonStyle.Elements(), element =>
            element.Name.LocalName == "Setter" &&
            element.Attribute("Property")?.Value == "Cursor");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
