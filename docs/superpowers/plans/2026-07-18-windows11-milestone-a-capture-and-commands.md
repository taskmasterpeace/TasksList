# Windows 11 Milestone A: Capture and Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make region capture copy an image to Windows Clipboard without creating a note automatically, add explicit image-note conversion, add Windows-standard note-card context commands, and remove horizontal note-grid overflow.

**Architecture:** Keep the existing capture overlay, payload store, SQLite capture history, and clipboard paste platform. Extract a small capture-completion boundary so the default save-and-copy contract is testable, centralize capture-to-note Markdown construction, centralize note-card command mutations behind a storage interface, and keep WPF code-behind responsible only for selection/menu wiring and UI refresh. Correct the note layout using the existing WPF list before introducing broader Fluent shell changes.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, Microsoft.Data.Sqlite, existing `ClipboardPasteService`, existing `PayloadStore`.

## Global Constraints

- The default region-capture path saves exactly one capture and copies the bitmap to Windows Clipboard.
- The default region-capture path creates no note and opens no sticky.
- Capture-to-note remains available only through an explicit user command.
- Clipboard writes suppress Task'sList clipboard monitoring so the screenshot is not captured twice.
- Context-menu commands and keyboard commands use the same `NoteCardCommandService` methods.
- Right-clicking an unselected note selects only that note; right-clicking an already-selected note preserves the selection.
- Move to Trash is recoverable and requires confirmation; permanent deletion is not part of this menu.
- The note grid must never expose a horizontal scrollbar.
- Production changes follow red-green TDD and each task ends in a focused commit.
- Use `D:\git\taskslist\.tools\dotnet\dotnet.exe` for all .NET commands.

---

## File map

**Create**

- `src/TasksList.App/Capture/CaptureCompletionOperation.cs` — testable save-then-copy ordering for a completed region capture.
- `src/TasksList.App/Capture/CaptureNoteFactory.cs` — converts an existing capture into portable Markdown and a context attachment.
- `src/TasksList.App/Clipboard/ClipboardWriteOperation.cs` — bounded retry for transient Windows clipboard ownership failures.
- `src/TasksList.App/Library/NoteCardSelectionPolicy.cs` — pure Windows right-click selection policy.
- `src/TasksList.App/Library/NoteCardCommandService.cs` — shared open, duplicate, copy, archive, Trash, and attach command implementation.
- `tests/TasksList.App.Tests/Capture/CaptureCompletionOperationTests.cs`
- `tests/TasksList.App.Tests/Capture/CaptureCompletionPresentationTests.cs`
- `tests/TasksList.App.Tests/Capture/CaptureNoteFactoryTests.cs`
- `tests/TasksList.App.Tests/Clipboard/ClipboardWriteOperationTests.cs`
- `tests/TasksList.App.Tests/Library/NoteCardSelectionPolicyTests.cs`
- `tests/TasksList.App.Tests/Library/NoteCardCommandServiceTests.cs`
- `tests/TasksList.App.Tests/Library/MainWindowNoteCardInteractionContractTests.cs`
- `tests/TasksList.App.Tests/Library/MainWindowNoteLibraryContractTests.cs`
- `tests/TasksList.Core.Tests/Notes/NotePresentationTests.cs` — proves archive semantics.

**Modify**

- `src/TasksList.Core/Notes/NotePresentation.cs` — add a reusable recoverable archive transition.
- `src/TasksList.App/MainWindow.xaml.cs` — route capture and note commands through the new boundaries.
- `src/TasksList.App/MainWindow.xaml` — add capture completion notice, note-card context invocation, and correct scrolling.
- `src/TasksList.App/Clipboard/ClipboardPasteService.cs` — keep image data alive after source streams close.
- `tests/TasksList.App.Tests/Clipboard/ClipboardPasteServiceTests.cs` — verify bitmap clipboard representation.

---

### Task 1: Default capture completion saves then copies without creating a note

**Files:**

- Create: `src/TasksList.App/Capture/CaptureCompletionOperation.cs`
- Create: `tests/TasksList.App.Tests/Capture/CaptureCompletionOperationTests.cs`
- Modify: `src/TasksList.App/MainWindow.xaml.cs:149-205`

**Interfaces:**

- Produces: `CaptureCompletionOperation.SaveAndCopyAsync(Func<Task<CaptureModel>>, Action<CaptureModel>) -> Task<CaptureModel>`
- Consumes: existing `ClipboardPasteService.Copy(CaptureModel, PasteRepresentation.Original)`
- Later tasks consume the returned capture as the pending explicit capture-to-note target.

- [ ] **Step 1: Write the failing operation tests**

```csharp
using TasksList.App.Capture;
using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureCompletionOperationTests
{
    [Fact]
    public async Task SaveAndCopyStoresBeforePublishingToClipboardAndReturnsStoredCapture()
    {
        var events = new List<string>();
        var stored = CaptureModel.Create(
            CaptureKind.Image,
            ContextId.New(),
            "Screen capture · 20 × 10",
            DateTimeOffset.Parse("2026-07-18T22:00:00-04:00"));

        var result = await CaptureCompletionOperation.SaveAndCopyAsync(
            () =>
            {
                events.Add("save");
                return Task.FromResult(stored);
            },
            capture => events.Add($"copy:{capture.Id}"));

        Assert.Same(stored, result);
        Assert.Equal(["save", $"copy:{stored.Id}"], events);
    }

    [Fact]
    public async Task SaveFailureNeverPublishesAnUnstoredCapture()
    {
        var copied = false;

        await Assert.ThrowsAsync<IOException>(() =>
            CaptureCompletionOperation.SaveAndCopyAsync(
                () => Task.FromException<CaptureModel>(new IOException("disk full")),
                _ => copied = true));

        Assert.False(copied);
    }
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~CaptureCompletionOperationTests --no-restore
```

