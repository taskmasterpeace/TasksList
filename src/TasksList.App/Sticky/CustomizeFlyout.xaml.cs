using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TasksList.Core.Notes;

namespace TasksList.App.Sticky;

public partial class CustomizeFlyout : UserControl
{
    private bool _loading;

    public CustomizeFlyout()
    {
        InitializeComponent();
        FontFamilyBox.ItemsSource = Fonts.SystemFontFamilies
            .Select(family => family.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public event Action<double>? OpacityChanged;
    public event Action<double>? InactiveOpacityChanged;
    public event Action<PaperPreset>? PresetChanged;
    public event Action<string, string, string>? ColorsSelected;
    public event Action<string, double, int, double>? TypographySelected;
    public event Action<NoteDensity>? DensitySelected;
    public event Action<ToolbarVisibility>? ToolbarSelected;
    public event Action<double, CornerStyle, bool, bool>? DecorationSelected;
    public event Action<string, bool>? SaveNamedStyleRequested;
    public event Action<NamedNoteStyle>? ApplyNamedStyleRequested;
    public event Action? ResetRequested;

    public void Load(NotePresentation presentation)
    {
        _loading = true;
        OpacitySlider.Value = presentation.ActiveOpacity * 100;
        OpacityValue.Text = $"{presentation.ActiveOpacity:P0}";
        InactiveOpacitySlider.Value = presentation.InactiveOpacity * 100;
        InactiveOpacityValue.Text = $"{presentation.InactiveOpacity:P0}";
        BackgroundColorBox.Text = presentation.BackgroundHex;
        TextColorBox.Text = presentation.TextHex;
        AccentColorBox.Text = presentation.AccentHex;
        FontFamilyBox.SelectedItem = FontFamilyBox.Items
            .Cast<string>()
            .FirstOrDefault(name => string.Equals(name, presentation.FontFamily, StringComparison.OrdinalIgnoreCase));
        SelectComboValue(FontSizeBox, presentation.FontSize.ToString("0"));
        SelectComboValue(FontWeightBox, presentation.FontWeight.ToString());
        SelectComboValue(LineSpacingBox, presentation.LineSpacing.ToString("0.0"));
        SelectComboValue(DensityBox, presentation.Density.ToString());
        SelectComboValue(ToolbarBox, presentation.ToolbarVisibility.ToString());
        ShadowSlider.Value = presentation.ShadowStrength * 100;
        SelectComboValue(CornerBox, presentation.CornerStyle.ToString());
        BorderCheck.IsChecked = presentation.BorderVisible;
        TextureCheck.IsChecked = presentation.TextureEnabled;
        _loading = false;
        UpdateContrastFeedback();
    }

    public void SetNamedStyles(IReadOnlyList<NamedNoteStyle> styles)
    {
        NamedStyleBox.ItemsSource = styles;
        NamedStyleBox.SelectedIndex = styles.Count > 0 ? 0 : -1;
    }

    private void OpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        OpacityValue.Text = $"{e.NewValue:0}%";
        if (!_loading)
        {
            OpacityChanged?.Invoke(e.NewValue / 100);
        }
    }

    private void InactiveOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        InactiveOpacityValue.Text = $"{e.NewValue:0}%";
        if (!_loading)
        {
            InactiveOpacityChanged?.Invoke(e.NewValue / 100);
        }
    }

    private void PresetClick(object sender, RoutedEventArgs e)
    {
        if (!_loading && sender is Button { Tag: string value } &&
            Enum.TryParse<PaperPreset>(value, out var preset))
        {
            PresetChanged?.Invoke(preset);
        }
    }

    private void ColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            UpdateContrastFeedback();
        }
    }

    private void ApplyColorsClick(object sender, RoutedEventArgs e)
    {
        if (AllColorsValid())
        {
            ColorsSelected?.Invoke(
                BackgroundColorBox.Text,
                TextColorBox.Text,
                AccentColorBox.Text);
        }
    }

    private void TypographyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || FontFamilyBox.SelectedItem is not string family ||
            !TryComboDouble(FontSizeBox, out var size) ||
            !TryComboInt(FontWeightBox, out var weight) ||
            !TryComboDouble(LineSpacingBox, out var lineSpacing))
        {
            return;
        }

        TypographySelected?.Invoke(family, size, weight, lineSpacing);
    }

    private void DensityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && TryComboEnum(DensityBox, out NoteDensity density))
        {
            DensitySelected?.Invoke(density);
        }
    }

    private void ToolbarChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && TryComboEnum(ToolbarBox, out ToolbarVisibility visibility))
        {
            ToolbarSelected?.Invoke(visibility);
        }
    }

    private void DecorationChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading && TryComboEnum(CornerBox, out CornerStyle corner))
        {
            DecorationSelected?.Invoke(
                ShadowSlider.Value / 100,
                corner,
                BorderCheck.IsChecked == true,
                TextureCheck.IsChecked == true);
        }
    }

    private void SaveNamedStyleClick(object sender, RoutedEventArgs e)
    {
        var name = StyleNameBox.Text.Trim();
        if (name.Length > 0)
        {
            SaveNamedStyleRequested?.Invoke(name, DefaultStyleCheck.IsChecked == true);
        }
    }

    private void ApplyNamedStyleClick(object sender, RoutedEventArgs e)
    {
        if (NamedStyleBox.SelectedItem is NamedNoteStyle style)
        {
            ApplyNamedStyleRequested?.Invoke(style);
        }
    }

    private void ResetClick(object sender, RoutedEventArgs e) => ResetRequested?.Invoke();

    private void UpdateContrastFeedback()
    {
        if (!AllColorsValid())
        {
            ContrastText.Text = "Use #RRGGBB colors";
            ContrastText.Foreground = Brushes.Firebrick;
            return;
        }

        var ratio = NoteAppearancePolicy.ContrastRatio(TextColorBox.Text, BackgroundColorBox.Text);
        var accessible = ratio >= 4.5;
        ContrastText.Text = $"Contrast {ratio:0.0}:1 · {(accessible ? "Good" : "Low")}";
        ContrastText.Foreground = accessible ? Brushes.SeaGreen : Brushes.Firebrick;
    }

    private bool AllColorsValid() =>
        NoteAppearancePolicy.IsLocalHexColor(BackgroundColorBox.Text) &&
        NoteAppearancePolicy.IsLocalHexColor(TextColorBox.Text) &&
        NoteAppearancePolicy.IsLocalHexColor(AccentColorBox.Text);

    private static bool TryComboDouble(ComboBox comboBox, out double value) =>
        double.TryParse((comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out value);

    private static bool TryComboInt(ComboBox comboBox, out int value) =>
        int.TryParse((comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out value);

    private static bool TryComboEnum<T>(ComboBox comboBox, out T value) where T : struct, Enum =>
        Enum.TryParse((comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out value);

    private static void SelectComboValue(ComboBox comboBox, string value)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Content?.ToString(),
                value,
                StringComparison.OrdinalIgnoreCase));
    }
}
