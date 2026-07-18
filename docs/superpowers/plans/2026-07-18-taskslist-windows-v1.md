# Task'sList Windows V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, package, and install a native Windows Task'sList application centered on sticky notes, unlimited clipboard history, contextual places, Markdown, themes, screen capture, and three capability-demonstrating plugins.

**Architecture:** A .NET 8 WPF host owns windows, storage, clipboard ingestion, context matching, permissions, and plugin supervision. A focused domain library keeps notes, captures, places, assignments, themes, and plugin contracts independently testable; SQLite stores metadata while content-addressed files store large payloads. Plugins are separate executables communicating through newline-delimited JSON over named pipes, with manifests and explicit grants validated by the host.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, Microsoft.Data.Sqlite, Markdig, Windows Graphics Capture/GDI interop, PowerShell installer.

## Global Constraints

- Platform: Windows 10 and Windows 11, x64 release artifact.
- Product name shown to users: `Task'sList`; code-safe identifier: `TasksList`.
- Core notes and clipboard history work with every plugin and AI provider disabled.
- Clipboard history has no fixed item-count limit; age and byte quotas are user-configurable.
- Markdown remains portable and proprietary metadata remains outside exported Markdown.
- Notes and captures can be assigned to multiple Places without changing source provenance.
- Plugin permissions are deny-by-default; plugins never write the database directly.
- Theme packages are declarative and cannot execute code.
- No cloud transmission occurs without a separately granted network/AI capability.

## File structure

```text
TasksList.sln
Directory.Build.props
src/
  TasksList.Core/                 domain records, services, validation, storage contracts
  TasksList.Infrastructure/       SQLite, payloads, clipboard/context/capture interop
  TasksList.PluginSdk/            manifests, capabilities, messages, plugin base host
  TasksList.App/                  WPF host, sticky windows, palette, manager, themes
plugins/
  TasksList.Plugin.BrowserContext/
  TasksList.Plugin.DeveloperWorkspace/
  TasksList.Plugin.CaptureWorkflows/
browser-extension/                Chrome/Edge MV3 companion for tab identity and sessions
themes/default/                   built-in declarative theme
tests/
  TasksList.Core.Tests/
  TasksList.Infrastructure.Tests/
  TasksList.PluginSdk.Tests/
  TasksList.App.Tests/
installer/install.ps1
installer/uninstall.ps1
scripts/build-release.ps1
```

---

### Task 1: Toolchain, solution skeleton, and domain records

**Files:**
- Create: `Directory.Build.props`
- Create: `TasksList.sln`
- Create: `src/TasksList.Core/TasksList.Core.csproj`
- Create: `src/TasksList.Core/Models/*.cs`
- Create: `tests/TasksList.Core.Tests/TasksList.Core.Tests.csproj`
- Create: `tests/TasksList.Core.Tests/Models/AssignmentTests.cs`

**Interfaces:**
- Produces: `Note`, `Capture`, `ContextRef`, `Place`, `Assignment`, `SavedTab`, `Attachment`, `ActivityEntry`, and their strongly typed IDs.

- [ ] Install the .NET 8 SDK non-interactively and confirm `dotnet --version` returns an 8.x SDK.
- [ ] Create solution and projects targeting `net8.0` / `net8.0-windows` with nullable and implicit usings enabled.
- [ ] Write `AssignmentTests` proving a capture retains its immutable `SourceContextId` while receiving multiple Place assignments.
- [ ] Run the focused test and confirm it fails because records/services are missing.
- [ ] Implement immutable provenance and many-to-many assignment domain records.
- [ ] Run `dotnet test tests/TasksList.Core.Tests/TasksList.Core.Tests.csproj` and confirm zero failures.
- [ ] Commit with `feat: add Task'sList domain model`.

### Task 2: SQLite repository and content-addressed payload storage