Expected: compilation fails because `CaptureCompletionOperation` does not exist.

- [ ] **Step 3: Add the minimal completion boundary**

```csharp
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Capture;

public static class CaptureCompletionOperation
{
    public static async Task<CaptureModel> SaveAndCopyAsync(
        Func<Task<CaptureModel>> saveCapture,
        Action<CaptureModel> copyCapture)
    {
        var capture = await saveCapture();
        copyCapture(capture);
        return capture;
    }
}
```

- [ ] **Step 4: Refactor `CaptureRegionCoreAsync` to store and copy only**

Replace the payload/history/note block after overlay completion with:

```csharp
var capture = await CaptureCompletionOperation.SaveAndCopyAsync(
    () => SaveScreenCaptureAsync(result, source),
    stored => _clipboardPasteService.Copy(stored, PasteRepresentation.Original));
_lastScreenCapture = new PendingScreenCapture(capture, source);
await ReloadClipboardAsync();
ShowCaptureNotice();
```

Add this helper next to `CaptureRegionCoreAsync`:

```csharp
private async Task<CaptureModel> SaveScreenCaptureAsync(
    ScreenCaptureResult result,
    ContextRef source)
{
    var payload = await _payloadStore.PutAsync(result.PngBytes, "image/png");
    await _database.SaveContextAsync(source);
    var capture = CaptureModel.Create(
            CaptureKind.Image,
            source.Id,
            $"Screen capture · {result.PixelWidth} × {result.PixelHeight}",
            DateTimeOffset.Now)
        .WithTextRepresentation("application/x-taskslist-payload-path", payload.Path);
    await _database.SaveCaptureAsync(capture);
    return capture;
}
```

Add the field/record used by later explicit actions:

```csharp
private PendingScreenCapture? _lastScreenCapture;

private sealed record PendingScreenCapture(CaptureModel Capture, ContextRef Source);
```

Delete the unconditional `Note.Create`, `_database.SaveNoteAsync`, `ReloadNotesAsync`, and `OpenSticky` calls from the capture path.

- [ ] **Step 5: Run the focused capture tests and verify GREEN**

Run the command from Step 2.

Expected: 2 passed, 0 failed.

- [ ] **Step 6: Commit the capture contract**

```powershell
git add src/TasksList.App/Capture/CaptureCompletionOperation.cs tests/TasksList.App.Tests/Capture/CaptureCompletionOperationTests.cs src/TasksList.App/MainWindow.xaml.cs
git commit -m "fix: copy region captures without creating notes"
```

---

### Task 2: Publish a persistent image clipboard representation

**Files:**

- Create: `src/TasksList.App/Clipboard/ClipboardWriteOperation.cs`
- Create: `tests/TasksList.App.Tests/Clipboard/ClipboardWriteOperationTests.cs`
- Modify: `src/TasksList.App/Clipboard/ClipboardPasteService.cs:74-118`
- Modify: `tests/TasksList.App.Tests/Clipboard/ClipboardPasteServiceTests.cs`

**Interfaces:**

- Consumes: capture representation key `application/x-taskslist-payload-path`
- Produces: a `DataObject` containing `DataFormats.Bitmap`, Unicode fallback text, and the private PNG byte format `PNG`.
- Produces: `ClipboardWriteOperation.Run(Action write, Action<TimeSpan> wait)` with four bounded attempts for `COMException`.

- [ ] **Step 1: Add failing clipboard retry tests**

```csharp
using System.Runtime.InteropServices;
using TasksList.App.Clipboard;

namespace TasksList.App.Tests.Clipboard;

public sealed class ClipboardWriteOperationTests
{
    [Fact]
    public void TransientOwnershipFailureRetriesFourTimesWithBoundedBackoff()
    {
        var attempts = 0;
        var waits = new List<TimeSpan>();

        ClipboardWriteOperation.Run(
            () =>
            {
                attempts++;
                if (attempts < 4) throw new COMException("clipboard busy");
            },
            waits.Add);

        Assert.Equal(4, attempts);
        Assert.Equal(
            [TimeSpan.FromMilliseconds(35), TimeSpan.FromMilliseconds(70), TimeSpan.FromMilliseconds(105)],
            waits);
    }

    [Fact]
    public void FinalOwnershipFailureEscapesForCaptureErrorReporting()
    {
        var attempts = 0;
        Assert.Throws<COMException>(() => ClipboardWriteOperation.Run(
            () =>
            {
                attempts++;
                throw new COMException("clipboard busy");
            },
            _ => { }));
        Assert.Equal(4, attempts);
    }
}
```

- [ ] **Step 2: Run the retry tests and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~ClipboardWriteOperationTests --no-restore
```

Expected: compilation fails because `ClipboardWriteOperation` does not exist.

- [ ] **Step 3: Implement bounded Windows clipboard retry**

```csharp
using System.Runtime.InteropServices;

namespace TasksList.App.Clipboard;

