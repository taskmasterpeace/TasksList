# Task'sList Brand and GitHub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a scalable Task'sList visual identity, integrate it into the Windows application and documentation, verify the release, and publish the project as `taskmasterpeace/TasksList` on GitHub.

**Architecture:** Deterministic SVG masters under `assets/brand` are the source of truth. A small PowerShell exporter produces fixed-size PNG and multi-frame ICO outputs, WPF consumes the ICO for the executable and tray, and tracked documentation consumes the SVG/PNG assets directly. GitHub publication happens only after fresh local verification.

**Tech Stack:** SVG, PowerShell, .NET 8 WPF, System.Drawing, xUnit, Git, GitHub CLI

## Global Constraints

- Preserve the product name `Task'sList`; name the GitHub repository exactly `TasksList`.
- Use the approved layered-context-card mark and the palette specified in the design.
- Keep every brand asset local, deterministic, and free of external font or image dependencies.
- Preserve all existing note, clipboard, context, capture, browser, theme, and plugin behavior.
- Do not add a software license without an explicit owner choice.
- Publish publicly under the authenticated `taskmasterpeace` account.

---

### Task 1: Brand masters and deterministic exports

**Files:**
- Create: `assets/brand/logo-mark.svg`
- Create: `assets/brand/wordmark-horizontal.svg`
- Create: `assets/brand/github-social-preview.svg`
- Create: `scripts/export-brand-assets.ps1`
- Create: `tests/TasksList.App.Tests/Branding/BrandAssetTests.cs`
- Create: generated PNG and ICO files under `assets/brand/generated/`

**Interfaces:**
- Consumes: approved palette and layered-card geometry from the design specification.
- Produces: `assets/brand/generated/TasksList.ico` and exact-size PNG exports for WPF, docs, and GitHub.

- [ ] **Step 1: Write the failing brand contract test**

Add an xUnit test that locates the repository root, asserts that all three SVG masters exist with a `viewBox` and `<title>`, asserts exact PNG dimensions of 16, 24, 32, 48, 64, 128, 256, and 512 pixels, and verifies the ICO frame directory contains 16, 24, 32, 48, 64, 128, and 256-pixel entries.

- [ ] **Step 2: Run the focused test and verify RED**

Run: `.\.tools\dotnet\dotnet.exe test tests\TasksList.App.Tests\TasksList.App.Tests.csproj -c Release --filter FullyQualifiedName~BrandAssetTests`

Expected: FAIL because `assets/brand/logo-mark.svg` is absent.

- [ ] **Step 3: Create the SVG masters**

Implement the square mark with bold no-text geometry, the horizontal wordmark with embedded vector-safe font fallbacks, and the 1280×640 social card with exact product copy and decorative contextual note cards.

- [ ] **Step 4: Implement and run deterministic export**

`scripts/export-brand-assets.ps1` must resolve the repository root, validate all output paths remain under `assets/brand/generated`, render each requested PNG size, compose a multi-frame ICO, and fail on missing or malformed output. Run it once to create the tracked exports.

- [ ] **Step 5: Run the focused test and verify GREEN**

Run the focused command from Step 2.

Expected: PASS with one brand-contract test assembly and zero failures.

- [ ] **Step 6: Commit the identity assets**

Run: `git add assets/brand scripts/export-brand-assets.ps1 tests/TasksList.App.Tests/Branding/BrandAssetTests.cs && git commit -m "feat: add Task'sList visual identity"`

### Task 2: Windows executable and tray branding

**Files:**
- Modify: `src/TasksList.App/TasksList.App.csproj`
- Modify: `src/TasksList.App/Shell/TrayService.cs`
- Create: `tests/TasksList.App.Tests/Shell/TrayIconLoaderTests.cs`

**Interfaces:**
- Consumes: `assets/brand/generated/TasksList.ico`.
- Produces: branded executable resources and `TrayIconLoader.Load(string executablePath): Icon` with a safe fallback.

- [ ] **Step 1: Write a failing tray icon test**

Assert that `TrayIconLoader.Load` returns a multi-size icon for a valid Task'sList executable/icon path and returns a non-null Windows fallback for a missing path.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `.\.tools\dotnet\dotnet.exe test tests\TasksList.App.Tests\TasksList.App.Tests.csproj -c Release --filter FullyQualifiedName~TrayIconLoaderTests`

