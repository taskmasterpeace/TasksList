using System.Windows.Media;
using TasksList.App.Theming;

namespace TasksList.App.Tests.Theming;

public sealed class WindowsThemePaletteTests
{
    [Fact]
    public void LightAndDarkPalettesContainDistinctCompleteSemanticTokens()
    {
        var accent = Color.FromRgb(0, 120, 212);

        var light = WindowsThemePalette.Create(false, false, accent);
        var dark = WindowsThemePalette.Create(true, false, accent);

        Assert.NotEqual(light.Window, dark.Window);
        Assert.NotEqual(light.Text, dark.Text);
        Assert.Equal(accent, light.Accent);
        Assert.Equal(accent, dark.Accent);
        Assert.All(light.ToResourceColors(), pair => Assert.NotEqual(default, pair.Value));
        Assert.Equal(14, light.ToResourceColors().Count);
    }

    [Fact]
    public void AccentTextChoosesAReadablePolarity()
    {
        var darkAccent = WindowsThemePalette.Create(
            false,
            false,
            Color.FromRgb(20, 45, 80));
        var lightAccent = WindowsThemePalette.Create(
            true,
            false,
            Color.FromRgb(250, 210, 80));

        Assert.Equal(Colors.White, darkAccent.AccentText);
        Assert.Equal(Colors.Black, lightAccent.AccentText);
    }

    [Fact]
    public void HighContrastUsesSuppliedSystemColorsAndIgnoresDecorativeAccent()
    {
        var system = new WindowsHighContrastColors(
            Window: Colors.Black,
            WindowText: Colors.Yellow,
            Control: Colors.Navy,
            ControlText: Colors.White,
            Highlight: Colors.Lime,
            HighlightText: Colors.Black,
            GrayText: Colors.Gray);

        var palette = WindowsThemePalette.Create(
            useDarkMode: false,
            highContrast: true,
            accent: Colors.Magenta,
            system);

        Assert.Equal(system.Window, palette.Window);
        Assert.Equal(system.WindowText, palette.Text);
        Assert.Equal(system.Highlight, palette.Accent);
        Assert.Equal(system.HighlightText, palette.AccentText);
        Assert.Equal(system.GrayText, palette.DisabledText);
    }
}
