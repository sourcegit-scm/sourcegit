# DevSpaces Screenshot CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic CI screenshots for fork-only DevSpaces features, with an artifact gallery on every pull request and master push.

**Architecture:** Use Avalonia headless rendering for deterministic fork-only DevSpaces UI scenarios and keep the capture catalog explicit so upstream screens are excluded. A Windows GitHub Actions job builds/runs the screenshot tests, generates a manifest/gallery, checks that fork-specific DevSpaces UI changes are represented, and uploads the whole screenshot directory as one artifact.

**Tech Stack:** .NET 10, xUnit, Avalonia 11.3.20, Avalonia.Headless.XUnit, Skia, GitHub Actions, PowerShell.

**Spec:** Approved chat design from 2026-08-29: hybrid fork-only DevSpaces screenshot CI with gallery artifact and upstream-diff audit.

## Global Constraints

- Capture only features implemented in `dhhieu113pro/sourcegit`, not generic upstream SourceGit screens.
- Include DevSpaces terminal/profile, Files explorer, Ctrl+P/file navigation, per-tab state, and worktree base badge surfaces where deterministic fixtures exist.
- Produce PNG screenshots plus `manifest.json`, `index.html`, and `fork-devspaces-diff.txt`.
- Run on pull requests and pushes to `master`.
- Do not require secrets.

---

### Task 1: Screenshot catalog contract

**Files:**
- Create: `tests/SourceGit.Tests/DevSpacesScreenshotCatalogTests.cs`
- Create: `tests/SourceGit.Tests/DevSpacesScreenshotCatalog.cs`

**Interfaces:**
- Produces: `DevSpacesScreenshotCatalog.All`, an immutable list of scenario ids, titles, categories, and fork-owned source paths.

- [ ] **Step 1:** Add tests asserting unique ids, required categories, and fork-owned source-path coverage.
- [ ] **Step 2:** Run the tests and verify failure because the catalog type is missing.
- [ ] **Step 3:** Add the minimum catalog implementation.
- [ ] **Step 4:** Re-run the tests and verify they pass.

### Task 2: Headless screenshot renderer

**Files:**
- Modify: `tests/SourceGit.Tests/SourceGit.Tests.csproj`
- Create: `tests/SourceGit.Tests/DevSpacesScreenshotTests.cs`
- Create: `tests/SourceGit.Tests/DevSpacesScreenshotApp.cs`
- Create: `tests/SourceGit.Tests/DevSpacesScreenshotRenderer.cs`

**Interfaces:**
- Produces: PNG files in `artifacts/devspaces-screenshots` and a JSON manifest entry for every catalog scenario.

- [ ] **Step 1:** Add a failing test that renders a minimal deterministic DevSpaces fixture and asserts a non-empty PNG.
- [ ] **Step 2:** Run the test and verify failure because the renderer is missing.
- [ ] **Step 3:** Add Avalonia headless test package/configuration and the minimal renderer.
- [ ] **Step 4:** Re-run the screenshot test and verify PNG output.

### Task 3: Fork-only coverage audit and gallery

**Files:**
- Create: `scripts/devspaces-screenshots.ps1`
- Create: `tests/SourceGit.Tests/DevSpacesScreenshotAuditTests.cs`

**Interfaces:**
- Consumes: `DevSpacesScreenshotCatalog.All` and generated PNGs.
- Produces: `fork-devspaces-diff.txt`, `manifest.json`, and `index.html` in the artifact folder.

- [ ] **Step 1:** Add tests for audit matching and gallery escaping/order.
- [ ] **Step 2:** Verify tests fail because audit/gallery helpers do not exist.
- [ ] **Step 3:** Implement the helpers and PowerShell orchestration.
- [ ] **Step 4:** Verify tests pass and the script fails when a catalog screenshot is missing.

### Task 4: GitHub Actions workflow

**Files:**
- Create: `.github/workflows/devspaces-screenshots.yml`

**Interfaces:**
- Produces: `devspaces-screenshots-<sha>` workflow artifact.

- [ ] **Step 1:** Add workflow validation assertions to the existing audit tests.
- [ ] **Step 2:** Verify they fail before the workflow exists.
- [ ] **Step 3:** Add a Windows workflow that checks out full history/submodules, installs .NET 10, runs the screenshot script, and uploads the artifact.
- [ ] **Step 4:** Run the full test suite and inspect the workflow YAML.

### Task 5: Verification and PR

- [ ] **Step 1:** Run `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj -c Release -p:DisableAOT=true`.
- [ ] **Step 2:** Run `pwsh ./scripts/devspaces-screenshots.ps1` on Windows or validate script/tests when Windows rendering is unavailable locally.
- [ ] **Step 3:** Review changed files against the approved scope.
- [ ] **Step 4:** Open a pull request into `master` with CI behavior and artifact contents documented.
