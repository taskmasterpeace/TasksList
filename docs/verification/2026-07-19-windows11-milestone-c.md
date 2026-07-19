# Windows 11 Milestone C Verification

**Artifact:** self-contained `win-x64` release installed per-user at `C:\Users\taskm\AppData\Local\Programs\TasksList`  
**SHA-256:** `124613858852479a33419134649303b8105c534815d4a8beb2ec55d8a0b1ca34`

## Automated evidence

- Release suite: 174 tests passed: App 96, Core 58, Infrastructure 11, Plugin SDK 9.
- Semantic Windows palette covers light, dark, high contrast, system accent, readable accent text, and 14 shared tokens.
- Live theme tests cover resource mutation, Windows preference events, custom-theme overrides, and high-contrast precedence.
- XAML contracts cover keyboard focus states and native chrome for the main window, sticky windows, Plugin Manager, New Place, and Clipboard Palette.
- Reusable notification tests cover replacement, action execution, one-time dismissal, and a polite UI Automation live region.

## Installed Windows evidence

- Installed executable hash exactly matched the staged release.
- Main window was responsive and exposed native caption, thick frame, system menu, minimize box, and maximize box styles.
- Main DWM attributes reported dark caption `1`, rounded corner preference `2`, and main-window backdrop `2`.
- Plugin Manager exposed a native resizable caption/system menu, dark caption `1`, rounded corners `2`, and backdrop `2`.
- New Place exposed a native fixed dialog caption/system menu, dark caption `1`, rounded corners `2`, and transient backdrop `3`.
- Clipboard Palette reported `HTCAPTION` on its header icon, `HTCLIENT` on Search and Close, and `HTLEFT` on its resize edge; it was non-transparent WPF chrome with dark caption, rounded corners, and transient backdrop.
- UI Automation successfully placed keyboard focus on the primary New sticky command while the installed app remained responsive.
- The installed plugin catalog exposed Browser Context, Capture Workflows, and Developer Workspace with their declared capabilities.

## Screenshot clipboard regression

An installed 120 by 80 region capture produced all of the following together:

- capture count changed from 27 to 28;
- note count remained exactly 8;
- Windows Clipboard exposed `System.Drawing.Bitmap`, `Bitmap`, `System.Windows.Media.Imaging.BitmapSource`, and `PNG` formats;
- clipboard image dimensions were exactly 120 by 80;
- the app displayed `Screenshot copied to clipboard` with separate `Create note` and `Dismiss` actions;
- dismissing the notification did not create a post-it note.

The Windows Application log contained zero new `.NET Runtime` or `Application Error` events for `TasksList.App` during installed verification.
