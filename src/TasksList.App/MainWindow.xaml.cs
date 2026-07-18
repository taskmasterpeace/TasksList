using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TasksList.App.Sticky;
using TasksList.App.Clipboard;
using TasksList.Core.Models;
using TasksList.Infrastructure.Storage;

namespace TasksList.App;

public partial class MainWindow : Window
{
    private readonly TasksListDatabase _database;
    private readonly ObservableCollection<NoteCardViewModel> _notes = [];
    private readonly ObservableCollection<ClipboardCardViewModel> _clipboardCards = [];
    private readonly Dictionary<NoteId, StickyWindow> _openStickies = [];
    private readonly ClipboardMonitor _clipboardMonitor;

    public MainWindow(TasksListDatabase database)
    {
        _database = database;
        InitializeComponent();
        NotesList.ItemsSource = _notes;
        ClipboardList.ItemsSource = _clipboardCards;
        _clipboardMonitor = new ClipboardMonitor(this, CaptureClipboardAsync);
        Loaded += OnLoaded;
        Closed += (_, _) => _clipboardMonitor.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ReloadNotesAsync(createWelcomeNote: true);
        await ReloadClipboardAsync();
    }

    private async Task ReloadNotesAsync(bool createWelcomeNote = false)
    {
        var notes = await _database.ListNotesAsync();
        if (createWelcomeNote && notes.Count == 0)
        {
            var welcome = Note.Create(
                "Welcome to Task'sList",
                "# Your work, right where it belongs\n\n" +
                "Create a sticky, keep it on top, or attach it to the application you are using.\n\n" +
                "- [ ] Make your first note\n" +
                "- [ ] Copy something and find it again\n" +
                "- [ ] File a clip into a place");
            await _database.SaveNoteAsync(welcome);
            notes = await _database.ListNotesAsync();
        }

        _notes.Clear();
        foreach (var note in notes)
        {
            _notes.Add(new NoteCardViewModel(note));
        }

        NoteCountText.Text = _notes.Count.ToString();
        ApplySearch();
    }

    private async void NewStickyClick(object sender, RoutedEventArgs e)
    {
        var note = Note.Create("New sticky", "# New sticky\n\nStart typing…");
        await _database.SaveNoteAsync(note);
        await ReloadNotesAsync();
        OpenSticky(note);
    }

