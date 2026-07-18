# Task'sList Sticky-First Power-User Redesign

**Date:** 2026-07-18  
**Status:** Approved direction; researched specification  
**Supersedes:** The interaction and presentation portions of `2026-07-18-taskslist-design.md`; the existing local-first data, context, browser bridge, capture, Markdown, and plugin architecture remain valid.

## Product correction

Task'sList is a sticky-note utility first. A note must feel like a physical object that happens to be intelligent: immediately movable, resizable, readable, and customizable. Clipboard history, context attachment, browser sessions, capture, and plugins support the sticky-note workflow; they do not replace it with a dashboard.

The default experience is the desktop notes plus a notification-area icon. The manager is an optional library. The clipboard palette is a separate compact power tool.

## Research findings

The redesign deliberately combines proven interaction patterns rather than copying another application's visual shell.

- Zhorn Stickies treats notes as persistent desktop objects. Its relevant strengths are per-note fonts/colors/styles, snap-to-note and snap-to-screen, application attachment, sleep/reminders, alarms, roll-up, lock/freeze, ghost click-through mode, live transparency preview, note-from-clipboard, bulk operations, tray commands, and F2 title editing. Sources: [Stickies product page](https://www.zhornsoftware.co.uk/stickies/), [Stickies version history](https://www.zhornsoftware.co.uk/stickies/versions.html), [Stickies API](https://www.zhornsoftware.co.uk/stickies/api.html).
- ClipAngel's strengths are hot-as-you-type filtering, favorites and used marks, source process/window/URL provenance, type filters, original/plain/special paste, duplicate promotion, editable clips and titles, multiselect, drag-and-drop, configurable history/privacy limits, monitoring pause, global hotkeys, and a window that appears near the current work. Sources: [ClipAngel SourceForge features](https://sourceforge.net/projects/clip-angel/), [ClipAngel source](https://github.com/tormozit/ClipAngel).
- ShareX demonstrates direct-manipulation power-user controls: a pinned item moves by dragging, changes opacity with Ctrl+mouse-wheel, minimizes by double-click, and exposes the same operation through tools, history, hotkeys, and workflows. Sources: [Pin to Screen](https://getsharex.com/docs/pin-to-screen.html), [ShareX keybinds](https://getsharex.com/docs/keybinds).
- Microsoft PowerToys reinforces that always-on-top must have a global shortcut, visible state, configurable feedback, and exclusions. Source: [PowerToys Always On Top](https://learn.microsoft.com/en-us/windows/powertoys/always-on-top).

## Current-state contradictions

1. The sticky title is a full-width `TextBox`; `TitleBarMouseDown` refuses to drag when the event source is a text box. Most of the apparent title bar therefore cannot move the note.
2. The note model stores only title, Markdown, and attachments. Window bounds and appearance are discarded on close/restart.
3. Topmost and roll-up are transient UI booleans. They are not durable note properties.
4. The manager contains decorative Microsoft Edge, Developer Workspace, and Docker rows that are not backed by detected contexts.
5. Several visible glyphs have mojibake, including status separators and controls.
6. Customization requires editing a global JSON theme and restarting. There is no per-note customization surface.
7. Clipboard data and provenance are captured, but the interface lacks favorite, paste, edit, delete, filter, assign, and multiselect workflows.
8. There is no tray lifecycle, global hotkey layer, reminder/sleep UI, undoable deletion, or click-through recovery mechanism.

## Experience architecture

Task'sList has three surfaces with separate responsibilities:

### 1. Desktop sticky

The primary surface. It is always a real independent Windows window. The visual chrome stays quiet until hover or keyboard focus.

- The entire header, including the displayed title, drags the window.
- Double-clicking the title or pressing F2 enters title-edit mode. Enter commits; Escape cancels; focus loss commits. Only the temporary edit field suppresses dragging.
- Hover reveals a compact toolbar: pin, roll, customize, attach, more, close.
- Right-click anywhere opens a complete note menu.
- The note autosaves content and presentation state independently.

### 2. Clipboard palette

A compact ClipAngel-style window summoned near the pointer/current application. It is optimized for keyboard use and fast dismissal, not archival browsing.

- Search receives focus immediately.
- Up/Down changes selection even while the search box remains focused.
- Enter pastes the selected representation into the previous application; Ctrl+Enter pastes plain text; Shift+Enter creates/opens a sticky.
- Escape hides the palette and returns focus without changing the clipboard.
- A resizable preview shows plain text, formatted HTML/RTF, images, files, and provenance.

### 3. Library

The optional manager for notes, clipboard history, Places/contexts, browser sessions, plugins, and settings. It contains real data only. Its navigation becomes explicit tabs rather than three unrelated simultaneous columns.

## User-visible customization and QoL requirements

The release must include the following 36 improvements.

### Direct note interaction

1. Drag from the displayed title or any non-button header space.
2. F2/double-click title editing with commit/cancel semantics.
3. Persistent per-note position and size across restart.
4. Multi-monitor rescue: clamp restored notes to a visible work area without collapsing intentional relative placement.
5. Edge and note-to-note snapping with a configurable tolerance; Alt temporarily disables snapping.
6. Roll/unroll by toolbar, menu, or Ctrl+M, remembering the expanded size.
7. Lock mode prevents content edits, movement, and resizing; Ctrl+L toggles it.
8. Duplicate note with Ctrl+D, offset by 24 device-independent pixels.
9. Close hides/archives a note; permanent deletion requires confirmation and remains recoverable from Trash for 30 days.
10. Ctrl+N creates a note beside the active note; Ctrl+Shift+N creates one from current clipboard content.

### Appearance

11. A quick-customize flyout opens from a palette button and never requires app restart.
12. Eight curated paper presets: Butter, Peach, Mint, Sky, Lavender, Rose, Graphite, and Glass.
13. Custom background, text, and accent colors with contrast warning and reset.
14. A live opacity slider from 20% to 100%, with numeric percentage and reset.
15. Ctrl+mouse-wheel and Ctrl+Plus/Minus change opacity in 5% steps and show a temporary percentage HUD.
16. Font family, size, weight, line spacing, and Markdown/editor mode are selectable per note.
17. Three density presets change header/footer/content padding without changing content.
18. Shadow strength, corner style, border visibility, and paper texture can be toggled.
19. Toolbar behavior is selectable: always visible, hover, focused only, or hidden.
20. Appearance can be saved as a named style, applied to selected notes, or made the default for new notes.

### Visibility, focus, and context

21. Always-on-top has a clear visual state and Ctrl+Shift+T shortcut.
22. Ghost/click-through mode is available after a safety confirmation; the tray always offers “Disable click-through for all notes.”
23. “Focus opacity” can use one opacity while active and another while inactive, with smooth 120 ms transition.
24. Attached notes expose four understandable modes: foreground only, while application exists, stay visible after activation, and sleep until application returns.
25. Detach and reattach are one-click actions, and the current attached application is shown by real name/icon.
26. Sleep presets include 15 minutes, 1 hour, tomorrow morning, next workday, and a custom date/time.
27. Reminders can play a sound, pulse the border, and optionally remain topmost until acknowledged.

### Clipboard palette

28. Global hotkey Ctrl+Shift+V opens a fast searchable palette near the pointer.
29. Filters cover favorite, used, unfiled, source application, representation type, and date range.
30. A clip can be pasted original, pasted plain, copied without pasting, edited, renamed, duplicated, favorited, deleted, or converted to a note.
31. Multiselect supports joined paste, joined note creation, bulk assignment to a Place/note, favorite, and deletion.
32. Every result retains process, window title, URL when available, timestamp, formats, size, and assigned Places.
33. Monitoring can be paused globally or excluded per application; ignored/private clipboard formats are respected.
34. Repeated copies can promote the existing item instead of producing noisy duplicates.

### System integration and visual cleanup

35. A tray menu exposes New Sticky, New from Clipboard, Clipboard Palette, Capture Region, Show/Hide All, Library, Disable Ghost Mode, Pause Monitoring, Settings, and Exit. User-configurable global hotkeys mirror the core actions.
36. All mojibake and decorative fake context rows are removed. Empty states explain how to populate real applications, tabs, notes, and clips.

## Built-in interactive note blocks

Task'sList keeps Markdown portable while allowing small useful interfaces. Blocks use fenced directives so the source remains readable in another editor.

- `:::progress value=65 label="Release"` renders an adjustable progress bar and writes the new value back to Markdown.
- `:::counter value=3 label="Attempts"` renders decrement/increment buttons and persists the value.
- `:::timer minutes=25 label="Focus"` renders a local countdown with start/pause/reset; running state is kept in the local database, while the duration remains in Markdown.
- Existing Markdown tasks remain interactive in preview.

No block runs arbitrary code, fetches a network resource, or gains plugin permissions. Plugins may register additional block types through the existing capability model.

## Persistence model

Add an additive SQLite migration. Existing notes must open with safe defaults.

`NotePresentation` is a one-to-one record keyed by note ID:

- bounds: left, top, width, height, expanded height, monitor identity;
- visibility: topmost, rolled, locked, ghost, toolbar mode;
- appearance: style ID, background/text/accent colors, opacity active/inactive, font family/size/weight, line spacing, density, shadow, corners, border, texture;
- lifecycle: hidden/archive timestamp, deleted timestamp, wake timestamp, reminder mode;
- timestamps: created, modified, presentation modified.

Global settings live in `%LOCALAPPDATA%\TasksList\settings.json`, are validated on load, and are written atomically. They contain default style, snap tolerance, global shortcuts, clipboard duplicate/privacy policy, start-with-Windows, notification choices, and tray behavior.

Window state writes are debounced during movement/resizing and synchronously flushed on close/application exit. A corrupt presentation row falls back per field; it must never prevent note content from loading.

## Interaction safety

- Opacity never goes below 20% through normal controls.
- Ghost mode cannot be enabled for every recovery surface: the tray menu and global recovery hotkey remain interactive.
- Lock and ghost are distinct. Lock protects the note; ghost passes mouse input through it.
- Closing a note is recoverable. Permanent delete is explicit.
- Password managers and applications using ignore/private clipboard formats are not captured.
- Pasting restores focus to the previously active application and does not generate a duplicate history record.
- Private browser windows remain excluded.

## Visual direction

The aesthetic is “precision stationery”: tactile paper surfaces over a restrained charcoal utility layer. Notes use subtle paper gradients/noise, warm ink, crisp typography, and small purposeful motion. Graphite and Glass are deliberate dark/translucent alternatives, not generic purple dashboards.

- Sticky controls use recognizable vector icons with tooltips and 32×32 minimum hit areas.
- Controls fade in as one coordinated group, not as scattered animations.
- Header height remains draggable at every supported density.
- Selected/focused/pinned/attached/locked/reminder states use different signals and are never communicated by color alone.
- The library uses the global design tokens but removes all placeholder app cards.
- Per-monitor DPI, keyboard navigation, screen readers, high contrast, and reduced motion are supported.

## Architecture boundaries

- `TasksList.Core.Notes.NotePresentation`: immutable validated presentation state and lifecycle transitions.
- `TasksList.Core.Notes.NoteAppearancePolicy`: opacity clamping, preset/style merging, contrast calculation, and default migration.
- `TasksList.Core.Notes.NoteSnapService`: pure bounds calculation for monitor/note snapping.
- `TasksList.Infrastructure.Storage`: migration 002 plus note-presentation, style, reminder, favorite/used clip, and soft-delete persistence.
- `TasksList.App.Sticky.StickyWindow`: rendering and event forwarding only.
- `TasksList.App.Sticky.StickyWindowController`: movement, edit mode, shortcuts, persistence debounce, snap, focus opacity, and lock/ghost coordination.
- `TasksList.App.Sticky.CustomizeFlyout`: live appearance editing and named styles.
- `TasksList.App.Clipboard.ClipboardPaletteWindow`: fast keyboard-first clip selection and actions.
- `TasksList.App.Shell.TrayService` and `GlobalHotkeyService`: recovery-safe app lifecycle and shortcuts.
- `TasksList.App.Library`: real-data tabs for Notes, Clipboard, Contexts, and Extensions.

## Verification requirements

Automated tests must prove:

- title-display pointer input requests a drag while title-edit input does not;
- opacity clamps to 20–100 and modifier-wheel steps exactly 5%;
- all appearance fields round-trip through SQLite and older databases receive defaults;
- movement/resize state restores and is clamped to available monitors;
- snapping respects tolerance and Alt bypass;
- lock blocks movement/edit/resize, while ghost changes hit testing without changing lock;
- sleep/reminder lifecycle transitions are deterministic using an injected clock;
- soft delete and 30-day Trash retention are correct;
- clipboard favorite/used/filter/duplicate-promotion behavior persists;
- paste suppresses self-recapture and restores the target context;
- every global hotkey conflict produces a visible actionable error;
- interactive block edits produce valid updated Markdown;
- all three bundled plugins still validate and load.

Installed-artifact verification must additionally exercise a real sticky window:

1. Create a note, drag it from the displayed title, resize it, set opacity to 65%, choose Graphite, pin and roll it.
2. Restart the installed app and verify all of those values visually and in the installed database.
3. Open the clipboard palette with Ctrl+Shift+V, filter the captured test clip, favorite it, create a note, and verify provenance.
4. Enable ghost mode, recover it from the tray, and verify mouse interaction returns.
5. Confirm no decorative application rows or corrupted glyphs remain.

## Delivery order

1. Durable presentation model, migration, title drag/edit fix, position/size restore.
2. Quick customization, opacity controls/HUD, presets, typography, lock/roll/pin persistence, snapping.
3. Tray, global hotkeys, hide/show, ghost recovery, sleep/reminders, archive/Trash.
4. Clipboard palette, favorites/used state, paste workflows, filters, multiselect, assignments.
5. Interactive blocks, library cleanup, settings, accessibility, final visual polish.
6. Self-contained rebuild, reinstall, and real installed-interaction verification.

