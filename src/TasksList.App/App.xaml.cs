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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDirectory = Environment.GetEnvironmentVariable("TASKSLIST_DATA_DIR") ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TasksList");
        var database = new TasksListDatabase(Path.Combine(dataDirectory, "taskslist.db"));

        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            LoadUserTheme(dataDirectory);
            await database.InitializeAsync();
            _settingsStore = new AppSettingsStore(Path.Combine(dataDirectory, "settings.json"));
            _settings = _settingsStore.Load();
            var window = new MainWindow(database, dataDirectory);
            MainWindow = window;
            window.Show();
            window.SetClipboardMonitoringPaused(_settings.MonitoringPaused);

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
        _hotkeys?.Dispose();
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
        owner.SetClipboardMonitoringPaused(result.MonitoringPaused);
        MessageBox.Show(
            "Settings saved. Global shortcut changes take effect the next time Task'sList starts.",
            "Task'sList Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExitApplication(MainWindow window)
    {
        window.PrepareForExit();
        Shutdown();
    }

    private void LoadUserTheme(string dataDirectory)
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

        var theme = File.Exists(activeThemePath)
            ? ThemeLoader.LoadOrDefault(activeThemePath)
            : ThemeLoader.Default;
        ApplyBrush(theme, "canvas", "CanvasBrush");
        ApplyBrush(theme, "panel", "PanelBrush");
        ApplyBrush(theme, "card", "CardBrush");
        ApplyBrush(theme, "border", "BorderBrush");
        ApplyBrush(theme, "accent", "PrimaryBrush");
        ApplyBrush(theme, "success", "SuccessBrush");
    }

    private void ApplyBrush(ThemeDefinition theme, string token, string resourceKey)
    {
        if (theme.Tokens.TryGetValue(token, out var value) &&
            ColorConverter.ConvertFromString(value) is Color color)
        {
            Resources[resourceKey] = new SolidColorBrush(color);
        }
    }
}
