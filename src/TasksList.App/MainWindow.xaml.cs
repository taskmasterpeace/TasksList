using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TasksList.App.Sticky;
using TasksList.App.Clipboard;
using TasksList.App.Capture;
using TasksList.App.Places;
using TasksList.App.Plugins;
using TasksList.Core.Models;
using TasksList.Infrastructure.Storage;
using CaptureModel = TasksList.Core.Models.Capture;
using TasksList.Core.Contexts;

namespace TasksList.App;

public partial class MainWindow : Window
{
    private readonly TasksListDatabase _database;
    private readonly ObservableCollection<NoteCardViewModel> _notes = [];
    private readonly ObservableCollection<ClipboardCardViewModel> _clipboardCards = [];
    private readonly ObservableCollection<PlaceCardViewModel> _placeCards = [];
    private readonly Dictionary<NoteId, StickyWindow> _openStickies = [];
    private readonly ClipboardMonitor _clipboardMonitor;
    private readonly BrowserBridgeMonitor _browserBridgeMonitor;
    private readonly PayloadStore _payloadStore;
    private readonly string _dataDirectory;
    private IReadOnlyList<LiveBrowserTab> _liveBrowserTabs = [];
    private readonly DispatcherTimer _contextTimer;
    private ContextRef? _lastExternalContext;
    private bool _contextTickRunning;

