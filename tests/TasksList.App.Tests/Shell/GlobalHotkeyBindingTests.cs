using TasksList.App.Shell;

namespace TasksList.App.Tests.Shell;

public sealed class GlobalHotkeyBindingTests
{
    [Fact]
    public void DuplicateGestureProducesAnActionableConflict()
    {
        var duplicate = new HotkeyGesture(HotkeyModifiers.Control, 0x4E);
        var settings = AppSettings.Default with
        {
            Hotkeys = new Dictionary<AppHotkeyAction, HotkeyGesture>
            {
                [AppHotkeyAction.NewSticky] = duplicate,
                [AppHotkeyAction.ShowLibrary] = duplicate,
                [AppHotkeyAction.DisableGhostMode] = new(
                    HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift,
                    0x47),
            },
        };

        var errors = GlobalHotkeyBindingPolicy.Validate(settings);

        Assert.Single(errors);
        Assert.Contains("New Sticky", errors[0].Message);
        Assert.Contains("Show Library", errors[0].Message);
    }

    [Fact]
    public void GhostRecoveryCannotBeUnbound()
    {
        var settings = AppSettings.Default with
        {
            Hotkeys = AppSettings.Default.Hotkeys
                .Where(pair => pair.Key != AppHotkeyAction.DisableGhostMode)
                .ToDictionary(),
        };

        var errors = GlobalHotkeyBindingPolicy.Validate(settings);

        Assert.Contains(errors, error =>
            error.Message.Contains("Disable Ghost Mode", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Ctrl+Shift+V", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x56)]
    [InlineData("Ctrl+Alt+Shift+G", HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x47)]
    [InlineData("Ctrl+Shift+5", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x35)]
    public void ShortcutTextRoundTrips(string text, HotkeyModifiers modifiers, uint key)
    {
        Assert.True(HotkeyGestureText.TryParse(text, out var gesture));
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.VirtualKey);
        Assert.Equal(text, HotkeyGestureText.Format(gesture));
    }
}
