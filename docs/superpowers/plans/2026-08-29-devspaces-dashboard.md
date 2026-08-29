# DevSpaces Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a first-class DevSpaces Dashboard that summarizes the active repository/worktree, existing DevSpace sessions, Git changes, Roslyn state, quick-start actions, and recent DevSpaces activity without duplicating terminal, Files, Working Copy, or Roslyn ownership.

**Architecture:** Extend the existing path-scoped `ViewModels.DevSpaces` owner with an internal page enum and one `DevSpaceDashboard` child view model. The dashboard projects existing session/Git/Roslyn state into lightweight immutable summaries and delegates all launches/navigation back to existing DevSpaces/SourceGit flows; it never owns PTYs, Git polling, Files tree state, or Roslyn sidecars.

**Tech Stack:** .NET 10, C#, Avalonia 11.x, CommunityToolkit.Mvvm, xUnit, existing SourceGit DevSpaces/Git/Roslyn models.

**Spec:** `docs/superpowers/specs/2026-08-29-devspaces-dashboard-design.md`

## Global Constraints

- Dashboard is a DevSpaces control center, not a second IDE.
- Preserve the existing repository/worktree-path ownership in `DevSpaceRegistry`.
- Preserve mounted terminal controls and PTY/TUI lifetime across Dashboard, Files, and Terminals navigation.
- Do not add a second `git status` polling loop, filesystem watcher, Roslyn sidecar, terminal backend, or repository-level workspace owner.
- Dashboard is the default DevSpaces internal page, while the existing first-session behavior still creates the first terminal exactly once when DevSpaces first becomes active.
- Roslyn and AI CLI availability are optional capability states and must never break the rest of Dashboard.
- Recent Activity is in-memory only, per worktree, capped at 20 entries.
- Reuse SourceGit dynamic theme resources and localization; do not introduce a dashboard-specific theme.
- Keep `Ctrl/Cmd+P` Go to File behavior unchanged.
- Tests use the existing `tests/SourceGit.Tests` xUnit project targeting `net10.0`.

---

## File Structure

**Create**
- `src/Models/DevSpacePage.cs` — internal DevSpaces page enum.
- `src/ViewModels/DevSpaceDashboard.cs` — dashboard projection, activity, health, and delegated actions.
- `src/ViewModels/DevSpaceDashboardModels.cs` — immutable dashboard row/summary records.
- `src/Views/DevSpaceDashboard.axaml` — responsive dashboard cards.
- `src/Views/DevSpaceDashboard.axaml.cs` — thin interaction/navigation bridge only where bindings are insufficient.
- `tests/SourceGit.Tests/DevSpacesDashboardTests.cs` — page/session/activity/quick-start isolation tests.
- `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs` — pure aggregation tests.

**Modify**
- `src/ViewModels/DevSpaces.cs` — own `ActivePage` and `Dashboard`, migrate `IsFilesActive` to page-derived state, expose navigation helpers.
- `src/Views/DevSpaces.axaml` — add Dashboard/Files/Terminals/Roslyn internal navigation and host the dashboard without unloading terminal controls.
- `src/Views/DevSpaces.axaml.cs` — switch page visibility/input state without recreating terminal surfaces.
- `src/DevSpaces/DevSpaceRegistry.cs` — pass the owning repository into the DevSpaces model if needed for summary/navigation while preserving path keying.
- `src/Resources/Locales/DevSpaces.axaml` — English/default Dashboard localization keys used by the injected DevSpaces resources.
- Existing locale resources only where SourceGit's localization validation requires matching keys.

---

### Task 1: Introduce the DevSpaces internal page model

**Files:**
- Create: `src/Models/DevSpacePage.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces: `Models.DevSpacePage { Dashboard, Files, Terminals, Roslyn }`.
- Produces: `ViewModels.DevSpaces.ActivePage`, `IsDashboardActive`, `IsFilesActive`, `IsTerminalsActive`, `IsRoslynActive`.
- Produces: `ActivateDashboard()`, `ActivateFiles()`, `ActivateTerminals()`, `ActivateRoslyn()`.

- [ ] **Step 1: Write failing page-state tests**

Create `DevSpacesDashboardTests.cs` with tests that construct `new ViewModels.DevSpaces(tempPath, fakeLauncher)` and assert:

```csharp
Assert.Equal(Models.DevSpacePage.Dashboard, spaces.ActivePage);
Assert.True(spaces.IsDashboardActive);
Assert.False(spaces.IsFilesActive);

