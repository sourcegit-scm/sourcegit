# DevSpaces Dashboard Design

## Goal

Add a first-class **Dashboard** inside SourceGit DevSpaces that acts as the control center for the active repository/worktree.

The dashboard does not replace SourceGit's existing History, Working Copy, Stashes, Files, or terminal experiences. It summarizes the current DevSpace and provides direct actions into those existing surfaces.

V1 must stay lightweight and reuse the existing per-worktree DevSpaces lifetime, terminal sessions, Files model, Git state, and Roslyn integration rather than creating a parallel workspace model.

## Product direction

The dashboard is a **DevSpaces Control Center**, not a second IDE.

Its responsibilities are:

- tell the user which repository/worktree and branch they are operating on;
- summarize Git changes and workspace health;
- show all active DevSpace sessions in one place;
- provide one-click launch actions for AI agents, terminals, Files, and Roslyn analysis;
- surface recent workspace activity;
- navigate to the existing detailed views when the user needs to work on something.

The dashboard must not embed full terminal controls, a full code editor, or a second copy of SourceGit's Working Copy UI.

## Existing architecture to preserve

Current DevSpaces already has the important ownership boundary required by the dashboard:

- `DevSpaceRegistry` keys DevSpaces state by repository/worktree path.
- `ViewModels.DevSpaces` owns `Files`, terminal `Sessions`, `ActiveTerminal`, terminal layout state, and the session launcher.
- `Views.DevSpaces` keeps the terminal subtree mounted so PTY/TUI state survives navigation and worktree-tab changes.
- terminal profiles and built-in AI CLI sessions are launched through the existing DevSpaces session flow.
- Files state is already scoped to the active repository/worktree.

Dashboard state must follow the same repository-path-scoped lifetime. Switching to another worktree tab shows that worktree's dashboard, sessions, counts, and activity without resetting the previous tab.

Dashboard also needs read access to the owning `ViewModels.Repository` for branch, upstream, and working-copy state. `DevSpaceRegistry` already owns both the path-keyed entry and current repository instance, so it should pass/update that repository context rather than introducing a separate repository lookup or new global registry.

## Navigation

DevSpaces gets a small internal page selector with these logical pages:

1. **Dashboard**
2. **Files**
3. **Terminals**
4. **Roslyn** when Roslyn analysis is available

Dashboard is the default first page when opening DevSpaces for a repository/worktree.

The existing terminal session tabs remain part of the Terminals page. The current `+` terminal/session launcher remains available there and may also be exposed from Dashboard Quick Start.

Do not add additional top-level repository navigation items for Dashboard, Files, Terminals, or Roslyn. They remain children of the existing DevSpaces repository item.

### Existing first-session behavior

V1 preserves the existing DevSpaces behavior where selecting DevSpaces calls `EnsureFirstSession()` when no terminal session exists. Therefore opening the Dashboard for a brand-new DevSpace may already show one automatically-created default terminal in Active Spaces.

Dashboard changes the landing surface, not the existing session-start contract. Removing automatic first-session creation can be considered separately later.

## V1 dashboard layout

Use a responsive card-based layout with a compact workspace header.

Conceptual desktop layout:

```text
DevSpaces / Dashboard
+----------------------------------------------------------------+
| feature/my-change                    base: develop              |
| C:/.../sourcegit-worktrees/my-change                 dirty 7   |
| ahead 2   behind 0                    2 errors  5 warnings      |
+--------------------------+-------------------------------------+
| ACTIVE SPACES            | WORKSPACE                           |
| Copilot       Running    | Changed files            7          |
| Codex         Running    | Added                    2          |
| PowerShell    Running    | Modified                 4          |
|                          | Deleted                  1          |
|                          | Roslyn: 2 errors / 5 warnings      |
+--------------------------+-------------------------------------+
| QUICK START              | RECENT ACTIVITY                     |
| Copilot  Codex           | App.cs modified                     |
| Antigravity Terminal     | View.axaml modified                 |
| Files     Analyze        | Roslyn analysis completed          |
+--------------------------+-------------------------------------+
```

On narrower windows the cards stack vertically rather than becoming horizontally scrollable.

## Workspace header

The header shows:

