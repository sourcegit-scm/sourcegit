# DevSpaces Design

## Goal

Add an opt-in **DevSpaces** page to each repository/worktree in SourceGit. DevSpaces appears as a fourth repository main view directly under **STASHES** and hosts multiple embedded interactive terminal sessions. Every terminal starts in the current repository/worktree path and launches GitHub Copilot CLI by default.

## User experience

### Feature toggle

Add a new **DevSpaces** tab in Preferences with:

- **Enable DevSpaces** — boolean, default `false`.
- **Default command** — string, default `copilot`.

When DevSpaces is disabled, SourceGit behaves exactly as it does today and the repository sidebar does not show DevSpaces.

If DevSpaces is disabled while a repository currently has the DevSpaces view selected, the repository switches back to Histories and all DevSpaces terminal sessions for that repository are stopped.

### Repository navigation

The existing repository main view list becomes:

1. Histories
2. Working Copy
3. Stashes
4. DevSpaces

DevSpaces is rendered immediately below Stashes and only when `Preferences.Instance.EnableDevSpaces` is true.

`Repository.SelectedViewIndex == 3` represents DevSpaces. Add `IsDevSpacesVisible` alongside the existing `IsHistoriesVisible`, `IsWorkingCopyVisible`, and `IsStashesVisible` properties.

### DevSpaces page

Selecting DevSpaces shows a full main-content page with an inner terminal tab strip:

```text
Copilot 1 | Copilot 2 | Terminal 3 | +
----------------------------------------
<embedded interactive terminal>
```

On the first visit to DevSpaces for a repository/worktree, SourceGit automatically creates one terminal session and starts the configured default command.

The `+` button immediately creates another terminal session using the same command and working directory. Tabs are numbered in creation order (`Copilot 1`, `Copilot 2`, etc.) for the default `copilot` command. A close button on each terminal tab stops only that terminal process.

## Working-directory semantics

Every terminal session is owned by one `Repository` instance and starts with:

- working directory: `Repository.FullPath`
- initial command: `Preferences.Instance.DevSpacesDefaultCommand`

This guarantees that a DevSpaces page opened from a newly-created worktree launches Copilot CLI in that worktree rather than in the original repository.

## Terminal implementation

Interactive Copilot CLI requires a real PTY/ConPTY-backed terminal. Redirecting `Process.StandardInput/Output` is insufficient for full-screen or interactive TUI behavior.

Use `Iciclecreek.Avalonia.Terminal` **1.0.11** for the first implementation. This version depends on Avalonia `>= 11.3.14`, which is compatible with SourceGit's current Avalonia `11.3.20`, and supports .NET versions compatible with SourceGit's `net10.0` target.

The package provides terminal emulation plus PTY support through Porta.Pty. SourceGit must verify Debug build and Release publish/AOT compatibility before the feature is considered complete.

Do not upgrade SourceGit to Avalonia 12 for this feature.

## Architecture

### `ViewModels.DevSpaces`

One instance per `Repository`.

Responsibilities:

- own the observable terminal-session collection;
- own the selected terminal session;
- create the first session lazily when the DevSpaces page is first selected;
- add and close sessions;
- dispose all sessions when the owning repository closes;
- generate stable display titles.

It receives the repository working directory in its constructor and does not perform Git operations.

### `ViewModels.DevSpaceTerminal`

Represents one terminal session.

Responsibilities:

- immutable session id;
- display title;
- command;
- working directory;
- terminal running/exited state;
- start/stop lifecycle.

PTY/control-specific implementation details should remain in the terminal view/control adapter rather than leaking through `Repository`.

### `Views.DevSpaces`

Contains:

- compact terminal tab strip;
- close button per terminal tab;
- trailing `+` button;
- content host for the selected terminal;
- empty/error state if a terminal cannot start.

### `Views.DevSpaceTerminal`

Small adapter around `Iciclecreek.Avalonia.Terminal.TerminalControl`.

Responsibilities:

- launch the configured command with the session working directory;
- connect terminal control to its PTY process;
- update the session when the process exits;
- dispose process and PTY resources when the tab closes.

### `Repository`

Add:

- `DevSpaces DevSpaces` lazy property/state;
- `IsDevSpacesVisible`;
- SelectedViewIndex notification for the new visibility property;
- lifecycle cleanup in `Close()`.

Repository remains the owner boundary because each outer SourceGit repository/worktree tab already owns a separate `Repository` instance and `FullPath`.

## Preferences persistence

Add to `ViewModels.Preferences`:

```text
EnableDevSpaces = false
DevSpacesDefaultCommand = "copilot"
```

These settings serialize through the existing `Preferences` JSON source-generation path into `preference.json`; no separate settings file is needed.

The new Preferences DevSpaces page should keep the first milestone deliberately small. Container runtime/image/mount configuration is deferred to the next milestone, but DevSpaces code should avoid assuming the terminal backend must always be local.

## Container-ready boundary

The first PR runs terminals locally. To make container-backed DevSpaces possible later without replacing the page model, terminal creation should go through a narrow session-launch boundary, conceptually:

```text
IDevSpaceSessionLauncher.Start(command, workingDirectory)
```

The first implementation is `LocalDevSpaceSessionLauncher`. A future container launcher can map `Repository.FullPath` into a container workspace and start the same terminal tab UI without changing repository navigation or session ownership.

Do not add container lifecycle, image pulls, mounts, or Docker/WSLC dependencies in this PR.

## Lifecycle and errors

- First DevSpaces selection starts one session automatically.
- `+` starts another independent session.
- Closing a terminal tab terminates that terminal process and disposes its PTY.
- Closing the repository/worktree tab terminates every DevSpaces session owned by that repository.
- A terminal process exiting naturally keeps the tab visible with an exited state so the user can read the final output; the user can then close it.
- Failure to find/start the configured command is shown inside the terminal page and must not crash SourceGit.
- Disabling DevSpaces stops sessions and hides the navigation item.

## Localization

Add localization keys for:

- DevSpaces
- Enable DevSpaces
- Default command
- New terminal
- Close terminal
- terminal start failure / exited status as needed

Follow the repository's existing localization-resource pattern; do not hard-code user-facing strings in XAML or view models.

## Testing and verification

The repository currently has no dedicated unit-test project, so this PR will not introduce a new test framework solely for DevSpaces UI behavior.

Verification requirements:

1. Restore/build SourceGit with initialized submodules.
2. Verify the new terminal package does not force an Avalonia 12 upgrade.
3. Debug build succeeds on the supported development environment.
4. Release publish/AOT succeeds for the existing supported targets or the dependency is rejected/reworked before merge.
5. Preferences default is OFF and persists after restart.
6. DevSpaces sidebar item is hidden while disabled and appears immediately after enabling.
7. Selecting DevSpaces auto-starts `copilot` in `Repository.FullPath`.
8. In a Git worktree tab, terminal working directory is that worktree path.
9. Multiple terminal tabs run independently.
10. Closing one terminal does not stop the others.
11. Closing the repository tab stops all owned terminal processes.
12. A missing `copilot` executable produces an in-page error rather than an application crash.

## Files expected to change

Likely additions:

- `src/ViewModels/DevSpaces.cs`
- `src/ViewModels/DevSpaceTerminal.cs`
- `src/Views/DevSpaces.axaml`
- `src/Views/DevSpaces.axaml.cs`
- `src/Views/DevSpaceTerminal.axaml`
- `src/Views/DevSpaceTerminal.axaml.cs`

Likely modifications:

- `src/SourceGit.csproj`
- `src/ViewModels/Preferences.cs`
- `src/ViewModels/Repository.cs`
- `src/Views/Preferences.axaml`
- `src/Views/Repository.axaml`
- localization resource files

Exact terminal adapter files may change after compiling against the chosen package API, but the ownership boundaries above should remain unchanged.

## Out of scope for this milestone

- automatic creation/removal of containers;
- Docker/Podman/WSLC runtime configuration;
- image selection/pulling;
- workspace mount configuration;
- restoring terminal processes across SourceGit application restarts;
- background terminal sessions after the owning repository tab closes;
- Claude Code/Codex-specific profiles beyond using a custom default command.