spaces.ActivateFiles();
Assert.Equal(Models.DevSpacePage.Files, spaces.ActivePage);

spaces.ActivateTerminals();
Assert.Equal(Models.DevSpacePage.Terminals, spaces.ActivePage);
```

Also assert `OpenFile(relativePath)` changes `ActivePage` to `Files` and does not modify `Sessions`.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpacesDashboardTests
```

Expected: compile/test failure because `DevSpacePage` and `ActivePage` do not exist.

- [ ] **Step 3: Implement the enum and single page source of truth**

Add:

```csharp
namespace SourceGit.Models;

public enum DevSpacePage
{
    Dashboard,
    Files,
    Terminals,
    Roslyn,
}
```

In `ViewModels.DevSpaces`, replace `_isFilesActive` as the authoritative state with:

```csharp
public Models.DevSpacePage ActivePage
{
    get => _activePage;
    private set
    {
        if (!SetProperty(ref _activePage, value))
            return;

        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsFilesActive));
        OnPropertyChanged(nameof(IsTerminalsActive));
        OnPropertyChanged(nameof(IsRoslynActive));
    }
}

public bool IsDashboardActive => ActivePage == Models.DevSpacePage.Dashboard;
public bool IsFilesActive => ActivePage == Models.DevSpacePage.Files;
public bool IsTerminalsActive => ActivePage == Models.DevSpacePage.Terminals;
public bool IsRoslynActive => ActivePage == Models.DevSpacePage.Roslyn;
```

Initialize `_activePage = Models.DevSpacePage.Dashboard` and make `ActivateTerminal(...)` / terminal creation select `Terminals`, while `OpenFile(...)` selects `Files`.

Do **not** move `EnsureFirstSession()` into Dashboard; it remains invoked by the existing DevSpaces outer activation path.

- [ ] **Step 4: Re-run focused tests**

Run the same `dotnet test --filter DevSpacesDashboardTests` command. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Models/DevSpacePage.cs src/ViewModels/DevSpaces.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: add DevSpaces internal page state"
```

---

### Task 2: Add lightweight dashboard summary models and bounded activity

**Files:**
- Create: `src/ViewModels/DevSpaceDashboardModels.cs`
- Create: `src/ViewModels/DevSpaceDashboard.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Test: `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs`
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces: `DevSpaceDashboardSessionRow`, `DevSpaceGitSummary`, `DevSpaceActivityEntry`, `DevSpaceCapabilityState`.
- Produces: `DevSpaceDashboard.Activity`, capped at 20, newest first.
- Produces: `DevSpaceDashboard.AddActivity(DevSpaceActivityKind kind, string text, DateTimeOffset? at = null)`.

- [ ] **Step 1: Write failing pure summary/activity tests**

Cover:
- status count aggregation for Added/Modified/Deleted/Renamed and staged/unstaged flags using explicit test input;
- activity insert order;
- adding 25 entries leaves exactly 20 and preserves the newest 20;
- two independently constructed `DevSpaces` instances have independent dashboard activity lists.

- [ ] **Step 2: Run the two dashboard test classes and verify RED**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter "DevSpaceDashboard"
```

Expected: missing types/properties.

- [ ] **Step 3: Implement immutable rows and the dashboard child owner**

Keep models data-only, for example:

```csharp
public sealed record DevSpaceGitSummary(
    int Total,
    int Added,
    int Modified,
    int Deleted,
    int Renamed,
    int Staged,
    int Unstaged);

public sealed record DevSpaceActivityEntry(
    DevSpaceActivityKind Kind,
    string Text,
    DateTimeOffset At);
