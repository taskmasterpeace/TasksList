using System.IO;
using System.Windows;
using System.Windows.Media;
using TasksList.App.Settings;
using TasksList.App.Shell;
using TasksList.App.Theming;
using TasksList.Infrastructure.Storage;

namespace TasksList.App;

public partial class App : Application
{
    private TrayService? _tray;
    private GlobalHotkeyService? _hotkeys;
    private AppSettingsStore? _settingsStore;
    private AppSettings _settings = AppSettings.Default;
    private WindowsThemeService? _windowsThemeService;
    private IWindowsAppNotificationService? _windowsNotifications;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var identityError = WindowsAppIdentity.TryApply();
        if (e.Args.Contains("--unregister-notifications", StringComparer.OrdinalIgnoreCase))
        {
            WindowsAppNotificationService.TryUnregisterAll();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var dataDirectory = Environment.GetEnvironmentVariable("TASKSLIST_DATA_DIR") ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TasksList");
        var database = new TasksListDatabase(Path.Combine(dataDirectory, "taskslist.db"));

        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var userTheme = LoadUserTheme(dataDirectory);
            _windowsThemeService = new WindowsThemeService(
                new WindowsThemeEnvironment(),
                new ApplicationThemeResourceSink(Resources, Dispatcher),
                userTheme);
            _windowsThemeService.Start();
            await database.InitializeAsync();
            _settingsStore = new AppSettingsStore(Path.Combine(dataDirectory, "settings.json"));
            _settings = _settingsStore.Load();
            _windowsNotifications = new WindowsAppNotificationService();
            var window = new MainWindow(database, dataDirectory, _windowsNotifications);
            _windowsNotifications.Activated += (_, activation) =>
                Dispatcher.BeginInvoke(() => _ = window.OpenNoteFromNotificationAsync(activation.NoteId));
            var notificationError = _windowsNotifications.TryRegister();
            window.ClipboardPaletteSizeChanged += (width, height) =>
            {
                _settings = _settings with
                {
                    ClipboardPaletteWidth = width,
                    ClipboardPaletteHeight = height,
                };
                _settingsStore.Save(_settings);
            };
            MainWindow = window;
            window.Show();
            window.ApplyClipboardSettings(_settings);
            if (identityError is not null)
            {
                window.ShowNotification(identityError, AppNotificationKind.Warning);
            }
            if (notificationError is not null)
            {
                window.ShowNotification(notificationError, AppNotificationKind.Warning);
            }

            _tray = new TrayService(
                new TrayCommands(
                    () => _ = window.NewStickyFromShellAsync(),
                    () => _ = window.NewFromClipboardFromShellAsync(),
                    window.ShowClipboardPaletteFromShell,
                    () => _ = window.CaptureRegionAsync(),
                    window.ToggleAllNotes,
                    window.ShowLibraryFromShell,
                    window.DisableGhostModeForAll,
                    paused => SetMonitoringPaused(window, paused),
                    () => ShowSettings(window),
                    () => ExitApplication(window)),
                _settings.MonitoringPaused);

            var startupError = StartupRegistrationService.TryApply(
                _settings.StartWithWindows,
                Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "TasksList.App.exe"));
            if (startupError is not null) _tray.ShowError(startupError);

            RegisterGlobalHotkeys(window);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Task'sList could not open its local data.\n\n{exception.Message}",
                "Task'sList",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _windowsThemeService?.Dispose();
        _hotkeys?.Dispose();
        _windowsNotifications?.Unregister();
        _tray?.Dispose();
        base.OnExit(e);
    }

    private void RegisterGlobalHotkeys(MainWindow window)
    {
        var validationErrors = GlobalHotkeyBindingPolicy.Validate(_settings);
        if (validationErrors.Count > 0)
        {
            _tray?.ShowError(string.Join(" ", validationErrors.Select(error => error.Message)));
            return;
        }

        _hotkeys = new GlobalHotkeyService(window);
        var callbacks = new Dictionary<AppHotkeyAction, Action>
        {
            [AppHotkeyAction.NewSticky] = () => _ = window.NewStickyFromShellAsync(),
            [AppHotkeyAction.NewFromClipboard] = () => _ = window.NewFromClipboardFromShellAsync(),
            [AppHotkeyAction.ClipboardPalette] = window.ShowClipboardPaletteFromShell,
            [AppHotkeyAction.CaptureRegion] = () => _ = window.CaptureRegionAsync(),
            [AppHotkeyAction.ToggleAllNotes] = window.ToggleAllNotes,
            [AppHotkeyAction.ShowLibrary] = window.ShowLibraryFromShell,
            [AppHotkeyAction.DisableGhostMode] = window.DisableGhostModeForAll,
        };

        foreach (var binding in _settings.Hotkeys)
        {
            if (!callbacks.TryGetValue(binding.Key, out var callback))
            {
                continue;
            }
            var result = _hotkeys.Register(binding.Key, binding.Value, callback);
            if (!result.Registered && result.ErrorMessage is not null)
            {
                _tray?.ShowError(result.ErrorMessage);
            }
        }
    }

    private void SetMonitoringPaused(MainWindow window, bool paused)
    {
        _settings = _settings with { MonitoringPaused = paused };
        _settingsStore?.Save(_settings);
        window.SetClipboardMonitoringPaused(paused);
    }

    private void ShowSettings(MainWindow owner)
    {
        var dialog = new SettingsWindow(_settings) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is not { } result)
        {
            return;
        }

        _settings = result;
        _settingsStore?.Save(result);
        owner.ApplyClipboardSettings(result);
        var startupError = StartupRegistrationService.TryApply(
            result.StartWithWindows,
            Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "TasksList.App.exe"));
        if (startupError is not null) _tray?.ShowError(startupError);
        owner.ShowNotification(
            "Settings saved. Shortcut changes take effect after restart.",
            AppNotificationKind.Success);
    }

    private void ExitApplication(MainWindow window)
    {
        window.PrepareForExit();
        Shutdown();
    }

    private ThemeDefinition LoadUserTheme(string dataDirectory)
    {
        var activeThemeDirectory = Path.Combine(dataDirectory, "themes", "active");
        var activeThemePath = Path.Combine(activeThemeDirectory, "theme.json");
        if (!File.Exists(activeThemePath))
        {
            Directory.CreateDirectory(activeThemeDirectory);
            var bundledThemePath = Path.Combine(AppContext.BaseDirectory, "themes", "default", "theme.json");
            if (File.Exists(bundledThemePath))
            {
                File.Copy(bundledThemePath, activeThemePath);
            }
        }

        return File.Exists(activeThemePath)
            ? ThemeLoader.LoadOrDefault(activeThemePath)
            : ThemeLoader.Default;
    }
}
