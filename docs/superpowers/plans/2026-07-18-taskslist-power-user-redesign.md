# Task'sList Power-User Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the complete 36-improvement sticky-first redesign, including durable customization, direct title dragging, tray/hotkeys, reminders, a keyboard-first clipboard palette, interactive blocks, and installed-app verification.

**Architecture:** Extend the immutable core domain with validated presentation/lifecycle records, persist them through an additive SQLite migration, and keep WPF windows thin by moving behavior into pure policies and focused services. The existing local-first database, browser bridge, context attachment, capture, Markdown, and plugin systems remain in place.

**Tech Stack:** .NET 8, C# 12, WPF, Win32 interop, Microsoft.Data.Sqlite, xUnit, self-contained win-x64 publishing.

## Global Constraints

- Windows 10/11 x64; no cloud account required.
- Existing databases migrate without losing note, capture, assignment, context, Place, or browser-session data.
- Normal opacity controls clamp to 20–100%.
- Ghost mode always has tray/global-hotkey recovery.
- Private/ignored clipboard data is never captured.
- All file writes are atomic where a partial write could break startup.
- Every new behavior follows red-green-refactor and receives a focused automated test.
- No decorative application, tab, or plugin data is shown.

---

### Task 1: Durable note presentation and lifecycle

**Files:**
- Create: `src/TasksList.Core/Notes/NotePresentation.cs`
- Create: `src/TasksList.Core/Notes/NoteAppearancePolicy.cs`
- Create: `src/TasksList.Infrastructure/Storage/Migrations/002_power_user.sql`
- Modify: `src/TasksList.Core/Models/Note.cs`
- Modify: `src/TasksList.Infrastructure/Storage/TasksListDatabase.cs`
- Test: `tests/TasksList.Core.Tests/Notes/NoteAppearancePolicyTests.cs`
- Test: `tests/TasksList.Infrastructure.Tests/Storage/NotePresentationDatabaseTests.cs`

**Interfaces:**
- Produces `NotePresentation Default(NoteId noteId)` and immutable `With*` methods.
- Produces `double NoteAppearancePolicy.ClampOpacity(double value)` and `AdjustOpacity(double current, int wheelDelta)`.
- Produces `SaveNotePresentationAsync`, `GetNotePresentationAsync`, `SaveNamedStyleAsync`, and `ListNamedStylesAsync` on `TasksListDatabase`.

- [ ] **Step 1: Write failing core tests** proving opacity clamps to `.20`/`1.00`, wheel increments are `.05`, every paper preset has valid colors, lock/roll/topmost transitions are immutable, and soft-delete/restore transitions retain content.
- [ ] **Step 2: Run the focused tests** with `dotnet test tests/TasksList.Core.Tests -c Release --filter FullyQualifiedName~NoteAppearancePolicyTests`; expect failures because the types do not exist.
- [ ] **Step 3: Implement the core records** with enums `PaperPreset`, `ToolbarVisibility`, `NoteDensity`, `CornerStyle`, `ReminderAttention`, and values for bounds, appearance, visibility, lifecycle, and timestamps. Use `NotePresentation.Default(noteId)` for backward compatibility.
- [ ] **Step 4: Run the core tests** and expect all focused tests to pass.
- [ ] **Step 5: Write failing database round-trip tests** that initialize an old schema, save every presentation field, reopen the database, and compare the restored record; also test corrupt optional fields fall back without hiding note content.
- [ ] **Step 6: Add migration 002 and storage APIs.** `InitializeAsync` must execute both embedded migrations idempotently. Create `note_presentations`, `named_styles`, and `note_reminders` tables keyed by note ID/style ID; add created, modified, hidden, and deleted timestamps to `notes` without rewriting note bodies.
- [ ] **Step 7: Run infrastructure and full tests** and expect no regressions.
- [ ] **Step 8: Commit** with `feat: persist sticky presentation and lifecycle`.

### Task 2: Correct sticky interaction, customization, and visual design