```

`DevSpaceDashboard` must receive the owning `DevSpaces` instance and workspace path, expose an `AvaloniaList<DevSpaceActivityEntry>`, insert at index 0, and remove the last item while count exceeds 20.

Instantiate exactly one dashboard in the `DevSpaces` constructor and dispose it from `DevSpaces.Dispose()` before/after `StopAll()` as appropriate to detach subscriptions without owning terminal disposal.

- [ ] **Step 4: Make session lifecycle feed activity without changing PTY ownership**

In existing `CreateTerminalAt(...)` and `CloseTerminal(...)`, add dashboard activity after the existing session mutation succeeds. Subscribe only to session metadata needed for state projection; never construct a terminal surface from Dashboard.

- [ ] **Step 5: Run focused tests**

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ViewModels/DevSpaceDashboard.cs src/ViewModels/DevSpaceDashboardModels.cs src/ViewModels/DevSpaces.cs tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: add DevSpaces dashboard state model"
```

---

### Task 3: Add delegated Dashboard navigation and Quick Start actions

**Files:**
- Modify: `src/ViewModels/DevSpaceDashboard.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Modify: `src/DevSpaces/DevSpaceAgent.cs` only if a shared public/internal built-in agent lookup is not already exposed.
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces: dashboard actions `OpenSession(DevSpaceTerminal)`, `OpenFiles()`, `StartDefaultTerminal()`, `StartProfile(DevSpaceTerminalProfile)`, `StartAgent(DevSpaceAgent)`, `CloseAllSessions()`.
- Consumes: existing `CreateTerminalAt`, `CreateProfileTerminalAt`, built-in agent launch path, `ActivateTerminal`, `StopAll`.

- [ ] **Step 1: Write failing delegation tests with a fake launcher**

Implement a test launcher for `IDevSpaceSessionLauncher` that records launches. Assert:
- default terminal creates exactly one new existing `DevSpaceTerminal` and selects `Terminals`;
- profile launch preserves profile startup command/path behavior by delegating through `CreateProfileTerminalAt`;
- built-in Codex/Antigravity/Copilot use the same existing agent command mapping;
- selecting a dashboard session sets `ActiveTerminal` to the **same object reference** and does not increase `Sessions.Count`;
- `CloseAllSessions()` results in zero sessions through `StopAll()`.

- [ ] **Step 2: Run and verify RED**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpacesDashboardTests
```

- [ ] **Step 3: Implement only delegation methods**

`DevSpaceDashboard` methods should look conceptually like:

```csharp
public void OpenSession(DevSpaceTerminal terminal)
{
    _owner.ActivateTerminal(terminal);
}

public DevSpaceTerminal StartDefaultTerminal()
{
    var terminal = _owner.CreateTerminal();
    _owner.ActivateTerminals();
    return terminal;
}
```

Use existing agent/profile methods rather than duplicating shell command construction.

- [ ] **Step 4: Run focused tests and commit**

```bash
git add src/ViewModels/DevSpaceDashboard.cs src/ViewModels/DevSpaces.cs src/DevSpaces/DevSpaceAgent.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs
git commit -m "feat: add DevSpaces dashboard quick actions"
```

---

### Task 4: Wire repository/Git/worktree summary without duplicate polling

**Files:**
- Modify: `src/DevSpaces/DevSpaceRegistry.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Modify: `src/ViewModels/DevSpaceDashboard.cs`
- Test: `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs`

**Interfaces:**
- `DevSpaces` receives or retains the owning `ViewModels.Repository` reference in addition to `FullPath`.
- `DevSpaceDashboard` exposes workspace name/path/current branch/base branch/ahead-behind/Git summary as bindable properties.

- [ ] **Step 1: Write failing summary tests around explicit repository/change inputs**

Avoid shelling out to Git in unit tests. Test the aggregation helper with representative change/status objects, including rename and mixed staged/unstaged state.

- [ ] **Step 2: Update registry/model construction**

Change new entry creation from:

```csharp
new ViewModels.DevSpaces(repository.FullPath)
```

to an overload that keeps the repository reference while preserving `DevSpaceRegistry` dictionary keying by `repository.FullPath`.

Keep the old constructor overload if tests/other code require it, forwarding to the new constructor with `repository: null`.

- [ ] **Step 3: Subscribe to existing repository property/change notifications**

Project values already held/refreshed by SourceGit. Do not add a timer or continuous command invocation. Base-branch display must call/reuse the existing worktree base-branch capability; if unavailable, expose null/empty.

- [ ] **Step 4: Implement `RefreshGitSummary()` as pure projection**

Map existing current working-copy/status collections to `DevSpaceGitSummary`, notify only changed properties, and leave unavailable values neutral.

- [ ] **Step 5: Run focused tests and commit**

```bash
git add src/DevSpaces/DevSpaceRegistry.cs src/ViewModels/DevSpaces.cs src/ViewModels/DevSpaceDashboard.cs tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs
git commit -m "feat: summarize workspace state on DevSpaces dashboard"
```

---

### Task 5: Project Roslyn and tool-health capability state

**Files:**
- Modify: `src/ViewModels/DevSpaceDashboard.cs`
- Modify: existing Roslyn DevSpaces integration files discovered on the implementation branch only where an observable state adapter is required.
- Test: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`