- repository/worktree display name;
- absolute workspace path;
- current branch;
- base branch when SourceGit can determine it reliably;
- clean/dirty state;
- ahead/behind count against the configured upstream when available;
- total changed-file count;
- Roslyn error/warning counts when analysis data is available.

Actions:

- copy workspace path;
- open workspace folder using the existing OS/file-manager behavior;
- jump to Working Copy when the changed-file summary is selected.

Base-branch information must reuse the worktree/base-branch capability already planned for SourceGit rather than adding a second heuristic in Dashboard.

If a value is unavailable, omit it or display a neutral unavailable state. Do not show an error banner for optional metadata such as a missing upstream or unknown base branch.

## Active Spaces card

Show one compact row/card per active DevSpace session.

Each row shows:

- session title, such as `Copilot 1`, `Codex 2`, profile name, or shell name;
- session type/icon;
- running, exited, starting, or failed state;
- working directory when it differs from the repository root;
- elapsed session time when available.

Interactions:

- clicking a running session switches to Terminals and activates that exact session;
- an exited/failed session remains selectable so the user can inspect its final terminal output;
- per-session close action reuses the existing terminal close lifecycle;
- a `Close All` action reuses `DevSpaces.StopAll()` and must require no new process-management layer.

The Dashboard must never create a second terminal control instance for a session. It only observes session metadata and navigates to the existing mounted terminal surface.

## Quick Start card

Provide one-click actions for:

- Copilot;
- Codex;
- Antigravity;
- default Terminal;
- configured terminal profiles;
- Files;
- Roslyn Analyze / Diagnostics when Roslyn is available.

AI-agent launches must reuse the existing built-in DevSpace agent definitions and terminal/session launcher. Copilot continues using the existing trusted-workspace preparation before launch.

Terminal-profile launches must reuse `DevSpaceProfileSettings` and the same workspace-relative path validation already used by DevSpaces.

After starting a terminal or agent from Dashboard, switch to Terminals and activate the new session by default. Files opens the existing DevSpace Files page. Roslyn Analyze opens or activates the Roslyn page and runs the existing diagnostic flow.

## Git Changes card

Summarize current working-copy state without duplicating the detailed Working Copy UI.

Show:

- total changed files;
- added count;
- modified count;
- deleted count;
- renamed count;
- staged count when available;
- unstaged count when available.

The values must derive from SourceGit's existing repository/working-copy state or the same Git status data already used by DevSpace Files. Do not execute a second continuous `git status` polling loop exclusively for Dashboard.

Interactions:

- selecting the overall card opens SourceGit Working Copy;
- selecting a file-status category may open DevSpaces Files with an equivalent filter only if that filter already exists or can be added without duplicating state.

Dashboard updates when the underlying repository working-copy state changes.

## Roslyn Diagnostics card

When Roslyn integration is available, show:

- detected solution/project target;
- analysis state: Not Run, Running, Ready, or Failed;
- error count;
- warning count;
- informational diagnostic count when available;
- last analysis time.

Actions:

- `Analyze` / `Analyze Again`;
- open Roslyn page;
- optionally open the first error when a stable diagnostic-to-file navigation path exists.

The card consumes the existing Roslyn analysis state. It must not launch a separate Roslyn MCP sidecar just to populate Dashboard.

If Roslyn is not installed, not ready, or not available for the workspace, show a compact neutral state with the appropriate existing setup/diagnostic action. This condition must not block the rest of Dashboard.

## Recent Activity card

V1 activity is intentionally local and lightweight. It is not a permanent audit log.

Keep a small in-memory, per-worktree list of recent meaningful events generated by DevSpaces itself, such as:

- DevSpace session started;
- DevSpace session exited or failed;
- file opened from DevSpaces Files;
- workspace file changed when that event is already available from the existing model;
- Roslyn analysis completed;
- Roslyn diagnostic counts changed.

Keep at most 20 entries and render the newest 5-10 on Dashboard.

Do not add filesystem watchers, database persistence, telemetry storage, or a new Git history scanner solely for Recent Activity in V1.

Activity may be lost when SourceGit exits. This is acceptable for V1.

## Workspace Health

V1 may show compact status indicators in the workspace header or Workspace card for capabilities needed by DevSpaces:

- Git repository available;
- configured default terminal available;
- Copilot CLI available;
- Codex CLI available;
- Antigravity CLI available;
- Roslyn ready when applicable.

