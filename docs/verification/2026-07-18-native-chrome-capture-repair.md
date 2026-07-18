# Native Chrome and Capture Repair Verification

**Date:** 2026-07-18  
**Installed path:** `C:\Users\taskm\AppData\Local\Programs\TasksList`  
**Repair commit:** `e8d1bde`

## Reported failures

1. The installed application did not show the new brand icon.
2. The main-window title strip only dragged from select rendered content and supplied no grab affordance.
3. Completing a region selection terminated `TasksList.App.exe`.

## Root causes

- The branded release had been built but not reinstalled. The reported installed EXE hash was `8ff2d34bf00df0702e7f9caf4ef56e7b530a30859a42707fc593bcf034bec944`, while the then-current branded release was different.
- The custom header `Border` had a mouse handler but a null background, so empty title-strip pixels were not hit-testable. It also had no cursor declaration.
- Windows `.NET Runtime` event 1026 at 2026-07-18 19:04:03 recorded `InvalidOperationException: DialogResult can be set only after Window is created and shown as dialog` at `CaptureOverlay.OverlayMouseUp`. Setting `Visibility.Hidden` ended WPF's modal state before the code assigned `DialogResult = true`.

## Corrective behavior

- The ICO and 64-pixel mark are embedded as WPF resources. The global window style sets the real icon, and the custom title displays the actual mark instead of a placeholder letter.
- The full non-button title strip uses `Background="Transparent"`, `Cursor="Hand"`, and the existing drag/double-click handler.
- The overlay sets `Opacity = 0` and `IsHitTestVisible = false` before screen copy without changing `Visibility`.
- `CaptureOperation.RunAsync` contains unexpected overlay, GDI, encoding, storage, or database failures and reports a useful error without terminating the dispatcher process.

## Automated evidence

The clean release build passed 125 tests:

- TasksList.Core.Tests: 57
- TasksList.Infrastructure.Tests: 11
- TasksList.PluginSdk.Tests: 9
- TasksList.App.Tests: 48

New regression coverage includes the main-window chrome resource/hit-surface contract, modal-safe overlay suppression on an STA thread, capture success, and capture exception containment.

## Installed interaction evidence

- Installed and release executable hashes matched: `48a04d307a26b08d21e96e336a8c043b3f019d2d2a1660cab4e53efef09c5978`.
- The icon was visibly present in the installed custom header.
- The cursor handle over the title strip exactly matched Windows `IDC_HAND`.
- Real mouse drags from the brand/title zone, an empty header zone, and the right non-button zone each moved the window by 30×20 pixels.
- A real double-click maximized the window; a second double-click restored it.
- A real selection opened the `Select capture region` modal, captured 256×160 pixels, closed the modal, and left the same process responsive.
- Database counts changed from 3 to 4 notes and 15 to 16 captures. The latest note contains a captured-image Markdown reference.
- No new `.NET Runtime` or `Application Error` event naming `TasksList.App.exe` appeared after the installed capture test.

The installed verification capture created one visible note titled `Capture from TasksList.App · Task'sList`; it is intentionally retained so the owner can inspect the exact successful output.
