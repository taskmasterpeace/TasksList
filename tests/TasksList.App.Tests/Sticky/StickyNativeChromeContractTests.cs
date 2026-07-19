using System.Xml.Linq;

namespace TasksList.App.Tests.Sticky;

public sealed class StickyNativeChromeContractTests
{
    [Fact]
    public void StickyUsesNativeCaptionResizeAndInteractiveHeaderExclusions()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "Sticky", "StickyWindow.xaml"));
        var root = document.Root!;

        Assert.Equal("False", root.Attribute("AllowsTransparency")?.Value);
        var chrome = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "WindowChrome"));
        Assert.Equal("48", chrome.Attribute("CaptionHeight")?.Value);
        Assert.Equal("6", chrome.Attribute("ResizeBorderThickness")?.Value);

        var header = Assert.Single(document.Descendants().Where(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "Header")));
        Assert.Null(header.Attribute("Cursor"));
        Assert.Null(header.Attribute("MouseLeftButtonDown"));

        foreach (var name in new[] { "TitleEditor", "ChromePanel", "ReminderBanner" })
        {
            var element = Assert.Single(document.Descendants().Where(candidate =>
                candidate.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name" && attribute.Value == name)));
            Assert.Contains(element.Attributes(), attribute =>
                attribute.Name.LocalName.EndsWith("IsHitTestVisibleInChrome", StringComparison.Ordinal) &&
                attribute.Value == "True");
        }
    }

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
