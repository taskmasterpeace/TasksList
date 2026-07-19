using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TasksList.App.Shell;

namespace TasksList.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;
    private readonly List<HotkeyRow> _rows;

    public SettingsWindow(AppSettings settings)
    {
        _original = settings;
        InitializeComponent();
        SourceInitialized += (_, _) => DwmWindowService.Apply(this, DwmWindowKind.Transient);
        SnapSlider.Value = settings.SnapTolerance;
        ReduceMotionCheck.IsChecked = settings.ReduceMotion;
        PauseMonitoringCheck.IsChecked = settings.MonitoringPaused;
        PromoteDuplicatesCheck.IsChecked = settings.PromoteDuplicateClips;
        ExcludedAppsBox.Text = string.Join(Environment.NewLine, settings.ExcludedClipboardApplications);
        _rows = Enum.GetValues<AppHotkeyAction>()
            .Select(action => new HotkeyRow(
                action,
                GlobalHotkeyBindingPolicy.DisplayName(action),
                settings.Hotkeys.TryGetValue(action, out var gesture)
                    ? HotkeyGestureText.Format(gesture)
                    : string.Empty))
            .ToList();
        HotkeyRows.ItemsSource = _rows;
    }

    public AppSettings? Result { get; private set; }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var hotkeys = new Dictionary<AppHotkeyAction, HotkeyGesture>();
        foreach (var row in _rows)
        {
            if (!HotkeyGestureText.TryParse(row.Text, out var gesture))
            {
                ShowError($"{row.Label}: enter a modifier and key, for example Ctrl+Shift+V.");
                return;
            }
            hotkeys[row.Action] = gesture;
        }

        var result = _original with
        {
            SnapTolerance = (int)SnapSlider.Value,
            ReduceMotion = ReduceMotionCheck.IsChecked == true,
            MonitoringPaused = PauseMonitoringCheck.IsChecked == true,
            PromoteDuplicateClips = PromoteDuplicatesCheck.IsChecked == true,
            ExcludedClipboardApplications = ExcludedAppsBox.Text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Hotkeys = hotkeys,
        };
        var errors = GlobalHotkeyBindingPolicy.Validate(result);
        if (errors.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, errors.Select(error => error.Message)));
            return;
        }

        Result = result;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

public sealed class HotkeyRow : INotifyPropertyChanged
{
    private string _text;

    public HotkeyRow(AppHotkeyAction action, string label, string text)
    {
        Action = action;
        Label = label;
        _text = text;
    }

    public AppHotkeyAction Action { get; }
    public string Label { get; }
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
