using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TasksList.App.Theming;

public sealed record WindowsThemeSnapshot(
    bool UseDarkMode,
    bool HighContrast,
    Color Accent);

public interface IWindowsThemeEnvironment
{
    event EventHandler? Changed;
    WindowsThemeSnapshot Read();
}

public interface IThemeResourceSink
{
    void Apply(IReadOnlyDictionary<string, Color> colors);
}

public sealed class WindowsThemeService : IDisposable
{
    private readonly IWindowsThemeEnvironment _environment;
    private readonly IThemeResourceSink _sink;
    private readonly ThemeDefinition _theme;
    private bool _started;

    public WindowsThemeService(
        IWindowsThemeEnvironment environment,
        IThemeResourceSink sink,
        ThemeDefinition theme)
    {
        _environment = environment;
        _sink = sink;
        _theme = theme;
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        _environment.Changed += EnvironmentChanged;
        ApplyCurrent();
    }

    public void Dispose()
    {
        if (!_started) return;
        _started = false;
        _environment.Changed -= EnvironmentChanged;
        if (_environment is IDisposable disposable) disposable.Dispose();
    }

    private void EnvironmentChanged(object? sender, EventArgs e) => ApplyCurrent();

    private void ApplyCurrent()
    {
        var snapshot = _environment.Read();
        var useCustomTheme = !snapshot.HighContrast &&
                             !string.Equals(
                                 _theme.Id,
                                 ThemeLoader.Default.Id,
                                 StringComparison.OrdinalIgnoreCase);
        var accent = useCustomTheme && TryThemeColor("accent", out var customAccent)
            ? customAccent
            : snapshot.Accent;
        var colors = WindowsThemePalette.Create(
                snapshot.UseDarkMode,
                snapshot.HighContrast,
                accent)
            .ToResourceColors()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        if (useCustomTheme)
        {
            ApplyOverride(colors, "canvas", "CanvasBrush");
            ApplyOverride(colors, "panel", "PanelBrush");
            ApplyOverride(colors, "card", "CardBrush");
            ApplyOverride(colors, "border", "BorderBrush");
            ApplyOverride(colors, "success", "SuccessBrush");
        }

        _sink.Apply(colors);
    }

    private void ApplyOverride(Dictionary<string, Color> colors, string token, string resourceKey)
    {
        if (TryThemeColor(token, out var color)) colors[resourceKey] = color;
    }

    private bool TryThemeColor(string token, out Color color)
    {
        color = default;
        if (!_theme.Tokens.TryGetValue(token, out var value) ||
            ColorConverter.ConvertFromString(value) is not Color parsed)
        {
            return false;
        }
        color = parsed;
        return true;
    }
}

public sealed class ApplicationThemeResourceSink(
    ResourceDictionary resources,
    Dispatcher? dispatcher) : IThemeResourceSink
{
    public void Apply(IReadOnlyDictionary<string, Color> colors)
    {
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ApplyCore(colors));
            return;
        }
        ApplyCore(colors);
    }

    private void ApplyCore(IReadOnlyDictionary<string, Color> colors)
    {
        foreach (var pair in colors)
        {
            if (resources[pair.Key] is SolidColorBrush { IsFrozen: false } brush)
            {
                brush.Color = pair.Value;
            }
            else
            {
                resources[pair.Key] = new SolidColorBrush(pair.Value);
            }
        }
    }
}

public sealed class WindowsThemeEnvironment : IWindowsThemeEnvironment, IDisposable
{
    private const string PersonalizeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public WindowsThemeEnvironment()
    {
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += SystemPreferenceChanged;
    }

    public event EventHandler? Changed;

    public WindowsThemeSnapshot Read() => new(
        UseDarkMode: !ReadDword("AppsUseLightTheme", true),
        HighContrast: SystemParameters.HighContrast,
        Accent: ReadAccent());

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= SystemPreferenceChanged;
    }

    private void SystemPreferenceChanged(object? sender, EventArgs e) =>
        Changed?.Invoke(this, EventArgs.Empty);

    private static Color ReadAccent()
    {
        try
        {
            if (DwmGetColorizationColor(out var colorization, out _) >= 0)
            {
                return Color.FromRgb(
                    (byte)((colorization >> 16) & 0xFF),
                    (byte)((colorization >> 8) & 0xFF),
                    (byte)(colorization & 0xFF));
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
        }
        return Color.FromRgb(0, 120, 212);
    }

    private static bool ReadDword(string valueName, bool defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            return key?.GetValue(valueName) is int value ? value != 0 : defaultValue;
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return defaultValue;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetColorizationColor(
        out uint colorization,
        [MarshalAs(UnmanagedType.Bool)] out bool opaqueBlend);
}
