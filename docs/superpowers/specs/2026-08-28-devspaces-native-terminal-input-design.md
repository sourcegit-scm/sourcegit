# DevSpaces Native Terminal Input Design

## Status

Proposed revised architecture after the native-terminal feasibility spike. Pending user review before implementation planning.

PR #7 keeps the Avalonia 11 terminal improvements already implemented as the portable fallback and proposes an optional Windows-native renderer for Windows x64. The native renderer is isolated in a separate helper process so SourceGit itself remains cross-platform and NativeAOT-compatible.

## Problem

DevSpaces currently embeds `Iciclecreek.Avalonia.Terminal` 1.0.12. PR #7 improves full-surface hit testing and adds discoverable Copy/Paste/Select All behavior, but the user wants the terminal interaction to feel like a native terminal, especially for Copilot selection, copy, paste, keyboard input, and mouse behavior.

The feasibility spike established that the Windows Terminal rendering stack can be hosted, but directly linking the practical WPF wrapper into SourceGit is not acceptable:

- SourceGit Release builds use NativeAOT.
- WPF is not NativeAOT-compatible.
- `EasyWindowsTerminalControl` 1.0.38 targets `net10.0-windows` but is explicitly x64-only.
- SourceGit also ships Windows ARM64, macOS x64/ARM64, and Linux x64/ARM64.
- Native child-window rendering has airspace/opacity limitations that differ from normal Avalonia controls.

Therefore the Windows Terminal renderer must not become a direct dependency of `SourceGit.exe`.

## Goals

1. Use the Windows Terminal rendering/input stack for DevSpaces on Windows x64.
2. Preserve SourceGit's existing NativeAOT release behavior.
3. Preserve the current Avalonia terminal as the automatic fallback on Windows ARM64, macOS, Linux, or any native-host initialization failure.
4. Preserve DevSpaces session lifetime: adding terminals, changing layouts, or switching History/Local Changes/Stashes must not restart a terminal.
5. Keep one DevSpaces session mapped to one terminal process and one terminal surface.
6. Preserve the existing terminal picker: Copilot, PowerShell 7, Windows PowerShell, Command Prompt, Git Bash, and platform-appropriate fallbacks.
7. Keep the native host invisible when DevSpaces is not the active repository page without stopping its process.
8. Keep the existing PR #7 Avalonia selection/copy/paste improvements because they remain the fallback implementation.

## Non-goals

- Reparenting or embedding the installed `wt.exe` application window.
- Disabling NativeAOT for SourceGit.
- Migrating SourceGit to WPF, WinUI 3, or Avalonia 12.
- Providing a native terminal renderer on macOS or Linux in this PR.
- Providing Windows Terminal native rendering on Windows ARM64 in this PR.
- Consolidating all native terminals into one broker process; one helper process per native DevSpaces terminal is intentionally simpler for the first implementation.
- Copilot CLI session-ID persistence across SourceGit restarts.
- Replacing the existing DevSpaces terminal picker or layout model.

## Platform Matrix

| Platform | Renderer |
| --- | --- |
| Windows x64 | Windows Terminal native host when available; Avalonia fallback on failure |
| Windows ARM64 | Avalonia terminal |
| macOS x64/ARM64 | Avalonia terminal |
| Linux x64/ARM64 | Avalonia terminal |

The renderer choice is internal. Users still create terminals through the same DevSpaces UI.

## Architecture

### SourceGit process

`SourceGit.exe` remains `net10.0`, Avalonia 11.3.20, and NativeAOT in Release.

SourceGit owns:

- DevSpaces session/view models;
- terminal layout and persistent pane lifetime;
- terminal picker and launch specification;
- native-host process creation and termination;
- the Avalonia `NativeControlHost` wrapper;
- visibility, sizing, and fallback decisions.

SourceGit does not reference WPF or `EasyWindowsTerminalControl` assemblies.

### Windows native helper process

Add a separate Windows-only project, tentatively:

`src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj`

