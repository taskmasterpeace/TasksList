# Windows 11 Milestone B: Native Window Foundation Implementation Plan

> Execute task-by-task with TDD in an isolated worktree. Each behavior change starts with a failing test, and the installed artifact is the completion gate.

**Goal:** Give Task'sList real Windows non-client behavior: system-owned main caption and caption buttons, DWM theme/backdrop/corners, native sticky dragging/resizing with reliable interactive exclusions, and standard Windows cursors.

**Architecture:** Keep WPF and use Windows' own windowing layers. The main library uses the standard WPF top-level frame. `DwmWindowService` applies optional Windows 11 DWM attributes through a testable platform boundary. Sticky notes remain borderless product surfaces but use `System.Windows.Shell.WindowChrome` for native caption and resize hit testing; interactive header controls opt back into client input.

**Tech Stack:** .NET 8, WPF, WindowChrome, DWM (`dwmapi.dll`), xUnit, Windows UI Automation.

---

## Task 1: Contract for a real main-window frame

**Files:**

- Create `tests/TasksList.App.Tests/Shell/MainWindowNativeChromeContractTests.cs`
- Modify `src/TasksList.App/MainWindow.xaml`
- Modify `src/TasksList.App/MainWindow.xaml.cs`

1. Add a failing XAML/source contract asserting:
   - `WindowStyle="SingleBorderWindow"`
   - `AllowsTransparency="False"`
   - `ResizeMode="CanResize"`
   - no `TitleBarMouseDown`, `MinimizeClick`, `MaximizeClick`, or `CloseClick`
   - no custom caption buttons named Minimize, Maximize, or Close
   - no caption `Cursor="Hand"`.
2. Run the focused test and verify RED.
3. Change the main window to standard chrome, retain the 64-pixel product command/header row as client content, move the status badge to the right of that row, remove the custom caption button stack and drag handler, and flatten the fake outer border/shadow.
4. Remove obsolete caption methods from `MainWindow.xaml.cs`.
5. Run the focused test and all App tests; verify GREEN.
6. Commit: `fix: restore native main window chrome`.

## Task 2: Testable DWM option policy

**Files:**

- Create `src/TasksList.App/Shell/DwmWindowOptions.cs`
- Create `tests/TasksList.App.Tests/Shell/DwmWindowOptionsTests.cs`

1. Add failing tests for these outcomes:
   - Windows 11 + transparency enabled + non-high-contrast => main-window backdrop and round corners.
   - high contrast, transparency disabled, remote session, or unsupported Windows => no backdrop.
   - caption dark mode follows the app/system dark-mode snapshot independently of backdrop support.
   - sticky options request round corners but never a main-window backdrop.
2. Implement immutable `DwmEnvironment`, `DwmWindowKind`, and `DwmWindowOptions.Resolve` policy types.
3. Run focused tests and commit: `test: define DWM window policy`.

## Task 3: DWM service and main-window wiring

**Files:**

- Create `src/TasksList.App/Shell/DwmWindowService.cs`
- Create `tests/TasksList.App.Tests/Shell/DwmWindowServiceTests.cs`
- Modify `src/TasksList.App/MainWindow.xaml.cs`

1. Add a fake-platform test proving the service requests:
   - `DWMWA_USE_IMMERSIVE_DARK_MODE` (20),
   - `DWMWA_WINDOW_CORNER_PREFERENCE` (33),
   - `DWMWA_SYSTEMBACKDROP_TYPE` (38),
   while treating unsupported attributes as optional rather than crashing the window.
2. Define `IDwmPlatform` and a Windows implementation around `DwmSetWindowAttribute`.
3. Add a `WindowsEnvironmentReader` for OS support, high contrast, remote session, transparency preference, and AppsUseLightTheme.
4. Apply the resolved main-window options from `SourceInitialized`; reapply caption dark mode on system preference changes and detach handlers when the window closes.
5. Run focused tests and all App tests; commit: `feat: apply Windows 11 DWM window attributes`.

## Task 4: Native sticky caption and resize policy

**Files:**

- Create `tests/TasksList.App.Tests/Sticky/StickyNativeChromeContractTests.cs`
- Modify `src/TasksList.App/Sticky/StickyWindow.xaml`
- Modify `src/TasksList.App/Sticky/StickyWindow.xaml.cs`

1. Add a failing contract asserting:
   - sticky `AllowsTransparency="False"`,
   - a `WindowChrome` with a caption height and native resize border,
   - header has no `Cursor="SizeAll"` and no `MouseLeftButtonDown="TitleBarMouseDown"`,
   - title editor and right toolbar are marked `WindowChrome.IsHitTestVisibleInChrome="True"`,
   - toolbar remains right-aligned and hidden states remain non-hit-testable.
2. Add `WindowChrome` with `CaptionHeight=48`, `ResizeBorderThickness=6`, no system caption buttons, and native resize behavior.
3. Mark only interactive header descendants as client hit-test regions.
4. Remove WPF `DragMove`, set the window background to the active paper brush, remove the fake outer drop shadow, and retain lock/roll behavior by switching `ResizeMode`.
5. Apply sticky DWM round-corner/dark-mode options when its HWND is initialized.
6. Run sticky and App tests; commit: `fix: use native sticky drag and resize chrome`.

## Task 5: Windows cursor audit

**Files:**

- Create `tests/TasksList.App.Tests/Shell/WindowsCursorContractTests.cs`
- Modify `src/TasksList.App/App.xaml`
- Modify `src/TasksList.App/Sticky/StickyWindow.xaml`
- Modify custom window XAML only where the test identifies inappropriate hand/move cursors.

1. Add a failing source/XAML contract that rejects `Cursor="Hand"` on Button styles and ordinary command buttons, and rejects `Cursor="SizeAll"` on caption regions. Permit Cross only on capture selection and resize cursors only on resize surfaces.
2. Remove pointer cursors from `GhostButton`, `StickyIconButton`, Attach, Preview, and other ordinary commands. Hyperlinks may retain the hand cursor.
3. Verify hover/pressed/focus visuals remain discoverable without cursor misuse.
4. Run all App tests; commit: `fix: use standard Windows command cursors`.

## Task 6: Release and installed Windows verification

1. Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-release.ps1`.
2. Install `artifacts\release\install.ps1` and verify installed/source SHA-256 equality.
3. Verify the installed main window:
   - icon and title in the real caption,
   - caption drag and double-click maximize/restore,
   - Alt+Space system menu,
   - native minimize/maximize/close,
   - resize borders and standard resize cursors,
   - Win+Arrow and Win+Z/Snap Layout entry.
4. Verify a sticky:
   - drag from empty left header space,
   - every right-side toolbar button remains clickable,
   - title editing remains clickable,
   - resize edges use native cursors,
   - lock removes drag/resize while preserving commands,
   - arrow cursor remains over caption/buttons.
5. Verify unsupported-DWM calls do not terminate the process and check Application event logs for new Task'sList errors.
6. Run `git diff --check`, confirm a clean worktree, merge to master, rerun the full suite, push, and clean the feature worktree.

## Milestone B completion gate

Milestone B is complete only when automated contracts and installed Windows evidence prove the main library delegates caption behavior to Windows, DWM enhancement safely falls back, sticky header controls and caption movement coexist reliably, and ordinary buttons/captions use the standard Windows pointer. Unit tests alone do not satisfy the gate.
