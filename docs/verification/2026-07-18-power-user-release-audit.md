# Task'sList 1.1.0 Power-User Release Audit

**Date:** 2026-07-18  
**Artifact source commit:** `7618fe0ed4bdab9bc0da74e7c63233771af0810b`  
**Installed executable SHA-256:** `8ff2d34bf00df0702e7f9caf4ef56e7b530a30859a42707fc593bcf034bec944`

## Release evidence

- Release test suite: 118 passing (`57 Core + 41 App + 11 Infrastructure + 9 Plugin SDK`), zero failures.
- Self-contained `win-x64` release: 492 checksum entries, zero mismatches.
- Packages: three installed plugin manifests/entry points and three `.taskplugin` packages.
- Per-user installation: executable hash matched staged artifact; Start Menu shortcut, 1.1.0 uninstall entry, Chrome native host, Edge native host, browser companion, theme, and three plugin directories verified.
- Installed database probe: Graphite sticky at 65% opacity, topmost, rolled, persistent Preview mode, favorite clipboard capture with exact text, converted clipboard note with source provenance, and three valid plugins.
- Installed UI automation: F2 title edit, Markdown entry, title-header drag from `(2570,246)` to `(2642,300)`, resize from `360×330` to `440×380`, opacity slider, Graphite swatch, pin toggle, roll shortcut, palette search, favorite, Make note, ghost confirmation, global ghost recovery, sleep state, scheduler wake, and interactive preview.
- Live screenshots inspected:
  - `artifacts/verification/final-installed-library.png`
  - `artifacts/verification/final-installed-sticky-preview.png`
  - `artifacts/verification/final-installed-clipboard-palette.png`
- Source scans: zero mojibake byte-pattern matches and zero decorative Microsoft Edge, Docker Desktop, or Developer workspace UI rows.
- Display environment: both attached monitors reported 1920×1080 at 96 DPI. Live screenshots therefore cover 100% scaling; per-monitor DPI conversion, monitor rescue, and work-area placement are covered by automated policies/tests. No display scaling setting was changed during verification.

## 36-requirement audit

| # | Status | Evidence |
|---:|:---:|---|
| 1 | Pass | Displayed title/non-control header calls drag; installed title drag changed persisted coordinates. |
| 2 | Pass | F2/double-click temporary editor with Enter, Escape, and focus-loss semantics; UI automation renamed installed note. |
| 3 | Pass | Debounced bounds persistence and installed restart/database probe. |
| 4 | Pass | Monitor work-area enumeration and restore clamping tests. |
| 5 | Pass | Screen/note snapping, settings-driven 0–40 tolerance, and Alt bypass tests. |
| 6 | Pass | Toolbar/menu/Ctrl+M roll with expanded-height persistence; installed roll restart/probe. |
| 7 | Pass | Lock prevents edit, drag, and resize; Ctrl+L and controller tests. |
| 8 | Pass | Ctrl+D duplicates presentation with 24-DIP offset. |
| 9 | Pass | Archive/hide, soft-delete Trash, restore, 30-day gate, confirmation, and cascade-safe permanent delete. |
| 10 | Pass | Ctrl+N/Ctrl+Shift+N create 24-DIP-offset notes beside the active sticky. |
| 11 | Pass | Live Customize popup; no restart. |
| 12 | Pass | Butter, Peach, Mint, Sky, Lavender, Rose, Graphite, Glass policies and controls. |
| 13 | Pass | Custom background/text/accent inputs, contrast ratio warning, apply, reset. |
| 14 | Pass | 20–100 active/inactive opacity sliders with percentage values and clamping tests. |
| 15 | Pass | Ctrl+wheel/Plus/Minus 5% steps and temporary HUD; exact-step tests. |
| 16 | Pass | Font family/size/weight/spacing plus durable Edit/Preview mode per note. |
| 17 | Pass | Compact, Comfortable, Spacious density changes. |
| 18 | Pass | Shadow, corner, border, and paper texture controls/persistence. |
| 19 | Pass | Always, Hover, Focused, Hidden toolbar modes with coordinated reduced-motion-aware fade. |
| 20 | Pass | Named styles, default style, per-note apply, and library multi-selection bulk apply. |
| 21 | Pass | Pin cue, toolbar/menu, Ctrl+Shift+T, and installed topmost probe. |
| 22 | Pass | Confirmed Win32 click-through plus tray/global recovery; installed enable/recovery probe. |
| 23 | Pass | Active/inactive opacity and 120 ms transition; reduced-motion direct state path. |
| 24 | Pass | Foreground, while-running, remain-visible, and sleep-until-return policies/menu. |
| 25 | Pass | Detach/reattach, persisted real display name, and executable icon extraction. |
| 26 | Pass | 15 minutes, 1 hour, tomorrow, next workday, and validated custom date/time. |
| 27 | Pass | Configurable sound/pulse/topmost reminder attention, banner acknowledgement, deterministic lifecycle tests, and installed scheduler wake exercise. |
| 28 | Pass | Ctrl+Shift+V registered and palette opens near pointer with monitor clamping. |
| 29 | Pass | Favorite, used, unfiled, source, type, and date filters. |
| 30 | Pass | Original/plain paste, copy, edit, rename, duplicate, favorite, soft delete, and note conversion. |
| 31 | Pass | Extended selection, joined paste/note, and bulk Place/note assignment/favorite/delete. |
| 32 | Pass | Process/window/URL/time/formats/size and assignments retained/rendered; installed provenance verified. |
| 33 | Pass | Pause, per-app exclusions, private-format suppression, maximum-size policy, and settings UI. |
| 34 | Pass | Formatting-aware duplicate hash and optional promotion tests/persistence. |
| 35 | Pass | Exact tray commands and configurable global hotkeys, conflict validation, startup registration, ghost invariant. |
| 36 | Pass | Real-data tabbed library and explanatory empty states; source scan and installed screenshots show no fake rows or corrupted glyphs. |

## Additional delivered capability

Portable `progress`, `counter`, and `timer` directives render as local controls. Progress/counter edits preserve unrelated Markdown byte-for-byte; timer runtime is stored separately in SQLite. Graphite/high-contrast rendering uses the note's ink and accent instead of hard-coded light-paper colors.
