using System.Collections.ObjectModel;
using System.ComponentModel;
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
using TasksList.Core.Notes;
using TasksList.Core.Clipboard;
using TasksList.App.Shell;
using TasksList.App.Library;

namespace TasksList.App;

public partial class MainWindow : Window
{
    private readonly TasksListDatabase _database;
    private readonly ObservableCollection<NoteCardViewModel> _notes = [];
    private readonly ObservableCollection<ClipboardCardViewModel> _clipboardCards = [];
    private readonly ObservableCollection<PlaceCardViewModel> _placeCards = [];
    private readonly ObservableCollection<ContextLibraryRow> _contextCards = [];
    private readonly ObservableCollection<ExtensionLibraryRow> _extensionCards = [];
    private readonly ObservableCollection<TrashCardViewModel> _trashCards = [];
    private readonly Dictionary<NoteId, StickyWindow> _openStickies = [];
    private readonly HashSet<NoteId> _openingStickies = [];
    private readonly ClipboardMonitor _clipboardMonitor;
    private readonly ClipboardPasteService _clipboardPasteService;
    private ClipboardPaletteWindow? _clipboardPalette;
    private readonly BrowserBridgeMonitor _browserBridgeMonitor;
    private readonly PayloadStore _payloadStore;
    private readonly string _dataDirectory;
    private IReadOnlyList<LiveBrowserTab> _liveBrowserTabs = [];
    private readonly DispatcherTimer _contextTimer;
    private ContextRef? _lastExternalContext;
    private bool _contextTickRunning;
    private bool _exitRequested;
    private readonly HashSet<NoteId> _activeReminders = [];
    private bool _promoteDuplicateClips = true;
    private double _paletteWidth = 940;
    private double _paletteHeight = 610;
    private int _snapTolerance = 12;
    private bool _reduceMotion;
    private bool _minimizeLibraryToTray = true;

    public event Action<double, double>? ClipboardPaletteSizeChanged;

    public MainWindow(TasksListDatabase database, string? dataDirectory = null)
    {
        _database = database;
        InitializeComponent();
        NotesList.ItemsSource = _notes;
        ClipboardList.ItemsSource = _clipboardCards;
        PlacesList.ItemsSource = _placeCards;
        ContextsList.ItemsSource = _contextCards;
        ExtensionsList.ItemsSource = _extensionCards;
        TrashList.ItemsSource = _trashCards;
        _clipboardMonitor = new ClipboardMonitor(this, CaptureClipboardAsync);
        _clipboardPasteService = new ClipboardPasteService(
            new WindowsClipboardPastePlatform(),
            _clipboardMonitor.SuppressNextChange);
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
        Closing += LibraryClosing;
        Closed += (_, _) =>
        {
            _clipboardMonitor.Dispose();
            _clipboardPalette?.DisposePalette();
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
        await ReloadLibraryAuxiliaryAsync();
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
            var presentation = await _database.GetNotePresentationAsync(note.Id);
            if (presentation.DeletedAt is null)
            {
                _notes.Add(new NoteCardViewModel(note, presentation));
            }
        }

        NoteCountText.Text = _notes.Count.ToString();
        ApplySearch();
    }

    private async void NewStickyClick(object sender, RoutedEventArgs e)
    {
        await NewStickyFromShellAsync();
    }

    private async void CaptureRegionClick(object sender, RoutedEventArgs e)
    {
        await CaptureRegionAsync();
    }

    private void ClipboardPaletteClick(object sender, RoutedEventArgs e) =>
        ShowClipboardPaletteFromShell();

    public async Task CaptureRegionAsync()
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

    public Task NewStickyFromShellAsync() =>
        CreateAndOpenStickyAsync("New sticky", "# New sticky\n\nStart typing…");

    public Task NewFromClipboardFromShellAsync()
    {
        var text = System.Windows.Clipboard.ContainsText()
            ? System.Windows.Clipboard.GetText()
            : string.Empty;
        return CreateAndOpenStickyAsync(
            "Clipboard note",
            string.IsNullOrWhiteSpace(text) ? "# Clipboard note" : text);
    }

