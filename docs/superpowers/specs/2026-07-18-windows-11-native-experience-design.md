# Task'sList Windows 11 Native Experience Design

**Date:** 2026-07-18  
**Status:** Proposed implementation design  
**Product objective:** Make Task'sList feel like a native Windows 11 program in every way.  
**Supersedes:** The custom-chrome interaction decisions in `2026-07-18-native-window-and-capture-reliability-design.md`. The local-first data model, plugin system, note-presentation model, clipboard history, and attachment architecture remain valid.

## Outcome

Task'sList remains a .NET 8 WPF application, but its shell stops imitating Windows and delegates window behavior to Windows. The main library uses the real non-client title bar and system caption buttons. Sticky notes remain specialized independent tool windows, but use DWM-backed corners, shadows, resize behavior, and native caption hit testing around their interactive controls.

The app follows the user's Windows settings for light/dark mode, accent color, transparency, high contrast, reduced motion, text scaling, and per-monitor DPI. Pointer, keyboard, touch, context-menu, notification, dialog, clipboard, and accessibility behavior follow Windows conventions. Task'sList retains its distinctive stationery surfaces only where product identity adds value: sticky paper, capture previews, and note content.

## Evidence from the current application

The installed application and current source contradict the objective in several concrete ways:

1. The main window uses `WindowStyle="None"`, `AllowsTransparency="True"`, a custom rounded border, and hand-written minimize/maximize/close buttons. Windows therefore cannot provide the complete caption, system menu, Snap Layout, shadow, corner, or accessibility behavior users expect.
2. Draggable title regions and ordinary desktop buttons use hand or four-arrow cursors. Native Windows title bars and command buttons use the standard arrow; resize cursors appear only at resizable edges.
3. The app hard-codes one dark palette instead of following the system app theme, accent, transparency, and high-contrast settings.
4. Note cards are hosted in a nested scrolling arrangement that exposes a horizontal scrollbar and clips the fourth card at the current default window width.
5. Note cards have no right-click command surface and limited keyboard command discovery.
6. Region capture always creates and opens an attached sticky. It stores the PNG, but never puts the bitmap on Windows Clipboard, so the user cannot immediately paste the screenshot elsewhere.
7. Transient status and failures mostly use custom text or legacy message boxes rather than Windows-style teaching notifications, task dialogs, and actionable error states.
8. Automation names exist only on part of the interface. Focus visuals, access keys, keyboard context menus, live-region announcements, and selection semantics are incomplete.

Microsoft's Windows design guidance defines the title bar as a system interaction surface with caption buttons and a drag region, recommends reserving passthrough regions for interactive title-bar controls, recommends Mica for long-lived window foundations and Acrylic for transient surfaces, and defines context menus as the secondary command surface for content items.

## Architecture decision

### Selected: native WPF retrofit

Keep WPF, SQLite, the plugin host, the clipboard engine, browser bridge, note windows, and existing domain models. Replace the shell and interaction infrastructure in focused layers:

- `WindowsThemeService` reads Windows theme, accent, high-contrast, transparency, and reduced-motion state and publishes semantic WPF resources.
- `DwmWindowService` applies supported DWM attributes for dark caption mode, system backdrop, corner preference, and native non-client behavior, with solid-color fallback.
- `StickyChromeService` owns sticky non-client hit testing, native dragging, resize edges, system-menu invocation, and interactive-control exclusions.
- `CaptureClipboardService` packages captured PNG bytes into a persistent Windows `DataObject`, suppresses self-recapture, and exposes a testable platform boundary.
- `NoteCardCommandService` centralizes the commands shared by note-card context menus, keyboard shortcuts, and future command bars.
- `AppNotificationService` provides in-app status/teaching notifications first and Windows toast integration where registration is available.

### Rejected: visual-only patch

Changing cursors, colors, and corner radii would leave the custom non-client implementation, missing Snap Layouts, incomplete system menus, clipboard failure, and accessibility gaps intact.

### Deferred: WinUI 3 rewrite

WinUI 3 offers first-class Fluent controls and system backdrops, but a rewrite would put the mature local database, independent sticky windows, plugin host, native clipboard monitoring, and current feature set at risk. The native retrofit reaches the requested behavior without replacing the product engine. A future shell migration remains possible because the new platform services are isolated.

## 1. Main library window

The main library returns to a real Windows top-level window:

- `AllowsTransparency` is false.
- Windows owns the title bar, app icon, title, caption buttons, resize frame, shadow, system menu, maximize/restore behavior, and Snap Layout affordance.
- The center status badge moves from the title bar into the app command/content area.
- Double-click, Alt+Space, right-click on the caption, Win+Arrow, Win+Z, taskbar thumbnails, Narrator caption names, and monitor/DPI transitions work through Windows rather than custom handlers.
- The normal arrow remains visible over caption and buttons. Hand cursors are reserved for actual hyperlinks, not buttons.
- The app requests dark or light caption treatment to match the current system theme.
- On supported Windows 11 versions, the client foundation uses Mica or Mica Alt once at the window level. Unsupported, high-contrast, remote-session, battery, or disabled-transparency conditions fall back to semantic solid colors.