**Interfaces:**
- Produces neutral capability states: `Available`, `Unavailable`, `Checking`, `Failed` (or the exact enum names introduced in Task 2).
- Dashboard consumes existing Roslyn analysis state; it does not start a sidecar merely to render.

- [ ] **Step 1: Write failing capability tests**

Assert missing Codex/Antigravity/Roslyn yields a non-throwing unavailable state and leaves other properties/actions usable.

- [ ] **Step 2: Add lazy cached CLI checks**

Use one check per capability per DevSpaces lifetime. Cache the result; never run process detection from a getter or every render.

- [ ] **Step 3: Add Roslyn projection adapter**

If Roslyn state is present, project target, analysis state, error/warning/info counts and last-analysis time. If not present, expose `Unavailable`. The `Analyze` action delegates to the existing Roslyn flow and then navigates to `DevSpacePage.Roslyn`.

- [ ] **Step 4: Run focused tests and commit**

```bash
git add src/ViewModels/DevSpaceDashboard.cs tests/SourceGit.Tests/DevSpacesDashboardTests.cs <only-required-existing-roslyn-files>
git commit -m "feat: surface DevSpaces capability health"
```

---

### Task 6: Build the Dashboard UI while keeping terminal controls mounted

**Files:**
- Create: `src/Views/DevSpaceDashboard.axaml`
- Create: `src/Views/DevSpaceDashboard.axaml.cs`
- Modify: `src/Views/DevSpaces.axaml`
- Modify: `src/Views/DevSpaces.axaml.cs`

**Interfaces:**
- Consumes all Task 1-5 dashboard properties/actions.
- Produces internal navigation: Dashboard, Files, Terminals, Roslyn.

- [ ] **Step 1: Replace the current Files/session-tab-only top bar with internal page navigation**

Add compact buttons/tabs for Dashboard, Files, Terminals and conditional Roslyn. Keep terminal session tabs/layout/+ controls in the Terminals page only.

- [ ] **Step 2: Host all page surfaces without breaking terminal persistence**

Dashboard and Files may use normal visibility. The existing terminal tree must continue using the current mounted/opacity/input strategy: switching away from Terminals must not remove or recreate terminal controls or PTYs.

- [ ] **Step 3: Create responsive Dashboard cards**

`DevSpaceDashboard.axaml` contains:
- workspace header;
- Active Spaces;
- Quick Start;
- Git Changes;
- Roslyn Diagnostics;
- Recent Activity.

Use `WrapPanel`, responsive Grid definitions, or existing SourceGit adaptive layout patterns so cards stack at narrow width. No horizontal dashboard scrolling as the normal narrow-window behavior.

- [ ] **Step 4: Wire session row click to exact existing terminal**

Use command/binding where possible. Code-behind is acceptable only to pass the bound `DevSpaceTerminal` instance to the dashboard owner. Verify the reference is not cloned/recreated.

- [ ] **Step 5: Build locally**

```bash
dotnet build src/SourceGit.csproj -c Debug
```

Expected: build succeeds without Avalonia binding/XAML compile errors.

- [ ] **Step 6: Commit**

```bash
git add src/Views/DevSpaceDashboard.axaml src/Views/DevSpaceDashboard.axaml.cs src/Views/DevSpaces.axaml src/Views/DevSpaces.axaml.cs
git commit -m "feat: add DevSpaces dashboard UI"
```

---

### Task 7: Localize Dashboard strings and verify accessibility behavior

**Files:**
- Modify: `src/Resources/Locales/DevSpaces.axaml`
- Modify locale files required by repository localization validation.
- Modify: `src/Views/DevSpaceDashboard.axaml`
- Modify: `src/Views/DevSpaces.axaml`

