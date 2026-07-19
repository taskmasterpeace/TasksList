using System.Windows;
using System.Windows.Media;

namespace TasksList.App.Theming;

public sealed record WindowsHighContrastColors(
    Color Window,
    Color WindowText,
    Color Control,
    Color ControlText,
    Color Highlight,
    Color HighlightText,
    Color GrayText)
{
    public static WindowsHighContrastColors Current => new(
        SystemColors.WindowColor,
        SystemColors.WindowTextColor,
        SystemColors.ControlColor,
        SystemColors.ControlTextColor,
        SystemColors.HighlightColor,
        SystemColors.HighlightTextColor,
        SystemColors.GrayTextColor);
}

public sealed record WindowsThemePalette(
    Color Window,
    Color Panel,
    Color Card,
    Color ControlHover,
    Color Divider,
    Color Accent,
    Color AccentSoft,
    Color Text,
    Color MutedText,
    Color Success,
    Color Danger,
    Color Focus,
    Color DisabledText,
    Color AccentText)
{
    public static WindowsThemePalette Create(
        bool useDarkMode,
        bool highContrast,
        Color accent,
        WindowsHighContrastColors? highContrastColors = null)
    {
        if (highContrast)
        {
            var system = highContrastColors ?? WindowsHighContrastColors.Current;
            return new WindowsThemePalette(
                system.Window,
                system.Control,
                system.Control,
                system.Highlight,
                system.WindowText,
                system.Highlight,
                system.Highlight,
                system.WindowText,
                system.ControlText,
                system.Highlight,
                system.Highlight,
                system.Highlight,
                system.GrayText,
                system.HighlightText);
        }

        var accentText = BestTextColor(accent);
        var accentSoft = Color.FromArgb(54, accent.R, accent.G, accent.B);
        return useDarkMode
            ? new WindowsThemePalette(
                Hex("#202020"),
                Hex("#272727"),
                Hex("#2D2D2D"),
                Hex("#383838"),
                Hex("#454545"),
                accent,
                accentSoft,
                Hex("#FFFFFF"),
                Hex("#C7C7C7"),
                Hex("#6CCB8B"),
                Hex("#FF99A4"),
                accent,
                Hex("#858585"),
                accentText)
            : new WindowsThemePalette(
                Hex("#F3F3F3"),
                Hex("#F9F9F9"),
                Hex("#FFFFFF"),
                Hex("#EBEBEB"),
                Hex("#D8D8D8"),
                accent,
                accentSoft,
                Hex("#1A1A1A"),
                Hex("#5D5D5D"),
                Hex("#0F7B3E"),
                Hex("#C42B1C"),
                accent,
                Hex("#8A8A8A"),
                accentText);
    }

    public IReadOnlyDictionary<string, Color> ToResourceColors() =>
        new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["CanvasBrush"] = Window,
            ["PanelBrush"] = Panel,
            ["CardBrush"] = Card,
            ["CardHoverBrush"] = ControlHover,
            ["BorderBrush"] = Divider,
            ["PrimaryBrush"] = Accent,
            ["PrimarySoftBrush"] = AccentSoft,
            ["TextBrush"] = Text,
            ["MutedTextBrush"] = MutedText,
            ["SuccessBrush"] = Success,
            ["DangerBrush"] = Danger,
            ["FocusBrush"] = Focus,
            ["DisabledTextBrush"] = DisabledText,
            ["AccentTextBrush"] = AccentText,
        };

    private static Color BestTextColor(Color background)
    {
        var backgroundLuminance = RelativeLuminance(background);
        var whiteContrast = 1.05 / (backgroundLuminance + 0.05);
        var blackContrast = (backgroundLuminance + 0.05) / 0.05;
        return whiteContrast >= blackContrast ? Colors.White : Colors.Black;
    }

    private static double RelativeLuminance(Color color) =>
        (0.2126 * Linear(color.R)) +
        (0.7152 * Linear(color.G)) +
        (0.0722 * Linear(color.B));

    private static double Linear(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static Color Hex(string value) =>
        (Color)ColorConverter.ConvertFromString(value);
}
