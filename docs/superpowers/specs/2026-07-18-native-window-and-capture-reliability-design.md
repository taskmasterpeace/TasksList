# Native Window and Capture Reliability Design

**Date:** 2026-07-18  
**Status:** Approved through explicit user-reported expected behavior

## Problem evidence

The installed binary is the pre-brand build with SHA-256 `8ff2d34bf00df0702e7f9caf4ef56e7b530a30859a42707fc593bcf034bec944`; the current branded release has a different hash and was never reinstalled. The main window also renders a placeholder `T` instead of the brand mark.

The custom main-window title strip has a `MouseLeftButtonDown` handler but no background. Empty portions are therefore not hit-testable, and the strip supplies no cursor cue. This makes dragging work only over select rendered children rather than the entire expected title surface.

Windows Application event 1026 at 2026-07-18 19:04:03 records the capture crash. `CaptureOverlay.OverlayMouseUp` sets `Visibility.Hidden`, which ends WPF's modal state, then sets `DialogResult = true`. WPF raises `InvalidOperationException: DialogResult can be set only after Window is created and shown as dialog`, and the exception escapes the `async void` command path and terminates the process.

## Selected repair

The window keeps its existing custom rounded shell. The complete 64-pixel header surface becomes hit-testable with `Background="Transparent"`, uses the hand cursor as the affordance requested by the user, retains double-click maximize/restore, and continues excluding the actual window buttons from dragging. The placeholder letter is replaced by the embedded Task'sList mark, while the real ICO is applied to WPF windows, the executable, taskbar, and tray.

The capture overlay remains a visible modal dialog until completion. Immediately before screen copy it becomes fully transparent and non-hit-testable rather than hidden. This removes it from the captured pixels without ending modal state. A reusable capture-operation boundary catches any unexpected overlay, screen-copy, encoding, storage, or database exception, restores control to the application, and displays a concise error instead of allowing process termination.

## Alternatives rejected

1. **Keep the current handler and only change the cursor.** This leaves empty title-strip pixels non-draggable.
2. **Adopt `WindowChrome` and rebuild every custom title surface.** This could improve native non-client behavior, but conflicts with the existing transparent rounded-window architecture and broadens a focused reliability repair.
3. **Call `Hide()` and close the dialog through another callback.** `Hide()` has the same modal-state problem as `Visibility.Hidden` and preserves the crash hazard.

## Verification

Automated contracts parse the actual XAML/project resources for the embedded icon, real header image, transparent hit surface, hand cursor, and drag handler. Capture tests prove that suppression preserves `Visibility`, makes the overlay transparent, and that an exception is reported without escaping the operation boundary. Full Release tests, packaging, checksum verification, installation, icon extraction, mouse-driven header dragging, cursor inspection, successful region capture, and a post-test Windows event-log scan complete the repair gate.
