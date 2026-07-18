namespace TasksList.App.Shell;

public sealed record HotkeyBindingError(
    AppHotkeyAction? Action,
    string Message);

public static class GlobalHotkeyBindingPolicy
{
    public static IReadOnlyList<HotkeyBindingError> Validate(AppSettings settings)
    {
        var errors = new List<HotkeyBindingError>();
        if (!settings.Hotkeys.TryGetValue(
                AppHotkeyAction.DisableGhostMode,
                out var recovery) ||
            !recovery.IsBound)
        {
            errors.Add(new HotkeyBindingError(
                AppHotkeyAction.DisableGhostMode,
                "Disable Ghost Mode must always have a global recovery shortcut."));
        }

        foreach (var group in settings.Hotkeys
                     .Where(pair => pair.Value.IsBound)
                     .GroupBy(pair => pair.Value)
                     .Where(group => group.Count() > 1))
        {
            var actions = string.Join(" and ", group.Select(pair => DisplayName(pair.Key)));
            errors.Add(new HotkeyBindingError(
                null,
                $"{actions} use the same shortcut. Choose a different shortcut for one action."));
        }

        return errors;
    }

    public static string DisplayName(AppHotkeyAction action) => action switch
    {
        AppHotkeyAction.NewSticky => "New Sticky",
        AppHotkeyAction.NewFromClipboard => "New from Clipboard",
        AppHotkeyAction.ClipboardPalette => "Clipboard Palette",
        AppHotkeyAction.CaptureRegion => "Capture Region",
        AppHotkeyAction.ToggleAllNotes => "Show/Hide All Notes",
        AppHotkeyAction.ShowLibrary => "Show Library",
        AppHotkeyAction.DisableGhostMode => "Disable Ghost Mode",
        _ => action.ToString(),
    };
}