Expected: FAIL because `TrayIconLoader` does not exist.

- [ ] **Step 3: Implement executable and tray integration**

Declare `<ApplicationIcon>..\..\assets\brand\generated\TasksList.ico</ApplicationIcon>` and add the icon as linked content. Implement the loader using `Icon.ExtractAssociatedIcon` with a cloned icon and `SystemIcons.Application` fallback, then replace the placeholder assignment in `TrayService`.

- [ ] **Step 4: Run focused and complete app tests**

Run the focused command from Step 2, then `.\.tools\dotnet\dotnet.exe test tests\TasksList.App.Tests\TasksList.App.Tests.csproj -c Release`.

Expected: all tests PASS with zero failures.

- [ ] **Step 5: Commit Windows integration**

Run: `git add src/TasksList.App tests/TasksList.App.Tests && git commit -m "feat: brand the Windows app and tray"`

### Task 3: Repository artwork and documentation

**Files:**
- Modify: `README.md`
- Create: `docs/BRAND.md`
- Create: `docs/assets/taskslist-library.png`
- Create: `docs/assets/taskslist-sticky.png`
- Create: `docs/assets/taskslist-clipboard.png`

**Interfaces:**
- Consumes: brand masters and verified installed screenshots.
- Produces: GitHub-renderable documentation with no ignored artifact dependencies.

- [ ] **Step 1: Add tracked documentation assets**

Copy the three already verified installed screenshots into `docs/assets` without altering their pixels or content.

- [ ] **Step 2: Upgrade the README presentation**

Add a centered wordmark, concise value proposition, platform/local-first badges, the current library screenshot, a focused “Why Task'sList” section, and links to the brand guide while preserving technical instructions.

- [ ] **Step 3: Document brand usage**

Record the mark variants, palette, minimum icon sizes, clear-space rule, file inventory, and the export command in `docs/BRAND.md`.

- [ ] **Step 4: Verify Markdown asset references**

Run: `rg -n "assets/brand|docs/assets" README.md docs/BRAND.md` and confirm each referenced file exists.

- [ ] **Step 5: Commit documentation**

Run: `git add README.md docs/BRAND.md docs/assets && git commit -m "docs: add branded GitHub presentation"`

### Task 4: Release validation and GitHub publication

**Files:**
- Modify if required by validation: `scripts/build-release.ps1`
- Generated and ignored: `artifacts/release/`

**Interfaces:**
- Consumes: branded source tree and all existing release components.
- Produces: verified local release and public `https://github.com/taskmasterpeace/TasksList` repository.

- [ ] **Step 1: Run the full test suite**

Run: `.\.tools\dotnet\dotnet.exe test TasksList.sln -c Release --no-restore`

Expected: all projects PASS with zero failures.

- [ ] **Step 2: Build and validate the release**

Run: `powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1`

Expected: exit code 0; `artifacts/release/app/TasksList.App.exe` has the branded icon; every line in `checksums.sha256` matches; all three `.taskplugin` packages exist.

- [ ] **Step 3: Inspect the final diff and working tree**

Run: `git status -sb`, `git diff --check`, and `git log --oneline -8`. Commit any release-script correction separately, then require a clean working tree.

- [ ] **Step 4: Create and push the public repository**

Run: `gh repo create TasksList --public --source=. --remote=origin --push --description "Local-first Windows sticky notes, infinite clipboard history, contextual app/tab attachments, Markdown, capture tools, and plugins."`

Expected: repository created as `taskmasterpeace/TasksList`, `origin` configured, and `master` pushed with tracking.

- [ ] **Step 5: Configure repository metadata**

Run: `gh repo edit taskmasterpeace/TasksList --add-topic windows --add-topic wpf --add-topic sticky-notes --add-topic clipboard-manager --add-topic markdown --add-topic productivity --add-topic plugins`

Expected: repository description, visibility, default branch, and topics match the design.

- [ ] **Step 6: Verify remote state**

Run: `gh repo view taskmasterpeace/TasksList --json nameWithOwner,url,visibility,description,defaultBranchRef,repositoryTopics` and `git status -sb`.

Expected: public repository URL resolves, remote default branch is `master`, expected topics are present, and the local branch is synchronized with `origin/master`.
