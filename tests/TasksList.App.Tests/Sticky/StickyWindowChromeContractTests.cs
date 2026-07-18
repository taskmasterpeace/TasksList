using System.Xml.Linq;

namespace TasksList.App.Tests.Sticky;

public sealed class StickyWindowChromeContractTests
{
    [Fact]
    public void StickyHeaderIsACompleteDraggableHitSurface()
    {
        var stickyWindow = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TasksList.App",
            "Sticky",
            "StickyWindow.xaml"));

        var header = Assert.Single(stickyWindow.Descendants().Where(element =>
            element.Name.LocalName == "Border" &&
            element.Attribute("MouseLeftButtonDown")?.Value == "TitleBarMouseDown"));

        Assert.Equal("Transparent", header.Attribute("Background")?.Value);
        Assert.Equal("SizeAll", header.Attribute("Cursor")?.Value);
        Assert.Equal("TitleBarMouseDown", header.Attribute("MouseLeftButtonDown")?.Value);
        Assert.Equal("HeaderMouseEnter", header.Attribute("MouseEnter")?.Value);
        Assert.Equal("HeaderMouseLeave", header.Attribute("MouseLeave")?.Value);
    }

    [Fact]
    public void HiddenToolbarDoesNotInterceptHeaderAndButtonsUseHandCursor()
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
        Assert.Contains(buttonStyle.Elements(), element =>
            element.Name.LocalName == "Setter" &&
            element.Attribute("Property")?.Value == "Cursor" &&
            element.Attribute("Value")?.Value == "Hand");
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
