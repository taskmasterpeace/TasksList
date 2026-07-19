using System.Xml.Linq;

namespace TasksList.App.Tests.Theming;

public sealed class KeyboardFocusVisualContractTests
{
    [Fact]
    public void SharedButtonsExposeKeyboardFocusAndDisabledStates()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "App.xaml"));
        var style = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "Style" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value == "GhostButton")));

        Assert.Contains(style.Descendants(), element =>
            element.Name.LocalName == "Trigger" &&
            element.Attribute("Property")?.Value == "IsKeyboardFocused" &&
            element.Attribute("Value")?.Value == "True");
        Assert.Contains(style.Descendants(), element =>
            element.Name.LocalName == "Trigger" &&
            element.Attribute("Property")?.Value == "IsEnabled" &&
            element.Attribute("Value")?.Value == "False");
    }

    [Fact]
    public void NavigationTabsAndNoteCardsExposeKeyboardFocusStates()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml"));

        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Trigger" &&
            element.Attribute("Property")?.Value == "IsKeyboardFocused" &&
            element.Attribute("Value")?.Value == "True");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Trigger" &&
            element.Attribute("Property")?.Value == "IsKeyboardFocusWithin" &&
            element.Attribute("Value")?.Value == "True");
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
