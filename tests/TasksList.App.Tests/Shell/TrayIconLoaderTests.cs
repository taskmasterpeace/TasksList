using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class TrayIconLoaderTests
{
    [Fact]
    public void LoadUsesBrandIconWhenAnIconFileExists()
    {
        var iconPath = Path.Combine(
            FindRepositoryRoot(),
            "assets",
            "brand",
            "generated",
            "TasksList.ico");

        using var icon = TrayIconLoader.Load(iconPath);

        Assert.NotEqual(IntPtr.Zero, icon.Handle);
        Assert.True(icon.Width >= 16);
        Assert.Equal(icon.Width, icon.Height);
    }

    [Fact]
    public void LoadReturnsAnOwnedFallbackWhenTheSourceIsMissing()
    {
        using var first = TrayIconLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"));
        using var second = TrayIconLoader.Load(null);

        Assert.NotEqual(IntPtr.Zero, first.Handle);
        Assert.NotEqual(IntPtr.Zero, second.Handle);
        Assert.NotEqual(first.Handle, second.Handle);
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
