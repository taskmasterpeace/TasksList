using System.Xml.Linq;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureCompletionPresentationTests
{
    [Fact]
    public void MainWindowOffersExplicitCreateNoteAfterScreenshotCopy()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml"));
        var notice = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "Border" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "CaptureNotice")));

        Assert.Contains(notice.Descendants(), element =>
            element.Name.LocalName == "TextBlock" &&
            element.Attribute("Text")?.Value == "Screenshot copied to clipboard");
        Assert.Contains(notice.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            element.Attribute("Click")?.Value == "CreateCapturedNoteClick");
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
