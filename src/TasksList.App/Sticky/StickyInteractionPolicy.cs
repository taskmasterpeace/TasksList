using TasksList.Core.Notes;

namespace TasksList.App.Sticky;

public enum StickyInteractionAction
{
    None,
    Drag,
    BeginTitleEdit,
    CommitTitleEdit,
    CancelTitleEdit,
    ToggleRoll,
}

public static class StickyInteractionPolicy
{
    public static StickyInteractionAction ResolveHeaderAction(
        bool titleEditing,
        bool overInteractiveControl,
        bool overDisplayedTitle,
        int clickCount)
    {
        if (titleEditing || overInteractiveControl)
        {
            return StickyInteractionAction.None;
        }

        if (clickCount >= 2)
        {
            return overDisplayedTitle
                ? StickyInteractionAction.BeginTitleEdit
                : StickyInteractionAction.ToggleRoll;
        }

        return StickyInteractionAction.Drag;
    }

    public static StickyInteractionAction ResolveTitleKey(string key) => key switch
    {
        "F2" => StickyInteractionAction.BeginTitleEdit,
        "Enter" => StickyInteractionAction.CommitTitleEdit,
        "Escape" => StickyInteractionAction.CancelTitleEdit,
        _ => StickyInteractionAction.None,
    };

    public static bool ShouldShowToolbar(
        ToolbarVisibility visibility,
        bool hasKeyboardFocus,
        bool isActive) => visibility switch
    {
        ToolbarVisibility.Always => true,
        ToolbarVisibility.Hover => true,
        ToolbarVisibility.Focused => hasKeyboardFocus,
        ToolbarVisibility.Hidden => false,
        _ => isActive,
    };
}
