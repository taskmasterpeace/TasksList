using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "taskslist-settings-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void MissingOrCorruptSettingsReturnSafeDefaults()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new AppSettingsStore(path);

        Assert.Equal(12, store.Load().SnapTolerance);

        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "not json");

        var recovered = store.Load();
        Assert.Equal(12, recovered.SnapTolerance);
        Assert.Contains(AppHotkeyAction.DisableGhostMode, recovered.Hotkeys.Keys);
    }

    [Fact]
    public void SaveAtomicallyRoundTripsAndValidatesValues()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new AppSettingsStore(path);
        var settings = AppSettings.Default with
        {
            SnapTolerance = 500,
            MonitoringPaused = true,
            ExcludedClipboardApplications = ["KeePass.exe", "1Password.exe"],
        };

        store.Save(settings);
        var restored = store.Load();

        Assert.Equal(40, restored.SnapTolerance);
        Assert.True(restored.MonitoringPaused);
        Assert.Equal(2, restored.ExcludedClipboardApplications.Count);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
