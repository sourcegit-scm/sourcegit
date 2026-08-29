# Worktree Base Branch Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a compact base-branch badge on worktree launcher tabs, using red for `develop`, pink for `master`, and orange for `release/*`.

**Architecture:** Add a small pure helper that classifies branch names and ranks base-branch candidates, then expose resolved base-branch metadata from `LauncherPage`. New worktrees persist their exact creation base in SourceGit metadata; existing worktrees fall back to Git ancestry inference and cache the result per open page. The launcher XAML renders the badge only for worktree tabs with a resolved base branch.

**Tech Stack:** .NET 10, Avalonia 11, CommunityToolkit.Mvvm, Git CLI, xUnit tests.

**Spec:** Approved conversation design on 2026-08-29.

## Global Constraints

- Preserve `Node.Name` as the primary tab label.
- Show the badge only for worktrees, never normal repository tabs.
- `develop` is red, `master` is pink, `release/*` is orange; matching is case-insensitive.
- Tooltip text is `Based on <branch>`.
- Do not show a badge when no base branch can be resolved.
- Persist exact base branch for newly created worktrees; infer existing worktrees from Git ancestry.
- Cache the resolved value for an open tab and refresh it when repository branch/worktree metadata changes.

---

### Task 1: Base-branch classification and inference rules

**Files:**
- Create: `tests/SourceGit.Tests/SourceGit.Tests.csproj`
- Create: `tests/SourceGit.Tests/WorktreeBaseBranchTests.cs`
- Create: `src/Models/WorktreeBaseBranch.cs`
- Modify: `SourceGit.slnx`

**Interfaces:**
- Produces: `WorktreeBaseBranch.GetKind(string)` and `WorktreeBaseBranch.SelectBestCandidate(IEnumerable<Candidate>)`.
- Produces: stable branch-kind values consumed by the launcher badge converter/template.

- [ ] **Step 1: Write failing tests** for case-insensitive classification of `develop`, `master`, and `release/*`, plus candidate ranking that prefers the nearest ancestor and returns empty when no supported branch exists.
- [ ] **Step 2: Run** `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj` and verify the tests fail because `WorktreeBaseBranch` does not exist.
- [ ] **Step 3: Implement minimal pure logic** in `src/Models/WorktreeBaseBranch.cs` to make those tests pass.
- [ ] **Step 4: Run** `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj` and verify PASS.
- [ ] **Step 5: Commit** the test project and helper.

### Task 2: Persist exact base branch for new worktrees

**Files:**
- Create: `src/Models/WorktreeMetadata.cs`
- Create: `src/Commands/WorktreeBaseBranch.cs`
- Modify: `src/ViewModels/AddWorktree.cs`
- Test: `tests/SourceGit.Tests/WorktreeMetadataTests.cs`

**Interfaces:**
- Produces: metadata read/write helpers keyed by linked-worktree Git directory.
- Consumes: the selected local/tracking branch already available in `AddWorktree`.

- [ ] **Step 1: Write failing tests** for metadata serialization and normalization of the base branch name.
- [ ] **Step 2: Run the focused tests** and verify RED.
- [ ] **Step 3: Implement metadata read/write** using a small SourceGit-owned JSON file inside the linked worktree Git directory.
- [ ] **Step 4: Update `AddWorktree.Sure()`** so successful worktree creation stores the exact selected creation base before opening the tab.
- [ ] **Step 5: Run focused tests** and verify GREEN.
- [ ] **Step 6: Commit** the persistence slice.

### Task 3: Resolve and cache base branch for open worktree tabs

**Files:**
- Modify: `src/ViewModels/LauncherPage.cs`
- Modify: `src/ViewModels/Launcher.cs`
- Create or modify: `src/Commands/WorktreeBaseBranch.cs`
- Test: `tests/SourceGit.Tests/WorktreeBaseBranchTests.cs`

**Interfaces:**
- Produces: `LauncherPage.BaseBranch`, `LauncherPage.BaseBranchKind`, and `LauncherPage.IsWorktree`.
- Resolution order: persisted metadata first; otherwise inspect supported local branches and choose the nearest ancestor by Git merge-base distance.

- [ ] **Step 1: Write failing tests** for candidate selection edge cases used by Git ancestry results.
- [ ] **Step 2: Run focused tests** and verify RED.
- [ ] **Step 3: Implement Git queries** to detect whether the opened repository path is a linked worktree, load persisted metadata, or compute candidate distances.
- [ ] **Step 4: Populate/cache launcher-page properties** when opening a repository tab, and refresh after relevant repository metadata/branch refresh events.
- [ ] **Step 5: Run focused tests and build** with `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj` and `dotnet build SourceGit.slnx -p:DisableAOT=true`.
- [ ] **Step 6: Commit** the resolution slice.

### Task 4: Render the right-side colored badge

**Files:**
- Modify: `src/Views/LauncherTabBar.axaml`
- Create or modify: `src/Converters/WorktreeBaseBranchConverters.cs`
- Test: `tests/SourceGit.Tests/WorktreeBaseBranchTests.cs`

**Interfaces:**
- Consumes: `LauncherPage.BaseBranch`, `LauncherPage.BaseBranchKind`, `LauncherPage.IsWorktree`.
- Produces: compact badge to the right of `Node.Name`, before the close button.

- [ ] **Step 1: Write failing converter/classification assertions** for the three requested visual families.
- [ ] **Step 2: Run tests** and verify RED.
- [ ] **Step 3: Implement badge styling** with compact padding/corner radius, tooltip `Based on {BaseBranch}`, and requested colors.
- [ ] **Step 4: Run tests and build** and verify GREEN.
- [ ] **Step 5: Commit** the UI slice.

### Task 5: Full verification and PR

**Files:**
- Verify all changed files.

**Interfaces:**
- Produces: merge-ready pull request from `feature/worktree-base-branch-tab` to `master`.

- [ ] **Step 1: Run** `dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj`.
- [ ] **Step 2: Run** `dotnet build SourceGit.slnx -p:DisableAOT=true`.
- [ ] **Step 3: Open a pull request** summarizing persistence, inference fallback, caching, and badge colors.
- [ ] **Step 4: Verify required GitHub checks** and report any blocker without merging automatically.