**Files:**
- Create: `src/TasksList.Infrastructure/TasksList.Infrastructure.csproj`
- Create: `src/TasksList.Infrastructure/Storage/TasksListDatabase.cs`
- Create: `src/TasksList.Infrastructure/Storage/PayloadStore.cs`
- Create: `src/TasksList.Infrastructure/Storage/Migrations/001_initial.sql`
- Create: `tests/TasksList.Infrastructure.Tests/Storage/RepositoryTests.cs`
- Create: `tests/TasksList.Infrastructure.Tests/Storage/PayloadStoreTests.cs`

**Interfaces:**
- Consumes: domain records from Task 1.
- Produces: `ITasksRepository`, `TasksListDatabase.InitializeAsync`, `PayloadStore.PutAsync`, and full-text `SearchCapturesAsync`.

- [ ] Write tests for note round-trip, immutable capture provenance, multiple assignments, FTS search, payload deduplication, and reference-count cleanup.
- [ ] Run storage tests and verify expected missing-type failures.
- [ ] Implement the migration, parameterized repositories, WAL mode, transactional writes, FTS5 index, and SHA-256 payload paths.
- [ ] Run storage tests and the complete solution test suite.
- [ ] Commit with `feat: persist notes captures and places`.

### Task 3: Sticky windows, lifecycle, and application attachment

**Files:**
- Create: `src/TasksList.App/TasksList.App.csproj`
- Create: `src/TasksList.App/App.xaml`
- Create: `src/TasksList.App/Sticky/StickyWindow.xaml`
- Create: `src/TasksList.App/Sticky/StickyWindowViewModel.cs`
- Create: `src/TasksList.App/Sticky/StickyWindowPlacement.cs`
- Create: `src/TasksList.Infrastructure/Windows/ForegroundContextService.cs`
- Create: `tests/TasksList.App.Tests/Sticky/StickyWindowPlacementTests.cs`
- Create: `tests/TasksList.Core.Tests/Contexts/ContextMatcherTests.cs`

**Interfaces:**
- Produces: `IContextObserver`, `ContextMatcher.Match`, `StickyWindowPlacement.ClampToMonitors`, and note visibility modes.

- [ ] Write failing tests for monitor clamping, relative placement restoration, exact window matching, executable fallback matching, and foreground-only visibility.
- [ ] Implement context identity and window-placement logic until tests pass.
- [ ] Build the WPF note window with drag, resize, roll-up, Topmost, lock, click-through, opacity, color, archive, delete, sleep, and recurrence controls.
- [ ] Add tray lifecycle, single-instance handling, and global new-note/palette shortcuts.
- [ ] Add a deterministic UI smoke mode that opens test notes, reports window flags to JSON, and exits.
- [ ] Run unit tests, smoke mode, and release build.
- [ ] Commit with `feat: add contextual sticky windows`.

### Task 4: Markdown-rich editor, file drops, and themes

**Files:**
- Create: `src/TasksList.Core/Markdown/MarkdownDocumentService.cs`
- Create: `src/TasksList.Core/Markdown/MarkdownFileLink.cs`
- Create: `src/TasksList.App/Editor/MarkdownEditor.xaml`
- Create: `src/TasksList.App/Theming/ThemeLoader.cs`
- Create: `themes/default/theme.json`
- Create: `tests/TasksList.Core.Tests/Markdown/MarkdownRoundTripTests.cs`
- Create: `tests/TasksList.App.Tests/Theming/ThemeLoaderTests.cs`

**Interfaces:**
- Produces: `MarkdownDocumentService.Parse/Render/ToggleTask`, atomic linked-file writes, `ThemeDefinition`, and safe fallback loading.

- [ ] Write failing tests for `#` headings, nested lists, GFM tables, fenced language blocks, checkbox toggling, front-matter preservation, link conflicts, and invalid-theme fallback.
- [ ] Implement Markdown parsing/round-trip operations and theme validation until tests pass.
- [ ] Build the split rich-preview/source editor, formatting toolbar, slash commands, drop menu, import, link-and-sync, split-by-heading, and conflict prompt.
- [ ] Apply default tokens across sticky windows, palette, and manager.
- [ ] Run Markdown/theme tests and release build.
- [ ] Commit with `feat: add Markdown editing and file themes`.

### Task 5: Unlimited clipboard history and filing

