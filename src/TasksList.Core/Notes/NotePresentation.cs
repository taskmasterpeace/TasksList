using TasksList.Core.Models;

namespace TasksList.Core.Notes;

public enum PaperPreset
{
    Butter,
    Peach,
    Mint,
    Sky,
    Lavender,
    Rose,
    Graphite,
    Glass,
}

public enum ToolbarVisibility
{
    Always,
    Hover,
    Focused,
    Hidden,
}

public enum NoteDensity
{
    Compact,
    Comfortable,
    Spacious,
}

public enum CornerStyle
{
    Square,
    Soft,
    Round,
}

public enum ReminderAttention
{
    None,
    Sound,
    Pulse,
    SoundAndPulse,
}

public sealed record NoteBounds(
    double Left,
    double Top,
    double Width,
    double Height,
    double ExpandedHeight,
    string MonitorId);

public sealed record PaperAppearance(
    string BackgroundHex,
    string TextHex,
    string AccentHex);

public sealed record NoteStyle
{
    public PaperPreset Preset { get; init; }

    public required string BackgroundHex { get; init; }

    public required string TextHex { get; init; }

    public required string AccentHex { get; init; }

    public double ActiveOpacity { get; init; }

    public double InactiveOpacity { get; init; }

    public required string FontFamily { get; init; }

    public double FontSize { get; init; }

    public int FontWeight { get; init; }

    public double LineSpacing { get; init; }

    public NoteDensity Density { get; init; }

    public ToolbarVisibility ToolbarVisibility { get; init; }

    public double ShadowStrength { get; init; }

    public CornerStyle CornerStyle { get; init; }

    public bool BorderVisible { get; init; }

    public bool TextureEnabled { get; init; }

    public static NoteStyle FromPreset(PaperPreset preset)
    {
        var paper = NoteAppearancePolicy.ForPreset(preset);
        return new NoteStyle
        {
            Preset = preset,
            BackgroundHex = paper.BackgroundHex,
            TextHex = paper.TextHex,
            AccentHex = paper.AccentHex,
            ActiveOpacity = 1,
            InactiveOpacity = 1,
            FontFamily = "Segoe UI Variable Text",
            FontSize = 13,
            FontWeight = 400,
            LineSpacing = 1.2,
            Density = NoteDensity.Comfortable,
            ToolbarVisibility = ToolbarVisibility.Hover,
            ShadowStrength = 0.36,
            CornerStyle = CornerStyle.Soft,
            BorderVisible = true,
            TextureEnabled = true,
        };
    }
}

public sealed record NamedNoteStyle(
    Guid Id,
    string Name,
    NoteStyle Style,
    bool IsDefault,
    DateTimeOffset ModifiedAt);

public sealed record NotePresentation
{
    public required NoteId NoteId { get; init; }

    public required NoteBounds Bounds { get; init; }

    public PaperPreset Preset { get; init; }

    public required string BackgroundHex { get; init; }

    public required string TextHex { get; init; }

    public required string AccentHex { get; init; }

    public double ActiveOpacity { get; init; }

    public double InactiveOpacity { get; init; }

    public required string FontFamily { get; init; }

    public double FontSize { get; init; }

    public int FontWeight { get; init; }

    public double LineSpacing { get; init; }

    public NoteDensity Density { get; init; }

    public ToolbarVisibility ToolbarVisibility { get; init; }

    public double ShadowStrength { get; init; }

    public CornerStyle CornerStyle { get; init; }

    public bool BorderVisible { get; init; }

    public bool TextureEnabled { get; init; }

    public bool Topmost { get; init; }

    public bool Rolled { get; init; }

    public bool Locked { get; init; }

    public bool Ghost { get; init; }

    public DateTimeOffset? HiddenAt { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }

    public DateTimeOffset? WakeAt { get; init; }

    public DateTimeOffset? ReminderAt { get; init; }

    public ReminderAttention ReminderAttention { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ModifiedAt { get; init; }

    public static NotePresentation Default(NoteId noteId, DateTimeOffset? createdAt = null)
    {
        var paper = NoteAppearancePolicy.ForPreset(PaperPreset.Butter);
        var timestamp = createdAt ?? DateTimeOffset.UnixEpoch;
        return new NotePresentation
        {
            NoteId = noteId,
            Bounds = new NoteBounds(100, 100, 360, 330, 330, string.Empty),
            Preset = PaperPreset.Butter,
            BackgroundHex = paper.BackgroundHex,
            TextHex = paper.TextHex,
            AccentHex = paper.AccentHex,
            ActiveOpacity = 1,
            InactiveOpacity = 1,
            FontFamily = "Segoe UI Variable Text",
            FontSize = 13,
            FontWeight = 400,
            LineSpacing = 1.2,
            Density = NoteDensity.Comfortable,
            ToolbarVisibility = ToolbarVisibility.Hover,
            ShadowStrength = 0.36,
            CornerStyle = CornerStyle.Soft,
            BorderVisible = true,
            TextureEnabled = true,
            Topmost = true,
            Rolled = false,
            Locked = false,
            Ghost = false,
            ReminderAttention = ReminderAttention.Pulse,
            CreatedAt = timestamp,
            ModifiedAt = timestamp,
        };
    }

    public NotePresentation WithOpacity(double opacity) =>
        this with { ActiveOpacity = NoteAppearancePolicy.ClampOpacity(opacity) };

    public NotePresentation ToggleTopmost() => this with { Topmost = !Topmost };

    public NotePresentation ToggleRolled() => this with { Rolled = !Rolled };

    public NotePresentation ToggleLocked() => this with { Locked = !Locked };

    public NotePresentation SoftDelete(DateTimeOffset deletedAt) =>
        this with { DeletedAt = deletedAt, ModifiedAt = deletedAt };

    public NotePresentation Restore(DateTimeOffset restoredAt) =>
        this with { DeletedAt = null, HiddenAt = null, ModifiedAt = restoredAt };

    public NotePresentation ApplyStyle(NoteStyle style) => this with
    {
        Preset = style.Preset,
        BackgroundHex = style.BackgroundHex,
        TextHex = style.TextHex,
        AccentHex = style.AccentHex,
        ActiveOpacity = NoteAppearancePolicy.ClampOpacity(style.ActiveOpacity),
        InactiveOpacity = NoteAppearancePolicy.ClampOpacity(style.InactiveOpacity),
        FontFamily = style.FontFamily,
        FontSize = style.FontSize,
        FontWeight = style.FontWeight,
        LineSpacing = style.LineSpacing,
        Density = style.Density,
        ToolbarVisibility = style.ToolbarVisibility,
        ShadowStrength = style.ShadowStrength,
        CornerStyle = style.CornerStyle,
        BorderVisible = style.BorderVisible,
        TextureEnabled = style.TextureEnabled,
    };
}
