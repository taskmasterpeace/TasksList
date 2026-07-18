# Task'sList Visual Identity and GitHub Publication Design

**Date:** 2026-07-18  
**Status:** Approved  
**Product:** Task'sList 1.1.0  
**Repository:** `taskmasterpeace/TasksList`

## Purpose

Task'sList needs an identity that is recognizable in a 16-pixel Windows tray slot, feels native to the warm-charcoal application, and scales into polished GitHub artwork. The repository should present the actual local-first Windows product rather than a speculative concept.

## Explored directions

### 1. Literal sticky and checkmark

A single yellow note with a checkmark is immediately understandable, but it is visually generic and does not express application attachment, clipboard history, or extensibility.

### 2. Layered context cards — selected

Two offset note cards form a compact stack: a violet rear context card and a warm amber foreground note with a coral folded corner. A four-point action spark provides the AI and automation cue without implying that AI is required. This direction remains readable at tray size while representing notes, history, context, and plugins.

### 3. Monogram command tile

A typographic `TL` or `T` tile would be extremely legible but would repeat the current placeholder-icon problem and communicate little about the product.

## Identity system

The primary mark is a rounded charcoal app tile containing the layered context cards. The front card uses the product's paper color, the rear card uses a restrained violet accent, the fold uses the existing coral-orange accent, and the action spark uses warm cream. Geometry is intentionally bold, with no hairlines or text inside the icon.

The wordmark pairs the mark with `Task'sList` and the line `Your work, right where it belongs.` The apostrophe remains part of the product name. The GitHub repository name omits punctuation and is exactly `TasksList`.

### Palette

| Token | Value | Purpose |
|---|---:|---|
| Charcoal canvas | `#17191B` | Application tile and social-card background |
| Graphite panel | `#252A2E` | Elevated surfaces |
| Paper amber | `#F4CE62` | Foreground sticky |
| Coral action | `#F19A4B` | Fold, CTA, active state |
| Context violet | `#8B6CF6` | Rear card and plugin/context cue |
| Mint status | `#72C29B` | Small positive status accents |
| Warm cream | `#F6F2EA` | Wordmark and spark |

## Asset matrix

All master artwork is deterministic SVG stored under `assets/brand/`. Raster exports are derived from those masters so future releases can reproduce them.

- `logo-mark.svg`: square master mark with safe padding.
- `wordmark-horizontal.svg`: mark, product name, and tagline.
- `github-social-preview.svg` and `.png`: 1280×640 repository social card with exact text.
- `app-icon-16.png`, `app-icon-24.png`, `app-icon-32.png`, `app-icon-48.png`, `app-icon-64.png`, `app-icon-128.png`, `app-icon-256.png`, and `app-icon-512.png`.
- `TasksList.ico`: multi-resolution Windows icon used by the executable and notification-area icon.
- `README` hero and current-product screenshots displayed from tracked documentation assets.

## Application integration

`TasksList.App.csproj` declares `ApplicationIcon` and copies the ICO into the build. `TrayService` loads the branded icon from the executable instead of `SystemIcons.Application`, while retaining the system icon as a defensive fallback. Installer and release output inherit the executable icon.

The integration does not change note data, clipboard behavior, hotkeys, or plugin permissions. Brand assets are presentation-only and remain usable offline.

## Repository presentation

The README begins with the centered wordmark and a concise product promise, followed by a current application screenshot. The existing feature, privacy, shortcut, browser companion, theme, plugin, and build documentation remains intact. A `docs/BRAND.md` file documents asset usage and color values. The GitHub repository description is:

> Local-first Windows sticky notes, infinite clipboard history, contextual app/tab attachments, Markdown, capture tools, and plugins.

The repository is public, uses `master` as its initial default branch, and receives topics for Windows, WPF, sticky notes, clipboard management, Markdown, productivity, and plugins. No license is invented; the repository remains without a license until the owner chooses one.

## Validation

Automated checks verify that the SVG masters contain view boxes and accessible titles, every required raster size has exact pixel dimensions, the ICO contains all required frames, the WPF project declares the icon, and the release includes the branded executable. The full existing Release test suite and release builder run after integration. Visual inspection covers the master mark, wordmark, social card, README rendering, executable icon, tray icon, and installed-app launch.

## Failure handling

If a raster tool is unavailable, a repository-local deterministic conversion path is used rather than checking in hand-edited binary variants. If loading the branded tray icon fails, Task'sList falls back to the Windows application icon and records no persistent error. GitHub repository creation occurs only after the working tree is clean, tests pass, and the intended commit is present.