public static class ClipboardWriteOperation
{
    public static void Run(Action write, Action<TimeSpan> wait)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                write();
                return;
            }
            catch (COMException) when (attempt < 3)
            {
                wait(TimeSpan.FromMilliseconds(35 * (attempt + 1)));
            }
        }
    }
}
```

Change `WindowsClipboardPastePlatform.SetClipboard` to:

```csharp
public void SetClipboard(CaptureModel capture, PasteRepresentation representation)
{
    var data = CreateDataObject(capture, representation);
    ClipboardWriteOperation.Run(
        () => System.Windows.Clipboard.SetDataObject(data, copy: true),
        Thread.Sleep);
}
```

- [ ] **Step 4: Run the retry tests and verify GREEN**

Run the command from Step 2.

Expected: 2 passed, 0 failed.

- [ ] **Step 5: Add a failing bitmap/PNG data-object test**

```csharp
[Fact]
public void OriginalImagePublishesPersistentBitmapAndPngRepresentations()
{
    var directory = Path.Combine(Path.GetTempPath(), $"taskslist-image-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var path = Path.Combine(directory, "capture.png");
        File.WriteAllBytes(path, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAF/gL+X3c3WQAAAABJRU5ErkJggg=="));
        var capture = CaptureModel.Create(
                CaptureKind.Image,
                ContextId.New(),
                "Screen capture · 1 × 1",
                DateTimeOffset.UtcNow)
            .WithTextRepresentation("application/x-taskslist-payload-path", path);

        var data = WindowsClipboardPastePlatform.CreateDataObject(
            capture,
            PasteRepresentation.Original);

        Assert.True(data.GetDataPresent(DataFormats.Bitmap));
        Assert.True(data.GetDataPresent("PNG"));
        Assert.IsType<byte[]>(data.GetData("PNG"));
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}
```

- [ ] **Step 6: Run the focused bitmap test and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~OriginalImagePublishesPersistentBitmapAndPngRepresentations --no-restore
```

Expected: FAIL because the `PNG` representation is absent.

- [ ] **Step 7: Load the image fully and publish both representations**

Replace the payload-image branch in `CreateDataObject` with:

```csharp
if (capture.TextRepresentations.TryGetValue(
        "application/x-taskslist-payload-path",
        out var payloadPath) && File.Exists(payloadPath))
{
    var pngBytes = File.ReadAllBytes(payloadPath);
    using var stream = new MemoryStream(pngBytes, writable: false);
    var decoder = new PngBitmapDecoder(
        stream,
        BitmapCreateOptions.PreservePixelFormat,
        BitmapCacheOption.OnLoad);
    var image = decoder.Frames[0];
    image.Freeze();
    data.SetData(DataFormats.Bitmap, image);
    data.SetData("PNG", pngBytes);
}
```

- [ ] **Step 8: Run all clipboard tests and verify GREEN**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~ClipboardPasteServiceTests --no-restore
```

Expected: all `ClipboardPasteServiceTests` pass.

- [ ] **Step 9: Commit persistent image clipboard data**

```powershell
git add src/TasksList.App/Clipboard/ClipboardWriteOperation.cs tests/TasksList.App.Tests/Clipboard/ClipboardWriteOperationTests.cs src/TasksList.App/Clipboard/ClipboardPasteService.cs tests/TasksList.App.Tests/Clipboard/ClipboardPasteServiceTests.cs
git commit -m "fix: publish persistent screenshot clipboard data"
```

---

### Task 3: Explicit capture-to-note conversion and completion notice

**Files:**

- Create: `src/TasksList.App/Capture/CaptureNoteFactory.cs`
- Create: `tests/TasksList.App.Tests/Capture/CaptureNoteFactoryTests.cs`
- Modify: `src/TasksList.App/MainWindow.xaml`
- Modify: `src/TasksList.App/MainWindow.xaml.cs`

**Interfaces:**

- Produces: `CaptureNoteFactory.Create(CaptureModel capture, ContextRef source) -> Note`
- Consumes: payload-path representation and source context.
- The main window consumes `_lastScreenCapture` from Task 1.

- [ ] **Step 1: Write failing image-note and text-note tests**

```csharp
using TasksList.App.Capture;
using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureNoteFactoryTests
{
    [Fact]
    public void ImageCaptureCreatesMarkdownWithLocalImageSourceAndDimensions()
    {
        var source = ContextRef.Create(ContextKind.Application, "windows", "paint.exe", "Paint");
        var capture = CaptureModel.Create(
                CaptureKind.Image,
                source.Id,
                "Screen capture · 640 × 360",
                DateTimeOffset.UtcNow)
            .WithTextRepresentation("application/x-taskslist-payload-path", @"C:\captures\shot.png");

        var note = CaptureNoteFactory.Create(capture, source);

        Assert.Equal("Capture from Paint", note.Title);
        Assert.Contains("![Captured region](<C:\\captures\\shot.png>)", note.Markdown);
        Assert.Contains("640 × 360", note.Markdown);
        Assert.Contains(note.Attachments, item => item.ContextId == source.Id);
    }

    [Fact]
    public void TextCaptureUsesPreviewWhenNoImagePayloadExists()
    {
        var source = ContextRef.Create(ContextKind.Application, "windows", "terminal.exe", "Terminal");
        var capture = CaptureModel.Create(
            CaptureKind.Text,
            source.Id,
            "docker ps",
            DateTimeOffset.UtcNow);

        var note = CaptureNoteFactory.Create(capture, source);

        Assert.Contains("docker ps", note.Markdown);
        Assert.DoesNotContain("![Captured region]", note.Markdown);
    }
}
```

- [ ] **Step 2: Run the factory tests and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~CaptureNoteFactoryTests --no-restore
```

Expected: compilation fails because `CaptureNoteFactory` does not exist.

- [ ] **Step 3: Implement the capture-note factory**

```csharp
using TasksList.Core.Models;
using CaptureModel = TasksList.Core.Models.Capture;

namespace TasksList.App.Capture;

public static class CaptureNoteFactory
{
    public static Note Create(CaptureModel capture, ContextRef source)
    {
        var body = capture.Kind == CaptureKind.Image &&
                   capture.TextRepresentations.TryGetValue(
                       "application/x-taskslist-payload-path",
                       out var payloadPath)
            ? $"![Captured region](<{payloadPath}>)\n\n**Size:** {capture.PreviewText.Replace("Screen capture · ", string.Empty, StringComparison.Ordinal)}"
            : capture.TextRepresentations.TryGetValue("text/plain", out var plain)
                ? plain
                : capture.PreviewText;
        return Note.Create(
                $"Capture from {source.DisplayName}",
                $"# Capture from {source.DisplayName}\n\n{body}\n\n**Source:** {source.DisplayName}")
            .AttachTo(source.Id, AttachmentVisibility.WhilePresent);
    }
}
```

- [ ] **Step 4: Add a failing completion-notice presentation contract**

```csharp
using System.Xml.Linq;

namespace TasksList.App.Tests.Capture;

public sealed class CaptureCompletionPresentationTests
{
    [Fact]
    public void MainWindowOffersExplicitCreateNoteAfterScreenshotCopy()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml"));
        var notice = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "Border" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "CaptureNotice")));
        Assert.Contains(notice.Descendants(), element =>
            element.Name.LocalName == "TextBlock" &&
            element.Attribute("Text")?.Value == "Screenshot copied to clipboard");
        Assert.Contains(notice.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            element.Attribute("Click")?.Value == "CreateCapturedNoteClick");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
```

- [ ] **Step 5: Run the presentation contract and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~CaptureCompletionPresentationTests --no-restore
```

Expected: FAIL because `CaptureNotice` does not exist.

- [ ] **Step 6: Add the capture completion notice**

Add this overlay as the last child of the main window's root content `Grid`:

```xml
<Border x:Name="CaptureNotice" Grid.RowSpan="2" Panel.ZIndex="20"
        Visibility="Collapsed" HorizontalAlignment="Center" VerticalAlignment="Bottom"
        Margin="24" Padding="14,10" CornerRadius="8"
        Background="{StaticResource CardBrush}" BorderBrush="{StaticResource BorderBrush}"
        BorderThickness="1" AutomationProperties.LiveSetting="Polite">
    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
        <TextBlock Text="Screenshot copied to clipboard" VerticalAlignment="Center" />
        <Button Style="{StaticResource GhostButton}" Content="Create note"
                Margin="12,0,0,0" Click="CreateCapturedNoteClick" />
        <Button Style="{StaticResource GhostButton}" Content="Dismiss"
                Margin="4,0,0,0" Click="DismissCaptureNoticeClick" />
    </StackPanel>
</Border>
```

Add these methods to `MainWindow`:

```csharp
private void ShowCaptureNotice()
{
    CaptureNotice.Visibility = Visibility.Visible;
    StatusText.Text = "SCREENSHOT COPIED";
}

private async void CreateCapturedNoteClick(object sender, RoutedEventArgs e)
{
    if (_lastScreenCapture is not { } pending) return;
    var note = CaptureNoteFactory.Create(pending.Capture, pending.Source);
    await _database.SaveNoteAsync(note);
    await ReloadNotesAsync();
    CaptureNotice.Visibility = Visibility.Collapsed;
    OpenSticky(note);
}

private void DismissCaptureNoticeClick(object sender, RoutedEventArgs e) =>
    CaptureNotice.Visibility = Visibility.Collapsed;
```

Refactor `ClipToNoteClick` to load the source context and call `CaptureNoteFactory.Create` so both explicit entry points use identical note construction.

- [ ] **Step 7: Run capture tests and verify GREEN**

Run the commands from Steps 2 and 5.

Expected: 2 passed, 0 failed.

- [ ] **Step 8: Commit explicit note conversion**

```powershell
git add src/TasksList.App/Capture/CaptureNoteFactory.cs tests/TasksList.App.Tests/Capture/CaptureNoteFactoryTests.cs tests/TasksList.App.Tests/Capture/CaptureCompletionPresentationTests.cs src/TasksList.App/MainWindow.xaml src/TasksList.App/MainWindow.xaml.cs
git commit -m "feat: make screenshot notes an explicit action"
```

---

### Task 4: Windows right-click selection policy

**Files:**

- Create: `src/TasksList.App/Library/NoteCardSelectionPolicy.cs`
- Create: `tests/TasksList.App.Tests/Library/NoteCardSelectionPolicyTests.cs`

**Interfaces:**

- Produces: `NoteCardSelectionPolicy.ResolveRightClick(int clickedIndex, IReadOnlyCollection<int> selectedIndices) -> IReadOnlyList<int>`
- Later menu wiring applies the returned indices to `NotesList.SelectedItems`.

- [ ] **Step 1: Write failing policy tests**

```csharp
using TasksList.App.Library;

namespace TasksList.App.Tests.Library;

public sealed class NoteCardSelectionPolicyTests
{
    [Fact]
    public void RightClickOnUnselectedCardSelectsOnlyThatCard() =>
        Assert.Equal([3], NoteCardSelectionPolicy.ResolveRightClick(3, [1, 2]));

    [Fact]
    public void RightClickOnSelectedCardPreservesMultiSelection() =>
        Assert.Equal([1, 3, 5], NoteCardSelectionPolicy.ResolveRightClick(3, [1, 3, 5]));
}
```

- [ ] **Step 2: Run the policy tests and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~NoteCardSelectionPolicyTests --no-restore
```

Expected: compilation fails because `NoteCardSelectionPolicy` does not exist.

- [ ] **Step 3: Implement the minimal policy**

```csharp
namespace TasksList.App.Library;

public static class NoteCardSelectionPolicy
{
    public static IReadOnlyList<int> ResolveRightClick(
        int clickedIndex,
        IReadOnlyCollection<int> selectedIndices) =>
        selectedIndices.Contains(clickedIndex)
            ? selectedIndices.Order().ToArray()
            : [clickedIndex];
}
```

- [ ] **Step 4: Run the policy tests and verify GREEN**

Run the command from Step 2.

Expected: 2 passed, 0 failed.

- [ ] **Step 5: Commit selection policy**

```powershell
git add src/TasksList.App/Library/NoteCardSelectionPolicy.cs tests/TasksList.App.Tests/Library/NoteCardSelectionPolicyTests.cs
git commit -m "feat: add Windows note-card right-click selection"
```

---

### Task 5: Shared note-card command service

**Files:**

- Modify: `src/TasksList.Core/Notes/NotePresentation.cs`
- Create: `tests/TasksList.Core.Tests/Notes/NotePresentationTests.cs`
- Create: `src/TasksList.App/Library/NoteCardCommandService.cs`
- Create: `tests/TasksList.App.Tests/Library/NoteCardCommandServiceTests.cs`

**Interfaces:**

- Produces: `INoteCardCommandStore`, `TasksListNoteCardCommandStore`, and `NoteCardCommandService`.
- `NoteCardCommandService` methods: `Open`, `DuplicateAsync`, `CopyMarkdown`, `ArchiveAsync`, `MoveToTrashAsync`, and `AttachAsync`.
- Main-window menu and future keyboard commands consume the same service instance.

- [ ] **Step 1: Add a failing archive transition test**

```csharp
using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.Core.Tests.Notes;

public sealed class NotePresentationTests
{
[Fact]
public void ArchiveHidesWithoutDeletingAndRestoreReturnsItToLibrary()
{
    var noteId = NoteId.New();
    var archivedAt = DateTimeOffset.Parse("2026-07-18T23:00:00-04:00");
    var presentation = NotePresentation.Default(noteId, archivedAt.AddHours(-1));

    var archived = presentation.Archive(archivedAt);

    Assert.Equal(archivedAt, archived.HiddenAt);
    Assert.Null(archived.DeletedAt);
    Assert.Null(archived.Restore(archivedAt.AddMinutes(1)).HiddenAt);
}
}
```

- [ ] **Step 2: Run the focused core test and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.Core.Tests/TasksList.Core.Tests.csproj --filter FullyQualifiedName~ArchiveHidesWithoutDeleting --no-restore
```

Expected: compilation fails because `NotePresentation.Archive` does not exist.

- [ ] **Step 3: Add the archive transition**

```csharp
public NotePresentation Archive(DateTimeOffset archivedAt) =>
    this with { HiddenAt = archivedAt, ModifiedAt = archivedAt };
```

- [ ] **Step 4: Write command-service tests with a fake store**

The test file defines this dictionary-backed store before the test methods:

```csharp
using TasksList.App.Library;
using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.App.Tests.Library;

public sealed class NoteCardCommandServiceTests
{
private sealed class FakeStore : INoteCardCommandStore
{
    public FakeStore(Note note, NotePresentation presentation)
    {
        Notes[note.Id] = note;
        Presentations[note.Id] = presentation;
    }

    public Dictionary<NoteId, Note> Notes { get; } = [];
    public Dictionary<NoteId, NotePresentation> Presentations { get; } = [];

    public Task SaveNoteAsync(Note note)
    {
        Notes[note.Id] = note;
        return Task.CompletedTask;
    }

    public Task<NotePresentation> GetPresentationAsync(NoteId noteId) =>
        Task.FromResult(Presentations[noteId]);

    public Task SavePresentationAsync(NotePresentation presentation)
    {
        Presentations[presentation.NoteId] = presentation;
        return Task.CompletedTask;
    }
}
```

Add these tests after the store:

```csharp
[Fact]
public async Task DuplicateCopiesContentAndPresentationWithoutLifecycleFlags()
{
    var source = Note.Create("Plan", "# Plan");
    var store = new FakeStore(source, NotePresentation.Default(source.Id) with
    {
        Topmost = false,
        HiddenAt = DateTimeOffset.UtcNow,
        DeletedAt = DateTimeOffset.UtcNow,
    });
    var service = new NoteCardCommandService(store, _ => { }, _ => { }, _ => { });

    var duplicates = await service.DuplicateAsync([source], DateTimeOffset.UtcNow);

    var duplicate = Assert.Single(duplicates);
    Assert.Equal("Plan copy", duplicate.Title);
    Assert.Equal(source.Markdown, duplicate.Markdown);
    Assert.Null(store.Presentations[duplicate.Id].HiddenAt);
    Assert.Null(store.Presentations[duplicate.Id].DeletedAt);
}

[Fact]
public async Task TrashAndArchiveUseDistinctRecoverableStates()
{
    var note = Note.Create("Plan", "# Plan");
    var store = new FakeStore(note, NotePresentation.Default(note.Id));
    var closed = new List<NoteId>();
    var service = new NoteCardCommandService(store, _ => { }, _ => { }, closed.Add);
    var now = DateTimeOffset.UtcNow;

    await service.ArchiveAsync([note], now);
    Assert.Equal(now, store.Presentations[note.Id].HiddenAt);
    Assert.Null(store.Presentations[note.Id].DeletedAt);

    await service.MoveToTrashAsync([note], now.AddMinutes(1));
    Assert.Equal(now.AddMinutes(1), store.Presentations[note.Id].DeletedAt);
    Assert.Contains(note.Id, closed);
}
}
```

- [ ] **Step 5: Run command-service tests and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~NoteCardCommandServiceTests --no-restore
```

Expected: compilation fails because the command interfaces and service do not exist.

- [ ] **Step 6: Implement the command store and service**

Create `NoteCardCommandService.cs` with this complete implementation:

```csharp
using TasksList.Core.Models;
using TasksList.Core.Notes;
using TasksList.Infrastructure.Storage;

namespace TasksList.App.Library;

public interface INoteCardCommandStore
{
    Task SaveNoteAsync(Note note);
    Task<NotePresentation> GetPresentationAsync(NoteId noteId);
    Task SavePresentationAsync(NotePresentation presentation);
}

public sealed class TasksListNoteCardCommandStore(TasksListDatabase database)
    : INoteCardCommandStore
{
    public Task SaveNoteAsync(Note note) => database.SaveNoteAsync(note);
    public Task<NotePresentation> GetPresentationAsync(NoteId noteId) =>
        database.GetNotePresentationAsync(noteId);
    public Task SavePresentationAsync(NotePresentation presentation) =>
        database.SaveNotePresentationAsync(presentation);
}

public sealed class NoteCardCommandService(
    INoteCardCommandStore store,
    Action<Note> open,
    Action<string> copyMarkdown,
    Action<NoteId> closeOpenSticky)
{
    public void Open(IReadOnlyList<Note> notes)
    {
        foreach (var note in notes) open(note);
    }

    public async Task<IReadOnlyList<Note>> DuplicateAsync(
        IReadOnlyList<Note> notes,
        DateTimeOffset now)
    {
        var duplicates = new List<Note>(notes.Count);
        foreach (var source in notes)
        {
            var duplicate = Note.Create($"{source.Title} copy", source.Markdown);
            foreach (var attachment in source.Attachments)
            {
                duplicate = duplicate.AttachTo(attachment.ContextId, attachment.Visibility);
            }
            await store.SaveNoteAsync(duplicate);
            var sourcePresentation = await store.GetPresentationAsync(source.Id);
            var bounds = sourcePresentation.Bounds;
            await store.SavePresentationAsync(sourcePresentation with
            {
                NoteId = duplicate.Id,
                Bounds = bounds with { Left = bounds.Left + 24, Top = bounds.Top + 24 },
                HiddenAt = null,
                DeletedAt = null,
                WakeAt = null,
                CreatedAt = now,
                ModifiedAt = now,
            });
            duplicates.Add(duplicate);
        }
        return duplicates;
    }

    public void CopyMarkdown(IReadOnlyList<Note> notes) =>
        copyMarkdown(string.Join(
            $"{Environment.NewLine}---{Environment.NewLine}",
            notes.Select(note => note.Markdown)));

    public async Task ArchiveAsync(IReadOnlyList<Note> notes, DateTimeOffset now)
    {
        foreach (var note in notes)
        {
            var presentation = await store.GetPresentationAsync(note.Id);
            await store.SavePresentationAsync(presentation.Archive(now));
            closeOpenSticky(note.Id);
        }
    }

    public async Task MoveToTrashAsync(IReadOnlyList<Note> notes, DateTimeOffset now)
    {
        foreach (var note in notes)
        {
            var presentation = await store.GetPresentationAsync(note.Id);
            await store.SavePresentationAsync(presentation.SoftDelete(now));
            closeOpenSticky(note.Id);
        }
    }

    public async Task<IReadOnlyList<Note>> AttachAsync(
        IReadOnlyList<Note> notes,
        ContextRef context)
    {
        var updated = new List<Note>(notes.Count);
        foreach (var note in notes)
        {
            var attached = note.AttachTo(context.Id, AttachmentVisibility.WhilePresent);
            await store.SaveNoteAsync(attached);
            updated.Add(attached);
        }
        return updated;
    }
}
```

- [ ] **Step 7: Run core and command tests and verify GREEN**

Run both commands from Steps 2 and 5.

Expected: all focused tests pass.

- [ ] **Step 8: Commit the command layer**

```powershell
git add src/TasksList.Core/Notes/NotePresentation.cs tests/TasksList.Core.Tests/Notes/NotePresentationTests.cs src/TasksList.App/Library/NoteCardCommandService.cs tests/TasksList.App.Tests/Library/NoteCardCommandServiceTests.cs
git commit -m "feat: centralize note-card commands"
```

---

### Task 6: Wire note-card context menus and keyboard invocation

**Files:**

- Modify: `src/TasksList.App/MainWindow.xaml:202-259`
- Modify: `src/TasksList.App/MainWindow.xaml.cs:59-70,315-405`
- Create: `tests/TasksList.App.Tests/Library/MainWindowNoteCardInteractionContractTests.cs`

**Interfaces:**

- Consumes: `NoteCardSelectionPolicy` and `NoteCardCommandService`.
- Produces: right-click and Shift+F10 command access on every note card.

- [ ] **Step 1: Write a failing note-card interaction contract**

```csharp
using System.Xml.Linq;

namespace TasksList.App.Tests.Library;

public sealed class MainWindowNoteCardInteractionContractTests
{
    [Fact]
    public void NoteCardsExposeMouseAndKeyboardContextInvocation()
    {
        var document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml"));
        var card = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "Button" &&
            element.Attribute("Click")?.Value == "OpenNoteClick"));
        Assert.Equal("NoteCardRightButtonDown", card.Attribute("PreviewMouseRightButtonDown")?.Value);
        Assert.Equal("NoteCardRightButtonUp", card.Attribute("PreviewMouseRightButtonUp")?.Value);
        Assert.Equal("NoteCardKeyDown", card.Attribute("KeyDown")?.Value);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
```

- [ ] **Step 2: Run the interaction contract and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~MainWindowNoteCardInteractionContractTests --no-restore
```

Expected: FAIL because the right-button and keyboard handlers are absent.

- [ ] **Step 3: Add the command service to `MainWindow`**

```csharp
private readonly NoteCardCommandService _noteCardCommands;
```

Initialize it after `InitializeComponent()`:

```csharp
_noteCardCommands = new NoteCardCommandService(
    new TasksListNoteCardCommandStore(_database),
    OpenSticky,
    markdown => System.Windows.Clipboard.SetText(markdown),
    noteId =>
    {
        if (_openStickies.Remove(noteId, out var sticky)) sticky.Close();
    });
```

- [ ] **Step 4: Make the note card a keyboard/context-menu host**

Add to the card `Button`:

```xml
AutomationProperties.Name="{Binding Title}"
PreviewMouseRightButtonDown="NoteCardRightButtonDown"
PreviewMouseRightButtonUp="NoteCardRightButtonUp"
KeyDown="NoteCardKeyDown"
```

- [ ] **Step 5: Implement selection and keyboard routing**

```csharp
private void NoteCardRightButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is not Button { Tag: NoteCardViewModel card }) return;
    var clickedIndex = NotesList.Items.IndexOf(card);
    var selectedIndices = NotesList.SelectedItems
        .Cast<NoteCardViewModel>()
        .Select(item => NotesList.Items.IndexOf(item))
        .ToArray();
    var resolved = NoteCardSelectionPolicy.ResolveRightClick(clickedIndex, selectedIndices);
    NotesList.SelectedItems.Clear();
    foreach (var index in resolved) NotesList.SelectedItems.Add(NotesList.Items[index]);
}

