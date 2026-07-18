# Native Window and Capture Reliability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Task'sList present its real icon, drag naturally from the entire title strip with a hand cursor, and contain all screenshot-capture failures without process termination.

**Architecture:** Existing custom WPF chrome remains in place, with resource and hit-testing corrections enforced by a XAML contract test. Capture presentation and fault containment move into two focused helpers used by the overlay and main-window command path. The corrected self-contained build is reinstalled and exercised through real mouse input.

**Tech Stack:** .NET 8, WPF, xUnit, System.Drawing, PowerShell, Windows event logs

## Global Constraints

- Keep the existing rounded transparent application shell.
- The complete non-button title strip must drag, use `Cursors.Hand`, and retain double-click maximize/restore.
- Apply the real Task'sList icon to visible window chrome, WPF window metadata, executable, taskbar, and tray.
- Never set `Visibility.Hidden` or call `Hide()` before assigning capture-dialog `DialogResult`.
- Unexpected capture exceptions must not escape to the WPF dispatcher.
- Reinstall and validate the exact release binary before completion.

---

### Task 1: Window chrome contract

**Files:**
- Create: `tests/TasksList.App.Tests/Shell/MainWindowChromeContractTests.cs`
- Modify: `src/TasksList.App/TasksList.App.csproj`
- Modify: `src/TasksList.App/App.xaml`
- Modify: `src/TasksList.App/MainWindow.xaml`

**Interfaces:**
- Consumes: `assets/brand/generated/TasksList.ico` and `app-icon-64.png`.
- Produces: a branded WPF window and a hit-testable title surface.

- [ ] Write a test that parses the project and XAML files and requires an embedded ICO resource, global window `Icon` setter, branded title `Image`, title `Background="Transparent"`, `Cursor="Hand"`, and `MouseLeftButtonDown="TitleBarMouseDown"`.
- [ ] Run the focused test and observe failure on the placeholder `Text="T"` and missing title background/cursor.
- [ ] Embed the icon assets, set the global window icon, replace the placeholder letter with the mark, and apply the transparent drag surface and hand cursor.
- [ ] Run the focused test and the full app test assembly with zero failures.

### Task 2: Modal-safe capture and fault boundary

**Files:**
- Create: `tests/TasksList.App.Tests/Capture/CaptureOverlayPresentationTests.cs`
- Create: `tests/TasksList.App.Tests/Capture/CaptureOperationTests.cs`
- Create: `src/TasksList.App/Capture/CaptureOverlayPresentation.cs`
- Create: `src/TasksList.App/Capture/CaptureOperation.cs`
- Modify: `src/TasksList.App/Capture/CaptureOverlay.xaml.cs`
- Modify: `src/TasksList.App/MainWindow.xaml.cs`

**Interfaces:**
- Produces: `CaptureOverlayPresentation.SuppressForCapture(Window)` and `CaptureOperation.RunAsync(Func<Task>, Action<string>)`.

- [ ] Write a WPF STA test requiring suppression to preserve `Window.Visibility`, set `Opacity` to zero, and disable hit testing.
- [ ] Write async tests requiring `CaptureOperation.RunAsync` to return success for completed work and report/contain a thrown exception.
- [ ] Run both focused tests and observe missing-type compilation failures.
- [ ] Implement the helpers, use suppression instead of `Visibility.Hidden`, and wrap `CaptureRegionCoreAsync` with the fault boundary and an owner-aware error dialog.
- [ ] Run focused tests and the complete app test assembly with zero failures.

### Task 3: Release, installed interaction, and publication

**Files:**
- Generated and ignored: `artifacts/release/`
- Installed: `%LOCALAPPDATA%\Programs\TasksList`

**Interfaces:**
- Produces: the corrected installed Task'sList build and synchronized GitHub `master`.

- [ ] Run `powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1` and require all tests, three plugin packages, and all checksums to pass.
- [ ] Stop only the crashed/stale Task'sList process if one remains, run the release installer, and launch the installed executable.
- [ ] Extract and inspect the installed EXE icon; confirm the installed hash matches the release.
- [ ] Use real mouse movement to verify the title-strip hand cursor, drag from left/empty/right non-button areas, and double-click maximize/restore.
- [ ] Perform a real region capture and verify the process remains responsive, a capture record/note is created, and no new `.NET Runtime` or `Application Error` event names `TasksList.App.exe`.
- [ ] Commit the repairs, push `master`, and verify local and remote SHAs match.