**Files:**
- Create: `src/TasksList.Infrastructure/Clipboard/ClipboardListener.cs`
- Create: `src/TasksList.Infrastructure/Clipboard/ClipboardIngestor.cs`
- Create: `src/TasksList.Core/Clipboard/ClipboardRetentionPolicy.cs`
- Create: `src/TasksList.App/Palette/CapturePalette.xaml`
- Create: `src/TasksList.App/Clipboard/ClipboardView.xaml`
- Create: `tests/TasksList.Core.Tests/Clipboard/ClipboardRetentionTests.cs`
- Create: `tests/TasksList.Infrastructure.Tests/Clipboard/ClipboardIngestorTests.cs`

**Interfaces:**
- Produces: asynchronous multi-format ingestion, quota pruning, source metadata, original/plain/Markdown paste modes, comparison, and Place filing commands.

- [ ] Write failing tests proving no fixed count limit, age/byte quota behavior, format preservation, source provenance, deduplication, exclusion rules, and filing without provenance mutation.
- [ ] Implement retention and ingestion services using injected clipboard snapshots so tests do not own the Windows clipboard.
- [ ] Register the native clipboard listener and ensure callbacks queue work without blocking the source process.
- [ ] Build searchable history with Unfiled, By source, By place, Favorites, saved filters, multi-select filing, compare, edit-copy, and create-note actions.
- [ ] Implement original/plain/Markdown paste and explicit emulated typing with safety confirmation.
- [ ] Run clipboard tests, full suite, and a two-process clipboard smoke script.
- [ ] Commit with `feat: add contextual clipboard history`.

### Task 6: Places tree and browser session model

**Files:**
- Create: `src/TasksList.Core/Places/PlaceService.cs`
- Create: `src/TasksList.Core/Places/BrowserSessionService.cs`
- Create: `src/TasksList.App/Places/PlacesTree.xaml`
- Create: `tests/TasksList.Core.Tests/Places/PlaceServiceTests.cs`
- Create: `tests/TasksList.Core.Tests/Places/BrowserSessionServiceTests.cs`

**Interfaces:**
- Produces: hierarchical Places, manual children, live/open groups, saved sessions, stable ordering, and non-destructive restore plans.

- [ ] Write failing tests for cycle prevention, manual subgroups under detected places, every-open-tab mirroring, save-one/save-window/save-all, duplicate URL preservation, large-session warning, and restore without closing unrelated tabs.
- [ ] Implement Place and browser-session services until tests pass.
- [ ] Build the tree UI, drag filing, assignment chips, source display, saved-session creation, and restore confirmation.
- [ ] Run focused and full tests.
- [ ] Commit with `feat: organize captures into contextual places`.

### Task 7: Screen capture, annotation, OCR, and pinning

**Files:**
- Create: `src/TasksList.Infrastructure/Capture/ScreenCaptureService.cs`
- Create: `src/TasksList.App/Capture/CaptureOverlay.xaml`
- Create: `src/TasksList.App/Capture/AnnotationWindow.xaml`
- Create: `src/TasksList.Core/Capture/CaptureRouter.cs`
- Create: `tests/TasksList.Core.Tests/Capture/CaptureRouterTests.cs`

**Interfaces:**
- Produces: region/window/monitor capture, non-destructive annotation operations, OCR provider abstraction, and capture routing.

- [ ] Write failing tests for routing to clipboard/file/note/place, redaction-before-export ordering, and provenance construction.
- [ ] Implement capture routing and Windows image acquisition.
- [ ] Build overlay and annotation tools for crop, arrow, rectangle, text, blur/pixelation, numbering, undo, and redact.
- [ ] Add Windows OCR adapter and pin-as-image-note action.
- [ ] Run capture tests, manual smoke capture in test mode, and release build.
- [ ] Commit with `feat: capture and route screen evidence`.

### Task 8: Plugin SDK, isolation, permissions, and packages

