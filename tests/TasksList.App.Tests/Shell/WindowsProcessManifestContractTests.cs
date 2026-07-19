using System.Xml.Linq;
using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class WindowsProcessManifestContractTests
{
    [Fact]
    public void AppProjectEmbedsModernWindowsManifest()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "src", "TasksList.App", "TasksList.App.csproj"));
        var properties = project.Descendants().ToLookup(element => element.Name.LocalName);

        Assert.Equal("net8.0-windows10.0.19041.0", Assert.Single(properties["TargetFramework"]).Value);
        Assert.Equal("app.manifest", Assert.Single(properties["ApplicationManifest"]).Value);
        Assert.Equal("None", Assert.Single(properties["WindowsPackageType"]).Value);
        Assert.Equal("true", Assert.Single(properties["WindowsAppSDKSelfContained"]).Value);
    }

    [Fact]
    public void ManifestDeclaresPerMonitorV2AndWindowsCapabilities()
    {
        var manifest = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "app.manifest"));
        var values = manifest.Descendants().ToLookup(element => element.Name.LocalName);

        Assert.Equal("asInvoker", Assert.Single(values["requestedExecutionLevel"]).Attribute("level")?.Value);
        Assert.Contains(values["dpiAware"], element => element.Value.Equals("true/pm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(values["dpiAwareness"], element => element.Value.Contains("PerMonitorV2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(values["longPathAware"], element => element.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(values["heapType"], element => element.Value.Equals("SegmentHeap", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(values["supportedOS"], element =>
            element.Attribute("Id")?.Value.Equals("{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(values["assemblyIdentity"], element =>
            element.Attribute("name")?.Value.Equals("Microsoft.Windows.Common-Controls", StringComparison.Ordinal) == true &&
            element.Attribute("version")?.Value == "6.0.0.0");
    }

    [Fact]
    public void AppAppliesStableIdentityBeforeWpfStartup()
    {
        Assert.Equal("TaskMasterPeace.TasksList", WindowsAppIdentity.AppUserModelId);

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "App.xaml.cs"));
        var identity = source.IndexOf("WindowsAppIdentity.TryApply()", StringComparison.Ordinal);
        var baseStartup = source.IndexOf("base.OnStartup(e)", StringComparison.Ordinal);

        Assert.True(identity >= 0, "App startup must apply the explicit Windows identity.");
        Assert.True(identity < baseStartup, "Windows identity must be set before WPF creates shell windows.");
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
