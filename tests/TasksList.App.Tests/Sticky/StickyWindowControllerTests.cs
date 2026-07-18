using TasksList.App.Sticky;
using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.App.Tests.Sticky;

public sealed class StickyWindowControllerTests
{
    [Fact]
    public void DirectControlsUpdateOneImmutablePresentationState()
    {
        var controller = new StickyWindowController(NotePresentation.Default(NoteId.New()));

        controller.AdjustOpacity(-120);
        controller.ApplyPreset(PaperPreset.Graphite);
        controller.ToggleLocked();
        controller.ToggleTopmost();
        controller.SetBounds(new NoteBounds(25, 50, 420, 360, 360, "DISPLAY1"));

        Assert.Equal(0.95, controller.Presentation.ActiveOpacity);
        Assert.Equal(PaperPreset.Graphite, controller.Presentation.Preset);
        Assert.True(controller.Presentation.Locked);
        Assert.False(controller.Presentation.Topmost);
        Assert.Equal(420, controller.Presentation.Bounds.Width);
    }

    [Fact]
    public void ApplyingPresetPreservesUsersOpacityAndTypography()
    {
        var original = NotePresentation.Default(NoteId.New()) with
        {
            ActiveOpacity = 0.65,
            FontFamily = "Cascadia Mono",
            FontSize = 16,
        };
        var controller = new StickyWindowController(original);

        controller.ApplyPreset(PaperPreset.Mint);

        Assert.Equal(0.65, controller.Presentation.ActiveOpacity);
        Assert.Equal("Cascadia Mono", controller.Presentation.FontFamily);
        Assert.Equal(16, controller.Presentation.FontSize);
        Assert.Equal(NoteAppearancePolicy.ForPreset(PaperPreset.Mint).BackgroundHex,
            controller.Presentation.BackgroundHex);
    }

    [Fact]
    public void CustomColorsAndTypographyAreValidatedBeforeEnteringWindowState()
    {
        var controller = new StickyWindowController(NotePresentation.Default(NoteId.New()));

        controller.SetColors("#102030", "#F0F0F0", "#44AA88");
        controller.SetFont("Cascadia Mono", 200, 999, 9);

        Assert.Equal("#102030", controller.Presentation.BackgroundHex);
        Assert.Equal("#F0F0F0", controller.Presentation.TextHex);
        Assert.Equal("#44AA88", controller.Presentation.AccentHex);
        Assert.Equal(48, controller.Presentation.FontSize);
        Assert.Equal(800, controller.Presentation.FontWeight);
        Assert.Equal(2, controller.Presentation.LineSpacing);
        Assert.Throws<ArgumentException>(() =>
            controller.SetColors("red", "#FFFFFF", "#000000"));
    }

    [Fact]
    public void InactiveOpacityIsClampedIndependentlyFromActiveOpacity()
    {
        var controller = new StickyWindowController(
            NotePresentation.Default(NoteId.New()) with { ActiveOpacity = 0.85 });

        controller.SetInactiveOpacity(0.12);

        Assert.Equal(0.85, controller.Presentation.ActiveOpacity);
        Assert.Equal(0.20, controller.Presentation.InactiveOpacity);
    }
}