private async void NoteCardRightButtonUp(object sender, MouseButtonEventArgs e)
{
    if (sender is Button { Tag: NoteCardViewModel card } button)
    {
        await OpenNoteCardContextMenuAsync(button, card);
        e.Handled = true;
    }
}

private async void NoteCardKeyDown(object sender, KeyEventArgs e)
{
    if (sender is not Button { Tag: NoteCardViewModel card }) return;
    if (e.Key == Key.Enter)
    {
        _noteCardCommands.Open([card.Note]);
        e.Handled = true;
    }
    else if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
    {
        await OpenNoteCardContextMenuAsync((FrameworkElement)sender, card);
        e.Handled = true;
    }
}
```

- [ ] **Step 6: Build one menu for mouse and keyboard**

Add this complete shared builder to `MainWindow`:

```csharp
private async Task OpenNoteCardContextMenuAsync(
    FrameworkElement target,
    NoteCardViewModel fallback)
{
    var cards = NotesList.SelectedItems.Cast<NoteCardViewModel>().ToArray();
    if (cards.Length == 0) cards = [fallback];
    var notes = cards.Select(card => card.Note).ToArray();
    var menu = new ContextMenu { PlacementTarget = target };

    MenuItem Add(string header, Action action, string? gesture = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? string.Empty };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
        return item;
    }

    Add("_Open sticky", () => _noteCardCommands.Open(notes), "Enter");
    Add("_Duplicate", () => _ = DuplicateSelectedNotesAsync(notes), "Ctrl+D");
    Add("Copy _Markdown", () => _noteCardCommands.CopyMarkdown(notes), "Ctrl+C");
    menu.Items.Add(new Separator());

    var attach = Add(
        "_Attach to last application",
        () => _ = AttachSelectedNotesAsync(notes),
        null);
    attach.IsEnabled = _lastExternalContext is not null;
    attach.ToolTip = attach.IsEnabled ? null : "Use another application first so Task'sList can identify it.";

    var styleMenu = new MenuItem { Header = "Apply _style" };
    foreach (var preset in Enum.GetValues<PaperPreset>())
    {
        var style = NoteStyle.FromPreset(preset);
        var item = new MenuItem { Header = preset.ToString(), Tag = style };
        item.Click += async (_, _) => await ApplyStyleToNotesAsync(cards, style);
        styleMenu.Items.Add(item);
    }
    var namedStyles = await _database.ListNamedStylesAsync();
    if (namedStyles.Count > 0) styleMenu.Items.Add(new Separator());
    foreach (var named in namedStyles)
    {
        var item = new MenuItem { Header = named.Name, Tag = named.Style };
        item.Click += async (_, _) => await ApplyStyleToNotesAsync(cards, named.Style);
        styleMenu.Items.Add(item);
    }
    menu.Items.Add(styleMenu);
    menu.Items.Add(new Separator());
    Add("_Archive", () => _ = ArchiveSelectedNotesAsync(notes));
    Add("Move to _Trash…", () => _ = TrashSelectedNotesAsync(notes), "Delete");
    target.ContextMenu = menu;
    menu.IsOpen = true;
}