**Files:**
- Create: `src/TasksList.App/Sticky/StickyInteractionPolicy.cs`
- Create: `src/TasksList.App/Sticky/StickyWindowController.cs`
- Create: `src/TasksList.App/Sticky/StickySnapService.cs`
- Create: `src/TasksList.App/Sticky/CustomizeFlyout.xaml`
- Create: `src/TasksList.App/Sticky/CustomizeFlyout.xaml.cs`
- Create: `src/TasksList.App/Sticky/OpacityHud.xaml`
- Create: `src/TasksList.App/Sticky/OpacityHud.xaml.cs`
- Modify: `src/TasksList.App/Sticky/StickyWindow.xaml`
- Modify: `src/TasksList.App/Sticky/StickyWindow.xaml.cs`
- Modify: `src/TasksList.App/MainWindow.xaml.cs`
- Test: `tests/TasksList.App.Tests/Sticky/StickyInteractionPolicyTests.cs`
- Test: `tests/TasksList.App.Tests/Sticky/StickySnapServiceTests.cs`

**Interfaces:**
- `StickyInteractionPolicy.HeaderAction(bool titleEditing, bool overInteractiveControl, int clickCount)` returns Drag, BeginTitleEdit, ToggleRoll, or None.
- `StickySnapService.Snap(WindowBounds moving, IReadOnlyList<WindowBounds> others, WindowBounds workArea, double tolerance, bool bypass)` returns snapped bounds.
- `StickyWindowController` accepts the WPF view, database, note/presentation, context provider, and sibling-bounds provider.

- [ ] **Step 1: Write failing interaction tests** proving displayed-title clicks drag, double-click begins title edit, temporary title editor never drags, toolbar buttons do not drag, F2 begins edit, Enter commits, and Escape cancels.
- [ ] **Step 2: Run focused interaction tests** and verify the missing types fail the build.
- [ ] **Step 3: Implement interaction policy and controller.** Replace the permanent header `TextBox` with a display `TextBlock` plus temporary editor. Forward non-control header pointer input to `DragMove`; persist location/size through a 250 ms debounce and flush on close.
- [ ] **Step 4: Write and fail snapping tests** for all four work-area edges, neighboring note edges, tolerance limits, Alt bypass, and multi-monitor clamping.
- [ ] **Step 5: Implement snapping and restore.** Preserve relative placement, use device-independent pixels, and never rescue a note until monitor enumeration proves it is fully unavailable.
- [ ] **Step 6: Build the customize flyout** with eight swatches, live custom colors, 20–100 opacity slider and numeric value, font family/size/weight, line spacing, density, toolbar behavior, shadow, border, corners, texture, named-style save/apply/default, and reset.
- [ ] **Step 7: Add direct controls** for Ctrl+wheel and Ctrl+Plus/Minus opacity, the transient HUD, pin/roll/lock persistence, duplicate, archive, Trash-safe delete, Ctrl+N, Ctrl+Shift+N, Ctrl+D, Ctrl+M, Ctrl+L, and Ctrl+Shift+T.
- [ ] **Step 8: Apply the precision-stationery visuals** using coordinated hover chrome, vector icons, paper gradients/noise, clear state badges, accessible focus, Graphite/Glass variants, and no mojibake.
- [ ] **Step 9: Run app/core/infrastructure and full tests.**
- [ ] **Step 10: Commit** with `feat: make stickies directly customizable and movable`.

### Task 3: Tray lifecycle, hotkeys, ghost recovery, sleep, and reminders

**Files:**
- Create: `src/TasksList.App/Shell/TrayService.cs`
- Create: `src/TasksList.App/Shell/GlobalHotkeyService.cs`
- Create: `src/TasksList.App/Shell/AppSettings.cs`
- Create: `src/TasksList.App/Shell/AppSettingsStore.cs`
- Create: `src/TasksList.App/Sticky/GhostModeService.cs`
- Create: `src/TasksList.Core/Notes/NoteLifecycleService.cs`
- Create: `src/TasksList.App/Settings/SettingsWindow.xaml`
- Create: `src/TasksList.App/Settings/SettingsWindow.xaml.cs`
- Modify: `src/TasksList.App/App.xaml.cs`
- Modify: `src/TasksList.App/TasksList.App.csproj`
- Test: `tests/TasksList.Core.Tests/Notes/NoteLifecycleServiceTests.cs`
- Test: `tests/TasksList.App.Tests/Shell/AppSettingsStoreTests.cs`
- Test: `tests/TasksList.App.Tests/Shell/GlobalHotkeyBindingTests.cs`

**Interfaces:**
- `NoteLifecycleService.Evaluate(NotePresentation state, DateTimeOffset now)` returns hidden, wake, or reminder actions using an injected clock.
- `GlobalHotkeyService.Register(AppHotkey binding, Action callback)` returns a conflict result with a user-facing message.
- `AppSettingsStore.Load/Save` validates and atomically replaces `%LOCALAPPDATA%\TasksList\settings.json`.