    public void ShowLibraryFromShell()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public async void ShowClipboardPaletteFromShell()
    {
        if (_clipboardPalette?.IsVisible == true)
        {
            _clipboardPalette.Hide();
            return;
        }
        var target = ClipboardTargetReader.ForegroundWindow();
        _clipboardPalette ??= new ClipboardPaletteWindow(
            _database,
            _clipboardPasteService,
            captures => _ = CreateNoteFromCapturesAsync(captures));
        _clipboardPalette.Width = _paletteWidth;
        _clipboardPalette.Height = _paletteHeight;
        _clipboardPalette.SizePreferenceChanged -= PaletteSizePreferenceChanged;
        _clipboardPalette.SizePreferenceChanged += PaletteSizePreferenceChanged;
        await _clipboardPalette.ShowNearPointerAsync(target);
    }

    private void PaletteSizePreferenceChanged(double width, double height)
    {
        _paletteWidth = width;
        _paletteHeight = height;
        ClipboardPaletteSizeChanged?.Invoke(width, height);
    }

    private async Task CreateNoteFromCapturesAsync(IReadOnlyList<CaptureModel> captures)
    {
        if (captures.Count == 0) return;
        var sections = new List<string>();
        foreach (var capture in captures)
        {
            var source = await _database.GetContextAsync(capture.SourceContextId);
            var text = capture.TextRepresentations.TryGetValue("text/plain", out var plain)
                ? plain
                : capture.PreviewText;
            var heading = string.IsNullOrWhiteSpace(capture.Title) ? capture.Kind.ToString() : capture.Title;
            sections.Add($"## {heading}\n\n{text}\n\n> Source: {source?.DisplayName ?? "Unknown"} · {capture.CapturedAt:g}");
        }

        await CreateAndOpenStickyAsync(
            captures.Count == 1 ? "Clipboard note" : $"Clipboard collection ({captures.Count})",
            $"# Clipboard collection\n\n{string.Join("\n\n---\n\n", sections)}");
    }

    public void ToggleAllNotes()
    {
        var anyVisible = _openStickies.Values.Any(sticky => sticky.IsVisible);
        foreach (var sticky in _openStickies.Values)
        {
            if (anyVisible) sticky.Hide(); else sticky.Show();
        }
    }

    public void DisableGhostModeForAll()
    {
        foreach (var sticky in _openStickies.Values)
        {
            sticky.DisableGhostMode();
        }
    }

    public void SetClipboardMonitoringPaused(bool paused) =>
        _clipboardMonitor.IsPaused = paused;

    public void ApplyClipboardSettings(AppSettings settings)
    {
        _clipboardMonitor.IsPaused = settings.MonitoringPaused;
        _clipboardMonitor.ExcludedApplications = settings.ExcludedClipboardApplications.ToList();
        _promoteDuplicateClips = settings.PromoteDuplicateClips;
        _paletteWidth = settings.ClipboardPaletteWidth;
        _paletteHeight = settings.ClipboardPaletteHeight;
        _snapTolerance = settings.SnapTolerance;
        _reduceMotion = settings.ReduceMotion;
        _minimizeLibraryToTray = settings.MinimizeLibraryToTray;
    }

    public void PrepareForExit()
    {
        _exitRequested = true;
        Close();
    }

