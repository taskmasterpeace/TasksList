# Task'sList Product and Technical Design

**Date:** July 18, 2026  
**Status:** Proposed for implementation  
**Platform:** Windows 10 and Windows 11  

## Product definition

Task'sList is a local-first Windows sticky-note and clipboard utility. At its core it must feel as immediate as Zhorn Stickies: create a note in one gesture, leave it on the desktop, keep it above other windows, attach it to a particular application or window, and dismiss it when it is no longer useful.

The same application keeps an effectively unbounded, searchable clipboard history inspired by ClipAngel and offers configurable capture workflows inspired by ShareX. These capabilities support the sticky-note experience; they do not turn Task'sList into a general knowledge-management suite.

The defining promise is:

> Anything copied, captured, or written can become a sticky note that remembers where it belongs.

## Product principles

1. **Sticky notes first.** The fastest path through the application is always creating, showing, hiding, attaching, and completing a note.
2. **Context without lock-in.** Notes can attach to applications, windows, browser tabs, conversations, projects, or files, but remain readable and exportable without those integrations.
3. **Local-first and private by default.** Notes and clipboard content stay on the computer unless the user explicitly configures a destination or cloud model.
4. **Power without mandatory complexity.** Basic note and paste behavior is immediate. Workflows, plugins, AI, scripting, and detailed retention controls are available when requested.
5. **Files are interfaces.** Markdown, theme files, plugin manifests, and exported records are human-readable and portable.
6. **Every automated mutation is inspectable.** Plugin and AI changes identify their author and can be undone.

## Core experience

### Sticky notes

Users can create a note from a global shortcut, tray menu, command palette, clipboard item, screen capture, dragged file, or plugin action.

Each note supports:

- free placement and resizing on one or multiple monitors;
- always-on-top, desktop-level, and ordinary-window modes;
- roll-up to a title bar, collapse, hide, archive, delete, lock, and click-through modes;
- configurable color, opacity, shadow, typography, and theme;
- reminders, alarms, sleep-until times, and recurrence;
- rich text, Markdown, checklists, tables, links, images, code blocks, and syntax highlighting;
- embedded capture objects and plugin-provided interface blocks;
- attachment to an application, window, tab, conversation, project, file, URL, or domain;
- an activity trail for automated edits;
- export as Markdown, HTML, plain text, image, or a portable Task'sList bundle.

When an attached context appears, its notes return at their saved relative positions. When the context disappears, those notes hide or sleep according to the user's rule. Rebinding is offered when a window identity changes but its application, project, URL, or document still matches.

### Rich text and Markdown

Markdown is the canonical portable representation for textual note content. The primary editor is a rich-text surface with an optional source view.

The editor supports CommonMark plus GitHub-style tables, task lists, strikethrough, fenced code blocks, and autolinks. Application-specific metadata is stored outside Markdown so exporting a note never adds proprietary control syntax to the document.

Dragging a `.md` or `.markdown` file into the application produces a correctly formatted preview. The default action imports a safe independent note and retains the source path in its provenance. The drop menu also offers **Link and sync**, **Split by top-level headings**, and **Attach to current note**. Linked files use atomic writes, preserve valid front matter and unsupported Markdown nodes, detect external changes, and ask before resolving conflicts.

Checkboxes are interactive and serialize back to `- [ ]` and `- [x]`. Headings beginning with `#` render as headings rather than raw punctuation. Code fences preserve their language identifier.

### Clipboard history

Task'sList records clipboard changes continuously while it is running, subject to exclusion and retention rules. The design target is effectively unlimited history: no fixed item count, with configurable age and storage quotas. Metadata and searchable text live in SQLite; large binary payloads use content-addressed files with deduplication.

Clipboard entries can preserve:

- Unicode and plain text;
- HTML and rich text;
- Markdown when detected or explicitly copied;
- images and image metadata;
- copied files and file lists;
- source application, process, window identity, timestamp, and available clipboard formats;
- derived OCR text, tags, favorites, paste count, and last-used time.

