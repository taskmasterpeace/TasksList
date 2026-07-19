using System.Xml.Linq;

namespace TasksList.App.Tests.Library;

public sealed class MainWindowNoteCardInteractionContractTests
{
    [Fact]
    public void NoteCardsExposeMouseAndKeyboardContextInvocation()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml"));
        var card = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "Button" &&
            element.Attribute("Click")?.Value == "OpenNoteClick"));

        Assert.Equal("NoteCardRightButtonDown", card.Attribute("PreviewMouseRightButtonDown")?.Value);
        Assert.Equal("NoteCardRightButtonUp", card.Attribute("PreviewMouseRightButtonUp")?.Value);
        Assert.Equal("NoteCardKeyDown", card.Attribute("KeyDown")?.Value);
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
