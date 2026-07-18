using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using TasksList.Core.Markdown;
using TasksList.Core.Models;
using TasksList.Infrastructure.Storage;

namespace TasksList.App.Editor;

public static class InteractiveBlockControls
{
    public static Block Build(
        InteractiveBlock block,
        Action<InteractiveBlock, int> valueChanged,
        NoteId noteId,
        TasksListDatabase database)
    {
        UIElement control = block switch
        {
            ProgressInteractiveBlock progress => Progress(progress, valueChanged),
            CounterInteractiveBlock counter => Counter(counter, valueChanged),
            TimerInteractiveBlock timer => new InteractiveTimerControl(timer, noteId, database),
            _ => new TextBlock { Text = block.Label },
        };
        return new BlockUIContainer(control)
        {
            Margin = new Thickness(0, 7, 0, 9),
        };
    }

    private static UIElement Progress(
        ProgressInteractiveBlock block,
        Action<InteractiveBlock, int> changed)
    {
        var valueText = new TextBlock
        {
            Text = $"{block.Value}%",
            FontWeight = FontWeights.SemiBold,
            Foreground = Ink,
        };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = block.Value,
            TickFrequency = 1,
            Margin = new Thickness(0, 9, 0, 0),
        };
        slider.ValueChanged += (_, args) =>
        {
            var value = (int)Math.Round(args.NewValue);
            valueText.Text = $"{value}%";
            changed(block, value);
        };
        return Card(block.Label, valueText, slider);
    }

    private static UIElement Counter(
        CounterInteractiveBlock block,
        Action<InteractiveBlock, int> changed)
    {
        var value = block.Value;
        var valueText = new TextBlock
        {
            Text = value.ToString(),
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = Ink,
            MinWidth = 46,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var minus = MiniButton("−", "Decrease counter");
        var plus = MiniButton("+", "Increase counter");
        minus.Click += (_, _) =>
        {
            value--;
            valueText.Text = value.ToString();
            changed(block, value);
        };
        plus.Click += (_, _) =>
        {
            value++;
            valueText.Text = value.ToString();
            changed(block, value);
        };
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(minus);
        controls.Children.Add(valueText);
        controls.Children.Add(plus);
        return Card(block.Label, controls);
    }

    private static Border Card(string label, params UIElement[] content)
    {
        var panel = new StackPanel();
        var header = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ink,
        };
        panel.Children.Add(header);
        foreach (var item in content) panel.Children.Add(item);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(22, 45, 40, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 45, 40, 30)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10, 12, 10),
            Child = panel,
        };
    }

    internal static Button MiniButton(string content, string automationName) => new()
    {
        Content = content,
        MinWidth = 38,
        MinHeight = 32,
        Margin = new Thickness(4, 0, 4, 0),
        Padding = new Thickness(8, 3, 8, 3),
        ToolTip = automationName,
    };

    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(58, 48, 34));
}

public sealed class InteractiveTimerControl : Border
{
    private readonly TimerInteractiveBlock _block;
    private readonly NoteId _noteId;
    private readonly TasksListDatabase _database;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _timeText;
    private readonly Button _startPause;
    private InteractiveTimerState _state;

    public InteractiveTimerControl(
        TimerInteractiveBlock block,
        NoteId noteId,
        TasksListDatabase database)
    {
        _block = block;
        _noteId = noteId;
        _database = database;
        _state = InteractiveTimerState.Default(noteId, block.TypeIndex, block.Minutes, DateTimeOffset.Now);

        Background = new SolidColorBrush(Color.FromArgb(22, 45, 40, 30));
        BorderBrush = new SolidColorBrush(Color.FromArgb(42, 45, 40, 30));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(12, 10, 12, 10);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel();
        labels.Children.Add(new TextBlock
        {
            Text = block.Label,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(58, 48, 34)),
        });
        _timeText = new TextBlock
        {
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(58, 48, 34)),
            Margin = new Thickness(0, 4, 0, 0),
        };
        labels.Children.Add(_timeText);
        root.Children.Add(labels);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _startPause = InteractiveBlockControls.MiniButton("Start", "Start or pause timer");
        var reset = InteractiveBlockControls.MiniButton("Reset", "Reset timer");
        _startPause.Click += StartPauseClick;
        reset.Click += ResetClick;
        buttons.Children.Add(_startPause);
        buttons.Children.Add(reset);
        Grid.SetColumn(buttons, 1);
        root.Children.Add(buttons);
        Child = root;

        _timer.Tick += TimerTick;
        Loaded += LoadState;
        Unloaded += (_, _) => _timer.Stop();
        UpdateDisplay();
    }

    private async void LoadState(object sender, RoutedEventArgs e)
    {
        var loaded = await _database.GetInteractiveTimerStateAsync(_noteId, _block.TypeIndex);
        if (loaded is not null && loaded.DurationSeconds == _block.Minutes * 60)
        {
            _state = loaded;
        }
        if (_state.IsRunning) _timer.Start();
        UpdateDisplay();
    }

    private async void StartPauseClick(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.Now;
        if (_state.IsRunning)
        {
            _state = _state with
            {
                RemainingSeconds = _state.RemainingAt(now),
                IsRunning = false,
                EndsAt = null,
                ModifiedAt = now,
            };
            _timer.Stop();
        }
        else
        {
            var remaining = _state.RemainingSeconds == 0
                ? _state.DurationSeconds
                : _state.RemainingSeconds;
            _state = _state with
            {
                RemainingSeconds = remaining,
                IsRunning = true,
                EndsAt = now.AddSeconds(remaining),
                ModifiedAt = now,
            };
            _timer.Start();
        }
        await _database.SaveInteractiveTimerStateAsync(_state);
        UpdateDisplay();
    }

    private async void ResetClick(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _state = InteractiveTimerState.Default(
            _noteId,
            _block.TypeIndex,
            _block.Minutes,
            DateTimeOffset.Now);
        await _database.SaveInteractiveTimerStateAsync(_state);
        UpdateDisplay();
    }

    private async void TimerTick(object? sender, EventArgs e)
    {
        var remaining = _state.RemainingAt(DateTimeOffset.Now);
        if (remaining <= 0)
        {
            _timer.Stop();
            _state = _state with
            {
                RemainingSeconds = 0,
                IsRunning = false,
                EndsAt = null,
                ModifiedAt = DateTimeOffset.Now,
            };
            await _database.SaveInteractiveTimerStateAsync(_state);
            SystemSounds.Exclamation.Play();
        }
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var remaining = _state.RemainingAt(DateTimeOffset.Now);
        _timeText.Text = $"{remaining / 60:00}:{remaining % 60:00}";
        _startPause.Content = _state.IsRunning ? "Pause" : "Start";
    }
}