Properties:

- `TargetFramework`: `net10.0-windows`
- `RuntimeIdentifier`: `win-x64`
- `PlatformTarget`: `x64`
- `UseWPF`: `true`
- normal self-contained/JIT deployment; no NativeAOT
- package: `EasyWindowsTerminalControl` pinned to the reviewed version used by the implementation

The helper process hosts exactly one `EasyTerminalControl` / Windows Terminal surface. It does not show SourceGit chrome, tabs, menus, repository UI, or its own independent terminal picker.

One helper process per DevSpaces native terminal keeps lifecycle ownership straightforward: closing one terminal terminates only that helper and its terminal process.

### Native HWND embedding

SourceGit adds a Windows-aware `NativeControlHost` implementation, for example `WindowsTerminalNativeHost`.

When Avalonia calls `CreateNativeControlCore(parent)`, SourceGit:

1. verifies `OperatingSystem.IsWindows()` and `RuntimeInformation.ProcessArchitecture == Architecture.X64`;
2. resolves the packaged helper executable;
3. starts the helper with the parent HWND, working directory, and launch specification;
4. waits for a bounded startup handshake containing the child terminal HWND;
5. returns that HWND as the native control handle.

The helper creates its terminal child HWND underneath the parent supplied by Avalonia and reports the HWND only after the terminal surface is initialized.

`DestroyNativeControlCore` stops the helper only when that DevSpaces terminal session is actually closed/disposed. Repository subpage navigation must not dispose the host.

## Go/No-Go Embedding Gate

Cross-process child-HWND hosting is the only remaining unproven part of the design. It must be proven on real Windows x64 before production integration is kept.

The implementation plan must start with a disposable probe that contains only:

- a tiny Avalonia `NativeControlHost` parent;
- the x64 WPF helper using `EasyWindowsTerminalControl`;
- one simple shell such as `cmd.exe`;
- the startup HWND handshake.

The probe must demonstrate all of the following on Windows x64:

1. the terminal child HWND renders inside the Avalonia host;
2. keyboard focus and typing work without manual focus hacks after every key;
3. mouse selection works;
4. resize follows the Avalonia host bounds;
5. hide/show does not stop the terminal process;
6. destroying the host terminates the helper cleanly;
7. the child HWND does not paint over unrelated Avalonia content after it is hidden.

If any of these fail in a way that requires unsupported global hooks, embedding `wt.exe`, disabling SourceGit NativeAOT, or invasive window-reparent hacks, stop the native-host work. Do not keep the probe code. PR #7 then remains the improved Avalonia terminal implementation only.

If the probe passes, production integration proceeds using the architecture in this spec.

## Startup Handshake

Use a small deterministic startup protocol rather than scraping logs.

The initial implementation may use redirected standard output for a single startup message because each helper process owns exactly one terminal. The first machine-readable line is:

```text
SOURCEGIT_TERMINAL_READY <decimal-hwnd>
```

Any diagnostic text must go to stderr, never stdout before the ready line.

SourceGit waits with a short bounded timeout. Failure cases include:

- helper executable missing;
- process exits before ready;
- malformed HWND;
- timeout;
- native terminal initialization exception.

Any of those failures dispose the partial helper and immediately fall back to `DevSpaceTerminalControl` using the same command and working directory.

There is no retry loop in the first implementation.

## Command and Working Directory

The existing `IDevSpaceSessionLauncher` remains the authority for translating a DevSpaces terminal choice into a launch specification.

The Windows native helper receives the same logical command and working directory used by the Avalonia backend. The helper starts that command through its ConPTY/Windows Terminal backend.

Examples:

- Copilot -> configured `copilot` launch command;
- PowerShell 7 -> `pwsh.exe`;
- Windows PowerShell -> `powershell.exe`;
- Command Prompt -> `cmd.exe`;
- Git Bash -> resolved Git Bash executable/arguments.

No terminal selection logic is duplicated inside the helper.

## DevSpaces Backend Boundary

