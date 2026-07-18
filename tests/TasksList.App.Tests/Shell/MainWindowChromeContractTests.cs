using System.Xml.Linq;

namespace TasksList.App.Tests.Shell;

public sealed class MainWindowChromeContractTests
{
    [Fact]
    public void MainWindowUsesBrandResourcesAndACompleteDraggableTitleSurface()
    {
        var repository = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(repository, "src", "TasksList.App", "TasksList.App.csproj"));
        var projectItems = project.Descendants().ToArray();
        Assert.Contains(projectItems, element =>
            element.Name.LocalName == "Resource" &&
            element.Attribute("Include")?.Value.EndsWith("TasksList.ico", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(projectItems, element =>
            element.Name.LocalName == "Resource" &&
            element.Attribute("Include")?.Value.EndsWith("app-icon-64.png", StringComparison.OrdinalIgnoreCase) == true);

        var application = XDocument.Load(Path.Combine(repository, "src", "TasksList.App", "App.xaml"));
        Assert.Contains(application.Descendants(), element =>
            element.Name.LocalName == "Setter" &&
            element.Attribute("Property")?.Value == "Icon" &&
            element.Attribute("Value")?.Value.Contains("TasksList.ico", StringComparison.OrdinalIgnoreCase) == true);

        var mainWindow = XDocument.Load(Path.Combine(repository, "src", "TasksList.App", "MainWindow.xaml"));
        var titleBar = Assert.Single(mainWindow.Descendants().Where(element =>
            element.Name.LocalName == "Border" &&
            element.Attribute("MouseLeftButtonDown")?.Value == "TitleBarMouseDown"));
        Assert.Equal("Transparent", titleBar.Attribute("Background")?.Value);
        Assert.Equal("Hand", titleBar.Attribute("Cursor")?.Value);
        Assert.Contains(titleBar.Descendants(), element =>
            element.Name.LocalName == "Image" &&
            element.Attribute("Source")?.Value.Contains("app-icon-64.png", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(titleBar.Descendants(), element =>
            element.Name.LocalName == "TextBlock" && element.Attribute("Text")?.Value == "T");
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
