using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.Core.Tests.Notes;

public sealed class NoteAppearancePolicyTests
{
    [Theory]
    [InlineData(-1, 0.20)]
    [InlineData(0, 0.20)]
    [InlineData(0.65, 0.65)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void OpacityIsClampedToSafeVisibleRange(double input, double expected)
    {
        Assert.Equal(expected, NoteAppearancePolicy.ClampOpacity(input), 2);
    }

    [Theory]
    [InlineData(0.65, 120, 0.70)]
    [InlineData(0.65, -120, 0.60)]
    [InlineData(0.98, 120, 1.00)]
    [InlineData(0.22, -120, 0.20)]
    [InlineData(0.65, 0, 0.65)]
    public void ModifierWheelChangesOpacityInFivePercentSteps(
        double current,
        int wheelDelta,
        double expected)
    {
        Assert.Equal(expected, NoteAppearancePolicy.AdjustOpacity(current, wheelDelta), 2);
    }

    [Fact]
    public void EveryPaperPresetHasValidDistinctPresentationColors()
    {
        foreach (var preset in Enum.GetValues<PaperPreset>())
        {
            var appearance = NoteAppearancePolicy.ForPreset(preset);

            Assert.Matches("^#[0-9A-F]{6}$", appearance.BackgroundHex);
            Assert.Matches("^#[0-9A-F]{6}$", appearance.TextHex);
            Assert.Matches("^#[0-9A-F]{6}$", appearance.AccentHex);
            Assert.NotEqual(appearance.BackgroundHex, appearance.TextHex);
        }
    }

    [Fact]
    public void PresentationTransitionsAreImmutable()
    {
        var original = NotePresentation.Default(NoteId.New());

        var changed = original
            .WithOpacity(0.65)
            .ToggleTopmost()
            .ToggleRolled()
            .ToggleLocked();

        Assert.Equal(1, original.ActiveOpacity);
        Assert.True(original.Topmost);
        Assert.False(original.Rolled);
        Assert.False(original.Locked);
        Assert.Equal(0.65, changed.ActiveOpacity);
        Assert.False(changed.Topmost);
        Assert.True(changed.Rolled);
        Assert.True(changed.Locked);
    }

    [Fact]
    public void SoftDeleteAndRestoreKeepThePresentationRecoverable()
    {
        var deletedAt = DateTimeOffset.Parse("2026-07-18T15:00:00-04:00");
        var presentation = NotePresentation.Default(NoteId.New()) with
        {
            Bounds = new NoteBounds(120, 80, 480, 360, 360, "DISPLAY1"),
            Preset = PaperPreset.Graphite,
        };

        var deleted = presentation.SoftDelete(deletedAt);
        var restored = deleted.Restore(deletedAt.AddMinutes(5));

        Assert.Equal(deletedAt, deleted.DeletedAt);
        Assert.Null(restored.DeletedAt);
        Assert.Equal(presentation.Bounds, restored.Bounds);
        Assert.Equal(PaperPreset.Graphite, restored.Preset);
    }

    [Fact]
    public void NamedStyleChangesAppearanceWithoutMovingOrReattachingTheNote()
    {
        var original = NotePresentation.Default(NoteId.New()) with
        {
            Bounds = new NoteBounds(40, 60, 500, 420, 420, "DISPLAY1"),
            Locked = true,
        };
        var graphite = NoteStyle.FromPreset(PaperPreset.Graphite) with
        {
            ActiveOpacity = 0.65,
            FontFamily = "Cascadia Mono",
        };

        var changed = original.ApplyStyle(graphite);

        Assert.Equal(original.Bounds, changed.Bounds);
        Assert.Equal(original.Locked, changed.Locked);
        Assert.Equal(PaperPreset.Graphite, changed.Preset);
        Assert.Equal(0.65, changed.ActiveOpacity);
        Assert.Equal("Cascadia Mono", changed.FontFamily);
    }
}
