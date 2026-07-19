using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class DwmWindowOptionsTests
{
    [Fact]
    public void Windows11MainWindowUsesBackdropRoundCornersAndDarkCaption()
    {
        var environment = new DwmEnvironment(
            IsWindows11OrGreater: true,
            IsHighContrast: false,
            IsTransparencyEnabled: true,
            IsRemoteSession: false,
            UseDarkMode: true);

        var options = DwmWindowOptions.Resolve(DwmWindowKind.MainWindow, environment);

        Assert.True(options.UseDarkCaption);
        Assert.Equal(DwmWindowCornerPreference.Round, options.CornerPreference);
        Assert.Equal(DwmSystemBackdropType.MainWindow, options.SystemBackdrop);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    public void AccessibilityAndFallbackEnvironmentsDisableBackdrop(
        bool isWindows11,
        bool highContrast,
        bool remoteSession,
        bool transparencyEnabled)
    {
        var environment = new DwmEnvironment(
            isWindows11,
            highContrast,
            transparencyEnabled,
            remoteSession,
            UseDarkMode: true);

        var options = DwmWindowOptions.Resolve(DwmWindowKind.MainWindow, environment);

        Assert.Null(options.SystemBackdrop);
        Assert.Equal(isWindows11 && !highContrast, options.CornerPreference is not null);
        Assert.Equal(!highContrast, options.UseDarkCaption);
    }

    [Fact]
    public void StickyUsesRoundCornersWithoutMainWindowBackdrop()
    {
        var environment = new DwmEnvironment(true, false, true, false, true);

        var options = DwmWindowOptions.Resolve(DwmWindowKind.Sticky, environment);

        Assert.Equal(DwmWindowCornerPreference.Round, options.CornerPreference);
        Assert.Null(options.SystemBackdrop);
    }

    [Fact]
    public void PaletteUsesTransientBackdrop()
    {
        var environment = new DwmEnvironment(true, false, true, false, true);

        var options = DwmWindowOptions.Resolve(DwmWindowKind.Palette, environment);

        Assert.Equal(DwmSystemBackdropType.TransientWindow, options.SystemBackdrop);
    }
}
