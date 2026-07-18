using System.Media;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TasksList.App.Editor;
using TasksList.Core.Markdown;
using TasksList.Core.Models;
using TasksList.Core.Notes;
using TasksList.Infrastructure.Storage;

namespace TasksList.App.Sticky;

public partial class StickyWindow : Window
{
    private readonly TasksListDatabase _database;
    private readonly DispatcherTimer _contentSaveTimer;
    private readonly DispatcherTimer _presentationSaveTimer;
    private readonly DispatcherTimer _hudTimer;
    private readonly MarkdownDocumentService _markdownService = new();
    private readonly Func<ContextRef?> _contextProvider;
    private readonly Func<IReadOnlyList<WindowBounds>> _siblingBoundsProvider;
    private readonly Func<double> _snapToleranceProvider;
    private readonly Func<bool> _reduceMotionProvider;
    private readonly StickyWindowController _controller;
    private Note _note;
    private bool _isLoading = true;
    private bool _isPreviewing;
    private bool _isTitleEditing;
    private bool _cancelTitleEdit;
    private string _titleBeforeEdit = string.Empty;
    private bool _suppressBoundsCapture;
    private IReadOnlyList<NamedNoteStyle> _namedStyles = [];
    private string _attachmentLabel = "UNATTACHED";

    public StickyWindow(
        Note note,
        NotePresentation presentation,
        TasksListDatabase database,
        Func<ContextRef?>? contextProvider = null,
        Func<IReadOnlyList<WindowBounds>>? siblingBoundsProvider = null,
        Func<double>? snapToleranceProvider = null,
        Func<bool>? reduceMotionProvider = null)
    {
        _note = note;
        _database = database;
        _contextProvider = contextProvider ?? (() => null);
        _siblingBoundsProvider = siblingBoundsProvider ?? (() => []);
        _snapToleranceProvider = snapToleranceProvider ?? (() => 12);
        _reduceMotionProvider = reduceMotionProvider ?? (() => false);
        _controller = new StickyWindowController(presentation);
        InitializeComponent();

        TitleText.Text = note.Title;
        TitleEditor.Text = note.Title;
        MarkdownBox.Text = note.Markdown;
        ContextText.Text = note.Attachments.Count == 0 ? "UNATTACHED" : "CONTEXT ATTACHED";

        _contentSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _contentSaveTimer.Tick += ContentSaveTimerTick;
        _presentationSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _presentationSaveTimer.Tick += PresentationSaveTimerTick;
        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _hudTimer.Tick += (_, _) =>
        {
            _hudTimer.Stop();
            OpacityPopup.IsOpen = false;
        };

        _controller.Changed += ControllerChanged;
        CustomizeControl.OpacityChanged += _controller.SetOpacity;
        CustomizeControl.InactiveOpacityChanged += _controller.SetInactiveOpacity;
        CustomizeControl.PresetChanged += _controller.ApplyPreset;
        CustomizeControl.ColorsSelected += _controller.SetColors;
        CustomizeControl.TypographySelected += _controller.SetFont;
        CustomizeControl.DensitySelected += _controller.SetDensity;
        CustomizeControl.ToolbarSelected += _controller.SetToolbarVisibility;
        CustomizeControl.DecorationSelected += _controller.SetDecoration;
        CustomizeControl.SaveNamedStyleRequested += SaveNamedStyle;
        CustomizeControl.ApplyNamedStyleRequested += style => _controller.ApplyStyle(style.Style);
        CustomizeControl.ResetRequested += () =>
            _controller.ApplyStyle(NoteStyle.FromPreset(PaperPreset.Butter));

        Loaded += async (_, _) =>
        {
            ApplyPresentation(_controller.Presentation, includeBounds: true);
            await RefreshAttachedContextLabelAsync();
            _isLoading = false;
        };
        SourceInitialized += (_, _) =>
            GhostModeService.SetGhost(this, _controller.Presentation.Ghost);
        LocationChanged += WindowBoundsChanged;
        SizeChanged += WindowBoundsChanged;
    }

