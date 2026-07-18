# Task'sList

Task'sList is a local-first Windows sticky-note and clipboard utility. Notes can stay always on top, attach to application contexts, render Markdown, and turn copied or captured material into contextual working notes.

## Included in this build

- independent always-on-top sticky windows with autosave and roll-up;
- rich Markdown preview for headings, tasks, tables, and fenced code;
- drag-and-drop `.md` and `.markdown` import;
- editable file-based themes;
- effectively unlimited SQLite clipboard history with plain, HTML, and RTF representations;
- source window provenance and full-text search storage;
- hierarchical Places, nested manual groups, and browser-session models;
- Chrome/Edge tab and conversation companion with private-window exclusion;
- region capture routed into a context-attached note;
- permissioned plugin SDK and three bundled plugins:
  - Browser Context;
  - Developer Workspace;
  - Capture Workflows.

## Browser companion

The native bridge is installed automatically. To enable live tabs during development, open `chrome://extensions` or `edge://extensions`, enable Developer mode, choose **Load unpacked**, and select the installed `browser-extension` folder. The fixed extension ID is `fjgjagcnipdddcimgbohdpahbkakmnie`.

The extension receives tab identity only. It does not read page bodies, form values, passwords, cookies, history, or private windows.

## Theme customization

The active theme is copied to `%LOCALAPPDATA%\TasksList\themes\active\theme.json`. Edit its semantic hexadecimal color tokens and restart Task'sList. Invalid themes fall back safely to the bundled theme.

## Build

Run `powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1`. The self-contained release is written to `artifacts\release`.