The content shell uses a Windows-style navigation rail and command hierarchy. Navigation remains Notes, Clipboard, Contexts, Extensions, and Trash. Frequent commands remain directly visible; secondary commands move into context menus or overflow menus. Selected navigation, keyboard focus, hover, and pressed state are visually distinct and never communicated only by color.

## 2. Sticky note windows

Sticky notes are purpose-built desktop objects, so they do not gain a full minimize/maximize/close caption. They do gain native window mechanics:

- DWM draws the outer shadow and Windows 11 rounded corners; the WPF window is no longer globally transparent solely to fake those effects.
- The header is a caption region except for title-edit fields and visible toolbar buttons.
- Windows non-client hit testing initiates movement and supplies the standard arrow cursor. WPF `DragMove()` and the four-arrow/hand caption cursor are removed.
- Resize borders return the corresponding Windows resize cursor and respect lock state.
- Right-clicking non-interactive header space opens an appropriate system/note menu without blocking the existing full note context menu.
- The right-aligned note toolbar has stable geometry. Hidden controls cannot intercept input; visible controls remain client input regions and are keyboard reachable.
- A minimum drag region remains available even on narrow or rolled notes.
- Per-monitor DPI conversion is applied to non-client hit rectangles and restored bounds.
- Locked and ghost modes remain explicit product features. Lock prevents movement/edit/resize; ghost passes pointer input through. Tray and global recovery remain available.

## 3. Screenshot capture contract

Region capture behaves like a Windows screenshot tool:

1. The user invokes Capture Region.
2. The overlay selects and encodes a PNG without appearing in the captured pixels.
3. The PNG is saved once to Task'sList capture history with source context and dimensions.
4. The image is copied to Windows Clipboard as a persistent bitmap/PNG data object.
5. Task'sList shows a non-blocking confirmation: **Screenshot copied** with actions for **Create note** and **Open capture**.
6. No sticky is created unless the user invokes Create note or uses a separately named capture-to-note command.

The clipboard monitor is suppressed while Task'sList writes the capture, preventing a duplicate history row. The captured image must paste into Paint, ChatGPT, Office, messaging clients, and image editors. If clipboard ownership is temporarily unavailable, the app retries using bounded Windows clipboard retry behavior; the image remains in history and the user receives an actionable Copy again command rather than losing the capture.

Capture history continues to support explicit Create note. A note created from a capture contains the local image reference, source application, dimensions, and attachment choice. This conversion is a user action, not a capture side effect.

## 4. Note-card commands and selection

Every note card in the main library has standard item semantics:

- Left click selects; double-click opens the sticky.
- Enter opens the selected note.
- Space toggles selection when extended selection is active.
- Right-click and Shift+F10/Apps key open the same context menu.
- Right-clicking an unselected card selects only that card before opening the menu.
- Right-clicking a selected card preserves the current multi-selection so safe bulk commands apply to the selection.

The context menu is grouped in this order:

1. **Open sticky**
2. **Duplicate** and **Copy Markdown**
3. **Attach to** and **Apply style** submenus
4. **Archive**
5. **Move to Trash…**

Commands use one implementation whether invoked by menu, keyboard, or visible command bar. Destructive bulk actions state the number of affected notes and require confirmation. Menu items expose access keys, shortcut text, automation names, disabled reasons, and high-contrast-safe icons.

## 5. Responsive library layout

The note library never exposes a horizontal scrollbar for its card grid.

- Remove the nested outer `ScrollViewer` around the note `ListBox`.
- Disable horizontal scrolling at the list level and allow cards to wrap to the next row.
- Compute card width from available content width within minimum and maximum bounds, so the grid transitions cleanly between one, two, three, and four columns.
- Preserve vertical virtualization where the chosen panel supports it; if the initial WPF wrap panel cannot virtualize, cap the first milestone to correct layout and introduce a tested virtualizing wrap panel before declaring large-history performance complete.
- Empty, loading, error, and no-filter-results states use the same content region and do not shift navigation.
- Text uses trimming only when the complete value remains available through tooltip and accessibility name.

## 6. Fluent visual system

The visual system becomes semantic and system-responsive:

- Base typography: Segoe UI Variable with Windows caption/body/title scale and the user's text scaling.
- Semantic resources: window, layer, card, control, control-hover, control-pressed, text-primary, text-secondary, divider, accent, accent-text, danger, success, focus, and disabled.
- Light, dark, and high-contrast dictionaries are complete. Theme changes update open windows without restart.
- Windows accent is the default interactive accent; custom theme files may override stationery colors but cannot remove required contrast/focus semantics.
- Main-window Mica is the foundation layer. Opaque content layers are used only where hierarchy requires them. Acrylic is limited to transient flyouts/menus when supported.
- Corners follow progressive Windows geometry instead of applying the same large radius everywhere.
- Standard controls use Windows-like minimum targets, padding, pressed states, and default arrow cursors.
- Animations are short and purposeful, and turn off when reduced motion is requested.

Sticky paper remains intentionally branded. Paper presets, texture, opacity, and ink colors do not override system focus visuals, context menus, dialogs, or accessibility requirements.