    public event EventHandler? NoteSaved;

    public event Action? NewStickyRequested;

    public event Action? NewFromClipboardRequested;

    public event Action<Note>? DuplicateRequested;

    public event Action<NoteId>? ArchiveRequested;

    public Note CurrentNote => _note;

    public NotePresentation CurrentPresentation => _controller.Presentation;

    public WindowBounds CurrentBounds => new(Left, Top, ActualWidth, ActualHeight);

    public void ApplyExternalStyle(NoteStyle style) => _controller.ApplyStyle(style);

    public void DisableGhostMode()
    {
        _controller.DisableGhost();
        GhostModeService.SetGhost(this, false);
    }

    public void RestoreVisibility()
    {
        _controller.RestoreVisibility(DateTimeOffset.Now);
        Show();
        Activate();
    }

    public void TriggerReminder(NoteLifecycleDecision decision)
    {
        if (!decision.ReminderDue)
        {
            return;
        }

        if (decision.RequireTopmost)
        {
            Topmost = true;
        }
        if (decision.PlaySound)
        {
            SystemSounds.Exclamation.Play();
        }
        if (decision.Pulse && !_reduceMotionProvider())
        {
            PaperShadow.BeginAnimation(
                System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
                new DoubleAnimation(0.95, TimeSpan.FromMilliseconds(420))
                {
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(4),
                });
        }
        ReminderBanner.Visibility = Visibility.Visible;
        Show();
        Activate();
    }