Users can search by content, source application, type, date, context, tag, and favorite status. They can paste the original representation, paste without formatting, paste as Markdown, emulate typing, compare two textual entries, pin entries, edit a copy, drag an entry out as a file, or turn an entry into a sticky note.

Password managers, private browser windows, configurable applications, and sensitive field types are excluded by default where Windows exposes reliable signals. Secret-like content is flagged locally, excluded from cloud processing, and can receive a short automatic expiry.

### Screen capture

The first release includes focused screen capture rather than the entire ShareX feature set:

- region, active-window, monitor, and full-desktop screenshots;
- copy, save, annotate, redact, OCR, pin as image note, or attach to context;
- configurable global shortcuts;
- a post-capture action menu;
- plugin-defined actions and destinations.

Screen recording, scrolling capture, uploads, and custom uploader definitions are extension targets rather than core requirements for the first release.

### Context attachment

Core Windows attachment works without plugins at application and top-level-window scope. A context record contains stable and fallback identity signals such as process identity, executable path hash, application user model ID, window class, title pattern, file/project hints, and monitor-relative position.

Plugins can provide richer identities for browser tabs, ChatGPT conversations, Claude Code sessions, repositories, Docker resources, and other application objects.

Each attachment declares its visibility behavior:

- show while the context exists;
- show only while the context is foreground;
- remain visible after the context closes;
- sleep when the context closes and return when it reopens.

## Interface

Task'sList runs primarily from the Windows notification area and global shortcuts. It exposes four surfaces:

1. **Sticky windows:** lightweight independent note windows with minimal chrome and strong keyboard support.
2. **Capture palette:** a fast searchable overlay for clipboard history, captures, commands, and note creation.
3. **Manager:** a full window for desktop, sleeping, recurring, archived, deleted, and context-attached notes; clipboard history; workflows; plugins; themes; permissions; and storage.
4. **Capture overlay:** region/window selection followed by annotation or routing.

The visual direction is a modern interpretation of physical working notes: warm charcoal workspace surfaces, saturated paper colors, crisp editorial typography, tactile layering, restrained motion, and ten-pixel radii. It must avoid generic dashboard styling. Dense power-user views remain compact and keyboard navigable.

## Theme and skin system

Users can change the application's appearance without recompiling it. A theme is a folder or packaged `.tasktheme` archive containing:

- `theme.json` for identity, semantic colors, dimensions, typography, and supported application version;
- optional CSS-like style resources using the documented Task'sList theme token format;
- optional fonts and raster assets with declared licenses;
- optional note templates;
- a preview image.

Themes can style notes, the manager, the palette, and capture overlays through stable semantic tokens. Themes cannot execute code, access files, or request network resources. Invalid values fall back to the base theme, and the user can always reset appearance from a safe-mode launch.

## Plugin system and ecosystem

Plugins are installable `.taskplugin` packages. A package contains a signed manifest, versioned API declaration, capabilities, entry points, settings schema, and optional user-interface contributions.

Plugin extension points include:

- context providers;
- capture providers;
- clipboard transformers;
- note block renderers;
- commands and slash commands;
- workflow triggers and actions;
- importers, exporters, and destinations;
- search providers;
- AI tools using the application's consent and redaction pipeline.

Plugins execute outside the main application process. They communicate through a versioned local RPC contract and receive only capability-scoped data. Installation presents requested permissions. Dangerous capabilities—process execution, unrestricted file access, screen reading, input automation, and network access—are individually declared and can require confirmation per action.

Plugins cannot directly mutate the note database. They propose typed operations through the host, which validates, logs, applies, and makes them undoable. A crashing plugin cannot take down sticky windows or clipboard capture.

The ecosystem includes a local plugin manager and a registry-compatible catalog format. The initial build supports installation from a local package; a hosted public registry, signing service, review process, ratings, and automatic security analysis are later operational work. The SDK includes templates, typed contracts, a test host, packaging tools, and documentation.

## Three showcase plugins

### 1. Browser Context

**Purpose:** Demonstrate deep context attachment and guarded content access.