    public MainWindow(TasksListDatabase database, string? dataDirectory = null)
    {
        _database = database;
        InitializeComponent();
        NotesList.ItemsSource = _notes;
        ClipboardList.ItemsSource = _clipboardCards;
        PlacesList.ItemsSource = _placeCards;
        _clipboardMonitor = new ClipboardMonitor(this, CaptureClipboardAsync);
        var resolvedDataDirectory = dataDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TasksList");
        _dataDirectory = resolvedDataDirectory;
        _payloadStore = new PayloadStore(Path.Combine(resolvedDataDirectory, "payloads"));
        var browserBridgePath = Path.Combine(
            resolvedDataDirectory,
            "bridge",
            "browser-tabs.json");
        _browserBridgeMonitor = new BrowserBridgeMonitor(browserBridgePath, BrowserSnapshotChangedAsync);
        _contextTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _contextTimer.Tick += ContextTimerTick;
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _clipboardMonitor.Dispose();
            _browserBridgeMonitor.Dispose();
            _contextTimer.Stop();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ReloadNotesAsync(createWelcomeNote: true);
        await ReloadClipboardAsync();
        await ReloadPlacesAsync();
        _contextTimer.Start();
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

    private async void CaptureRegionClick(object sender, RoutedEventArgs e)
    {
        var source = ForegroundContextReader.Read();
        var overlay = new CaptureOverlay();
        if (overlay.ShowDialog() != true || overlay.Result is not { } result)
        {
            return;
        }

        var payload = await _payloadStore.PutAsync(result.PngBytes, "image/png");
        await _database.SaveContextAsync(source);
        var capture = CaptureModel.Create(
                CaptureKind.Image,
                source.Id,
                $"Screen capture · {result.PixelWidth} × {result.PixelHeight}",
                DateTimeOffset.Now)
            .WithTextRepresentation("application/x-taskslist-payload-path", payload.Path);
        await _database.SaveCaptureAsync(capture);
        var note = Note.Create(
                $"Capture from {source.DisplayName}",
                $"# Screen capture\n\n![Captured region](<{payload.Path}>)\n\n" +
                $"**Source:** {source.DisplayName}\n\n" +
                $"**Size:** {result.PixelWidth} × {result.PixelHeight}")
            .AttachTo(source.Id, AttachmentVisibility.WhilePresent);
        await _database.SaveNoteAsync(note);
        await ReloadClipboardAsync();
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
        var sticky = new StickyWindow(note, _database, () => _lastExternalContext)
        {
            Left = Left + 310 + (index * 28),
            Top = Top + 120 + (index * 24),
        };
        sticky.NoteSaved += async (_, _) => await ReloadNotesAsync();
        sticky.Closed += (_, _) => _openStickies.Remove(note.Id);
        _openStickies[note.Id] = sticky;
        sticky.Show();
    }

    private async void ContextTimerTick(object? sender, EventArgs e)
    {
        if (_contextTickRunning)
        {
            return;
        }

        _contextTickRunning = true;
        try
        {
            var observed = ForegroundContextReader.Read();
            if (!observed.StableIdentity.Contains("TasksList.App", StringComparison.OrdinalIgnoreCase))
            {
                _lastExternalContext = observed;
            }

            if (_lastExternalContext is null)
            {
                return;
            }

            var notes = await _database.ListNotesAsync();
            foreach (var note in notes.Where(note => note.Attachments.Count > 0))
            {
                var shouldShow = AttachmentVisibilityPolicy.ShouldShow(note, _lastExternalContext.Id);
                if (shouldShow && !_openStickies.ContainsKey(note.Id))
                {
                    OpenSticky(note);
                }
                else if (_openStickies.TryGetValue(note.Id, out var sticky))
                {
                    if (shouldShow && !sticky.IsVisible)
                    {
                        sticky.Show();
                    }
                    else if (!shouldShow && sticky.IsVisible)
                    {
                        sticky.Hide();
                    }
                }
            }
        }
        finally
        {
            _contextTickRunning = false;
        }
    }

    private async Task CaptureClipboardAsync(ClipboardSnapshot snapshot)
    {
        await _database.SaveContextAsync(snapshot.Source);
        var capture = CaptureModel.Create(
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

    private async Task ReloadPlacesAsync()
    {
        var places = await _database.ListPlacesAsync();
        var byId = places.ToDictionary(place => place.Id);
        _placeCards.Clear();
        foreach (var place in places.Where(place =>
                     place.Kind is PlaceKind.ManualGroup or PlaceKind.BrowserSession or PlaceKind.Project))
        {
            var depth = 0;
            var parent = place.ParentId;
            while (parent is { } parentId && byId.TryGetValue(parentId, out var parentPlace))
            {
                depth++;
                parent = parentPlace.ParentId;
            }
            _placeCards.Add(new PlaceCardViewModel(place, depth));
        }

        if (_liveBrowserTabs.Count > 0)
        {
            var browserPlaceId = DeterministicPlaceId("browser:chromium");
            var browser = new Place(browserPlaceId, PlaceKind.Browser, "Browser · Open now", null, "browser:chromium");
            _placeCards.Insert(0, new PlaceCardViewModel(browser, 0, $"{_liveBrowserTabs.Count} TABS"));
            var insertionIndex = 1;
            foreach (var tab in _liveBrowserTabs)
            {
                var place = new Place(
                    DeterministicPlaceId($"browser-tab:{tab.WindowId}:{tab.Id}"),
                    tab.Kind,
                    tab.Title,
                    browserPlaceId,
                    tab.Url);
                _placeCards.Insert(insertionIndex++, new PlaceCardViewModel(place, 1, tab.IsActive ? "ACTIVE" : string.Empty));
            }
        }
    }

    private async Task BrowserSnapshotChangedAsync(LiveBrowserSnapshot snapshot)
    {
        await await Dispatcher.InvokeAsync(async () =>
        {
            _liveBrowserTabs = snapshot.Tabs;
            await ReloadPlacesAsync();
            StatusText.Text = $"BROWSER · {snapshot.Tabs.Count} OPEN TABS";
        });
    }

    private static PlaceId DeterministicPlaceId(string identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant()));
        return new PlaceId(new Guid(hash.AsSpan(0, 16)));
    }

    private async void NewPlaceClick(object sender, RoutedEventArgs e)
    {
        var places = await _database.ListPlacesAsync();
        var dialog = new NewPlaceDialog(places) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var place = Place.Create(
            PlaceKind.ManualGroup,
            dialog.PlaceName,
            dialog.ParentPlaceId,
            $"manual:{Guid.NewGuid():N}");
        await _database.SavePlaceAsync(place);
        await ReloadPlacesAsync();
    }

    private void PluginsClick(object sender, RoutedEventArgs e)
    {
        var bundled = PluginCatalog.Load(Path.Combine(AppContext.BaseDirectory, "plugins"));
        var personal = PluginCatalog.Load(Path.Combine(_dataDirectory, "plugins"));
        var catalog = new PluginCatalogSnapshot(
            bundled.Plugins.Concat(personal.Plugins)
                .GroupBy(entry => entry.Manifest.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.Manifest.Version, StringComparer.Ordinal).First())
                .OrderBy(entry => entry.Manifest.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            bundled.Errors.Concat(personal.Errors).ToArray());
        var window = new PluginManagerWindow(catalog) { Owner = this };
        window.ShowDialog();
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
    public ClipboardCardViewModel(CaptureModel capture, ContextRef? source)
    {
        Capture = capture;
        SourceName = source?.DisplayName ?? "Unknown application";
    }

    public CaptureModel Capture { get; }

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

public sealed class PlaceCardViewModel
{
    public PlaceCardViewModel(Place place, int depth, string? kindLabel = null)
    {
        Place = place;
        Prefix = depth == 0 ? "▾" : new string(' ', depth * 2) + "↳";
        KindLabel = kindLabel ?? (place.Kind == PlaceKind.BrowserSession ? "SESSION" : string.Empty);
    }

    public Place Place { get; }

    public string Name => Place.Name;

    public string Prefix { get; }

    public string StableIdentity => Place.StableIdentity;

    public string KindLabel { get; }
}