private async Task DuplicateSelectedNotesAsync(IReadOnlyList<Note> notes)
{
    var duplicates = await _noteCardCommands.DuplicateAsync(notes, DateTimeOffset.Now);
    await ReloadNotesAsync();
    foreach (var duplicate in duplicates) OpenSticky(duplicate);
}

private async Task AttachSelectedNotesAsync(IReadOnlyList<Note> notes)
{
    if (_lastExternalContext is null) return;
    await _noteCardCommands.AttachAsync(notes, _lastExternalContext);
    await ReloadNotesAsync();
}

private async Task ArchiveSelectedNotesAsync(IReadOnlyList<Note> notes)
{
    await _noteCardCommands.ArchiveAsync(notes, DateTimeOffset.Now);
    await ReloadNotesAsync();
}

private async Task TrashSelectedNotesAsync(IReadOnlyList<Note> notes)
{
    var confirmed = MessageBox.Show(
        this,
        $"Move {notes.Count} note{(notes.Count == 1 ? string.Empty : "s")} to Trash? They can be restored for 30 days.",
        "Move notes to Trash",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning) == MessageBoxResult.Yes;
    if (!confirmed) return;
    await _noteCardCommands.MoveToTrashAsync(notes, DateTimeOffset.Now);
    await ReloadNotesAsync();
    await ReloadTrashAsync();
}
```

- [ ] **Step 7: Run the interaction contract and all app tests**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --no-restore
```

