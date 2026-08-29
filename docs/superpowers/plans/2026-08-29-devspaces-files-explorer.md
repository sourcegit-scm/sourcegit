# DevSpaces Files Explorer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Files page under DevSpaces that explores the current worktree, searches files/folders, decorates Git changes, previews unchanged files, shows HEAD-to-worktree diffs for changed files, and preserves Files state independently for each worktree tab.

**Architecture:** Extend the repository-path-scoped `ViewModels.DevSpaces` instance with a `DevSpaceFiles` child view model. `DevSpaceFiles` owns search, selected path, expanded tree nodes, workspace enumeration, Git status overlay and selected-file detail. The existing `DevSpaceRegistry` remains the lifetime boundary, so switching launcher tabs does not recreate or reset Files state. Changed files reuse `DiffContext`/`DiffView`; unchanged text files use a small read-only workspace-file viewer.

**Tech Stack:** .NET 10, C#, Avalonia 11, CommunityToolkit.Mvvm, existing SourceGit Git command and diff infrastructure.

**Spec:** Approved conversation design for DevSpaces → Files on 2026-08-29.

## Global Constraints

- Files state is scoped by the current repository/worktree path, matching `DevSpaceRegistry`.
- Switching to another launcher/worktree tab and back must preserve search text, selected path, expanded folders and current Files/terminal selection.
- File tree is filesystem-first and overlays `QueryLocalChanges` status.
- `.git` is never shown.
- Search matches relative file/folder paths case-insensitively and retains ancestor folders.
- Added, deleted, renamed, modified, untracked and conflicted states are visible in the tree.
- Changed-file preview compares HEAD to the current worktree and uses SourceGit's existing diff renderer.
- No new editor dependency is introduced.

---

### Task 1: Files tree/state model

**Files:**
- Create: `src/ViewModels/DevSpaceFileNode.cs`
- Create: `src/ViewModels/DevSpaceFiles.cs`
- Modify: `src/ViewModels/DevSpaces.cs`

**Interfaces:**
- Produces: `DevSpaceFiles Files`, `bool IsFilesActive`, `void ActivateFiles()` on `DevSpaces`.
- Produces: `string Filter`, `IReadOnlyList<DevSpaceFileNode> VisibleRoots`, `DevSpaceFileNode SelectedNode`, `object DetailContext`, `Task RefreshAsync()` on `DevSpaceFiles`.

- [ ] **Step 1: Define the file-node model** with relative path, display name, directory flag, children, expanded state and `Models.ChangeState` status.
- [ ] **Step 2: Implement filesystem enumeration** while skipping `.git`, handling inaccessible paths, and sorting directories before files with `Models.NumericSort`.
- [ ] **Step 3: Overlay `QueryLocalChanges`** onto existing nodes and synthesize deleted paths that are absent from disk.
- [ ] **Step 4: Implement filtering** so path matches are case-insensitive and ancestors remain visible.
- [ ] **Step 5: Preserve expanded and selected paths across refresh** by snapshotting paths before rebuilding.
- [ ] **Step 6: Wire the child state into `DevSpaces`** so it lives for the lifetime of the repository-path-scoped DevSpaces model.

### Task 2: Selected-file detail and HEAD-to-worktree diff

**Files:**
- Create: `src/ViewModels/DevSpaceWorkspaceFile.cs`
- Modify: `src/ViewModels/DevSpaceFiles.cs`
- Reuse: `src/ViewModels/DiffContext.cs`, `src/Views/DiffView.axaml`

**Interfaces:**
- `DevSpaceFiles.DetailContext` is either `DiffContext`, `DevSpaceWorkspaceFile`, or `null`.

- [ ] **Step 1: For changed files**, create `DiffContext(repo, new Models.DiffOption(change, true), previousDiff)` so existing green/red text diff rendering is reused.
- [ ] **Step 2: For unchanged text files**, load the current workspace file asynchronously into a read-only `DevSpaceWorkspaceFile` model.
- [ ] **Step 3: For deleted files**, keep the synthetic tree node selectable and render the deletion through `DiffContext`.
- [ ] **Step 4: Guard large/binary/unreadable unchanged files** with an explanatory read-only placeholder instead of decoding arbitrary bytes.

### Task 3: DevSpaces Files UI

**Files:**
- Create: `src/Views/DevSpaceFiles.axaml`
- Create: `src/Views/DevSpaceFiles.axaml.cs`
- Modify: `src/Views/DevSpaces.axaml`
- Modify: `src/Views/DevSpaces.axaml.cs`

**Interfaces:**
- Files appears as the first persistent DevSpaces tab before terminal sessions.

- [ ] **Step 1: Add a persistent Files tab** to the DevSpaces toolbar and bind its active styling/visibility to `IsFilesActive`.
- [ ] **Step 2: Keep terminal panes mounted** while Files is active; switch visibility/hit-testing instead of destroying panes.
- [ ] **Step 3: Build the Files split view** with search + refresh on the left and selected-file detail on the right.
- [ ] **Step 4: Render the tree recursively** with expand/collapse, file/folder icons and Git status badges (`M`, `A`, `D`, `R`, `?`, `!`).
- [ ] **Step 5: Bind changed detail to existing `DiffView`** and unchanged detail to a read-only monospaced viewer with line numbers.

### Task 4: Worktree-state regression and verification

**Files:**
- Modify only if needed based on build/format feedback.

- [ ] **Step 1: Verify model lifetime**: `DevSpaceRegistry` continues keying DevSpaces by repository full path.
- [ ] **Step 2: Verify switching A → B → A** does not reinitialize `DevSpaceFiles`; search, selection and expanded paths remain owned by each existing DevSpaces instance.
- [ ] **Step 3: Run `dotnet format --verify-no-changes src/SourceGit.csproj`.** Expected: exit 0.
- [ ] **Step 4: Run `dotnet build src/SourceGit.csproj -c Release -p:DisableAOT=true`.** Expected: exit 0.
- [ ] **Step 5: Open a PR against `master`** summarizing Files behavior and the per-worktree state guarantee.
