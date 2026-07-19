namespace TasksList.App.Tests.Shell;

public sealed class InstallerWindowsIntegrationContractTests
{
    [Fact]
    public void InstallerCreatesBrandedShortcutAndCompleteAppsFeaturesMetadata()
    {
        var root = FindRepositoryRoot();
        var installer = File.ReadAllText(Path.Combine(root, "installer", "install.ps1"));

        Assert.Contains("$shortcut.IconLocation", installer, StringComparison.Ordinal);
        Assert.Contains("DisplayVersion -Value '1.2.0'", installer, StringComparison.Ordinal);
        Assert.Contains("DisplayIcon", installer, StringComparison.Ordinal);
        Assert.Contains("QuietUninstallString", installer, StringComparison.Ordinal);
        Assert.Contains("EstimatedSize", installer, StringComparison.Ordinal);
        Assert.Contains("URLInfoAbout", installer, StringComparison.Ordinal);
        Assert.Contains("https://github.com/taskmasterpeace/TasksList", installer, StringComparison.Ordinal);
    }

    [Fact]
    public void UninstallerUnregistersWindowsNotificationsAndPreservesUserData()
    {
        var uninstaller = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "installer", "uninstall.ps1"));

        Assert.Contains("--unregister-notifications", uninstaller, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalAppData\\TasksList' -Recurse", uninstaller, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("User notes and clipboard data remain", uninstaller, StringComparison.Ordinal);
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
