# Ctrl+P Go to File Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a keyboard-first Ctrl/Cmd+P Go to File modal that searches current worktree file paths and text content, then opens the selected file in DevSpaces Files.

**Architecture:** Keep `Launcher` responsible for the global shortcut and modal overlay, put query/ranking/cancellation in a dedicated `GoToFileSearch` view model, and reuse `DevSpaceFiles` as the single source of workspace paths and file-opening behavior. Content search reads current workspace files asynchronously with strict size/binary/result limits and never creates a second preview subsystem.

**Tech Stack:** .NET 10, C#, Avalonia 11, CommunityToolkit.Mvvm, existing SourceGit DevSpaces and Git-aware workspace enumeration.

**Spec:** `docs/superpowers/specs/2026-08-29-ctrl-p-go-to-file-design.md`

## Global Constraints

- `Ctrl+T` behavior must not change.
- Go to File is `Ctrl+P` on Windows/Linux and `Cmd+P` on macOS through the existing `cmdKey` abstraction.
- Search scope is the current repository/worktree only.
- Path/name matches rank before content-only matches.
- Content search must never block the UI thread.
- Skip files larger than 1 MiB and binary files detected by NUL bytes.
- Display at most 100 results.
- Reuse `DevSpaceFiles` workspace paths, selection, diff, and preview behavior.
- No persistent index, regex engine, symbol search, or new editor dependency.

---

### Task 1: Expose searchable files and open-by-path from DevSpaceFiles

**Files:**
- Modify: `src/ViewModels/DevSpaceFiles.cs`
- Modify: `src/ViewModels/DevSpaces.cs`

**Interfaces:**
- Produces: `IReadOnlyList<string> GetSearchableFilePaths()` on `DevSpaceFiles`.
- Produces: `bool OpenFile(string relativePath)` on `DevSpaceFiles`.
- Produces: `bool OpenFile(string relativePath)` on `DevSpaces`.

- [ ] **Step 1: Add a snapshot API for searchable files**

In `DevSpaceFiles`, expose only non-directory nodes from `_nodesByPath`, normalized and sorted with `Models.NumericSort`:

```csharp
public IReadOnlyList<string> GetSearchableFilePaths()
{
    return _nodesByPath.Values
        .Where(x => !x.IsDirectory)
        .Select(x => x.RelativePath)
        .OrderBy(x => x, Comparer<string>.Create(Models.NumericSort.Compare))
        .ToArray();
}
```

- [ ] **Step 2: Add OpenFile to DevSpaceFiles**

Implement exact behavior:

```csharp
public bool OpenFile(string relativePath)
{
    var normalized = NormalizePath(relativePath);
    if (!_nodesByPath.TryGetValue(normalized, out var node) || node.IsDirectory)
        return false;

    Filter = string.Empty;

    var current = normalized;
    while (true)
    {
        var slash = current.LastIndexOf('/');
        if (slash <= 0)
            break;

        current = current[..slash];
        if (_nodesByPath.TryGetValue(current, out var parent) && parent.IsDirectory)
            parent.IsExpanded = true;
    }

    RebuildVisibleItems();
    SelectedNode = node;
    return true;
}
```

- [ ] **Step 3: Delegate from DevSpaces**

Add:

```csharp
public bool OpenFile(string relativePath)
{
    ActivateFiles();
    return Files.OpenFile(relativePath);
}
```

- [ ] **Step 4: Verify formatting/build for this task**

Run:

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
```

Expected: both exit 0.

- [ ] **Step 5: Commit**

```bash
git add src/ViewModels/DevSpaceFiles.cs src/ViewModels/DevSpaces.cs
git commit -m "feat: expose DevSpaces file navigation"
```

---

### Task 2: Add search result and search engine model

**Files:**
- Create: `src/ViewModels/GoToFileSearchResult.cs`
- Create: `src/ViewModels/GoToFileSearch.cs`

**Interfaces:**
- Consumes: `DevSpaceFiles.GetSearchableFilePaths()`.
- Consumes: `DevSpaces.OpenFile(string relativePath)`.
- Produces: `AvaloniaList<GoToFileSearchResult> Results`.
- Produces: `string Query`.
- Produces: `GoToFileSearchResult SelectedResult`.
- Produces: `Task RefreshAsync()`.
- Produces: `bool OpenSelected()`.

- [ ] **Step 1: Define result model**

Create:

```csharp
namespace SourceGit.ViewModels
{
    public enum GoToFileMatchKind
    {
        Path,
        Content,
    }

    public sealed class GoToFileSearchResult
    {
        public string RelativePath { get; }
        public string FileName { get; }
        public GoToFileMatchKind MatchKind { get; }
        public string PreviewText { get; }
        public int Rank { get; }