Expected: 0 failed.

- [ ] **Step 8: Commit context menu wiring**

```powershell
git add tests/TasksList.App.Tests/Library/MainWindowNoteCardInteractionContractTests.cs src/TasksList.App/MainWindow.xaml src/TasksList.App/MainWindow.xaml.cs
git commit -m "feat: add note-card context commands"
```

---

### Task 7: Remove horizontal note-grid overflow

**Files:**

- Create: `tests/TasksList.App.Tests/Library/MainWindowNoteLibraryContractTests.cs`
- Modify: `src/TasksList.App/MainWindow.xaml:200-260`

**Interfaces:**

- Produces: a note list with one vertical scrolling owner, disabled horizontal scrolling, and wrapping cards.

- [ ] **Step 1: Write the failing layout contract**

```csharp
using System.Xml.Linq;

namespace TasksList.App.Tests.Library;

public sealed class MainWindowNoteLibraryContractTests
{
    [Fact]
    public void NoteLibraryWrapsCardsWithoutNestedOrHorizontalScrolling()
    {
        var mainWindow = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "src", "TasksList.App", "MainWindow.xaml"));
        var notesList = Assert.Single(mainWindow.Descendants().Where(element =>
            element.Name.LocalName == "ListBox" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "NotesList")));

        Assert.Equal("Disabled", notesList.Attribute("ScrollViewer.HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", notesList.Attribute("ScrollViewer.VerticalScrollBarVisibility")?.Value);
        Assert.DoesNotContain(notesList.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(notesList.Descendants(), element => element.Name.LocalName == "WrapPanel");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TasksList.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Task'sList repository root.");
    }
}
```