- [ ] **Step 1: Write failing lifecycle tests** for all sleep presets, next-workday calculation, reminder acknowledgement, pulse/topmost attention, archive, restore, Trash retention, and permanent deletion eligibility at 30 days.
- [ ] **Step 2: Implement lifecycle service** and add a dispatcher-driven scheduler that queries due notes without busy waiting.
- [ ] **Step 3: Write failing settings/hotkey tests** for defaults, corrupt JSON fallback, atomic save, duplicate/conflicting bindings, and the invariant that ghost recovery cannot be unbound.
- [ ] **Step 4: Implement settings and Win32 hotkeys** for New Sticky, New from Clipboard, Clipboard Palette, Capture Region, Show/Hide All, Library, and Disable Ghost Mode.
- [ ] **Step 5: Implement the notification-area menu** with the exact commands in the spec. Closing the library hides it; Exit terminates after flushing all note state.
- [ ] **Step 6: Implement ghost mode** by toggling `WS_EX_TRANSPARENT` and `WS_EX_LAYERED`, show a first-use confirmation, preserve keyboard recovery, and provide Disable Ghost Mode globally and from the tray.
- [ ] **Step 7: Add sleep/reminder UI** to note menus with presets/custom date, visible wake state, optional sound, border pulse, and acknowledgement.
- [ ] **Step 8: Run full tests and commit** with `feat: add tray hotkeys reminders and ghost recovery`.

### Task 4: Clipboard palette and ClipAngel-grade workflows

**Files:**
- Create: `src/TasksList.Core/Clipboard/ClipboardQuery.cs`
- Create: `src/TasksList.Core/Clipboard/ClipboardDuplicatePolicy.cs`
- Create: `src/TasksList.App/Clipboard/ClipboardPaletteWindow.xaml`
- Create: `src/TasksList.App/Clipboard/ClipboardPaletteWindow.xaml.cs`
- Create: `src/TasksList.App/Clipboard/ClipboardPasteService.cs`
- Create: `src/TasksList.App/Clipboard/ClipboardCapturePolicy.cs`
- Modify: `src/TasksList.Core/Models/Capture.cs`
- Modify: `src/TasksList.Infrastructure/Storage/Migrations/002_power_user.sql`
- Modify: `src/TasksList.Infrastructure/Storage/TasksListDatabase.cs`
- Modify: `src/TasksList.App/Clipboard/ClipboardMonitor.cs`
- Test: `tests/TasksList.Core.Tests/Clipboard/ClipboardQueryTests.cs`
- Test: `tests/TasksList.Core.Tests/Clipboard/ClipboardDuplicatePolicyTests.cs`
- Test: `tests/TasksList.App.Tests/Clipboard/ClipboardCapturePolicyTests.cs`
- Test: `tests/TasksList.App.Tests/Clipboard/ClipboardPasteServiceTests.cs`

**Interfaces:**
- `ClipboardQuery` contains text, favorite/used/unfiled flags, sources, formats, and date bounds.
- `SearchCapturesAsync(ClipboardQuery query, int limit)` returns provenance-rich results in stable order.
- `ClipboardPasteService.PasteAsync(Capture capture, PasteRepresentation representation, ContextRef target)` temporarily suppresses monitor capture, restores focus, sets clipboard representations, and sends paste.

- [ ] **Step 1: Write failing domain/query tests** for favorites, used marks, unfiled, source/type/date filters, stable ordering, duplicate promotion, and formatting-aware duplicate detection.
- [ ] **Step 2: Extend capture persistence** with favorite, used, title, deleted, size, URL, and duplicate hash fields; add query/update APIs and retain old captures.
- [ ] **Step 3: Write failing capture-policy tests** for monitor pause, excluded applications, private/ignored formats, self-paste suppression, maximum size, and formatted representation selection.
- [ ] **Step 4: Implement the palette** as a compact keyboard-first window near the pointer with immediate search, Up/Down selection while search stays focused, type/source/date chips, favorite/used/unfiled filters, multiselect, preview, provenance, and clear empty states.
- [ ] **Step 5: Implement actions** for original/plain paste, copy without paste, edit, rename, duplicate, favorite, delete/restore, make note, joined paste/note, drag-out, and bulk assignment to Places/notes.
- [ ] **Step 6: Implement focus-safe paste** and prove the operation does not create a duplicate capture.
- [ ] **Step 7: Wire Ctrl+Shift+V and tray command**, remember palette size, and position the palette beside the pointer while clamping it to the active monitor work area.
- [ ] **Step 8: Run full tests and commit** with `feat: add power user clipboard palette`.