**Interfaces:**
- Produces resource keys from the spec: Dashboard, Active Spaces, Quick Start, Workspace, Git Changes, Recent Activity, Workspace Health, states/actions/status labels.

- [ ] **Step 1: Add/reuse localization keys**

Prefer existing generic `Text.*` resources when wording already exists; add DevSpaces-specific resources only when needed. Do not leave user-facing hard-coded English in Dashboard XAML.

- [ ] **Step 2: Add accessible labels/tooltips for icon-only controls**

Ensure Copy Path, Open Folder, Close, Analyze and similar icon-only buttons have localized `ToolTip.Tip`/accessible text and that status includes textual Running/Exited/Failed labels rather than color-only meaning.

- [ ] **Step 3: Verify keyboard navigation manually**

Keyboard through internal page buttons, Quick Start, session rows, Close All and card navigation. Confirm moving to Terminals restores terminal focus and Dashboard does not intercept terminal shortcuts.

- [ ] **Step 4: Run format/build and commit**

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release

git add src/Resources/Locales src/Views/DevSpaceDashboard.axaml src/Views/DevSpaces.axaml
git commit -m "feat: localize DevSpaces dashboard"
```

---

### Task 8: Complete regression tests and final verification

**Files:**
- Modify: `tests/SourceGit.Tests/DevSpacesDashboardTests.cs`
- Modify: `tests/SourceGit.Tests/DevSpaceDashboardSummaryTests.cs`
- Modify product files only for defects exposed by the tests.

**Interfaces:**
- Covers all V1 acceptance criteria from the spec that are testable without pixel/runtime-terminal validation.

- [ ] **Step 1: Add the remaining acceptance tests**

Ensure explicit tests exist for:
1. Dashboard default page.
2. Dashboard -> Files -> Terminals preserves the same session objects.
3. Dashboard session activation selects the same terminal reference.
4. Quick Start delegates to existing launcher/profile/agent paths.
5. Git summary counts are correct.
6. Different workspace paths keep independent dashboards/activity.
7. Activity cap is 20.
8. Dashboard disposal does not double-dispose terminal sessions.
9. Missing optional capabilities are non-fatal.
10. Existing layout/session behavior remains unchanged.

- [ ] **Step 2: Run the full test project**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj -c Release
```

Expected: all tests pass.

- [ ] **Step 3: Run format verification**

```bash
dotnet format src/SourceGit.csproj --verify-no-changes
```

Expected: exit code 0.

- [ ] **Step 4: Run Release build**

```bash
dotnet build src/SourceGit.csproj -c Release
```

Expected: exit code 0.

- [ ] **Step 5: Perform manual DevSpaces acceptance**

On a real repository/worktree:
- open DevSpaces and confirm Dashboard appears first;
- verify the existing auto-created first terminal still exists once and is visible under Terminals;
- launch Copilot/Codex/Antigravity/default terminal/profile from Dashboard;
- switch Dashboard -> Files -> Terminals repeatedly and confirm every TUI retains state;
- open a second worktree tab and confirm independent dashboard/session/activity state;
- confirm Git counts update with working-copy changes;
- confirm Roslyn unavailable/failure does not affect terminals/Files;
- resize narrow/wide and verify cards stack without becoming unusable;
- close the repository/worktree tab and confirm existing session cleanup still runs once.

- [ ] **Step 6: Commit verification fixes, if any, then inspect the final diff**

```bash
git diff master...HEAD --check
git status --short
```

Expected: no whitespace errors; only Dashboard-related product/tests/localization/docs changes.

- [ ] **Step 7: Final commit if verification required changes**

```bash
git add src tests
git commit -m "test: verify DevSpaces dashboard behavior"
```

---

## Final PR Acceptance Gate

Before opening/merging the implementation PR, require:

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj -c Release
dotnet format src/SourceGit.csproj --verify-no-changes
dotnet build src/SourceGit.csproj -c Release
```

and the repository's normal GitHub PR Check matrix on Windows x64/ARM64, Linux x64/ARM64, and macOS Intel/Apple Silicon where configured.

Do not claim terminal/TUI persistence purely from CI: complete the manual navigation smoke test because mounted native/Avalonia terminal behavior is runtime UI behavior.