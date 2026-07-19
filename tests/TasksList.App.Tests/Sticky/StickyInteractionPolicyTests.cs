using TasksList.App.Sticky;
using TasksList.Core.Notes;

namespace TasksList.App.Tests.Sticky;

public sealed class StickyInteractionPolicyTests
{
    [Fact]
    public void SingleClickOnDisplayedTitleStartsWindowDrag()
    {
        var action = StickyInteractionPolicy.ResolveHeaderAction(
            titleEditing: false,
            overInteractiveControl: false,
            overDisplayedTitle: true,
            clickCount: 1);

        Assert.Equal(StickyInteractionAction.Drag, action);
    }

    [Fact]
    public void DoubleClickOnDisplayedTitleBeginsTitleEditing()
    {
        var action = StickyInteractionPolicy.ResolveHeaderAction(false, false, true, 2);

        Assert.Equal(StickyInteractionAction.BeginTitleEdit, action);
    }

    [Fact]
    public void TitleEditorAndToolbarControlsNeverStartDrag()
    {
        Assert.Equal(
            StickyInteractionAction.None,
            StickyInteractionPolicy.ResolveHeaderAction(true, false, true, 1));
        Assert.Equal(
            StickyInteractionAction.None,
            StickyInteractionPolicy.ResolveHeaderAction(false, true, false, 1));
    }

    [Fact]
    public void DoubleClickOnEmptyHeaderTogglesRoll()
    {
        var action = StickyInteractionPolicy.ResolveHeaderAction(false, false, false, 2);

        Assert.Equal(StickyInteractionAction.ToggleRoll, action);
    }

    [Theory]
    [InlineData("F2", StickyInteractionAction.BeginTitleEdit)]
    [InlineData("Enter", StickyInteractionAction.CommitTitleEdit)]
    [InlineData("Escape", StickyInteractionAction.CancelTitleEdit)]
    [InlineData("A", StickyInteractionAction.None)]
    public void TitleEditingKeysHaveExplicitActions(string key, StickyInteractionAction expected)
    {
        Assert.Equal(expected, StickyInteractionPolicy.ResolveTitleKey(key));
    }

    [Theory]
    [InlineData(ToolbarVisibility.Always, false, false, true)]
    [InlineData(ToolbarVisibility.Hover, false, false, true)]
    [InlineData(ToolbarVisibility.Focused, true, false, true)]
    [InlineData(ToolbarVisibility.Focused, false, true, false)]
    [InlineData(ToolbarVisibility.Hidden, true, true, false)]
    public void ToolbarVisibilityNeverDependsOnCrossingAHiddenControl(
        ToolbarVisibility visibility,
        bool hasKeyboardFocus,
        bool isActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            StickyInteractionPolicy.ShouldShowToolbar(visibility, hasKeyboardFocus, isActive));
    }
}
