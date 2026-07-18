using TasksList.Core.Notes;

namespace TasksList.App.Sticky;

public sealed class StickyWindowController
{
    public StickyWindowController(NotePresentation presentation)
    {
        Presentation = presentation;
    }

    public NotePresentation Presentation { get; private set; }

    public event EventHandler<NotePresentation>? Changed;

    public void SetBounds(NoteBounds bounds) => Set(Presentation with { Bounds = bounds });

    public void SetOpacity(double opacity) => Set(Presentation.WithOpacity(opacity));

    public void SetInactiveOpacity(double opacity) => Set(Presentation with
    {
        InactiveOpacity = NoteAppearancePolicy.ClampOpacity(opacity),
    });

    public void AdjustOpacity(int wheelDelta) => SetOpacity(
        NoteAppearancePolicy.AdjustOpacity(Presentation.ActiveOpacity, wheelDelta));

    public void ApplyPreset(PaperPreset preset)
    {
        var paper = NoteAppearancePolicy.ForPreset(preset);
        Set(Presentation with
        {
            Preset = preset,
            BackgroundHex = paper.BackgroundHex,
            TextHex = paper.TextHex,
            AccentHex = paper.AccentHex,
        });
    }

    public void ApplyStyle(NoteStyle style) => Set(Presentation.ApplyStyle(style));

    public void SetColors(string backgroundHex, string textHex, string accentHex)
    {
        if (!NoteAppearancePolicy.IsLocalHexColor(backgroundHex) ||
            !NoteAppearancePolicy.IsLocalHexColor(textHex) ||
            !NoteAppearancePolicy.IsLocalHexColor(accentHex))
        {
            throw new ArgumentException("Note colors must use #RRGGBB format.");
        }

        Set(Presentation with
        {
            BackgroundHex = backgroundHex.ToUpperInvariant(),
            TextHex = textHex.ToUpperInvariant(),
            AccentHex = accentHex.ToUpperInvariant(),
        });
    }

    public void SetFont(string family, double size, int weight, double lineSpacing) => Set(
        Presentation with
        {
            FontFamily = family,
            FontSize = Math.Clamp(size, 9, 48),
            FontWeight = Math.Clamp(weight, 300, 800),
            LineSpacing = Math.Clamp(lineSpacing, 1, 2),
        });

    public void SetDensity(NoteDensity density) => Set(Presentation with { Density = density });

    public void SetToolbarVisibility(ToolbarVisibility visibility) =>
        Set(Presentation with { ToolbarVisibility = visibility });

    public void SetDecoration(
        double shadowStrength,
        CornerStyle cornerStyle,
        bool borderVisible,
        bool textureEnabled) => Set(Presentation with
    {
        ShadowStrength = Math.Clamp(shadowStrength, 0, 1),
        CornerStyle = cornerStyle,
        BorderVisible = borderVisible,
        TextureEnabled = textureEnabled,
    });

    public void ToggleLocked() => Set(Presentation.ToggleLocked());

    public void ToggleTopmost() => Set(Presentation.ToggleTopmost());

    public void ToggleRolled() => Set(Presentation.ToggleRolled());

    public void ToggleGhost() => Set(Presentation with { Ghost = !Presentation.Ghost });

    public void Archive(DateTimeOffset archivedAt) => Set(Presentation with
    {
        HiddenAt = archivedAt,
        ModifiedAt = archivedAt,
    });

    private void Set(NotePresentation presentation)
    {
        Presentation = presentation;
        Changed?.Invoke(this, presentation);
    }
}
