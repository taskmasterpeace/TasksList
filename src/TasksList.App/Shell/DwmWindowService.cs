using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace TasksList.App.Shell;

public interface IDwmPlatform
{
    bool TrySetInt32(nint windowHandle, int attribute, int value);
}

public static class DwmWindowService
{
    public const int ImmersiveDarkModeAttribute = 20;
    public const int CornerPreferenceAttribute = 33;
    public const int SystemBackdropAttribute = 38;

    public static void Apply(Window window, DwmWindowKind kind)
    {
        ArgumentNullException.ThrowIfNull(window);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return;

        Apply(
            handle,
            DwmWindowOptions.Resolve(kind, WindowsEnvironmentReader.Read()),
            WindowsDwmPlatform.Instance);
    }

    public static void Apply(nint windowHandle, DwmWindowOptions options, IDwmPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(platform);
        if (windowHandle == nint.Zero) return;

        platform.TrySetInt32(
            windowHandle,
            ImmersiveDarkModeAttribute,
            options.UseDarkCaption ? 1 : 0);
        if (options.CornerPreference is { } corner)
        {
            platform.TrySetInt32(windowHandle, CornerPreferenceAttribute, (int)corner);
        }
        if (options.SystemBackdrop is { } backdrop)
        {
            platform.TrySetInt32(windowHandle, SystemBackdropAttribute, (int)backdrop);
        }
    }
}

public sealed class WindowsDwmPlatform : IDwmPlatform
{
    public static WindowsDwmPlatform Instance { get; } = new();

    private WindowsDwmPlatform()
    {
    }

    public bool TrySetInt32(nint windowHandle, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(windowHandle, attribute, ref value, sizeof(int)) >= 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}

public static class WindowsEnvironmentReader
{
    private const string PersonalizeRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static DwmEnvironment Read() => new(
        IsWindows11OrGreater: OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
        IsHighContrast: SystemParameters.HighContrast,
        IsTransparencyEnabled: ReadDword("EnableTransparency", defaultValue: true),
        IsRemoteSession: System.Windows.Forms.SystemInformation.TerminalServerSession,
        UseDarkMode: !ReadDword("AppsUseLightTheme", defaultValue: true));

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
}