The plugin connects to Chrome and Edge through a browser extension companion. It recognizes browser windows, tabs, normalized URLs, domains, and supported conversation identities such as individual ChatGPT threads.

Features:

- attach a sticky to the browser, window, domain, exact page, or supported conversation;
- restore notes when the matching tab or conversation returns;
- capture selected text and page title into a note with provenance;
- group clipboard entries by source tab when permission is granted;
- expose only identity metadata by default, with page-content reading as a separate permission;
- block private browsing and configured sensitive domains by default.

### 2. Developer Workspace

**Purpose:** Demonstrate application context, local automation, Markdown, and agent handoffs.

The plugin recognizes Windows Terminal, PowerShell, VS Code, Git repositories, Claude Code sessions, and project directories.

Features:

- attach notes to a repository, branch, terminal session, or Claude Code workspace;
- convert copied terminal output into a formatted, syntax-aware capture;
- turn Markdown checklists into live project stickies;
- create a local `Where I left off` note from user-selected context;
- expose safe read-only project facts such as branch, changed-file count, and recent command exit status;
- offer command-copy buttons while requiring explicit confirmation before execution;
- keep model access optional and redact configured secrets before any cloud request.

### 3. Capture Workflows

**Purpose:** Demonstrate ShareX-style composability and plugin-provided note interfaces.

The plugin provides a visual workflow editor and actions for screenshots, clipboard entries, and dragged files.

Included workflows:

- **Bug evidence:** capture region → annotate/redact → OCR → attach to active application → create a checklist note;
- **Research card:** copy URLs or selected browser content → deduplicate → create a source list with read/unread controls;
- **Markdown task board:** drop Markdown → parse task lists → render progress → optionally sync changes back;
- **Sensitive capture:** detect secret-like content → prevent network destinations → expire the payload on schedule.

The workflow format is declarative JSON. Plugin nodes declare input/output types, permissions, retry behavior, and whether they mutate external state.

## AI behavior

AI is optional and provider-neutral. The core application works without a model or account. AI can suggest transformations, summarize user-selected material, extract tasks, create contextual handoffs, and populate plugin-defined interface blocks.

AI never receives the full clipboard database or unrestricted desktop view. Each request shows or logs the exact selected context, provider, redactions, and resulting note operations. AI-authored changes are visually identified and undoable. Plugins use the same host-controlled AI gateway instead of handling unrestricted credentials.

## Architecture

The recommended implementation is a native Windows application using modern .NET, with Windows-specific integration isolated behind testable services.

Primary components:

- **Task'sList Host:** lifecycle, notification-area integration, shortcuts, settings, command routing, permissions, and plugin supervision;
- **Sticky Window Service:** independent note windows, positioning, always-on-top behavior, click-through, context visibility, and monitor recovery;
- **Editor:** rich-text/Markdown document model, serialization, embeds, undo, and file synchronization;
- **Clipboard Service:** Windows clipboard listener, multi-format ingestion, exclusions, retention, deduplication, and paste routing;
- **Capture Service:** screen selection, image acquisition, annotation, OCR routing, and capture records;
- **Context Service:** foreground-window observation, context identity matching, attachments, and visibility rules;
- **Data Service:** SQLite metadata, full-text search, content-addressed payload storage, migrations, backup, and recovery;
- **Workflow Engine:** typed declarative pipelines, cancellation, audit records, and confirmation boundaries;
- **Plugin Host:** package validation, capability grants, isolated processes, RPC, health monitoring, and version compatibility;
- **Agent Gateway:** optional provider integrations, local redaction, consent, rate limits, and operation validation.

The executable is installable and can also support a portable mode. A single-instance coordinator routes shortcuts and files to the running process. Startup-critical services—sticky restoration and clipboard capture—must not wait for plugins or AI.

## Data model

Principal records:

