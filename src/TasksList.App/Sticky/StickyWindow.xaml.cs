using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TasksList.App.Editor;
using TasksList.Core.Markdown;
using TasksList.Core.Models;
using TasksList.Infrastructure.Storage;

namespace TasksList.App.Sticky;

public partial class StickyWindow : Window
{
    private readonly TasksListDatabase _database;
    private readonly DispatcherTimer _saveTimer;
    private readonly MarkdownDocumentService _markdownService = new();
    private Note _note;
    private bool _isLoading = true;
    private bool _isRolled;
    private double _expandedHeight;
    private bool _isPreviewing;

    public StickyWindow(Note note, TasksListDatabase database)
    {
        _note = note;
        _database = database;
        InitializeComponent();
        TitleBox.Text = note.Title;
        MarkdownBox.Text = note.Markdown;
        ContextText.Text = note.Attachments.Count == 0 ? "UNATTACHED" : "CONTEXT ATTACHED";
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _saveTimer.Tick += SaveTimerTick;
        _isLoading = false;
    }

    public event EventHandler? NoteSaved;

    private void ContentChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isLoading || _saveTimer is null)
        {
            return;
        }

        SaveText.Text = "EDITING";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void SaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        _note = _note.UpdateContent(TitleBox.Text.Trim(), MarkdownBox.Text);
        await _database.SaveNoteAsync(_note);
        SaveText.Text = "SAVED";
        NoteSaved?.Invoke(this, EventArgs.Empty);
    }

    private void PinClick(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Foreground = Topmost
            ? System.Windows.Media.Brushes.SeaGreen
            : System.Windows.Media.Brushes.SaddleBrown;
        PinButton.ToolTip = Topmost ? "Always on top" : "Normal window level";
    }

    private void ModeClick(object sender, RoutedEventArgs e)
    {
        _isPreviewing = !_isPreviewing;
        if (_isPreviewing)
        {
            PreviewViewer.Document = MarkdownFlowDocumentBuilder.Build(
                _markdownService.Parse(MarkdownBox.Text),
                ToggleTaskFromPreview);
            MarkdownBox.Visibility = Visibility.Collapsed;
            PreviewViewer.Visibility = Visibility.Visible;
            ModeButton.Content = "EDIT";
        }
        else
        {
            PreviewViewer.Visibility = Visibility.Collapsed;
            MarkdownBox.Visibility = Visibility.Visible;
            ModeButton.Content = "PREVIEW";
            MarkdownBox.Focus();
        }
    }

    private void ToggleTaskFromPreview(int taskIndex, bool isChecked)
    {
        var parsed = _markdownService.Parse(MarkdownBox.Text);
        var task = parsed.Blocks.OfType<MarkdownTask>().Single(item => item.TaskIndex == taskIndex);
        if (task.IsChecked == isChecked)
        {
            return;
        }

        MarkdownBox.Text = _markdownService.ToggleTask(MarkdownBox.Text, taskIndex);
        PreviewViewer.Document = MarkdownFlowDocumentBuilder.Build(
            _markdownService.Parse(MarkdownBox.Text),
            ToggleTaskFromPreview);
    }

    private void RollClick(object sender, RoutedEventArgs e)
    {
        if (!_isRolled)
        {
            _expandedHeight = ActualHeight;
            ContentRow.Height = new GridLength(0);
            FooterRow.Height = new GridLength(0);
            Height = 50;
            ResizeMode = ResizeMode.NoResize;
        }
        else
        {
            ContentRow.Height = new GridLength(1, GridUnitType.Star);
            FooterRow.Height = new GridLength(34);
            Height = Math.Max(_expandedHeight, 230);
            ResizeMode = ResizeMode.CanResizeWithGrip;
        }

        _isRolled = !_isRolled;
    }

    private void TitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.Source is not System.Windows.Controls.TextBox)
        {
            DragMove();
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    protected override async void OnClosed(EventArgs e)
    {
        if (_saveTimer.IsEnabled)
        {
            _saveTimer.Stop();
            _note = _note.UpdateContent(TitleBox.Text.Trim(), MarkdownBox.Text);
            await _database.SaveNoteAsync(_note);
            NoteSaved?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosed(e);
    }
}