## 7. Keyboard, accessibility, and input

The complete application is operable without a mouse:

- Predictable Tab and Shift+Tab order on every surface.
- Arrow-key navigation within lists, cards, menus, tabs, and toolbar groups.
- Enter invokes primary action; Space toggles; Escape dismisses transient UI; Shift+F10 opens context menus.
- Access keys are assigned to frequent visible commands without conflicting with global hotkeys.
- Focus visuals remain visible in light, dark, high contrast, and custom themes.
- Every icon-only control has an automation name and tooltip. Every note/capture card exposes title, source, state, and selection to UI Automation.
- Status changes such as screenshot copied, note saved, capture failed, and item moved to Trash use polite live-region announcements.
- Color is never the only indication of pinned, attached, locked, selected, reminder, favorite, or error state.
- Touch and pen can invoke context menus through press-and-hold; minimum command targets remain usable at Windows scale factors.

## 8. Dialogs, notifications, and errors

- Confirmations and errors use Windows task-dialog semantics with a clear primary action, Cancel, descriptive title, and expandable technical detail when useful.
- Routine success does not interrupt with a modal dialog.
- Capture completion, copy completion, save state, and recoverable errors use an in-app teaching/status notification with direct next actions.
- Windows toast notifications are used for reminders and background events after app identity/shortcut registration is verified. Toast failure never blocks local reminders.
- Destructive actions remain recoverable through Trash wherever possible.

## 9. Packaging and system integration

- The executable, native title bar, taskbar, Alt+Tab entry, Start menu shortcut, tray icon, and uninstall entry use the real Task'sList icon.
- The app manifest declares supported Windows versions, DPI awareness, long-path behavior where required, and execution level.
- Startup registration, notification identity, native browser messaging, and uninstall behavior remain per-user and reversible.
- Windows 10 receives functional solid-color and standard-window fallbacks; Windows 11 receives the full DWM/backdrop experience.

## Implementation milestones

### Milestone A: capture and command correctness

- Screenshot to clipboard plus history; no automatic note.
- Explicit Create note from capture.
- Note-card context menu and keyboard invocation.
- Correct note-card wrapping with no horizontal scrollbar.

### Milestone B: native window foundation

- Real main-window title bar and caption behavior.
- DWM theme, backdrop, corner, and fallback services.
- Native sticky caption/resize hit testing with interactive exclusions.
- Arrow/button/resize cursor audit across all windows.

### Milestone C: system-responsive Fluent resources

- Light/dark/accent/high-contrast/transparency/reduced-motion response.
- Control templates, focus visuals, navigation, menus, and transient surfaces.
- Task-dialog and in-app notification infrastructure.

### Milestone D: accessibility and system integration

- Keyboard and UI Automation audit.
- DPI/text-scale/multi-monitor audit.
- Toast reminders, manifest, taskbar/Start/tray/uninstall verification.
- Installed end-to-end Windows 11 behavior matrix.

Each milestone ends with a self-contained release build, reinstall, real desktop interaction checks, and a pushed commit. Temporary compatibility shims are removed before the final audit.

## Verification contract

Automated tests must cover:

- capture result packages bitmap data and suppresses self-recapture;
- capture stores exactly one history item and does not create a note by default;
- explicit capture-to-note creates exactly one note with correct source and payload;
- note-card right-click selection and multi-selection policy;
- every context-menu command routes through the shared command service;
- note layout disables horizontal scrolling and wraps cards;
- main window uses non-transparent native chrome and contains no custom caption buttons;
- sticky hit-test policy returns caption, client control, resize edge, or nowhere correctly;
- theme service produces complete light, dark, and high-contrast semantic tokens;
- reduced motion, transparency disabled, and unsupported-Windows fallbacks;
- keyboard commands and automation names for all primary controls;
- existing database, plugin, clipboard, browser, and sticky tests remain green.

Installed-artifact verification must prove:

1. Main caption drag, system menu, minimize, maximize/restore, close, resize, Win+Arrow, Win+Z/Snap Layout, taskbar, and DPI-monitor transitions.
2. Arrow cursor on captions and buttons; correct resize cursors only on resize borders.
3. Sticky drag from empty header space, button clicks on the right, resize, lock, ghost recovery, roll, and multi-monitor persistence.
4. Region capture pastes as an image into at least Paint and one rich destination, appears once in history, and creates no note until requested.
5. Note-card mouse selection, right-click, Shift+F10, keyboard navigation, bulk menu actions, confirmation, and Trash restore.
6. No horizontal note-card scrollbar at minimum, default, maximized, and common scaled window sizes.
7. Live switching between Windows light/dark, accent, transparency, reduced motion, high contrast, text scaling, and 100/125/150/200 percent DPI.
8. Narrator reads window titles, navigation, cards, selection, button names, status changes, menus, and dialogs in a meaningful order.
9. Release installer, upgrade, launch, startup option, tray, browser bridge, notification identity, and uninstall remain functional.

The objective is complete only when every installed-artifact item has direct evidence. Passing unit tests alone is insufficient.
