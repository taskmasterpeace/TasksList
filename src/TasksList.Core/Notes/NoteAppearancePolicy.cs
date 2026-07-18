using System.Globalization;

namespace TasksList.Core.Notes;

public static class NoteAppearancePolicy
{
    private static readonly IReadOnlyDictionary<PaperPreset, PaperAppearance> Presets =
        new Dictionary<PaperPreset, PaperAppearance>
        {
            [PaperPreset.Butter] = new("#F4CE62", "#2E271C", "#8A6117"),
            [PaperPreset.Peach] = new("#EFA66F", "#35251C", "#9A4C22"),
            [PaperPreset.Mint] = new("#9FD4B5", "#1E3028", "#36765A"),
            [PaperPreset.Sky] = new("#A8C5E8", "#1E2C3C", "#3D6F9F"),
            [PaperPreset.Lavender] = new("#C7B4E5", "#2D2538", "#76549F"),
            [PaperPreset.Rose] = new("#E7AFC0", "#38232A", "#9C4E68"),
            [PaperPreset.Graphite] = new("#24262D", "#F4EFE6", "#E9B85F"),
            [PaperPreset.Glass] = new("#D9E4E7", "#203034", "#4C7D86"),
        };

    public static double ClampOpacity(double value) =>
        Math.Round(Math.Clamp(value, 0.20, 1.00), 2, MidpointRounding.AwayFromZero);

    public static double AdjustOpacity(double current, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return ClampOpacity(current);
        }

        var direction = Math.Sign(wheelDelta);
        return ClampOpacity(Math.Round(current / 0.05) * 0.05 + (direction * 0.05));
    }

    public static PaperAppearance ForPreset(PaperPreset preset) => Presets[preset];

    public static bool IsLocalHexColor(string value) =>
        value.Length == 7 &&
        value[0] == '#' &&
        int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);

    public static double ContrastRatio(string foregroundHex, string backgroundHex)
    {
        if (!IsLocalHexColor(foregroundHex) || !IsLocalHexColor(backgroundHex))
        {
            throw new ArgumentException("Contrast colors must use #RRGGBB format.");
        }

        var foreground = RelativeLuminance(foregroundHex);
        var background = RelativeLuminance(backgroundHex);
        var lighter = Math.Max(foreground, background);
        var darker = Math.Min(foreground, background);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var red = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        var green = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        var blue = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        return (0.2126 * Linearize(red)) + (0.7152 * Linearize(green)) + (0.0722 * Linearize(blue));
    }

    private static double Linearize(double component) =>
        component <= 0.04045
            ? component / 12.92
            : Math.Pow((component + 0.055) / 1.055, 2.4);
}
