using System.Xml.Linq;

namespace TasksList.App.Tests.Library;

public sealed class MainWindowNoteLibraryContractTests
{
    [Fact]
    public void NoteLibraryWrapsCardsWithoutNestedOrHorizontalScrolling()
    {
        var mainWindow = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml"));
        var notesList = Assert.Single(mainWindow.Descendants().Where(element =>
            element.Name.LocalName == "ListBox" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "NotesList")));

        Assert.Equal("Disabled", notesList.Attribute("ScrollViewer.HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", notesList.Attribute("ScrollViewer.VerticalScrollBarVisibility")?.Value);
        Assert.DoesNotContain(notesList.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(notesList.Descendants(), element => element.Name.LocalName == "WrapPanel");
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