    private void OpenNoteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NoteCardViewModel card })
        {
            OpenSticky(card.Note);
        }
    }

    private void OpenSticky(Note note)
    {
        if (_openStickies.TryGetValue(note.Id, out var existing))
        {
            existing.Activate();
            return;
        }

        var index = _openStickies.Count;
        var sticky = new StickyWindow(note, _database)
        {
            Left = Left + 310 + (index * 28),
            Top = Top + 120 + (index * 24),
        };
        sticky.NoteSaved += async (_, _) => await ReloadNotesAsync();
        sticky.Closed += (_, _) => _openStickies.Remove(note.Id);
        _openStickies[note.Id] = sticky;
        sticky.Show();
    }

    private async Task CaptureClipboardAsync(ClipboardSnapshot snapshot)
    {
        await _database.SaveContextAsync(snapshot.Source);
        var capture = Capture.Create(
            snapshot.Kind,
            snapshot.Source.Id,
            snapshot.PreviewText,
            DateTimeOffset.Now);
        foreach (var representation in snapshot.TextRepresentations)
        {
            capture = capture.WithTextRepresentation(representation.Key, representation.Value);
        }
        await _database.SaveCaptureAsync(capture);
        await ReloadClipboardAsync();
        StatusText.Text = $"CAPTURED · {snapshot.Source.DisplayName}";
    }

    private async Task ReloadClipboardAsync()
    {
        var captures = await _database.ListCapturesAsync();
        _clipboardCards.Clear();
        foreach (var capture in captures.Take(50))
        {
            var source = await _database.GetContextAsync(capture.SourceContextId);
            _clipboardCards.Add(new ClipboardCardViewModel(capture, source));
        }
    }

    private async void ClipToNoteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ClipboardCardViewModel card })
        {
            return;
        }

        var note = Note.Create(
            $"Clip from {card.SourceName}",
            $"# Captured from {card.SourceName}\n\n{card.Capture.PreviewText}")
            .AttachTo(card.Capture.SourceContextId, AttachmentVisibility.WhilePresent);
        await _database.SaveNoteAsync(note);
        await ReloadNotesAsync();
        OpenSticky(note);
    }

    private void CopyClipPlainClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ClipboardCardViewModel card })
        {
            System.Windows.Clipboard.SetText(
                card.Capture.TextRepresentations.TryGetValue("text/plain", out var plain)
                    ? plain
                    : card.Capture.PreviewText);
        }
    }

    private void SearchTextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

    private void MarkdownDragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasMarkdownFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void MarkdownDrop(object sender, DragEventArgs e)
    {
        if (!HasMarkdownFile(e.Data) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files)
        {
            return;
        }

        foreach (var path in files.Where(IsMarkdownPath))
        {
            var markdown = await File.ReadAllTextAsync(path);
            var title = Path.GetFileNameWithoutExtension(path);
            var note = Note.Create(title, markdown);
            await _database.SaveNoteAsync(note);
        }

        await ReloadNotesAsync();
        StatusText.Text = $"IMPORTED · {files.Count(IsMarkdownPath)} MARKDOWN";
    }

    private static bool HasMarkdownFile(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) &&
        data.GetData(DataFormats.FileDrop) is string[] files &&
        files.Any(IsMarkdownPath);

    private static bool IsMarkdownPath(string path) =>
        string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), ".markdown", StringComparison.OrdinalIgnoreCase);

    private void ApplySearch()
    {
        if (NotesList is null)
        {
            return;
        }

        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        NotesList.Items.Filter = item =>
        {
            if (item is not NoteCardViewModel card || query.Length == 0)
            {
                return true;
            }

            return card.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   card.Note.Markdown.Contains(query, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void TitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            DragMove();
        }
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}

public sealed class NoteCardViewModel
{
    private static readonly Brush[] Papers =
    [
        new SolidColorBrush(Color.FromRgb(245, 206, 91)),
        new SolidColorBrush(Color.FromRgb(235, 163, 111)),
        new SolidColorBrush(Color.FromRgb(151, 203, 176)),
        new SolidColorBrush(Color.FromRgb(166, 184, 222)),
    ];

    public NoteCardViewModel(Note note)
    {
        Note = note;
        var colorIndex = Math.Abs(note.Id.Value.GetHashCode()) % Papers.Length;
        PaperBrush = Papers[colorIndex];
        EdgeBrush = new SolidColorBrush(Color.FromArgb(80, 45, 38, 25));
        InkBrush = new SolidColorBrush(Color.FromRgb(46, 39, 28));
        MutedInkBrush = new SolidColorBrush(Color.FromRgb(93, 78, 58));
        StatusBrush = new SolidColorBrush(Color.FromRgb(61, 101, 80));
    }

    public Note Note { get; }

    public string Title => Note.Title;

    public string Preview => Note.Markdown
        .Replace("#", string.Empty, StringComparison.Ordinal)
        .Replace("- [ ]", "□", StringComparison.Ordinal)
        .Replace("- [x]", "■", StringComparison.OrdinalIgnoreCase)
        .Trim();

    public string SourceLabel => Note.Attachments.Count == 0 ? "UNATTACHED" : "CONTEXT ATTACHED";

    public Brush PaperBrush { get; }

    public Brush EdgeBrush { get; }

    public Brush InkBrush { get; }

    public Brush MutedInkBrush { get; }

    public Brush StatusBrush { get; }
}

public sealed class ClipboardCardViewModel
{
    public ClipboardCardViewModel(Capture capture, ContextRef? source)
    {
        Capture = capture;
        SourceName = source?.DisplayName ?? "Unknown application";
    }

    public Capture Capture { get; }

    public string Preview => Capture.PreviewText.Replace("\r", string.Empty, StringComparison.Ordinal).Trim();

    public string SourceName { get; }

    public string SourceLabel => $"SOURCE  {SourceName}";

    public string TimeLabel => Capture.CapturedAt.LocalDateTime.ToString("h:mm tt");

    public string FormatLabel => Capture.Kind switch
    {
        CaptureKind.RichText => "RICH TEXT",
        CaptureKind.Html => "HTML + TEXT",
        CaptureKind.Markdown => "MARKDOWN",
        _ => "TEXT",
    };
}