    private void ContentChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || _contentSaveTimer is null || _controller.Presentation.Locked)
        {
            return;
        }

        SaveText.Text = "EDITING";
        _contentSaveTimer.Stop();
        _contentSaveTimer.Start();
    }

    private async void ContentSaveTimerTick(object? sender, EventArgs e)
    {
        _contentSaveTimer.Stop();
        await SaveContentAsync();
    }

    private async Task SaveContentAsync()
    {
        _note = _note.UpdateContent(NormalizeTitle(TitleText.Text), MarkdownBox.Text);
        await _database.SaveNoteAsync(_note);
        SaveText.Text = "SAVED";
        NoteSaved?.Invoke(this, EventArgs.Empty);
    }

    private void ControllerChanged(object? sender, NotePresentation presentation)
    {
        ApplyPresentation(presentation, includeBounds: false);
        QueuePresentationSave();
    }

    private void QueuePresentationSave()
    {
        if (_isLoading)
        {
            return;
        }

        _presentationSaveTimer.Stop();
        _presentationSaveTimer.Start();
    }

    private async void PresentationSaveTimerTick(object? sender, EventArgs e)
    {
        _presentationSaveTimer.Stop();
        await _database.SaveNotePresentationAsync(_controller.Presentation);
    }

    private void WindowBoundsChanged(object? sender, EventArgs e)
    {
        if (_isLoading || _suppressBoundsCapture || _controller.Presentation.Rolled)
        {
            return;
        }

        var current = _controller.Presentation.Bounds;
        _controller.SetBounds(current with
        {
            Left = Left,
            Top = Top,
            Width = ActualWidth,
            Height = ActualHeight,
            ExpandedHeight = ActualHeight,
        });
    }

    private void ApplyPresentation(NotePresentation presentation, bool includeBounds)
    {
        _suppressBoundsCapture = true;
        try
        {
            if (includeBounds)
            {
                Left = presentation.Bounds.Left;
                Top = presentation.Bounds.Top;
                Width = Math.Max(MinWidth, presentation.Bounds.Width);
                Height = presentation.Rolled
                    ? HeaderHeight(presentation.Density) + 2
                    : Math.Max(230, presentation.Bounds.Height);
            }

            Topmost = presentation.Topmost;
            GhostModeService.SetGhost(this, presentation.Ghost);
            Opacity = IsActive ? presentation.ActiveOpacity : presentation.InactiveOpacity;
            ResizeMode = presentation.Locked || presentation.Rolled
                ? ResizeMode.NoResize
                : ResizeMode.CanResizeWithGrip;
            MarkdownBox.IsReadOnly = presentation.Locked;
            TitleText.Cursor = presentation.Locked ? Cursors.Arrow : Cursors.SizeAll;

            var highContrast = SystemParameters.HighContrast;
            var background = highContrast ? SystemColors.WindowColor : ParseColor(presentation.BackgroundHex);
            var text = highContrast ? SystemColors.WindowTextColor : ParseColor(presentation.TextHex);
            var accent = highContrast ? SystemColors.HighlightColor : ParseColor(presentation.AccentHex);
            Paper.Background = presentation.TextureEnabled && !highContrast
                ? CreatePaperBrush(background)
                : new SolidColorBrush(background);
            Paper.BorderBrush = highContrast
                ? SystemColors.WindowTextBrush
                : new SolidColorBrush(Color.FromArgb(
                    presentation.BorderVisible ? (byte)100 : (byte)0,
                    accent.R,
                    accent.G,
                    accent.B));
            Paper.BorderThickness = highContrast
                ? new Thickness(2)
                : presentation.BorderVisible ? new Thickness(1) : new Thickness(0);
            Paper.CornerRadius = presentation.CornerStyle switch
            {
                CornerStyle.Square => new CornerRadius(2),
                CornerStyle.Round => new CornerRadius(18),
                _ => new CornerRadius(10),
            };
            PaperShadow.Opacity = highContrast ? 0 : presentation.ShadowStrength;

            var textBrush = new SolidColorBrush(text);
            var mutedBrush = new SolidColorBrush(Color.FromArgb(185, text.R, text.G, text.B));
            TitleText.Foreground = textBrush;
            TitleEditor.Foreground = textBrush;
            MarkdownBox.Foreground = textBrush;
            PreviewViewer.Foreground = textBrush;
            ContextText.Foreground = mutedBrush;
            OpacityText.Foreground = mutedBrush;
            SaveText.Foreground = new SolidColorBrush(accent);

            var fontFamily = new FontFamily(presentation.FontFamily);
            MarkdownBox.FontFamily = fontFamily;
            TitleText.FontFamily = fontFamily;
            TitleEditor.FontFamily = fontFamily;
            MarkdownBox.FontSize = presentation.FontSize;
            MarkdownBox.FontWeight = FontWeight.FromOpenTypeWeight(presentation.FontWeight);
            MarkdownBox.SetValue(
                TextBlock.LineHeightProperty,
                presentation.FontSize * presentation.LineSpacing);
            TitleText.FontSize = Math.Max(12, presentation.FontSize + 1);
            TitleEditor.FontSize = TitleText.FontSize;

            ApplyDensity(presentation.Density);
            ApplyRolledState(presentation.Rolled);
            ApplyEditorMode(presentation.EditorMode);
            ApplyToolbarState(presentation.ToolbarVisibility);
            PinButton.Foreground = presentation.Topmost ? new SolidColorBrush(accent) : mutedBrush;
            LockMenuItem.IsChecked = presentation.Locked;
            TopmostMenuItem.IsChecked = presentation.Topmost;
            RollMenuItem.IsChecked = presentation.Rolled;
            GhostMenuItem.IsChecked = presentation.Ghost;
            var stateLabels = new List<string>();
            if (presentation.Locked) stateLabels.Add("LOCKED");
            if (presentation.Ghost) stateLabels.Add("GHOST");
            if (presentation.WakeAt is not null) stateLabels.Add("SLEEPING");
            ContextText.Text = stateLabels.Count > 0
                ? string.Join(" · ", stateLabels)
                : _attachmentLabel;
            OpacityText.Text = $"{presentation.ActiveOpacity:P0}";
            CustomizeControl.Load(presentation);
        }
        finally
        {
            _suppressBoundsCapture = false;
        }
    }

    private void ApplyDensity(NoteDensity density)
    {
        var (header, footer, padding) = density switch
        {
            NoteDensity.Compact => (40d, 30d, new Thickness(12, 9, 12, 9)),
            NoteDensity.Spacious => (56d, 42d, new Thickness(20, 17, 20, 17)),
            _ => (48d, 36d, new Thickness(16, 13, 16, 13)),
        };
        HeaderRow.Height = new GridLength(header);
        if (!_controller.Presentation.Rolled)
        {
            FooterRow.Height = new GridLength(footer);
        }
        MarkdownBox.Padding = padding;
    }

    private void ApplyRolledState(bool rolled)
    {
        if (rolled)
        {
            ContentRow.Height = new GridLength(0);
            FooterRow.Height = new GridLength(0);
            Height = HeaderHeight(_controller.Presentation.Density) + 2;
        }
        else
        {
            ContentRow.Height = new GridLength(1, GridUnitType.Star);
            FooterRow.Height = new GridLength(FooterHeight(_controller.Presentation.Density));
            Height = Math.Max(_controller.Presentation.Bounds.ExpandedHeight, 230);
        }
    }

    private void ApplyToolbarState(ToolbarVisibility visibility)
    {
        if (visibility == ToolbarVisibility.Always)
        {
            SetToolbarOpacity(1);
            ChromePanel.IsHitTestVisible = true;
        }
        else if (visibility == ToolbarVisibility.Hidden)
        {
            SetToolbarOpacity(0);
            ChromePanel.IsHitTestVisible = false;
        }
        else
        {
            var visible = Header.IsMouseOver ||
                          (visibility == ToolbarVisibility.Focused && IsKeyboardFocusWithin);
            SetToolbarOpacity(visible ? 1 : 0);
            ChromePanel.IsHitTestVisible = visible;
        }
    }

    private void SetToolbarOpacity(double target)
    {
        if (_reduceMotionProvider())
        {
            ChromePanel.BeginAnimation(OpacityProperty, null);
            ChromePanel.Opacity = target;
            return;
        }
        ChromePanel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private void HeaderMouseEnter(object sender, MouseEventArgs e) =>
        ApplyToolbarState(_controller.Presentation.ToolbarVisibility);

    private void HeaderMouseLeave(object sender, MouseEventArgs e) =>
        ApplyToolbarState(_controller.Presentation.ToolbarVisibility);

    private void TitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _controller.Presentation.Locked)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        var overControl = FindAncestor<ButtonBase>(source) is not null ||
                          FindAncestor<TextBox>(source) is not null;
        var overTitle = IsWithin(source, TitleText);
        var action = StickyInteractionPolicy.ResolveHeaderAction(
            _isTitleEditing,
            overControl,
            overTitle,
            e.ClickCount);
        switch (action)
        {
            case StickyInteractionAction.BeginTitleEdit:
                BeginTitleEdit();
                e.Handled = true;
                break;
            case StickyInteractionAction.ToggleRoll:
                _controller.ToggleRolled();
                e.Handled = true;
                break;
            case StickyInteractionAction.Drag:
                DragMove();
                SnapAfterDrag();
                e.Handled = true;
                break;
        }
    }

    private void SnapAfterDrag()
    {
        var requested = new WindowBounds(Left, Top, ActualWidth, ActualHeight);
        var workArea = StickyWindowPlacement.ClosestMonitor(
            requested,
            MonitorWorkAreaProvider.GetWorkAreas());
        var snapped = StickySnapService.Snap(
            requested,
            _siblingBoundsProvider(),
            workArea,
            _snapToleranceProvider(),
            Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        Left = snapped.Left;
        Top = snapped.Top;
        WindowBoundsChanged(this, EventArgs.Empty);
    }

    private void BeginTitleEdit()
    {
        if (_controller.Presentation.Locked)
        {
            return;
        }

        _isTitleEditing = true;
        _cancelTitleEdit = false;
        _titleBeforeEdit = TitleText.Text;
        TitleEditor.Text = TitleText.Text;
        TitleText.Visibility = Visibility.Collapsed;
        TitleEditor.Visibility = Visibility.Visible;
        TitleEditor.Focus();
        TitleEditor.SelectAll();
    }

    private void CommitTitleEdit()
    {
        if (!_isTitleEditing)
        {
            return;
        }

        TitleText.Text = NormalizeTitle(TitleEditor.Text);
        EndTitleEdit();
        _contentSaveTimer.Stop();
        _contentSaveTimer.Start();
    }

    private void CancelTitleEdit()
    {
        if (!_isTitleEditing)
        {
            return;
        }

        _cancelTitleEdit = true;
        TitleText.Text = _titleBeforeEdit;
        TitleEditor.Text = _titleBeforeEdit;
        EndTitleEdit();
    }

    private void EndTitleEdit()
    {
        _isTitleEditing = false;
        TitleEditor.Visibility = Visibility.Collapsed;
        TitleText.Visibility = Visibility.Visible;
        TitleText.Focus();
    }

    private void TitleEditorKeyDown(object sender, KeyEventArgs e)
    {
        var action = StickyInteractionPolicy.ResolveTitleKey(e.Key.ToString());
        if (action == StickyInteractionAction.CommitTitleEdit)
        {
            CommitTitleEdit();
            e.Handled = true;
        }
        else if (action == StickyInteractionAction.CancelTitleEdit)
        {
            CancelTitleEdit();
            e.Handled = true;
        }
    }

    private void TitleEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_isTitleEditing && !_cancelTitleEdit)
        {
            CommitTitleEdit();
        }
    }

    private void WindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            BeginTitleEdit();
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (!modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        if (e.Key == Key.N && modifiers.HasFlag(ModifierKeys.Shift))
        {
            NewFromClipboardRequested?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == Key.N)
        {
            NewStickyRequested?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == Key.D)
        {
            DuplicateRequested?.Invoke(_note);
            e.Handled = true;
        }
        else if (e.Key == Key.M)
        {
            _controller.ToggleRolled();
            e.Handled = true;
        }
        else if (e.Key == Key.L)
        {
            _controller.ToggleLocked();
            e.Handled = true;
        }
        else if (e.Key == Key.T && modifiers.HasFlag(ModifierKeys.Shift))
        {
            _controller.ToggleTopmost();
            e.Handled = true;
        }
        else if (e.Key is Key.OemPlus or Key.Add)
        {
            ChangeOpacity(120);
            e.Handled = true;
        }
        else if (e.Key is Key.OemMinus or Key.Subtract)
        {
            ChangeOpacity(-120);
            e.Handled = true;
        }
    }

    private void WindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ChangeOpacity(e.Delta);
            e.Handled = true;
        }
    }

    private void ChangeOpacity(int delta)
    {
        _controller.AdjustOpacity(delta);
        OpacityHudControl.SetOpacity(_controller.Presentation.ActiveOpacity);
        OpacityPopup.IsOpen = true;
        _hudTimer.Stop();
        _hudTimer.Start();
    }

    private void WindowActivated(object sender, EventArgs e)
    {
        AnimateWindowOpacity(_controller.Presentation.ActiveOpacity);
        ApplyToolbarState(_controller.Presentation.ToolbarVisibility);
    }

    private void WindowDeactivated(object sender, EventArgs e)
    {
        if (!CustomizePopup.IsOpen)
        {
            AnimateWindowOpacity(_controller.Presentation.InactiveOpacity);
        }
        ApplyToolbarState(_controller.Presentation.ToolbarVisibility);
    }

    private void AnimateWindowOpacity(double target)
    {
        if (_reduceMotionProvider())
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = target;
            return;
        }
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    private void PinClick(object sender, RoutedEventArgs e) => _controller.ToggleTopmost();

    private void RollClick(object sender, RoutedEventArgs e) => _controller.ToggleRolled();

    private void LockClick(object sender, RoutedEventArgs e) => _controller.ToggleLocked();

    private void GhostClick(object sender, RoutedEventArgs e)
    {
        if (!_controller.Presentation.Ghost)
        {
            var result = MessageBox.Show(
                "Ghost mode lets mouse clicks pass through this sticky. Recover it from the Task'sList tray menu or press Ctrl+Alt+Shift+G.\n\nEnable click-through?",
                "Enable Ghost Mode",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                GhostMenuItem.IsChecked = false;
                return;
            }
        }

        _controller.ToggleGhost();
    }

    private async void SleepPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } ||
            !Enum.TryParse<SleepPreset>(tag, out var preset))
        {
            return;
        }

        DateTimeOffset? custom = null;
        if (preset == SleepPreset.Custom)
        {
            var dialog = new ScheduleDialog("Sleep this note until…", allowAttention: false)
            {
                Owner = this,
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }
            custom = dialog.SelectedDateTime;
        }

        _controller.Sleep(preset, DateTimeOffset.Now, custom);
        await _database.SaveNotePresentationAsync(_controller.Presentation);
        Hide();
    }

    private void ReminderPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag })
        {
            return;
        }

        var now = DateTimeOffset.Now;
        DateTimeOffset reminderAt;
        var attention = ReminderAttention.SoundAndPulse;
        var remainTopmost = true;
        if (tag == "15")
        {
            reminderAt = now.AddMinutes(15);
        }
        else if (tag == "60")
        {
            reminderAt = now.AddHours(1);
        }
        else if (tag == "Tomorrow")
        {
            var tomorrow = now.AddDays(1);
            reminderAt = new DateTimeOffset(
                tomorrow.Year,
                tomorrow.Month,
                tomorrow.Day,
                8,
                0,
                0,
                tomorrow.Offset);
        }
        else
        {
            var dialog = new ScheduleDialog("Remind me…", allowAttention: true)
            {
                Owner = this,
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }
            reminderAt = dialog.SelectedDateTime;
            attention = dialog.Attention;
            remainTopmost = dialog.RemainTopmost;
        }

        _controller.ScheduleReminder(reminderAt, attention, now, remainTopmost);
        SaveText.Text = $"REMINDER {reminderAt:g}";
    }

    private void AcknowledgeReminderClick(object sender, RoutedEventArgs e)
    {
        ReminderBanner.Visibility = Visibility.Collapsed;
        PaperShadow.BeginAnimation(
            System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            null);
        _controller.AcknowledgeReminder(DateTimeOffset.Now);
    }

    private async void CustomizeClick(object sender, RoutedEventArgs e)
    {
        _namedStyles = await _database.ListNamedStylesAsync();
        CustomizeControl.SetNamedStyles(_namedStyles);
        CustomizeControl.Load(_controller.Presentation);
        CustomizePopup.IsOpen = true;
    }

    private async void SaveNamedStyle(string name, bool makeDefault)
    {
        var existing = _namedStyles.FirstOrDefault(style =>
            string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase));
        var named = new NamedNoteStyle(
            existing?.Id ?? Guid.NewGuid(),
            name,
            NoteStyle.FromPresentation(_controller.Presentation),
            makeDefault,
            DateTimeOffset.Now);
        await _database.SaveNamedStyleAsync(named);
        _namedStyles = await _database.ListNamedStylesAsync();
        CustomizeControl.SetNamedStyles(_namedStyles);
        SaveText.Text = "STYLE SAVED";
    }

    private void MoreClick(object sender, RoutedEventArgs e)
    {
        if (Paper.ContextMenu is { } menu)
        {
            menu.PlacementTarget = sender as UIElement;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private async void AttachClick(object sender, RoutedEventArgs e)
    {
        var context = _contextProvider();
        if (context is null)
        {
            MessageBox.Show(
                "Switch to the application you want, return to this sticky, then choose Attach.",
                "No application context yet",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await _database.SaveContextAsync(context);
        _note = _note.Attachments.Any(attachment => attachment.ContextId == context.Id)
            ? _note.SetAttachmentVisibility(context.Id, AttachmentVisibility.ForegroundOnly)
            : _note.AttachTo(context.Id, AttachmentVisibility.ForegroundOnly);
        await _database.SaveNoteAsync(_note);
        SetAttachedContext(context, AttachmentVisibility.ForegroundOnly);
        NoteSaved?.Invoke(this, EventArgs.Empty);
    }

    private async void AttachModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } ||
            !Enum.TryParse<AttachmentVisibility>(tag, out var visibility))
        {
            return;
        }

        var context = _contextProvider();
        if (context is null)
        {
            MessageBox.Show(
                "Switch to the application you want, return to this sticky, then choose an attachment mode.",
                "No application context yet",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await _database.SaveContextAsync(context);
        _note = _note.Attachments.Any(attachment => attachment.ContextId == context.Id)
            ? _note.SetAttachmentVisibility(context.Id, visibility)
            : _note.AttachTo(context.Id, visibility);
        await _database.SaveNoteAsync(_note);
        SetAttachedContext(context, visibility);
        NoteSaved?.Invoke(this, EventArgs.Empty);
    }

    private async void DetachClick(object sender, RoutedEventArgs e)
    {
        foreach (var attachment in _note.Attachments.ToArray())
        {
            _note = _note.DetachFrom(attachment.ContextId);
        }
        await _database.SaveNoteAsync(_note);
        SetDetachedContext();
        NoteSaved?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshAttachedContextLabelAsync()
    {
        if (_note.Attachments.FirstOrDefault() is not { } attachment)
        {
            return;
        }

        var context = await _database.GetContextAsync(attachment.ContextId);
        if (context is null)
        {
            _attachmentLabel = $"ATTACHED · {VisibilityLabel(attachment.Visibility)}";
            ContextText.Text = _attachmentLabel;
            ContextIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            SetAttachedContext(context, attachment.Visibility);
        }
    }

    private void SetAttachedContext(ContextRef context, AttachmentVisibility visibility)
    {
        _attachmentLabel = $"ATTACHED · {context.DisplayName} · {VisibilityLabel(visibility)}";
        ContextText.Text = _attachmentLabel;
        ContextIcon.Source = TryLoadContextIcon(context);
        ContextIcon.Visibility = ContextIcon.Source is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetDetachedContext()
    {
        _attachmentLabel = "UNATTACHED";
        ContextText.Text = _attachmentLabel;
        ContextIcon.Source = null;
        ContextIcon.Visibility = Visibility.Collapsed;
    }

    private static ImageSource? TryLoadContextIcon(ContextRef context)
    {
        var executablePath = context.StableIdentity.Split('|', 2)[0];
        if (!File.Exists(executablePath)) return null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null) return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
            source.Freeze();
            return source;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string VisibilityLabel(AttachmentVisibility visibility) => visibility switch
    {
        AttachmentVisibility.ForegroundOnly => "Foreground",
        AttachmentVisibility.WhilePresent => "While running",
        AttachmentVisibility.RemainVisible => "Stay visible",
        AttachmentVisibility.SleepUntilReturn => "Sleep until return",
        _ => visibility.ToString(),
    };

    private void ModeClick(object sender, RoutedEventArgs e)
    {
        _controller.SetEditorMode(
            _controller.Presentation.EditorMode == NoteEditorMode.Edit
                ? NoteEditorMode.Preview
                : NoteEditorMode.Edit);
    }

    private void ApplyEditorMode(NoteEditorMode mode)
    {
        var shouldPreview = mode == NoteEditorMode.Preview;
        if (shouldPreview && !_isPreviewing)
        {
            _isPreviewing = true;
            PreviewViewer.Document = BuildPreviewDocument();
            MarkdownBox.Visibility = Visibility.Collapsed;
            PreviewViewer.Visibility = Visibility.Visible;
            ModeButton.Content = "EDIT";
        }
        else if (!shouldPreview && _isPreviewing)
        {
            _isPreviewing = false;
            PreviewViewer.Visibility = Visibility.Collapsed;
            MarkdownBox.Visibility = Visibility.Visible;
            ModeButton.Content = "PREVIEW";
            if (IsLoaded) MarkdownBox.Focus();
        }
    }

    private void ToggleTaskFromPreview(int taskIndex, bool isChecked)
    {
        if (_controller.Presentation.Locked)
        {
            return;
        }

        var parsed = _markdownService.Parse(MarkdownBox.Text);
        var task = parsed.Blocks.OfType<MarkdownTask>().Single(item => item.TaskIndex == taskIndex);
        if (task.IsChecked == isChecked)
        {
            return;
        }

        MarkdownBox.Text = _markdownService.ToggleTask(MarkdownBox.Text, taskIndex);
        PreviewViewer.Document = BuildPreviewDocument();
    }

    private FlowDocument BuildPreviewDocument() => MarkdownFlowDocumentBuilder.Build(
        _markdownService.Parse(MarkdownBox.Text),
        ToggleTaskFromPreview,
        UpdateInteractiveBlock,
        _note.Id,
        _database,
        _controller.Presentation);

    private void UpdateInteractiveBlock(InteractiveBlock block, int value)
    {
        if (_controller.Presentation.Locked)
        {
            return;
        }

        var service = new InteractiveBlockService();
        MarkdownBox.Text = block switch
        {
            ProgressInteractiveBlock => service.SetProgress(MarkdownBox.Text, block.TypeIndex, value),
            CounterInteractiveBlock => service.SetCounter(MarkdownBox.Text, block.TypeIndex, value),
            TimerInteractiveBlock => service.SetTimerDuration(MarkdownBox.Text, block.TypeIndex, value),
            _ => MarkdownBox.Text,
        };
    }

    private void NewStickyMenuClick(object sender, RoutedEventArgs e) => NewStickyRequested?.Invoke();

    private void NewFromClipboardMenuClick(object sender, RoutedEventArgs e) =>
        NewFromClipboardRequested?.Invoke();

    private void DuplicateMenuClick(object sender, RoutedEventArgs e) => DuplicateRequested?.Invoke(_note);

    private void EditTitleMenuClick(object sender, RoutedEventArgs e) => BeginTitleEdit();

    private void ArchiveMenuClick(object sender, RoutedEventArgs e) => ArchiveAndClose();

    private void MoveToTrashClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Move this note to Trash? It can be restored for 30 days.",
            "Move note to Trash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _controller.MoveToTrash(DateTimeOffset.Now);
        ArchiveRequested?.Invoke(_note.Id);
        Close();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => ArchiveAndClose();

    private void ArchiveAndClose()
    {
        _controller.Archive(DateTimeOffset.Now);
        ArchiveRequested?.Invoke(_note.Id);
        Close();
    }

    protected override async void OnClosed(EventArgs e)
    {
        _contentSaveTimer.Stop();
        _presentationSaveTimer.Stop();
        _hudTimer.Stop();
        if (!_controller.Presentation.Locked)
        {
            await SaveContentAsync();
        }
        await _database.SaveNotePresentationAsync(_controller.Presentation);
        base.OnClosed(e);
    }

    private static string NormalizeTitle(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();

    private static double HeaderHeight(NoteDensity density) => density switch
    {
        NoteDensity.Compact => 40,
        NoteDensity.Spacious => 56,
        _ => 48,
    };

    private static double FooterHeight(NoteDensity density) => density switch
    {
        NoteDensity.Compact => 30,
        NoteDensity.Spacious => 42,
        _ => 36,
    };

    private static Color ParseColor(string value) =>
        (Color)ColorConverter.ConvertFromString(value);

    private static Brush CreatePaperBrush(Color color)
    {
        var lighter = Color.FromRgb(
            (byte)Math.Min(255, color.R + 10),
            (byte)Math.Min(255, color.G + 10),
            (byte)Math.Min(255, color.B + 8));
        return new LinearGradientBrush(
            new GradientStopCollection
            {
                new(lighter, 0),
                new(color, 0.55),
                new(Color.FromRgb(
                    (byte)Math.Max(0, color.R - 7),
                    (byte)Math.Max(0, color.G - 7),
                    (byte)Math.Max(0, color.B - 5)), 1),
            },
            new Point(0, 0),
            new Point(1, 1));
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private static bool IsWithin(DependencyObject? source, DependencyObject target)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, target))
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}