    private void LibraryClosing(object? sender, CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            if (_minimizeLibraryToTray)
            {
                Hide();
            }
            else
            {
                WindowState = WindowState.Minimized;
            }
        }
    }

    private void OpenNoteClick(object sender, RoutedEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }
        if (sender is Button { Tag: NoteCardViewModel card })
        {
            OpenSticky(card.Note);
        }
    }

    private async void ApplyStyleToSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = NotesList.SelectedItems.Cast<NoteCardViewModel>().ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show(
                this,
                "Select one or more note cards first. Use Ctrl+click or Shift+click to select several.",
                "Apply note style",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var menu = new ContextMenu { PlacementTarget = sender as Button };
        foreach (var preset in Enum.GetValues<PaperPreset>())
        {
            var item = new MenuItem { Header = preset.ToString(), Tag = NoteStyle.FromPreset(preset) };
            item.Click += async (_, _) => await ApplyStyleToNotesAsync(selected, (NoteStyle)item.Tag);
            menu.Items.Add(item);
        }

        var namedStyles = await _database.ListNamedStylesAsync();
        if (namedStyles.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var namedStyle in namedStyles)
            {
                var item = new MenuItem
                {
                    Header = $"Saved · {namedStyle.Name}",
                    Tag = namedStyle.Style,
                };
                item.Click += async (_, _) => await ApplyStyleToNotesAsync(selected, (NoteStyle)item.Tag);
                menu.Items.Add(item);
            }
        }
        menu.IsOpen = true;
    }

    private async Task ApplyStyleToNotesAsync(
        IReadOnlyList<NoteCardViewModel> selected,
        NoteStyle style)
    {
        foreach (var card in selected)
        {
            var presentation = await _database.GetNotePresentationAsync(card.Note.Id);
            var updated = presentation.ApplyStyle(style) with { ModifiedAt = DateTimeOffset.Now };
            await _database.SaveNotePresentationAsync(updated);
            if (_openStickies.TryGetValue(card.Note.Id, out var sticky))
            {
                sticky.ApplyExternalStyle(style);
            }
        }
        StatusText.Text = $"STYLED · {selected.Count} NOTE{(selected.Count == 1 ? string.Empty : "S")}";
    }

    private async void OpenSticky(Note note)
    {
        if (_openStickies.TryGetValue(note.Id, out var existing))
        {
            existing.RestoreVisibility();
            return;
        }

        if (!_openingStickies.Add(note.Id))
        {
            return;
        }

        try
        {
            var presentation = await _database.GetNotePresentationAsync(note.Id);
            if (presentation.CreatedAt == DateTimeOffset.UnixEpoch)
            {
                var index = _openStickies.Count;
                var now = DateTimeOffset.Now;
                var defaultStyle = (await _database.ListNamedStylesAsync())
                    .FirstOrDefault(style => style.IsDefault);
                if (defaultStyle is not null)
                {
                    presentation = presentation.ApplyStyle(defaultStyle.Style);
                }
                presentation = presentation with
                {
                    Bounds = presentation.Bounds with
                    {
                        Left = Left + 310 + (index * 28),
                        Top = Top + 120 + (index * 24),
                    },
                    CreatedAt = now,
                    ModifiedAt = now,
                };
                await _database.SaveNotePresentationAsync(presentation);
            }
            else if (presentation.HiddenAt is not null)
            {
                presentation = presentation with
                {
                    HiddenAt = null,
                    WakeAt = null,
                    ModifiedAt = DateTimeOffset.Now,
                };
                await _database.SaveNotePresentationAsync(presentation);
            }

            var requestedBounds = new WindowBounds(
                presentation.Bounds.Left,
                presentation.Bounds.Top,
                presentation.Bounds.Width,
                presentation.Bounds.Height);
            var visibleBounds = StickyWindowPlacement.ClampToMonitors(
                requestedBounds,
                MonitorWorkAreaProvider.GetWorkAreas());
            if (visibleBounds != requestedBounds)
            {
                presentation = presentation with
                {
                    Bounds = presentation.Bounds with
                    {
                        Left = visibleBounds.Left,
                        Top = visibleBounds.Top,
                        Width = visibleBounds.Width,
                        Height = visibleBounds.Height,
                        ExpandedHeight = Math.Min(
                            presentation.Bounds.ExpandedHeight,
                            visibleBounds.Height),
                    },
                    ModifiedAt = DateTimeOffset.Now,
                };
                await _database.SaveNotePresentationAsync(presentation);
            }

            var sticky = new StickyWindow(
                note,
                presentation,
                _database,
                () => _lastExternalContext,
                () => _openStickies
                    .Where(pair => pair.Key != note.Id)
                    .Select(pair => pair.Value.CurrentBounds)
                    .ToArray(),
                () => _snapTolerance,
                () => _reduceMotion);
            sticky.NoteSaved += async (_, _) => await ReloadNotesAsync();
            sticky.NewStickyRequested += async () =>
                await CreateAndOpenStickyBesideAsync(
                    "New sticky",
                    "# New sticky\n\nStart typing…",
                    sticky.CurrentBounds);
            sticky.NewFromClipboardRequested += async () =>
            {
                var text = System.Windows.Clipboard.ContainsText()
                    ? System.Windows.Clipboard.GetText()
                    : string.Empty;
                await CreateAndOpenStickyBesideAsync(
                    "Clipboard note",
                    string.IsNullOrWhiteSpace(text) ? "# Clipboard note" : text,
                    sticky.CurrentBounds);
            };
            sticky.DuplicateRequested += async source =>
            {
                var sourcePresentation = sticky.CurrentPresentation;
                var duplicate = Note.Create($"{source.Title} copy", source.Markdown);
                await _database.SaveNoteAsync(duplicate);
                var now = DateTimeOffset.Now;
                var duplicatePresentation = sourcePresentation with
                {
                    NoteId = duplicate.Id,
                    Bounds = sourcePresentation.Bounds with
                    {
                        Left = sourcePresentation.Bounds.Left + 24,
                        Top = sourcePresentation.Bounds.Top + 24,
                    },
                    HiddenAt = null,
                    DeletedAt = null,
                    CreatedAt = now,
                    ModifiedAt = now,
                };
                await _database.SaveNotePresentationAsync(duplicatePresentation);
                await ReloadNotesAsync();
                OpenSticky(duplicate);
            };
            sticky.ArchiveRequested += async _ =>
            {
                await ReloadNotesAsync();
                await ReloadTrashAsync();
            };
            sticky.Closed += (_, _) => _openStickies.Remove(note.Id);
            _openStickies[note.Id] = sticky;
            sticky.Show();
        }
        finally
        {
            _openingStickies.Remove(note.Id);
        }
    }

    private async Task CreateAndOpenStickyAsync(string title, string markdown)
    {
        var note = Note.Create(title, markdown);
        await _database.SaveNoteAsync(note);
        await ReloadNotesAsync();
        OpenSticky(note);
    }

    private async Task CreateAndOpenStickyBesideAsync(
        string title,
        string markdown,
        WindowBounds sourceBounds)
    {
        var note = Note.Create(title, markdown);
        await _database.SaveNoteAsync(note);
        var now = DateTimeOffset.Now;
        var presentation = NotePresentation.Default(note.Id, now) with
        {
            Bounds = new NoteBounds(
                sourceBounds.Left + 24,
                sourceBounds.Top + 24,
                Math.Max(260, sourceBounds.Width),
                Math.Max(230, sourceBounds.Height),
                Math.Max(230, sourceBounds.Height),
                string.Empty),
            CreatedAt = now,
            ModifiedAt = now,
        };
        var defaultStyle = (await _database.ListNamedStylesAsync())
            .FirstOrDefault(style => style.IsDefault);
        if (defaultStyle is not null) presentation = presentation.ApplyStyle(defaultStyle.Style);
        await _database.SaveNotePresentationAsync(presentation);
        await ReloadNotesAsync();
        OpenSticky(note);
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

            var notes = await _database.ListNotesAsync();
            await ProcessLifecycleAsync(notes);
            if (_lastExternalContext is null)
            {
                return;
            }

            var runningExecutables = RunningApplicationReader.GetExecutablePaths();
            foreach (var note in notes.Where(note => note.Attachments.Count > 0))
            {
                var evaluatedNote = note;
                var presentation = await _database.GetNotePresentationAsync(note.Id);
                var lifecycleHidden = presentation.HiddenAt is not null ||
                                      presentation.DeletedAt is not null ||
                                      presentation.WakeAt is not null;
                if (lifecycleHidden)
                {
                    if (_openStickies.TryGetValue(note.Id, out var hiddenSticky) && hiddenSticky.IsVisible)
                    {
                        hiddenSticky.Hide();
                    }
                    continue;
                }

                var presentContexts = new HashSet<ContextId>();
                foreach (var attachment in note.Attachments.Where(item =>
                             item.Visibility == AttachmentVisibility.WhilePresent))
                {
                    var attachedContext = await _database.GetContextAsync(attachment.ContextId);
                    if (attachedContext is not null &&
                        RunningApplicationReader.IsRunning(attachedContext, runningExecutables))
                    {
                        presentContexts.Add(attachment.ContextId);
                    }
                }

                if (note.Attachments.Any(attachment =>
                        attachment.Visibility == AttachmentVisibility.SleepUntilReturn &&
                        attachment.ContextId == _lastExternalContext.Id))
                {
                    evaluatedNote = note.SetAttachmentVisibility(
                        _lastExternalContext.Id,
                        AttachmentVisibility.RemainVisible);
                    await _database.SaveNoteAsync(evaluatedNote);
                }

                var shouldShow = AttachmentVisibilityPolicy.ShouldShow(
                    evaluatedNote,
                    _lastExternalContext.Id,
                    presentContexts);
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

    private async Task ProcessLifecycleAsync(IReadOnlyList<Note> notes)
    {
        var now = DateTimeOffset.Now;
        foreach (var note in notes)
        {
            var presentation = await _database.GetNotePresentationAsync(note.Id);
            var decision = NoteLifecycleService.Evaluate(presentation, now);
            if (decision.ShouldWake)
            {
                var awake = NoteLifecycleService.Wake(presentation, now);
                await _database.SaveNotePresentationAsync(awake);
                if (_openStickies.TryGetValue(note.Id, out var sleepingSticky))
                {
                    sleepingSticky.Show();
                }
                else
                {
                    OpenSticky(note);
                }
            }

            if (decision.ReminderDue)
            {
                if (_openStickies.TryGetValue(note.Id, out var reminderSticky))
                {
                    if (_activeReminders.Add(note.Id))
                    {
                        reminderSticky.TriggerReminder(decision);
                    }
                }
                else
                {
                    OpenSticky(note);
                }
            }
            else
            {
                _activeReminders.Remove(note.Id);
            }
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
        if (snapshot.ImagePng is { Length: > 0 } png)
        {
            var payload = await _payloadStore.PutAsync(png, "image/png");
            capture = capture.WithTextRepresentation(
                "application/x-taskslist-payload-path",
                payload.Path);
        }
        if (snapshot.Files is { Count: > 0 } files)
        {
            capture = capture.WithTextRepresentation(
                "application/x-taskslist-files",
                System.Text.Json.JsonSerializer.Serialize(files));
        }
        capture = capture.WithSourceUrl(snapshot.SourceUrl);
        capture = capture.WithDuplicateHash(ClipboardDuplicatePolicy.ComputeHash(capture));
        var existing = _promoteDuplicateClips
            ? await _database.FindCaptureByDuplicateHashAsync(capture.DuplicateHash)
            : null;
        await _database.SaveCaptureAsync(existing is null
            ? capture
            : ClipboardDuplicatePolicy.Promote(existing, capture.CapturedAt));
        await ReloadClipboardAsync();
        StatusText.Text = $"CAPTURED · {snapshot.Source.DisplayName}";
    }

    private async Task ReloadClipboardAsync()
    {
        var captures = await _database.ListCapturesAsync();
        _clipboardCards.Clear();
        foreach (var capture in captures.Where(item => item.DeletedAt is null).Take(50))
        {
            var source = await _database.GetContextAsync(capture.SourceContextId);
            _clipboardCards.Add(new ClipboardCardViewModel(capture, source));
        }
    }

    private async Task ReloadLibraryAuxiliaryAsync()
    {
        var contexts = await _database.ListContextsAsync();
        var catalog = LoadPluginCatalog();
        var data = LibraryDataBuilder.Build(contexts, _liveBrowserTabs, catalog.Plugins);

        _contextCards.Clear();
        foreach (var context in data.Contexts)
        {
            _contextCards.Add(context);
        }
        ContextsEmptyText.Text = data.ContextsEmptyMessage;

        _extensionCards.Clear();
        foreach (var extension in data.Extensions)
        {
            _extensionCards.Add(extension);
        }
        ExtensionsEmptyText.Text = data.ExtensionsEmptyMessage;

        await ReloadTrashAsync();
    }

    private async Task ReloadTrashAsync()
    {
        var now = DateTimeOffset.Now;
        var cards = new List<TrashCardViewModel>();
        foreach (var note in await _database.ListNotesAsync())
        {
            var presentation = await _database.GetNotePresentationAsync(note.Id);
            if (presentation.DeletedAt is { } deletedAt)
            {
                cards.Add(TrashCardViewModel.ForNote(note, deletedAt, now));
            }
        }
        foreach (var capture in (await _database.ListCapturesAsync()).Where(item => item.DeletedAt is not null))
        {
            cards.Add(TrashCardViewModel.ForCapture(capture, capture.DeletedAt!.Value, now));
        }

        _trashCards.Clear();
        foreach (var card in cards.OrderByDescending(card => card.DeletedAt))
        {
            _trashCards.Add(card);
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
            await ReloadLibraryAuxiliaryAsync();
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
        var catalog = LoadPluginCatalog();
        var window = new PluginManagerWindow(catalog) { Owner = this };
        window.ShowDialog();
        _ = ReloadLibraryAuxiliaryAsync();
    }

    private PluginCatalogSnapshot LoadPluginCatalog()
    {
        var bundled = PluginCatalog.Load(Path.Combine(AppContext.BaseDirectory, "plugins"));
        var personal = PluginCatalog.Load(Path.Combine(_dataDirectory, "plugins"));
        return new PluginCatalogSnapshot(
            bundled.Plugins.Concat(personal.Plugins)
                .GroupBy(entry => entry.Manifest.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.Manifest.Version, StringComparer.Ordinal).First())
                .OrderBy(entry => entry.Manifest.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            bundled.Errors.Concat(personal.Errors).ToArray());
    }

    private async void RestoreTrashClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TrashCardViewModel card }) return;
        var now = DateTimeOffset.Now;
        if (card.Note is { } note)
        {
            var presentation = await _database.GetNotePresentationAsync(note.Id);
            await _database.SaveNotePresentationAsync(presentation.Restore(now));
        }
        else if (card.Capture is { } capture)
        {
            await _database.SaveCaptureAsync(capture.RestoreDeleted(now));
        }
        await ReloadNotesAsync();
        await ReloadClipboardAsync();
        await ReloadTrashAsync();
    }

    private async void DeleteTrashPermanentlyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TrashCardViewModel { CanDeletePermanently: true } card }) return;
        var confirmed = MessageBox.Show(
            this,
            $"Permanently delete '{card.Title}'? This cannot be undone.",
            "Delete forever",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
        if (!confirmed) return;

        if (card.Note is { } note)
        {
            if (_openStickies.Remove(note.Id, out var sticky)) sticky.Close();
            await _database.DeleteNotePermanentlyAsync(note.Id);
        }
        else if (card.Capture is { } capture)
        {
            await _database.DeleteCapturePermanentlyAsync(capture.Id);
        }
        await ReloadTrashAsync();
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
    public NoteCardViewModel(Note note, NotePresentation presentation)
    {
        Note = note;
        PaperBrush = ParseBrush(presentation.BackgroundHex);
        InkBrush = ParseBrush(presentation.TextHex);
        var accent = (SolidColorBrush)ParseBrush(presentation.AccentHex);
        EdgeBrush = new SolidColorBrush(Color.FromArgb(
            100,
            accent.Color.R,
            accent.Color.G,
            accent.Color.B));
        var ink = ((SolidColorBrush)InkBrush).Color;
        MutedInkBrush = new SolidColorBrush(Color.FromArgb(185, ink.R, ink.G, ink.B));
        StatusBrush = presentation.Topmost
            ? accent
            : new SolidColorBrush(Color.FromArgb(100, ink.R, ink.G, ink.B));
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

    private static Brush ParseBrush(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
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

public sealed class TrashCardViewModel
{
    private static readonly TimeSpan PermanentDeleteDelay = TimeSpan.FromDays(30);

    private TrashCardViewModel(
        string title,
        string kind,
        DateTimeOffset deletedAt,
        DateTimeOffset now,
        Note? note,
        CaptureModel? capture)
    {
        Title = string.IsNullOrWhiteSpace(title) ? $"Untitled {kind.ToLowerInvariant()}" : title;
        DeletedAt = deletedAt;
        Note = note;
        Capture = capture;
        var age = now - deletedAt;
        CanDeletePermanently = age >= PermanentDeleteDelay;
        Detail = $"{kind} · removed {deletedAt.LocalDateTime:g}";
        var remaining = PermanentDeleteDelay - age;
        DeleteHint = CanDeletePermanently
            ? "Permanently delete this item"
            : $"Available in {Math.Max(1, (int)Math.Ceiling(remaining.TotalDays))} days";
    }

    public string Title { get; }
    public string Detail { get; }
    public DateTimeOffset DeletedAt { get; }
    public bool CanDeletePermanently { get; }
    public string DeleteHint { get; }
    public Note? Note { get; }
    public CaptureModel? Capture { get; }

    public static TrashCardViewModel ForNote(Note note, DateTimeOffset deletedAt, DateTimeOffset now) =>
        new(note.Title, "Note", deletedAt, now, note, null);

    public static TrashCardViewModel ForCapture(CaptureModel capture, DateTimeOffset deletedAt, DateTimeOffset now) =>
        new(capture.Title.Length == 0 ? capture.PreviewText : capture.Title, "Clipboard item", deletedAt, now, null, capture);
}