        public GoToFileSearchResult(string relativePath, GoToFileMatchKind matchKind, string previewText, int rank)
        {
            RelativePath = relativePath;
            FileName = System.IO.Path.GetFileName(relativePath);
            MatchKind = matchKind;
            PreviewText = previewText ?? string.Empty;
            Rank = rank;
        }
    }
}
```

- [ ] **Step 2: Implement deterministic path ranking helper**

In `GoToFileSearch`, use this order:

```csharp
private static int GetPathRank(string path, string query)
{
    var fileName = Path.GetFileName(path);
    if (fileName.Equals(query, StringComparison.OrdinalIgnoreCase))
        return 0;
    if (fileName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        return 10;
    if (fileName.Contains(query, StringComparison.OrdinalIgnoreCase))
        return 20;
    if (path.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        return 30;
    if (path.Contains(query, StringComparison.OrdinalIgnoreCase))
        return 40;
    return int.MaxValue;
}
```

- [ ] **Step 3: Implement query versioning and immediate path results**

`Query` setter increments `_searchVersion` and starts `RefreshAsync()`. `RefreshAsync()` must:

1. trim query;
2. clear results for empty query;
3. build path matches synchronously from `GetSearchableFilePaths()`;
4. publish ordered path matches immediately on the UI thread;
5. start content search off-thread;
6. discard results if the captured version is stale.

Use a hard cap:

```csharp
private const int MaxResults = 100;
private const long MaxSearchBytes = 1024 * 1024;
```

- [ ] **Step 4: Implement safe content search**

For each remaining file path:

```csharp
var absolutePath = Path.Combine(_workingDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
var info = new FileInfo(absolutePath);
if (!info.Exists || info.Length > MaxSearchBytes)
    continue;
```

Read an 8192-byte sample and skip on any NUL byte. Then read text and inspect lines until the first case-insensitive match. Store a trimmed preview of at most 240 characters.

Unreadable files must be caught individually and skipped.

- [ ] **Step 5: Merge content results without duplicate rows**

Path matches stay first. A content match for a path already in path results may update that result's preview representation, but must not add a duplicate row. Content-only results start at rank 1000 and sort by relative path.

- [ ] **Step 6: Implement OpenSelected**

```csharp
public bool OpenSelected()
{
    if (SelectedResult == null)
        return false;

    return _devSpaces.OpenFile(SelectedResult.RelativePath);
}
```

- [ ] **Step 7: Verify formatting/build**

Run:

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
```

Expected: both exit 0.

- [ ] **Step 8: Commit**

```bash
git add src/ViewModels/GoToFileSearchResult.cs src/ViewModels/GoToFileSearch.cs
git commit -m "feat: add go to file search model"
```

---

### Task 3: Connect repository DevSpaces to launcher modal state

**Files:**
- Modify: `src/ViewModels/Launcher.cs`
- Inspect/reuse: existing `SourceGit.DevSpaces.DevSpaceRegistry`

**Interfaces:**
- Consumes: repository-path-scoped DevSpaces instance for active `Repository`.
- Produces: nullable `GoToFileSearch GoToFileSearch` property on `Launcher`.
- Produces: `void OpenGoToFile(Repository repo)` and `void CloseGoToFile()`.

- [ ] **Step 1: Add modal state property**

```csharp
public GoToFileSearch GoToFileSearch
{
    get => _goToFileSearch;
    set => SetProperty(ref _goToFileSearch, value);
}
```

- [ ] **Step 2: Resolve the active repository DevSpaces instance**

Follow the existing DevSpaceRegistry API already used by SourceGit. Do not instantiate a second `DevSpaces`. `OpenGoToFile(repo)` must resolve the same path-scoped instance that the DevSpaces tab uses and construct:

```csharp
GoToFileSearch = new GoToFileSearch(repo.FullPath, devSpaces);
```

- [ ] **Step 3: Add close method**

```csharp
public void CloseGoToFile()
{
    GoToFileSearch = null;
}
```

- [ ] **Step 4: Ensure active-page changes close stale modal state**

When launcher active page changes away from the repository that owns the current search model, clear `GoToFileSearch` rather than allowing a search result to target another worktree.

- [ ] **Step 5: Build/format and commit**

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
git add src/ViewModels/Launcher.cs
git commit -m "feat: add launcher go to file state"
```

---

### Task 4: Add Go to File overlay UI and keyboard navigation

**Files:**
- Create: `src/Views/GoToFileSearch.axaml`
- Create: `src/Views/GoToFileSearch.axaml.cs`
- Modify: `src/Views/Launcher.axaml`
- Modify: `src/Views/Launcher.axaml.cs`

**Interfaces:**
- Consumes: `Launcher.GoToFileSearch`.
- Consumes: `GoToFileSearch.Query`, `Results`, `SelectedResult`, `OpenSelected()`.

- [ ] **Step 1: Add centered overlay to Launcher.axaml**

Render only while `GoToFileSearch != null`. Use the existing launcher overlay/palette layer rather than a child native window. Add a dimmed click-catcher background and center `GoToFileSearch` at about 680 px width with max-height around 520 px.

- [ ] **Step 2: Build search UI**

`GoToFileSearch.axaml` contains:

```xml
<Grid RowDefinitions="Auto,*,Auto">
  <TextBox x:Name="SearchBox"
           Text="{Binding Query, Mode=TwoWay}"
           Watermark="Search files and content..." />
  <ListBox x:Name="ResultsList"
           Grid.Row="1"
           ItemsSource="{Binding Results}"
           SelectedItem="{Binding SelectedResult, Mode=TwoWay}" />
  <TextBlock Grid.Row="2" Text="↑↓ Navigate    Enter Open    Esc Close" />
</Grid>
```

Each result row shows `FileName`, `RelativePath`, and `PreviewText` only when non-empty.

- [ ] **Step 3: Focus search box when overlay opens**

In view code-behind, on attachment/open, post focus to `SearchBox` through `Dispatcher.UIThread.Post` so keyboard input goes directly to the query.

- [ ] **Step 4: Add Ctrl/Cmd+P shortcut in Launcher.OnKeyDown**

Inside the active-repository shortcut block, before existing generic command-palette handling conflicts, add:

```csharp
if (e.Key == Key.P && e.KeyModifiers == cmdKey && vm.ActivePage.Data is ViewModels.Repository repo)
{
    vm.OpenGoToFile(repo);
    e.Handled = true;
    return;
}
```

Do not alter the existing Ctrl+T branch.

- [ ] **Step 5: Handle modal keys before normal launcher hotkeys**

When `vm.GoToFileSearch != null`:

- `Escape` -> close modal;
- `Down` -> move selection +1 within bounds;
- `Up` -> move selection -1 within bounds;
- `Enter` -> call `OpenSelected()`, close modal only when it returns true;
- all other text editing keys continue to the focused TextBox.

- [ ] **Step 6: Clicking backdrop closes the modal**

Add a launcher handler equivalent to existing command-palette backdrop behavior but calling `CloseGoToFile()`.

- [ ] **Step 7: Build/format and commit**

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
git add src/Views/GoToFileSearch.axaml src/Views/GoToFileSearch.axaml.cs src/Views/Launcher.axaml src/Views/Launcher.axaml.cs
git commit -m "feat: add Ctrl+P go to file modal"
```

---

### Task 5: Regression verification and PR preparation

**Files:**
- Modify only if verification finds issues.

**Interfaces:**
- Verifies all interfaces produced by Tasks 1-4.

- [ ] **Step 1: Verify Ctrl+T regression**

Manual check: Ctrl+T still opens a new launcher tab exactly as before.

- [ ] **Step 2: Verify keyboard-only Go to File**

Manual sequence:

1. Open a repository tab.
2. Press Ctrl+P.
3. Type a known filename fragment.
4. Use Down/Up.
5. Press Enter.
6. Confirm DevSpaces Files becomes active and selects the file.
7. Press Ctrl+P again and press Escape; confirm existing selected file remains unchanged.

- [ ] **Step 3: Verify content-only matching**

Search for a unique source-code text string that does not appear in the filename. Confirm a result appears with a matching-line preview and Enter opens the correct file.

- [ ] **Step 4: Verify worktree isolation**

With worktree A and worktree B open in separate SourceGit tabs, run Ctrl+P from B and open a file. Confirm B's DevSpaces Files state changes and A's search/selection state remains untouched.

- [ ] **Step 5: Final verification commands**

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
```

Expected: both exit 0.

- [ ] **Step 6: Review the feature diff**

```bash
git diff master...HEAD --stat
git diff master...HEAD -- src/ViewModels/DevSpaceFiles.cs src/ViewModels/DevSpaces.cs src/ViewModels/GoToFileSearch.cs src/ViewModels/GoToFileSearchResult.cs src/ViewModels/Launcher.cs src/Views/GoToFileSearch.axaml src/Views/GoToFileSearch.axaml.cs src/Views/Launcher.axaml src/Views/Launcher.axaml.cs
```

Confirm no unrelated changes and no Ctrl+T behavior changes.

- [ ] **Step 7: Prepare PR**

PR summary must mention:

- Ctrl/Cmd+P keyboard-first file finder;
- file/path + content search;
- path-first ranking and asynchronous cancellable content reads;
- opens through existing DevSpaces Files preview/diff;
- current worktree isolation;
- format/build verification.
