using System.Globalization;
using System.Windows;
using TasksList.App.Shell;
using TasksList.Core.Notes;

namespace TasksList.App.Sticky;

public partial class ScheduleDialog : Window
{
    private readonly bool _allowAttention;

    public ScheduleDialog(string heading, bool allowAttention)
    {
        _allowAttention = allowAttention;
        InitializeComponent();
        SourceInitialized += (_, _) => DwmWindowService.Apply(this, DwmWindowKind.Transient);
        HeadingText.Text = heading;
        AttentionPanel.Visibility = allowAttention ? Visibility.Visible : Visibility.Collapsed;
        DateBox.SelectedDate = DateTime.Today.AddDays(1);
    }

    public DateTimeOffset SelectedDateTime { get; private set; }

    public ReminderAttention Attention => (SoundCheck.IsChecked, PulseCheck.IsChecked) switch
    {
        (true, true) => ReminderAttention.SoundAndPulse,
        (true, _) => ReminderAttention.Sound,
        (_, true) => ReminderAttention.Pulse,
        _ => ReminderAttention.None,
    };

    public bool RemainTopmost => TopmostCheck.IsChecked == true;

    private void ScheduleClick(object sender, RoutedEventArgs e)
    {
        if (DateBox.SelectedDate is not { } date ||
            !TimeSpan.TryParseExact(
                TimeBox.Text.Trim(),
                ["h\\:mm", "hh\\:mm"],
                CultureInfo.InvariantCulture,
                out var time))
        {
            ShowError("Choose a date and enter time as HH:mm.");
            return;
        }

        var local = date.Date.Add(time);
        SelectedDateTime = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        if (SelectedDateTime <= DateTimeOffset.Now)
        {
            ShowError("Choose a future date and time.");
            return;
        }

        if (_allowAttention && Attention == ReminderAttention.None)
        {
            ShowError("Select sound, pulse, or both.");
            return;
        }

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