**Files:**
- Create: `src/TasksList.PluginSdk/TasksList.PluginSdk.csproj`
- Create: `src/TasksList.PluginSdk/PluginManifest.cs`
- Create: `src/TasksList.PluginSdk/PluginCapability.cs`
- Create: `src/TasksList.PluginSdk/Rpc/*.cs`
- Create: `src/TasksList.Infrastructure/Plugins/PluginSupervisor.cs`
- Create: `src/TasksList.App/Plugins/PluginManagerView.xaml`
- Create: `tests/TasksList.PluginSdk.Tests/*.cs`

**Interfaces:**
- Produces: versioned manifests, `.taskplugin` validation, named-pipe RPC, typed proposed operations, permission grants, health/restart state, and install/disable/update/uninstall.

- [ ] Write failing tests for manifest compatibility, path traversal rejection, unsigned local-package warning, denied capability calls, operation validation, crash isolation, and bounded restart.
- [ ] Implement SDK/package validation and RPC protocol until contract tests pass.
- [ ] Implement external-process supervision and host-side operation application.
- [ ] Build plugin manager permissions, health, and activity views.
- [ ] Run plugin tests and kill-process fault smoke test.
- [ ] Commit with `feat: add isolated plugin ecosystem`.

### Task 9: Three showcase plugins and browser companion

**Files:**
- Create: `plugins/TasksList.Plugin.BrowserContext/*`
- Create: `plugins/TasksList.Plugin.DeveloperWorkspace/*`
- Create: `plugins/TasksList.Plugin.CaptureWorkflows/*`
- Create: `browser-extension/manifest.json`
- Create: `browser-extension/background.js`
- Create: `browser-extension/options.html`
- Create: `tests/TasksList.PluginSdk.Tests/ShowcasePluginContractTests.cs`

**Interfaces:**
- Browser Context provides browser/window/tab/conversation Places and saved-session actions.
- Developer Workspace provides repository/branch/terminal/Claude Code context and Markdown handoff commands.
- Capture Workflows provides typed declarative pipelines and four bundled workflows.

- [ ] Write failing contract tests for each manifest, required capability boundary, context output, workflow type safety, and absent-cloud behavior.
- [ ] Implement Browser Context host and MV3 native-messaging companion with identity-only default access and private-window exclusion.
- [ ] Implement Developer Workspace read-only facts, terminal formatting, Markdown task conversion, and confirmed command-copy blocks.
- [ ] Implement Capture Workflows nodes and bundled Bug evidence, Research card, Markdown task board, and Sensitive capture workflows.
- [ ] Package all three `.taskplugin` files and register them as bundled, disabled-until-consented plugins.
- [ ] Run contract tests and end-to-end plugin smoke mode.
- [ ] Commit with `feat: ship showcase plugins`.

### Task 10: Accessibility, backup, packaging, installation, and completion audit

**Files:**
- Create: `scripts/build-release.ps1`
- Create: `installer/install.ps1`
- Create: `installer/uninstall.ps1`
- Create: `tests/smoke/installed-app.ps1`
- Create: `README.md`
- Create: `CHANGELOG.md`

**Interfaces:**
- Produces: self-contained `win-x64` distribution, per-user installation, Start Menu shortcut, optional startup shortcut, uninstall entry/script, and installed smoke verification.

- [ ] Add accessible names, focus order, keyboard equivalents, high-contrast behavior, reduced motion, DPI handling, and multi-monitor recovery tests.
- [ ] Add automatic database backup/recovery, safe mode, and portable mode.
- [ ] Write the release script to run tests, publish self-contained artifacts, include themes/plugins/extension, and calculate checksums.
- [ ] Write the installer to copy into `%LOCALAPPDATA%\Programs\TasksList`, create shortcuts, register browser native messaging when approved, and preserve user data on upgrade.
- [ ] Run `dotnet test TasksList.sln -c Release` and require zero failures.
- [ ] Run the release build and inspect the artifact manifest and checksums.
- [ ] Install Task'sList for the current user, launch it, create a note, exercise clipboard capture, and run `tests/smoke/installed-app.ps1`.
- [ ] Audit every acceptance criterion in the design against direct test, runtime, package, or installed-state evidence.
- [ ] Commit with `release: build installable Task'sList v1`.