Health checks should be lazy and cached for the lifetime of the DevSpaces instance. They must not repeatedly spawn processes every time a card redraws.

A missing optional agent CLI is a setup state, not a fatal dashboard error.

## View-model design

### `Models.DevSpacePage`

Introduce a small page enum:

```text
Dashboard
Files
Terminals
Roslyn
```

Roslyn remains hidden/unavailable when the integration is not present for the current build/workspace.

### `ViewModels.DevSpaces`

Extend the existing instance rather than creating another repository-level owner.

Add or expose:

- `ActivePage`;
- `Dashboard` child view model;
- commands/methods to activate Dashboard, Files, Terminals, and Roslyn;
- helpers to activate a terminal from Dashboard;
- repository-context update support from `DevSpaceRegistry`;
- existing terminal and Files lifecycle remains unchanged.

The current `IsFilesActive` boolean should be migrated to page-derived state rather than layering several additional booleans as more DevSpace pages are added.

Compatibility properties may remain temporarily if needed by existing bindings, but the source of truth becomes `ActivePage`.

`DevSpaces` may continue to be initially constructed from `repository.FullPath`, but it must also receive the current `ViewModels.Repository` reference, either through an updated constructor or a narrow `UpdateRepositoryContext(repository)` method. This is required because the path alone cannot provide live current-branch, upstream, and working-copy state. The repository reference is read context; it does not change the path-keyed ownership rule.

### `ViewModels.DevSpaceDashboard`

One instance owned by one `ViewModels.DevSpaces` instance.

Responsibilities:

- expose workspace summary values for binding;
- project current terminal sessions into dashboard rows without owning their lifecycle;
- expose Git status summary from the owning repository/existing providers;
- expose Roslyn status from the existing Roslyn model/provider;
- own the small in-memory Recent Activity list;
- execute navigation/quick-start commands by delegating back to DevSpaces/existing services;
- cache lightweight tool-health results.

It must not own PTYs, Git process polling, Roslyn sidecars, or Files tree state.

### `Views.DevSpaceDashboard`

A pure Avalonia dashboard surface containing:

- workspace header;
- Active Spaces card;
- Quick Start card;
- Git Changes card;
- Roslyn Diagnostics card;
- Recent Activity card.

Use SourceGit's existing dynamic theme resources, typography, icons, spacing, and button styles. Do not introduce a separate dashboard theme.

## Data flow

```text
Repository/worktree tab
        |
        v
DevSpaceRegistry[path] ------ current Repository context
        |
        v
ViewModels.DevSpaces
   |       |        |
   |       |        +--> existing Roslyn state
   |       +-----------> DevSpaceFiles
   +-------------------> Sessions / ActiveTerminal
        |
        v
DevSpaceDashboard
        |
        v
Dashboard cards + navigation commands
```

Dashboard reads existing state and invokes existing commands. The existing models remain authoritative.

## Refresh behavior

Dashboard should be event-driven wherever SourceGit already exposes change notifications.

- session list/state updates when DevSpaces session properties change;
- Git summary updates when repository working-copy state changes;
- Roslyn card updates when analysis state changes;
- Recent Activity updates when DevSpaces receives meaningful events.

Do not create a high-frequency dashboard timer.

A low-frequency elapsed-time display update is acceptable if needed for session uptime, but it should stop when the DevSpaces instance is disposed.

## Persistence and worktree isolation

The following remain scoped by repository/worktree path through the existing DevSpace registry:

- active dashboard instance;
- terminal sessions;
- Files state;
- selected DevSpaces page;
- Recent Activity;
- cached workspace-health results.

Switching worktree tabs must not transfer one worktree's sessions, activity, diagnostics, or selected internal page into another.

When the owning repository/worktree tab closes, the existing DevSpaces disposal path remains responsible for terminal cleanup and the dashboard releases any event subscriptions/timers.

V1 does not add cross-application persistence for dashboard activity.

## Error handling

Dashboard cards fail independently.

Examples:

- missing upstream: omit ahead/behind rather than failing Dashboard;
- Git status unavailable: show Git summary unavailable while keeping sessions usable;
- missing AI CLI: disable or annotate that Quick Start action and provide existing setup guidance where available;
- Roslyn failure: show Failed plus the returned diagnostic/error summary, without affecting terminal or Files functionality;
- session launch failure: preserve the failed/exited terminal session so its output can be inspected through Terminals.