Introduce a small DevSpaces terminal-surface boundary so `DevSpaceTerminal` does not contain two unrelated process implementations inline.

Conceptually:

```csharp
interface IDevSpaceTerminalSurface : IDisposable
{
    Control View { get; }
    event EventHandler<int> Exited;
    void Start(DevSpaceLaunchSpec spec);
    void SetVisible(bool visible);
}
```

Two implementations:

1. `AvaloniaDevSpaceTerminalSurface`
   - wraps the existing `DevSpaceTerminalControl`;
   - keeps PR #7 full-surface hit testing and Copy/Paste/Select All behavior;
   - remains responsible for the existing embedded PTY path.

2. `WindowsNativeDevSpaceTerminalSurface`
   - available only at runtime on Windows x64 when the helper exists;
   - wraps the Avalonia `NativeControlHost` plus helper process;
   - does not recreate the native child window during layout changes or repository page switches.

`DevSpaceTerminal` chooses native first on Windows x64 and transparently falls back to Avalonia if native startup fails.

## Selection, Copy, Paste, and Mouse Input

### Native Windows backend

The Windows Terminal control owns selection, mouse handling, keyboard input, rendering, and clipboard behavior. SourceGit must not overlay a fake Avalonia selection layer or intercept normal terminal shortcuts.

The purpose of the native backend is to inherit Windows Terminal's interaction semantics rather than reproduce them.

### Avalonia fallback

Keep the already implemented PR #7 behavior:

- whole terminal rectangle hit-testable;
- `Ctrl+C`: copy selection or reach the process when there is no selection;
- `Ctrl+Shift+C`: copy;
- `Ctrl+Shift+V`: paste;
- right-click Copy/Paste/Select All when terminal mouse reporting is inactive;
- mouse-aware TUIs retain right-click ownership.

## Layout and Navigation Lifetime

Native HWNDs do not follow Avalonia opacity/composition exactly, so the current "leave mounted with Opacity=0" behavior is not sufficient by itself for the native backend.

Required behavior:

- Adding another terminal: do not destroy/recreate existing native or Avalonia terminal surfaces.
- Changing Auto / 1x2 / 2x2 / 3x3: resize/reposition existing native HWNDs only.
- Switching History/Local Changes/Stashes: call `SetVisible(false)` for native HWNDs while leaving helper processes and ConPTY sessions alive.
- Returning to DevSpaces: call `SetVisible(true)` and resize to the current pane bounds.
- Closing one DevSpaces session: terminate only that session's terminal backend.
- Closing the repository/worktree or disabling DevSpaces: stop all associated terminal backends.

## Airspace Rules

A native terminal HWND is rendered outside Avalonia's normal composition layer.

Therefore:

- do not place Avalonia controls over the native terminal rectangle;
- terminal headers/tabs/layout controls remain outside the terminal HWND bounds;
- error UI for native startup is shown only after the failed native host is removed and the Avalonia fallback is active;
- clipping/visibility is controlled through the native host rather than relying on Avalonia opacity.

## Packaging and Build

Do not add the Windows WPF helper as a normal project reference of `SourceGit.csproj`.

The existing six-platform `dotnet build -c Release` for SourceGit must remain cross-platform.

For Windows x64 CI/publishing:

1. build/publish `SourceGit.WindowsTerminalHost` separately;
2. copy the helper executable and its required dependencies into a dedicated SourceGit publish subdirectory, for example `native-terminal/win-x64/`;
3. publish SourceGit normally with NativeAOT;
4. verify SourceGit can resolve the helper relative to its application directory.

Windows ARM64 publishing must not include or try to execute the x64 helper.

macOS/Linux jobs must not restore or build the WPF helper project.

The PR Check must add an explicit Windows-x64 helper build step so the helper cannot rot outside the normal gate.

Formatting should also explicitly validate the Windows helper project from a Windows runner because the existing format workflow only formats `src/SourceGit.csproj` on Linux.

## Error Handling