- [ ] **Step 2: Run the layout test and verify RED**

Run:

```powershell
& 'D:\git\taskslist\.tools\dotnet\dotnet.exe' test tests/TasksList.App.Tests/TasksList.App.Tests.csproj --filter FullyQualifiedName~MainWindowNoteLibraryContractTests --no-restore
```

Expected: FAIL because an outer `ScrollViewer` exists and list scrolling attributes are absent.

- [ ] **Step 3: Give the list sole ownership of scrolling**

Remove the `ScrollViewer` that directly wraps `NotesList`. Set:

```xml
<ListBox x:Name="NotesList" SelectionMode="Extended"
         Background="Transparent" BorderThickness="0"
         ScrollViewer.HorizontalScrollBarVisibility="Disabled"
         ScrollViewer.VerticalScrollBarVisibility="Auto"
         ScrollViewer.CanContentScroll="False">
```

Keep the existing `WrapPanel`, card width, and vertical layout for this milestone. Disabling horizontal scroll forces the clipped fourth card onto the next row at the default width.

- [ ] **Step 4: Run the layout test and verify GREEN**

Run the command from Step 2.

Expected: 1 passed, 0 failed.

- [ ] **Step 5: Commit responsive note wrapping**

```powershell
git add tests/TasksList.App.Tests/Library/MainWindowNoteLibraryContractTests.cs src/TasksList.App/MainWindow.xaml
git commit -m "fix: wrap note cards without horizontal overflow"
```

