# Windows 11 Milestone C: System-Responsive Fluent UI Plan

**Goal:** Make every Task'sList shell surface respond to Windows light/dark, accent, high contrast, transparency, focus, and standard dialog conventions while preserving optional user theme-file customization and sticky-paper identity.

**Approach:** Introduce a pure semantic palette policy and a live `WindowsThemeService` that mutates shared brush resources so existing `StaticResource` consumers update without window recreation. Default Task'sList follows Windows; a non-default local theme may override allowed shell/stationery tokens, but never focus, accessibility, or high-contrast tokens. Replace remaining fake framed dialogs with standard/native WPF windows and add visible keyboard focus states.

---

## Task 1: Semantic light, dark, and high-contrast palettes

- Create `src/TasksList.App/Theming/WindowsThemePalette.cs`.
- Create `tests/TasksList.App.Tests/Theming/WindowsThemePaletteTests.cs`.
- Test complete tokens for window, panel, card, hover, divider, accent, accent-soft, text, muted text, success, danger, focus, and disabled.
- Test light/dark differentiation, supplied accent use, readable accent text, and high-contrast mapping to `SystemColors`.
- Implement immutable color policy; commit `feat: define Windows semantic theme palettes`.

## Task 2: Live Windows theme service

- Create `src/TasksList.App/Theming/WindowsThemeService.cs` and tests with a fake environment/resource sink.
- Read AppsUseLightTheme, DWM colorization accent, high contrast, transparency, and system preference events.
- Mutate existing `SolidColorBrush.Color` values rather than replacing resources so open windows update.
- Start the service during `App.OnStartup`, stop during shutdown, and reapply DWM caption treatment to open windows through existing handlers.
- Default bundled theme follows Windows. A theme whose id is not `taskslist-default` may override canvas/panel/card/divider/accent/success and sticky paper/ink; high contrast always wins.
- Commit `feat: follow live Windows theme and accent settings`.

## Task 3: Focus and control-state audit

- Add an XAML contract for visible keyboard focus on `GhostButton`, `PrimaryButton`, tabs, text boxes, list items, toggle buttons, and icon buttons.
- Add semantic `DangerBrush`, `FocusBrush`, `DisabledTextBrush`, and `AccentTextBrush` resources.
- Update templates with distinct hover, pressed, disabled, selected, and keyboard-focus states; keep normal arrow cursors.
- Ensure focus is not communicated only by color and high contrast retains a system focus rectangle.
- Commit `fix: add Windows keyboard focus and control states`.

## Task 4: Native secondary windows and dialogs

- Add source contracts requiring `PluginManagerWindow` and `NewPlaceDialog` to use non-transparent standard frames with native icon/title/system menu and no `DragMove`/custom close button.
- Change Plugin Manager to a resizable standard window and New Place to a standard non-resizable dialog.
- Give Clipboard Palette non-layered `WindowChrome` caption/resize hit testing and DWM transient-window treatment while preserving its compact topmost palette role.
- Leave transparency only on actual WPF `Popup` surfaces and the region-capture overlay.
- Commit `fix: use native chrome across secondary windows`.

## Task 5: Non-modal application feedback

- Generalize the existing bottom capture notice into an `AppNotificationHost` model supporting success, information, warning, action, dismiss, and polite live-region announcements.
- Route settings-saved, copied, attached, archived, restored, and recoverable clipboard errors through the host instead of success message boxes.
- Keep destructive confirmations modal and recovery-safe.
- Add tests for message/action replacement, dismissal, and UI Automation live settings.
- Commit `feat: add native-feeling in-app notifications`.

## Task 6: Release and installed verification

- Run the full Release build and install exact artifact; verify SHA-256.
- Inspect default light/dark/current accent resources, high-contrast fallback policy, and live resource mutation with open main/sticky/dialog windows.
- Verify Tab/Shift+Tab and visible focus across navigation, cards, command buttons, settings, plugin manager, and New Place.
- Verify Plugin Manager/New Place have native captions/system menus and Clipboard Palette has native drag/resize with clickable controls.
- Verify screenshots, note commands, plugins, browser bridge, settings, tray, and reminders regressions remain green.
- Check installed process responsiveness and Windows Application event log.
- Merge, rerun full suite, push master, and remove the feature worktree.

## Completion gate

Milestone C is complete only when semantic palette tests, source contracts, and installed evidence show live system response, keyboard-visible controls, native secondary chrome, non-modal routine feedback, zero new Task'sList runtime errors, and no regression to capture/sticky/plugin behavior.
