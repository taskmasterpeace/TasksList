namespace TasksList.App.Shell;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

public enum AppHotkeyAction
{
    NewSticky,
    NewFromClipboard,
    ClipboardPalette,
    CaptureRegion,
    ToggleAllNotes,
    ShowLibrary,
    DisableGhostMode,
}

public sealed record HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public bool IsBound => Modifiers != HotkeyModifiers.None && VirtualKey != 0;
}

public sealed record AppSettings
{
    public int SnapTolerance { get; init; } = 12;

    public bool MonitoringPaused { get; init; }

    public bool StartWithWindows { get; init; }

    public bool MinimizeLibraryToTray { get; init; } = true;

    public bool ReduceMotion { get; init; }

    public bool PromoteDuplicateClips { get; init; } = true;

    public List<string> ExcludedClipboardApplications { get; init; } = [];

    public Dictionary<AppHotkeyAction, HotkeyGesture> Hotkeys { get; init; } = [];

    public static AppSettings Default => new()
    {
        Hotkeys = new Dictionary<AppHotkeyAction, HotkeyGesture>
        {
            [AppHotkeyAction.NewSticky] = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E),
            [AppHotkeyAction.NewFromClipboard] = new(
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
                0x4E),
            [AppHotkeyAction.ClipboardPalette] = new(
                HotkeyModifiers.Control | HotkeyModifiers.Shift,
                0x56),
            [AppHotkeyAction.CaptureRegion] = new(
                HotkeyModifiers.Control | HotkeyModifiers.Shift,
                0x35),
            [AppHotkeyAction.ToggleAllNotes] = new(
                HotkeyModifiers.Control | HotkeyModifiers.Alt,
                0x53),
            [AppHotkeyAction.ShowLibrary] = new(
                HotkeyModifiers.Control | HotkeyModifiers.Alt,
                0x4C),
            [AppHotkeyAction.DisableGhostMode] = new(
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
                0x47),
        },
    };
}