---

### Task 8: Release verification for Milestone A

**Files:**

- Modify only if verification exposes a defect in Milestone A files.

- [ ] **Step 1: Run the full Release build**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-release.ps1
```

Expected: all solution tests pass, app and three plugins publish, required release files validate, and `artifacts\release` is created.

- [ ] **Step 2: Install the exact release artifact**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File artifacts\release\install.ps1
```

Expected: installed executable is launched from `%LOCALAPPDATA%\Programs\TasksList` and its SHA-256 equals `artifacts\release\app\TasksList.App.exe`.

- [ ] **Step 3: Verify screenshot-to-clipboard physically**

Use the installed Capture Region command to capture a known 120×80 test region. Verify:

1. The completion notice says Screenshot copied.
2. The Clipboard tab gains exactly one image capture.
3. The Notes tab gains no note.
4. Pasting into Paint produces the selected pixels at the expected dimensions.
5. Create note from the notice creates exactly one note containing the captured image.

- [ ] **Step 4: Verify note-card commands physically**

Create two disposable notes. Verify left-click selection, Ctrl+click multi-selection, right-click selection preservation, Shift+F10, Open, Duplicate, Copy Markdown, Apply style, Archive, Move to Trash confirmation, and Trash restore. Remove the disposable records through recoverable Trash actions.

- [ ] **Step 5: Verify layout at supported widths**

At minimum width, default width, and maximized width, confirm note cards wrap vertically, remain fully visible, and no horizontal scrollbar appears.

- [ ] **Step 6: Verify process and Windows event log health**

Confirm `TasksList.App` remains responsive and no new `.NET Runtime` or `Application Error` event for `TasksList.App.exe` appears after the verification start time.

- [ ] **Step 7: Run final source checks and push**

```powershell
git diff --check
git status --short
git push origin master
```

Expected: no whitespace errors, no uncommitted source changes, and remote `master` matches local `HEAD`.

---

## Milestone A completion gate

Milestone A is complete only when automated and installed evidence proves all four outcomes together: region capture copies a persistent image and creates no note by default; explicit conversion creates one correct note; note cards expose Windows-standard mouse/keyboard context commands; and the note library has no horizontal overflow. Passing only the unit tests does not satisfy the gate.

## Plan self-review record

- **Specification coverage:** Tasks 1–3 cover screenshot history, persistent clipboard publishing, no automatic note, explicit note creation, and completion feedback. Tasks 4–6 cover Windows right-click selection, shared commands, keyboard invocation, recovery-safe Archive/Trash, Attach, and style actions. Task 7 covers horizontal-overflow removal. Task 8 covers the installed evidence required by Milestone A.
- **Placeholder scan:** The plan contains no unresolved markers, deferred implementation instructions, or unnamed error-handling work.
- **Type consistency:** `CaptureModel`, `PendingScreenCapture`, `CaptureNoteFactory`, `INoteCardCommandStore`, `NoteCardCommandService`, and their method signatures are consistent between producer and consumer tasks. Context factory argument order matches `ContextRef.Create(kind, provider, stableIdentity, displayName)`.