- `Note`: identity, title, Markdown document, appearance, window state, timestamps, provenance, and lifecycle state;
- `Attachment`: note, context identity, match strategy, visibility rule, and relative placement;
- `Context`: provider, type, stable identity, display metadata, sensitivity, and last seen time;
- `Capture`: kind, representations, source context, timestamps, hashes, derived metadata, and retention policy;
- `Payload`: content hash, media type, storage location, size, encryption state, and reference count;
- `Workflow`: trigger, typed nodes, edges, permissions, and enabled state;
- `Plugin`: package identity, version, compatibility, grants, settings, and health;
- `Activity`: actor, operation, affected records, reason, consent, and undo data.

## Reliability and recovery

- Notes autosave locally after a short debounce and on focus loss.
- Clipboard ingestion is transactional and never blocks the Windows clipboard owner.
- Large payloads stream to disk and are deduplicated by hash.
- Database migrations create a recoverable backup.
- Window positions are clamped to connected monitors after display changes.
- Plugin crashes use bounded restart policies and visible health reporting.
- Safe mode disables plugins and custom themes without hiding user data.
- Deleted notes enter a recoverable trash unless explicitly purged.

## Security boundaries

- Local storage can be encrypted using Windows-protected keys.
- Plugin permissions are deny-by-default and revocable.
- Browser content, screen content, file access, process execution, input automation, network access, and cloud AI are separate capabilities.
- Sensitive applications and domains can be excluded globally.
- Password-like captures are detected locally and never silently uploaded.
- External actions show the responsible plugin and target.
- The host maintains an auditable, user-readable activity log.
- Updates and ecosystem packages require signature verification when installed from the catalog.

## First-release acceptance criteria

The first complete release is successful when a user can:

1. Install or launch a Windows executable and create a sticky through a global shortcut.
2. Move, resize, roll up, color, lock, hide, archive, and keep a note always on top.
3. Attach a note to a running application or window and see it hide and return correctly.
4. Author rich text and Markdown, including correctly rendered `#` headings, tables, code, and interactive checkboxes.
5. Drag in Markdown as an imported or linked note without corrupting the source.
6. Capture and search large clipboard histories containing text, HTML, images, and files.
7. Paste original or plain text, compare clips, favorite clips, and turn a clip into a sticky.
8. Capture and annotate a screen region, OCR it, and attach it to a note or context.
9. Install, disable, update, and uninstall a packaged plugin with visible permissions.
10. Use all three showcase plugins to exercise context, automation, custom blocks, and guarded actions.
11. Install or edit a theme file and recover safely from an invalid theme.
12. Use core notes and clipboard history with every plugin and AI provider disabled.

## Explicit non-goals for the first release

- Obsidian-style backlink graphs or a general personal knowledge base;
- team collaboration and cloud synchronization;
- mobile or macOS clients;
- reproducing every ShareX uploader and recording mode;
- unrestricted autonomous control of applications;
- a public plugin marketplace operated at production scale;
- silently sending clipboard, browser, or screen contents to a cloud model.

## Testing strategy

- Unit tests cover Markdown round-tripping, context matching, retention, deduplication, permission evaluation, workflow typing, and plugin manifest validation.
- Integration tests cover clipboard formats, SQLite migrations, payload recovery, plugin RPC, file synchronization conflicts, and theme fallback.
- Windows UI automation covers note creation, positioning, always-on-top behavior, application attachment, rich editing, capture routing, and keyboard workflows.
- Contract tests run each showcase plugin against supported plugin API versions.
- Fault tests terminate plugins, corrupt temporary theme values, disconnect monitors, lock files, and interrupt clipboard owners to validate recovery.
- Privacy tests verify that exclusions, secret detection, permission revocation, and cloud-request redaction fail closed.

## Delivery sequence

1. Native host, data store, sticky windows, tray, shortcuts, and core manager.
2. Rich-text/Markdown editor, file import/linking, themes, and note lifecycle.
3. Clipboard capture, search, paste behavior, deduplication, retention, and provenance.
4. Application/window context matching and attached-note visibility.
5. Screen capture, annotation, OCR routing, and capture-to-note.
6. Plugin SDK, isolated host, package manager, permissions, and activity log.
7. Browser Context, Developer Workspace, and Capture Workflows plugins.
8. Optional AI gateway, packaging, performance hardening, accessibility, and release validation.