Native hosting is an enhancement, not a startup dependency.

If any native-host step fails:

1. record a diagnostic through SourceGit logging;
2. terminate/dispose any partial helper process or HWND;
3. start the existing Avalonia terminal backend with the same session command and working directory;
4. keep the DevSpaces session alive.

A native-host failure must never crash SourceGit, close sibling terminals, or leave the DevSpaces pane permanently blank.

If the native helper dies after successful startup, mark that terminal exited/failed using the same session state model used by the Avalonia backend. Do not silently create a new process because that would lose terminal state.

## Security and Process Ownership

- Pass command/working-directory data as structured arguments or an encoded payload; do not build an unescaped shell command line from user-controlled path text.
- The helper accepts only the parent HWND and one launch specification required for its session.
- SourceGit owns the helper process handle and terminates it when the session closes.
- The helper must not listen on a network socket.
- No global named service or persistent broker is introduced.

## Verification

The existing DevSpaces test-project exception remains in effect; this feature is UI/native-host integration and the repository still has no dedicated DevSpaces test assembly.

Verification is split into compile/package evidence and runtime acceptance.

### CI

Required green checks on the final PR head:

- existing SourceGit format check;
- SourceGit Windows x64 build/publish;
- SourceGit Windows ARM64 build/publish using Avalonia fallback only;
- SourceGit macOS x64/ARM64 build/publish;
- SourceGit Linux x64/ARM64 build/publish;
- Windows-x64 build/publish of `SourceGit.WindowsTerminalHost`;
- packaging assertion that the native helper exists in the Windows x64 artifact and is absent from non-x64 artifacts.

### Manual Windows x64 acceptance

1. Start SourceGit Release build.
2. Open a worktree and DevSpaces.
3. Create Copilot using the terminal picker.
4. Confirm the native backend is active through a diagnostic/log marker, not visual guessing.
5. Drag-select text and blank-space boundaries.
6. Copy/paste using normal Windows Terminal interactions.
7. Verify Ctrl+C reaches Copilot when appropriate.
8. Open a second terminal and confirm the first terminal state does not reload.
9. Change layouts and confirm both sessions persist.
10. Switch to History/Stashes and back; terminal output/process state remains intact and the native HWND does not bleed over the repository page.
11. Close one terminal; only that helper/process exits.
12. Temporarily remove/rename the helper executable and confirm DevSpaces falls back to the Avalonia renderer instead of failing.

### Manual Windows ARM64 acceptance

Confirm DevSpaces uses the Avalonia backend and continues to work exactly as PR #7 currently does.

## Risks

1. **Unofficial/beta Windows Terminal packaging.** `EasyWindowsTerminalControl` depends on unofficial/beta Windows Terminal packaging and may require maintenance when low-level APIs change. Pin versions and upgrade intentionally.
2. **x64 limitation.** The reviewed WPF package is x64-only. Windows ARM64 remains fallback in this PR.
3. **HWND airspace.** Native terminal surfaces cannot participate in Avalonia composition like normal controls; explicit native visibility/resize handling is mandatory.
4. **Cross-process embedding.** The helper HWND/process handshake is a new boundary and must pass the explicit go/no-go probe before production integration.
5. **Resource cost.** One helper process per native terminal uses more memory than an in-process control. This is accepted for the first implementation to keep failure and lifetime boundaries simple.

## Acceptance Criteria

PR #7 is ready to merge only when:

- the Windows x64 embedding probe has passed all go/no-go criteria;
- SourceGit itself still publishes with NativeAOT;
- Windows x64 includes the separate native terminal helper and can fall back safely;
- Windows ARM64/macOS/Linux continue using the Avalonia terminal;
- existing DevSpaces terminal state is not restarted by add/layout/navigation;
- the final PR Check is green across the existing matrix plus the Windows-native helper checks;
- manual Windows x64 acceptance verifies the HWND/native terminal interaction;
- CI evidence and manual runtime evidence are reported separately.