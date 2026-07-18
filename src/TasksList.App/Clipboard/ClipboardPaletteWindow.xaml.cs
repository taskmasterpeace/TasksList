using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TasksList.App.Sticky;
using TasksList.Core.Clipboard;
using TasksList.Core.Models;
using TasksList.Infrastructure.Storage;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Clipboard;

public partial class ClipboardPaletteWindow : Window
{
    private readonly TasksListDatabase _database;
    private readonly ClipboardPasteService _pasteService;
    private readonly ObservableCollection<ClipboardPaletteItem> _items = [];
    private readonly Action<IReadOnlyList<CaptureModel>> _makeNote;
    private bool _loading = true;
    private bool _allowClose;
    private nint _targetHandle;
    private Point _dragStart;
    private bool _hasBeenShown;
    private int _refreshVersion;

    public ClipboardPaletteWindow(
        TasksListDatabase database,
        ClipboardPasteService pasteService,
        Action<IReadOnlyList<CaptureModel>> makeNote)
    {
        _database = database;
        _pasteService = pasteService;
        _makeNote = makeNote;
        InitializeComponent();
        ClipList.ItemsSource = _items;
        Closing += PaletteClosing;
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible && _hasBeenShown)
            {
                SizePreferenceChanged?.Invoke(Width, Height);
            }
        };
        _loading = false;
    }

    public event Action<double, double>? SizePreferenceChanged;

    public async Task ShowNearPointerAsync(nint targetHandle)
    {
        _targetHandle = targetHandle;
        await LoadSourcesAndPlacesAsync();
        await RefreshAsync();

        var cursor = ClipboardTargetReader.CursorPosition();
        var requested = new WindowBounds(cursor.X + 18, cursor.Y + 18, Width, Height);
        var workArea = StickyWindowPlacement.ClosestMonitor(
            requested,
            MonitorWorkAreaProvider.GetWorkAreas());
        Left = Math.Clamp(requested.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(requested.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));

        Show();
        _hasBeenShown = true;
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void DisposePalette()
    {
        _allowClose = true;
        Close();
    }

    private async Task LoadSourcesAndPlacesAsync()
    {
        _loading = true;
        try
        {
            var captures = await _database.ListCapturesAsync();
            var sources = new List<SourceFilterItem> { new(null, "All sources") };
            foreach (var sourceId in captures.Select(capture => capture.SourceContextId).Distinct())
            {
                if (await _database.GetContextAsync(sourceId) is { } context)
                {
                    sources.Add(new SourceFilterItem(context, context.DisplayName));
                }
            }
            var previousSource = (SourceFilter.SelectedItem as SourceFilterItem)?.Context?.Id;
            SourceFilter.ItemsSource = sources;
            SourceFilter.SelectedItem = sources.FirstOrDefault(item => item.Context?.Id == previousSource) ?? sources[0];

            var places = await _database.ListPlacesAsync();
            var notes = await _database.ListNotesAsync();
            var destinations = places
                .Select(place => new AssignmentDestination($"Place · {place.Name}", place, null))
                .Concat(notes.Select(note => new AssignmentDestination($"Note · {note.Title}", null, note)))
                .ToArray();
            PlaceBox.ItemsSource = destinations;
            if (PlaceBox.SelectedIndex < 0 && destinations.Length > 0) PlaceBox.SelectedIndex = 0;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RefreshAsync()
    {
        if (_loading) return;
        var refreshVersion = ++_refreshVersion;
        var query = BuildQuery();
        var captures = await _database.SearchCapturesAsync(query, 1000);
        var nextItems = new List<ClipboardPaletteItem>(captures.Count);
        foreach (var capture in captures)
        {
            var source = await _database.GetContextAsync(capture.SourceContextId);
            nextItems.Add(new ClipboardPaletteItem(capture, source?.DisplayName ?? "Unknown source"));
        }
        if (refreshVersion != _refreshVersion)
        {
            return;
        }
        _items.Clear();
        foreach (var item in nextItems)
        {
            _items.Add(item);
        }

        if (_items.Count > 0)
        {
            ClipList.SelectedIndex = 0;
        }
        else
        {
            PreviewTitle.Text = "Nothing matches";
            ProvenanceText.Text = "Try clearing a filter or copy something new.";
            PreviewText.Text = string.Empty;
        }
    }

    private ClipboardQuery BuildQuery()
    {
        var query = new ClipboardQuery
        {
            Text = SearchBox.Text,
            Favorite = FavoriteFilter.IsChecked == true ? true : null,
            Used = UsedFilter.IsChecked == true ? true : null,
            Unfiled = UnfiledFilter.IsChecked == true ? true : null,
        };
        if ((TypeFilter.SelectedItem as ComboBoxItem)?.Tag is string type &&
            type != "All" && Enum.TryParse<CaptureKind>(type, out var kind))
        {
            query = query with { Kinds = [kind] };
        }
        if ((DateFilter.SelectedItem as ComboBoxItem)?.Tag is string date && date != "All")
        {
            var now = DateTimeOffset.Now;
            var after = date == "Today"
                ? new DateTimeOffset(now.Date, now.Offset)
                : now.AddDays(-int.Parse(date));
            query = query with { CapturedAfter = after };
        }
        if (SourceFilter.SelectedItem is SourceFilterItem { Context: { } context })
        {
            query = query with { SourceContextIds = [context.Id] };
        }
        return query;
    }

    private IReadOnlyList<ClipboardPaletteItem> SelectedItems() =>
        ClipList.SelectedItems.Cast<ClipboardPaletteItem>().ToArray();

    private async void SearchChanged(object sender, TextChangedEventArgs e) => await RefreshAsync();
    private async void FilterChanged(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            MoveSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteEnter();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideAndRestoreFocus();
            e.Handled = true;
        }
    }

    private void ListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteEnter();
            e.Handled = true;
        }
    }

    private void ClipMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(ClipList);

    private void ClipMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            ClipList.SelectedItem is not ClipboardPaletteItem item)
        {
            return;
        }
        var current = e.GetPosition(ClipList);
        if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }
        var data = WindowsClipboardPastePlatform.CreateDataObject(
            item.Capture,
            PasteRepresentation.Original);
        DragDrop.DoDragDrop(ClipList, data, DragDropEffects.Copy);
    }

    private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideAndRestoreFocus();
            e.Handled = true;
        }
    }

    private void ExecuteEnter()
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            MakeSelectedNote();
        }
        else
        {
            PasteSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                ? PasteRepresentation.PlainText
                : PasteRepresentation.Original);
        }
    }

    private void MoveSelection(int delta)
    {
        if (_items.Count == 0) return;
        ClipList.SelectedIndex = Math.Clamp(ClipList.SelectedIndex + delta, 0, _items.Count - 1);
        ClipList.ScrollIntoView(ClipList.SelectedItem);
        SearchBox.Focus();
    }

    private async void ClipSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClipList.SelectedItem is not ClipboardPaletteItem item) return;
        var capture = item.Capture;
        var source = await _database.GetContextAsync(capture.SourceContextId);
        PreviewTitle.Text = item.Heading;
        PreviewText.Text = capture.TextRepresentations.TryGetValue("text/plain", out var plain)
            ? plain
            : capture.PreviewText;
        PreviewImageHost.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        if (capture.TextRepresentations.TryGetValue(
                "application/x-taskslist-payload-path",
                out var payloadPath) && File.Exists(payloadPath))
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(payloadPath, UriKind.Absolute);
            image.EndInit();
            PreviewImage.Source = image;
            PreviewImageHost.Visibility = Visibility.Visible;
        }
        var formats = capture.TextRepresentations.Count == 0
            ? capture.Kind.ToString()
            : string.Join(", ", capture.TextRepresentations.Keys);
        ProvenanceText.Text =
            $"{source?.DisplayName ?? "Unknown source"} · {capture.CapturedAt:g}\n" +
            $"{formats} · {FormatBytes(capture.SizeBytes)}" +
            (string.IsNullOrWhiteSpace(capture.SourceUrl) ? string.Empty : $"\n{capture.SourceUrl}");
    }

    private async void PasteSelected(PasteRepresentation representation)
    {
        if (ClipList.SelectedItem is not ClipboardPaletteItem item) return;
        _pasteService.Paste(item.Capture, representation, _targetHandle);
        await _database.SaveCaptureAsync(item.Capture.MarkUsed(DateTimeOffset.Now));
        Hide();
    }

    private void PasteClick(object sender, RoutedEventArgs e) => PasteSelected(PasteRepresentation.Original);
    private void PlainPasteClick(object sender, RoutedEventArgs e) => PasteSelected(PasteRepresentation.PlainText);

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        if (ClipList.SelectedItem is ClipboardPaletteItem item)
        {
            _pasteService.Copy(item.Capture, PasteRepresentation.Original);
        }
    }

    private async void JoinedPasteClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItems();
        if (selected.Count == 0) return;
        _pasteService.PasteJoined(selected.Select(item => item.Capture).ToArray(), "\n", _targetHandle);
        var now = DateTimeOffset.Now;
        foreach (var item in selected)
        {
            await _database.SaveCaptureAsync(item.Capture.MarkUsed(now));
        }
        Hide();
    }

    private void MakeNoteClick(object sender, RoutedEventArgs e) => MakeSelectedNote();
    private void MakeSelectedNote()
    {
        var selected = SelectedItems();
        if (selected.Count == 0) return;
        _makeNote(selected.Select(item => item.Capture).ToArray());
        Hide();
    }

    private async void FavoriteClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItems();
        if (selected.Count == 0) return;
        var favorite = selected.Any(item => !item.Capture.IsFavorite);
        foreach (var item in selected)
        {
            await _database.SaveCaptureAsync(item.Capture.WithFavorite(favorite));
        }
        await RefreshAsync();
    }

    private async void DeleteClick(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItems();
        if (selected.Count == 0) return;
        var result = MessageBox.Show(
            $"Move {selected.Count} clipboard item{(selected.Count == 1 ? string.Empty : "s")} to Trash?",
            "Delete clipboard items",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        foreach (var item in selected)
        {
            await _database.SaveCaptureAsync(item.Capture.SoftDelete(DateTimeOffset.Now));
        }
        await RefreshAsync();
    }

    private async void EditClick(object sender, RoutedEventArgs e)
    {
        if (ClipList.SelectedItem is not ClipboardPaletteItem item) return;
        var dialog = new ClipboardEditDialog(item.Capture) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var now = DateTimeOffset.Now;
        var updated = item.Capture
            .Rename(dialog.ClipTitle, now)
            .ReplaceWithPlainText(dialog.ClipContent, now);
        updated = updated.WithDuplicateHash(ClipboardDuplicatePolicy.ComputeHash(updated));
        await _database.SaveCaptureAsync(updated);
        await RefreshAsync();
    }

    private async void DuplicateClick(object sender, RoutedEventArgs e)
    {
        if (ClipList.SelectedItem is not ClipboardPaletteItem item) return;
        await _database.SaveCaptureAsync(item.Capture.Duplicate(DateTimeOffset.Now));
        await RefreshAsync();
    }

    private async void AssignClick(object sender, RoutedEventArgs e)
    {
        if (PlaceBox.SelectedItem is not AssignmentDestination destination) return;
        foreach (var item in SelectedItems())
        {
            var capture = destination.Place is { } place
                ? item.Capture.AssignTo(place.Id, AssignmentActor.User)
                : destination.Note is { } note
                    ? item.Capture.AssignToNote(note.Id)
                    : item.Capture;
            await _database.SaveCaptureAsync(capture);
        }
        await RefreshAsync();
    }

    private void HideClick(object sender, RoutedEventArgs e) => HideAndRestoreFocus();

    private void HideAndRestoreFocus()
    {
        Hide();
        _pasteService.RestoreFocus(_targetHandle);
    }
    private void PaletteClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:0.0} MB",
        >= 1024 => $"{bytes / 1024d:0.0} KB",
        _ => $"{bytes} B",
    };
}

public sealed record SourceFilterItem(ContextRef? Context, string DisplayName);

public sealed record AssignmentDestination(string Name, Place? Place, Note? Note);

public sealed class ClipboardPaletteItem
{
    public ClipboardPaletteItem(CaptureModel capture, string sourceName)
    {
        Capture = capture;
        Heading = string.IsNullOrWhiteSpace(capture.Title)
            ? capture.PreviewText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Untitled clip"
            : capture.Title;
        Preview = capture.PreviewText.Replace('\r', ' ').Replace('\n', ' ');
        Meta = $"{sourceName} · {capture.CapturedAt:g} · {capture.Kind}";
        FavoriteGlyph = capture.IsFavorite ? "★" : string.Empty;
    }

    public CaptureModel Capture { get; }
    public string Heading { get; }
    public string Preview { get; }
    public string Meta { get; }
    public string FavoriteGlyph { get; }
}
