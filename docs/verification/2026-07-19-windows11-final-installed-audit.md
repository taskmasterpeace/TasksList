# Task'sList Windows 11 Final Installed Audit

**Installed version:** 1.2.0  
**Installed path:** `C:\Users\taskm\AppData\Local\Programs\TasksList`  
**Exact SHA-256:** `6e05ad43160bdac68c37c2c98cd093d235dac1f7fbc16dac2fd963de78abe7f9`

## Release gate

- 222 Release tests passed: App 144, Core 58, Infrastructure 11, Plugin SDK 9.
- The staged and installed executable hashes matched exactly.
- The installed probe found the branded Start menu shortcut, Add/Remove Programs version 1.2.0 metadata, both Chrome and Edge native hosts, all three bundled plugins, the database, eight notes, and zero failures.
- The clean-upgrade installer removed obsolete program payload while preserving `%LOCALAPPDATA%\TasksList` user data. Installed payload differed from the staged app only by the copied uninstall script.

## Native process and window behavior

- The embedded executable manifest directly contained `asInvoker`, Common Controls v6, Windows 10/11 compatibility, `PerMonitorV2`, long-path awareness, and Segment Heap.
- The installed main HWND reported Per-Monitor-V2 awareness, 96 DPI on the current monitor, native caption/resize/system-menu styles, DWM dark caption `1`, corner preference `2`, and main-window backdrop `2`.
- The taskbar automation ID was exactly `Appid: TaskMasterPeace.TasksList`.
- Final sticky hit testing reported header `HTCAPTION`, toolbar `HTCLIENT`, and left edge `HTLEFT`; the sticky was not layered. Always-on-top and roll-up were invoked twice each and returned to the prior state while the process stayed responsive.
- Plugin Manager, New Place, Settings, Schedule, and Clipboard Palette native-frame contracts remained green. Clipboard Palette client controls and resize/caption regions were physically verified in Milestone C.

## UI Automation and keyboard

- Forty accessibility contracts cover stable control names, five named library tabs, Places, clipboard filters/history/preview/actions, settings, New Place, Schedule, sticky title/Markdown/mode, and the complete appearance flyout.
- Validation and reminder surfaces use assertive live regions; ordinary app notifications use a polite live region.
- The installed main window exposed 32 keyboard-focusable elements. Every application-owned focusable surface had a name; the only unnamed element was the WPF `ListBox` template's internal `ScrollViewer` pane.
- Installed UI Automation focused and invoked main commands, native dialogs, sticky toolbar actions, the reminder acknowledgement, and notification dismissal without a focus trap or hang.

## Windows app notifications

- Startup created `HKCU\Software\Classes\AppUserModelId\TaskMasterPeace.TasksList` with Task'sList display name, branded notification icon, and a COM activator pointing to the installed executable.
- A controlled reminder advanced Windows' `LastNotificationAddedTime`, opened the existing verification sticky with its reminder banner, and exposed Acknowledge. Acknowledge cleared the armed reminder.
- The maintenance command exited `0` and removed the AppUserModel registration. A normal restart recreated it and remained responsive. Windows retains its notification-settings/history key, as expected, while the application registration itself is reversible.
- Unsupported or disabled notification state is caught; sticky banner, sound, pulse, and topmost behavior remain the local fallback.

## Screenshot regression

Against the exact final installed hash, a 120 by 80 region capture:

- exposed a 120 by 80 Windows Clipboard image;
- published both Bitmap and PNG formats;
- increased capture history by exactly one, from 29 to 30;
- left the note count exactly 8;
- displayed `Screenshot copied to clipboard` with separate `Create note` and `Dismiss` actions;
- created no post-it note when Dismiss was invoked.

The Windows Application event log contained zero new `.NET Runtime` or `Application Error` events for `TasksList.App` during the final installed audit.

## Platform references

- Microsoft desktop application manifest guidance: https://learn.microsoft.com/windows/win32/sbscs/application-manifests
- Microsoft Per-Monitor-V2 guidance: https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows
- Microsoft WPF/.NET app notification guidance: https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-dotnet
