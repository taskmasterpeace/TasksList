# Windows 11 Milestone D: Integration and Accessibility Plan

**Goal:** Finish Task'sList as a first-class Windows 11 desktop application by declaring the correct process capabilities, completing UI Automation and keyboard contracts, replacing legacy reminder balloons with Windows app notifications, strengthening installer identity, and proving the installed result end to end.

**Microsoft guidance:** Use an application manifest for Per-Monitor-V2 DPI awareness and current Windows compatibility. For an unpackaged WPF app, use Windows App SDK `AppNotificationManager`; register the activation handler before `Register`, show notifications only after registration, and unregister during orderly shutdown. Keep an in-app reminder banner as the durable fallback when system notifications are unavailable.

---

## Task 1: Windows manifest and DPI identity

- Add a tested `app.manifest` with `asInvoker`, Common Controls v6, Windows 10/11 compatibility, `PerMonitorV2` with safe fallback, long-path awareness, and modern heap behavior.
- Target the Windows SDK contract required by Windows App SDK, set `WindowsPackageType=None`, and publish the Windows App SDK self-contained with the existing self-contained application.
- Set one explicit process AppUserModelID before any HWND is created so taskbar, shortcuts, notifications, and activation share an identity.
- Verify the published executable embeds the manifest and the installed main/palette/sticky HWNDs report PMv2 awareness.
- Commit `feat: declare modern Windows process capabilities`.

## Task 2: UI Automation and keyboard completion

- Add source contracts that enumerate every window and require names/help text for icon-only controls, labels for text inputs, live regions for validation/reminders, logical tab navigation, and default/cancel dialog behavior.
- Add or correct automation names for main navigation, note cards, sticky title/editor/toolbar, clipboard palette filters/actions, settings, plugin manager, New Place, Schedule, and customization controls.
- Make Settings and Schedule standard DWM-treated secondary windows and replace remaining shell hard-coded colors with semantic resources.
- Use installed UI Automation to walk each surface, place focus, invoke primary/cancel actions, and verify no focus trap.
- Commit `fix: complete Windows accessibility contracts`.

## Task 3: Native Windows app notifications

- Add a small notification abstraction whose pure request/activation policy is unit tested independently from Windows APIs.
- Implement it with Windows App SDK `AppNotificationManager` for unpackaged WPF, register safely at startup, unregister on orderly exit, and tolerate unsupported or disabled notifications without crashing.
- Send a reminder notification once per due note with title, preview, note identifier, and an Open note action while retaining the sticky banner/sound/pulse fallback.
- Route notification activation onto the WPF dispatcher, restore/show the library, open the referenced note, and foreground the relevant window.
- Add a maintenance command used by uninstall to call `UnregisterAll` without opening the normal application.
- Commit `feat: add actionable Windows reminder notifications`.

## Task 4: Installer and uninstall integration

- Give the Start menu shortcut the branded icon and stable identity-aligned target/working directory.
- Expand Add/Remove Programs metadata with display icon, comments, help/home URL, quiet uninstall string, estimated size, and the new version.
- Keep upgrades per-user and data-preserving; unregister notification integration during uninstall before deleting program files.
- Extend the installed probe to validate shortcut, uninstall metadata, executable version/hash, plugins, native browser registrations, and notification cleanup command.
- Commit `fix: integrate Windows identity with installation`.

## Task 5: Final installed matrix

- Run the full Release suite and build the exact self-contained artifact.
- Install and verify hash, manifest resources, PMv2 HWND context, DWM/native frame styles, taskbar/Start/tray branding, shortcut and uninstall metadata, browser bridge, and all three plugins.
- Verify screenshot capture still adds one history item, exposes Bitmap and PNG on Windows Clipboard, and creates no note until the explicit action.
- Verify right-click/Shift+F10 note commands, sticky drag/toolbar/resize, palette drag/client controls/resize, keyboard focus/UIA traversal, theme/accent/high-contrast policy, reminder notification fallback, settings, startup, and clean shutdown.
- Inspect installed screenshots at current scaling and Windows Application events; fix every discovered regression.
- Commit verification evidence, merge, rerun the full suite on `master`, push, and remove the worktree.

## Completion gate

The native-Windows objective is complete only when the exact installed executable directly proves PMv2/manifest identity, UI Automation and keyboard usability, native app-notification integration with safe fallback, reversible installer registration, the screenshot/no-auto-note behavior, and zero new Task'sList runtime errors. Unit tests or source inspection alone do not satisfy this gate.