No dashboard refresh failure should terminate a DevSpace session.

## Accessibility and keyboard behavior

- every card action must be reachable by keyboard;
- focus order follows visual reading order;
- status must not rely on color alone;
- tooltips/localized accessible labels are required for icon-only actions;
- the dashboard must not steal terminal keyboard shortcuts when the user is on the Terminals page;
- existing `Ctrl/Cmd+P` Go to File behavior remains unchanged.

## Localization

Add localization keys for at least:

- Dashboard
- Active Spaces
- Quick Start
- Workspace
- Git Changes
- Recent Activity
- Workspace Health
- Running
- Starting
- Exited
- Failed
- Analyze
- Analyze Again
- Close All
- Copy Path
- Open Folder
- Errors
- Warnings
- Added
- Modified
- Deleted
- Renamed
- Staged
- Unstaged

Reuse existing DevSpaces/Roslyn strings where they already exist.

## Testing

Add unit coverage around view-model/state logic rather than pixel-level UI.

Required V1 tests:

1. Dashboard is the default DevSpace internal page for a new workspace.
2. Existing `EnsureFirstSession()` behavior still creates only one default session when entering a brand-new DevSpace through Dashboard.
3. Switching Dashboard -> Files -> Terminals preserves terminal sessions.
4. Activating a dashboard session selects the same existing terminal instance.
5. Quick Start delegates to the existing launcher/profile/agent path.
6. Git summary aggregation produces correct status counts from existing repository state.
7. Dashboard state is isolated across different repository/worktree paths.
8. Updating repository context for an existing path-keyed registry entry does not recreate or dispose its sessions.
9. Recent Activity caps its entries and remains per-worktree.
10. Dashboard disposal removes event subscriptions and does not double-dispose terminal sessions.
11. Missing optional agent/Roslyn capability produces a non-fatal health state.
12. Legacy DevSpaces layout preference/session behavior remains unchanged.

CI remains the normal SourceGit build/test/format matrix. Manual acceptance should additionally verify responsive dashboard layout and that terminal TUI state is unchanged after Dashboard <-> Terminals <-> Files navigation.

## V1 acceptance criteria

V1 is complete when:

- selecting DevSpaces opens Dashboard by default;
- existing automatic first-session behavior remains intact;
- the workspace header identifies the correct active repository/worktree and branch;
- Active Spaces accurately reflects the sessions already owned by that worktree;
- clicking a session opens the exact existing terminal without restart;
- Quick Start can launch Copilot, Codex, Antigravity, default terminal, and configured profiles through existing launch paths;
- Git Changes displays current workspace counts and can navigate to Working Copy;
- Roslyn summary displays existing analysis state when available and can trigger/open analysis;
- Recent Activity displays a bounded local history of DevSpaces events;
- switching repository/worktree tabs preserves each tab's independent dashboard and session state;
- dashboard navigation never reloads or reparents existing terminal PTY state;
- failures in one dashboard card do not break the other cards or running sessions;
- build, tests, and format checks pass on supported platforms.

## Deferred after V1

The following are explicitly out of scope for the first dashboard PR:

- GitHub PR and CI/check status;
- issue/PR association with the branch;
- CPU/RAM/process resource charts;
- token/cost statistics for AI sessions;
- persistent activity history across SourceGit restarts;
- full DevSpace presets that launch multiple terminals/services as one saved environment;
- auto-detection and management of development servers/ports beyond data already available from existing sessions;
- container lifecycle management;
- remote workspace orchestration.

These can be added later as independent dashboard providers/cards once V1 establishes the page model and aggregation boundary.

## Implementation boundary

The first implementation should favor a small set of focused types:

- `Models.DevSpacePage`
- `ViewModels.DevSpaceDashboard`
- small immutable dashboard row/summary models where useful
- `Views.DevSpaceDashboard.axaml` + code-behind only for UI interactions that cannot be expressed cleanly through bindings
- targeted changes to `DevSpaceRegistry`, `ViewModels.DevSpaces`, and `Views.DevSpaces`
- localization resources
- unit tests

Avoid unrelated refactors of SourceGit repository navigation, Working Copy, terminal backends, or Roslyn internals.