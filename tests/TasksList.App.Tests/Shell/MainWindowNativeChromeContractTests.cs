using System.Xml.Linq;

namespace TasksList.App.Tests.Shell;

public sealed class MainWindowNativeChromeContractTests
{
    [Fact]
    public void MainWindowDelegatesCaptionAndFrameBehaviorToWindows()
    {
        var root = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml")).Root!;

        Assert.Equal("SingleBorderWindow", root.Attribute("WindowStyle")?.Value);
        Assert.Equal("False", root.Attribute("AllowsTransparency")?.Value);
        Assert.Equal("CanResize", root.Attribute("ResizeMode")?.Value);
        Assert.DoesNotContain(root.DescendantsAndSelf().Attributes(), attribute =>
            attribute.Name.LocalName is "MouseLeftButtonDown" &&
            attribute.Value == "TitleBarMouseDown");
        Assert.DoesNotContain(root.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" &&
                attribute.Value is "Minimize" or "Maximize" or "Close"));

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml.cs"));
        Assert.DoesNotContain("TitleBarMouseDown", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MinimizeClick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximizeClick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CloseClick", source, StringComparison.Ordinal);
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