### Task 5: Interactive blocks, real-data library, and accessibility

**Files:**
- Create: `src/TasksList.Core/Markdown/InteractiveBlockService.cs`
- Create: `src/TasksList.App/Editor/InteractiveBlockControls.cs`
- Create: `src/TasksList.App/Library/LibraryWindow.xaml`
- Create: `src/TasksList.App/Library/LibraryWindow.xaml.cs`
- Modify: `src/TasksList.App/Editor/MarkdownFlowDocumentBuilder.cs`
- Modify: `src/TasksList.App/MainWindow.xaml`
- Modify: `src/TasksList.App/MainWindow.xaml.cs`
- Modify: `src/TasksList.App/App.xaml`
- Test: `tests/TasksList.Core.Tests/Markdown/InteractiveBlockServiceTests.cs`
- Test: `tests/TasksList.App.Tests/Library/LibraryDataTests.cs`

**Interfaces:**
- `InteractiveBlockService.Parse/SetProgress/SetCounter/SetTimerDuration` preserves unrelated Markdown byte-for-byte.
- `LibraryDataBuilder.Build(notes, captures, contexts, tabs, plugins)` returns real-data tab models and empty states only.

- [ ] **Step 1: Write failing interactive-block tests** for progress, counter, timer, malformed directives, bounds, escaping, and preservation of unrelated Markdown.
- [ ] **Step 2: Implement parser and preview controls** with progress adjustment, counter buttons, timer start/pause/reset, database-backed running state, and no arbitrary code/network execution.
- [ ] **Step 3: Write failing library-data tests** proving no hard-coded Edge/Docker/developer rows exist and real detected contexts/tabs populate their respective tabs.
- [ ] **Step 4: Replace the three-column dashboard** with Notes, Clipboard, Contexts, and Extensions tabs, searchable views, multi-select style application, Trash, and explanatory empty states.
- [ ] **Step 5: Finish visual/accessibility cleanup**: semantic icons, correct Unicode, keyboard traversal, automation names, high-contrast tokens, per-monitor DPI, reduced motion, minimum hit areas, and state cues beyond color.
- [ ] **Step 6: Run full tests and commit** with `feat: add interactive notes and real data library`.

### Task 6: Packaging, installed interaction verification, and completion audit

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `scripts/build-release.ps1`
- Modify: `installer/install.ps1`
- Modify: `installer/uninstall.ps1`
- Create: `tests/TasksList.InstalledProbe/TasksList.InstalledProbe.csproj`
- Create: `tests/TasksList.InstalledProbe/Program.cs`

**Interfaces:**
- Installed probe reads the installed SQLite database and settings after a UI-driven smoke run and validates bounds, style, opacity, pin, roll, favorite clip, note provenance, and plugin count.

- [ ] **Step 1: Run `dotnet test TasksList.sln -c Release`** and require every test to pass with no test-host crash.
- [ ] **Step 2: Build the self-contained release** and verify checksums, all three `.taskplugin` packages, theme/style assets, browser companion, native host, and installer scripts.
- [ ] **Step 3: Install the exact release artifact** over the current per-user installation and verify executable hash, shortcut, tray startup, native-host registry, plugins, and browser extension files.
- [ ] **Step 4: Exercise the installed sticky**: create, title-drag, resize, set 65% opacity, apply Graphite, pin, roll, restart, and verify the visual state plus database values.
- [ ] **Step 5: Exercise the installed clipboard palette**: invoke Ctrl+Shift+V, filter a test clip, favorite it, create a note, and verify source provenance and no self-paste duplicate.
- [ ] **Step 6: Exercise ghost recovery, sleep/wake, and reminder acknowledgement.** Confirm tray recovery always restores hit testing.
- [ ] **Step 7: Capture installed screenshots** at 100% and high DPI; inspect for mojibake, fake rows, clipping, inaccessible contrast, missing hover/focus states, and inconsistent icons.
- [ ] **Step 8: Audit all 36 numbered requirements** against code, automated tests, installed database, screenshots, and runtime behavior. Continue implementation for every missing or indirect item.
- [ ] **Step 9: Commit release updates** with `release: ship Task'sList power user redesign`, launch the installed app, and only then mark the active goal complete.
