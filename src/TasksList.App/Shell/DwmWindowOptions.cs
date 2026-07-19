namespace TasksList.App.Shell;

public enum DwmWindowKind
{
    MainWindow,
    Sticky,
}

public enum DwmWindowCornerPreference
{
    Default = 0,
    DoNotRound = 1,
    Round = 2,
    RoundSmall = 3,
}

public enum DwmSystemBackdropType
{
    Auto = 0,
    None = 1,
    MainWindow = 2,
    TransientWindow = 3,
    TabbedWindow = 4,
}

public sealed record DwmEnvironment(
    bool IsWindows11OrGreater,
    bool IsHighContrast,
    bool IsTransparencyEnabled,
    bool IsRemoteSession,
    bool UseDarkMode);

public sealed record DwmWindowOptions(
    bool UseDarkCaption,
    DwmWindowCornerPreference? CornerPreference,
    DwmSystemBackdropType? SystemBackdrop)
{
    public static DwmWindowOptions Resolve(DwmWindowKind kind, DwmEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var canUseWindows11Visuals = environment.IsWindows11OrGreater &&
                                     !environment.IsHighContrast;
        var useBackdrop = kind == DwmWindowKind.MainWindow &&
                          canUseWindows11Visuals &&
                          environment.IsTransparencyEnabled &&
                          !environment.IsRemoteSession;

        return new DwmWindowOptions(
            UseDarkCaption: environment.UseDarkMode && !environment.IsHighContrast,
            CornerPreference: canUseWindows11Visuals
                ? DwmWindowCornerPreference.Round
                : null,
            SystemBackdrop: useBackdrop
                ? DwmSystemBackdropType.MainWindow
                : null);
    }
}
