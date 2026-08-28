# DevSpaces Native Terminal Input Design

## Status

Approved in chat as the replacement architecture for the earlier helper-process design. Pending written-spec review before a new implementation plan is created.

This revision supersedes the earlier `SourceGit.WindowsTerminalHost.exe` / WPF helper-process architecture and the implementation plan at `docs/superpowers/plans/2026-08-28-devspaces-windows-native-terminal.md`.

PR #7 keeps the Avalonia 11 terminal improvements already implemented as the portable fallback and adds a Windows-native renderer by calling the native Windows Terminal control directly from `SourceGit.exe`.

## Problem

DevSpaces currently embeds `Iciclecreek.Avalonia.Terminal` 1.0.12. PR #7 already improves whole-surface selection and adds Copy/Paste/Select All behavior, but the user wants Copilot and shell terminals on Windows to behave like Windows Terminal itself for selection, keyboard input, clipboard behavior, rendering, and mouse handling.

The earlier helper-process spike was useful because it proved the relevant dependencies compile on Windows, but the helper process is not required for the final architecture.

Microsoft's own WPF terminal wrapper is a managed shell around the native `Microsoft.Terminal.Control.dll` exports. Its `TerminalContainer`:

- calls `CreateTerminal(parentHwnd, out childHwnd, out terminal)`;
- registers a terminal write callback;
- sends PTY output into the renderer with `TerminalSendOutput`;
- forwards focus, key, character, DPI, and resize events;
- calls `DestroyTerminal` on teardown.

That native control can therefore be hosted directly under Avalonia's `NativeControlHost` parent HWND without loading WPF into SourceGit.

## Goals

1. Use the Windows Terminal native renderer/input stack for DevSpaces on Windows x64 and Windows ARM64.
2. Keep `SourceGit.exe` on .NET 10, Avalonia 11.3.20, and NativeAOT in Release.
3. Do not introduce a helper terminal process, WPF runtime dependency, WinUI dependency, or installed `wt.exe` dependency.
4. Reuse Porta.Pty 1.0.7 for the PTY/process side so the native renderer does not introduce a second ConPTY implementation.
5. Keep the current Iciclecreek/Avalonia terminal as the automatic fallback on macOS, Linux, unsupported Windows architectures, or any native initialization failure.
6. Preserve the existing DevSpaces session lifecycle: adding terminals, changing layouts, switching History/Local Changes/Stashes, or changing active terminal must not restart an existing session.
7. Preserve the existing terminal picker and `IDevSpaceSessionLauncher` as the authority for command/working-directory resolution.
8. Package the correct Windows Terminal native DLL automatically in Windows release artifacts.
9. Keep PR #7's existing Avalonia selection/copy/paste improvements because they remain the fallback implementation.

## Non-goals

- Embedding or reparenting the installed `wt.exe` application.
- Using WPF `HwndHost` or `EasyWindowsTerminalControl` at runtime.
- Creating `SourceGit.WindowsTerminalHost.exe` or any terminal broker process.
- Building/forking the entire Windows Terminal repository as part of normal SourceGit builds.
- Migrating SourceGit to Avalonia 12, WPF, WinUI 3, or Windows App SDK.
- Providing a native terminal renderer on macOS or Linux in this PR.
- Copilot CLI session-ID persistence across SourceGit restarts.
- Replacing the DevSpaces terminal picker, grid model, or worktree lifecycle.
- Reimplementing Windows Terminal selection/keyboard semantics in Avalonia.

## Platform Matrix

| Platform | Renderer |
| --- | --- |
| Windows x64 | Native Windows Terminal control when available; Avalonia fallback on failure |
| Windows ARM64 | Native Windows Terminal control when available; Avalonia fallback on failure |
| Windows other architecture | Avalonia terminal |
| macOS x64/ARM64 | Avalonia terminal |
| Linux x64/ARM64 | Avalonia terminal |

Renderer selection is internal. Users create Copilot, PowerShell, Command Prompt, Git Bash, or other configured terminals through the same DevSpaces UI.

## Dependency Strategy

### Existing terminal fallback

Keep:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.12" />
```

This remains the cross-platform fallback and already depends on `Porta.Pty` 1.0.7.

### Explicit PTY dependency

Add an explicit SourceGit dependency on the same PTY version rather than relying on a transitive package contract:

```xml
<PackageReference Include="Porta.Pty" Version="1.0.7" />
```

The native Windows renderer uses Porta.Pty directly for process creation, input/output streams, resize, PID, exit code, wait, and kill.

### Native Windows Terminal asset

Use the reviewed Windows Terminal WPF CI package only as a **native asset source**, not as a managed runtime dependency:

```xml
<PackageDownload Include="CI.Microsoft.Terminal.Wpf" Version="[1.25.260303002]" />
```

Do not reference `Microsoft.Terminal.Wpf.dll` and do not enable WPF in `SourceGit.csproj`.

The package version is pinned because the reusable native control ABI is not treated as stable. The package contains `Microsoft.Terminal.Control.dll` under RID-specific native paths for x86, x64, and ARM64. This PR uses only x64 and ARM64.

## Architecture

### SourceGit process

`SourceGit.exe` remains:

- `TargetFramework`: `net10.0`;
- Avalonia 11.3.20;
- NativeAOT + trimming in Release;
- one cross-platform executable project.

The Windows native renderer is ordinary managed code inside SourceGit plus static P/Invoke declarations. No WPF assembly is loaded.

### Terminal surface boundary

Introduce a focused DevSpaces terminal-surface abstraction:

```csharp
internal interface IDevSpaceTerminalSurface : IDisposable
{
    Control View { get; }
    event EventHandler<int> Exited;
    Task StartAsync(DevSpaceLaunchSpec spec);
    void SetPageActive(bool active);
}
```

Two implementations:

1. `AvaloniaDevSpaceTerminalSurface`
   - wraps the existing `DevSpaceTerminalControl` from PR #7;
   - keeps the existing Iciclecreek PTY/rendering path;
   - preserves the full-surface hit testing and Copy/Paste/Select All menu already implemented.

2. `WindowsTerminalDevSpaceSurface`
   - available only on Windows x64/ARM64 when `Microsoft.Terminal.Control.dll` can be loaded;
   - owns one `WindowsTerminalNativeHost` and one Porta.Pty connection;
   - uses the native Windows Terminal HWND for rendering/input;
   - contains no WPF types.

`Views.DevSpaceTerminal` selects the Windows-native surface first on supported Windows platforms. If native startup fails before a usable session is established, it disposes the partial native surface and starts the Avalonia surface using the same `DevSpaceLaunchSpec`.

After a native session has started successfully, a later PTY/process exit is reported as that session's exit. SourceGit must not silently restart it with the fallback because that would destroy terminal state.

## Native Windows Terminal Interop

Create a small interop layer, for example:

`src/Native/WindowsTerminal.cs`

It is responsible only for the native ABI and DLL resolution. It does not own DevSpaces state or PTY process lifetime.

### Native library resolution

Register a `NativeLibrary.SetDllImportResolver` for the SourceGit assembly before the first Windows Terminal call.

For library name `Microsoft.Terminal.Control`, resolve:

- x64 -> `<AppContext.BaseDirectory>/native-terminal/win-x64/Microsoft.Terminal.Control.dll`
- ARM64 -> `<AppContext.BaseDirectory>/native-terminal/win-arm64/Microsoft.Terminal.Control.dll`

Development builds may use the same directory layout under `bin/...`.

If the architecture is unsupported, the file is missing, or `NativeLibrary.Load` fails, native terminal support is unavailable and DevSpaces uses the Avalonia fallback.

Do not modify process-wide `PATH`, use `SetDllDirectory`, or search arbitrary system locations.

### AOT-safe P/Invoke

Use source-generated `LibraryImport` declarations rather than reflection or runtime-generated interop.

Required native exports for the first implementation:

- `AvoidBuggyTSFConsoleFlags`
- `CreateTerminal`
- `DestroyTerminal`
- `TerminalRegisterWriteCallback`
- `TerminalSendOutput`
- `TerminalTriggerResize`
- `TerminalDpiChanged`
- `TerminalSetFocused`
- `TerminalSendKeyEvent`
- `TerminalSendCharEvent`
- `TerminalIsSelectionActive`
- `TerminalGetSelection`

Functions that return HRESULT in the native ABI are declared as integer results and checked explicitly with `Marshal.ThrowExceptionForHR` when negative. Do not rely on `PreserveSig=false` magic in generated interop.

Callback delegates use the exact StdCall signatures expected by the native control and are stored as fields for the full terminal lifetime so the GC cannot collect them while native code holds their function pointers.

### Native handle lifetime

`WindowsTerminalNativeHost : NativeControlHost` owns:

- native terminal object pointer;
- child terminal HWND returned by `CreateTerminal`;
- native callback delegates;
- a Win32 window subclass callback used for input/focus/resize forwarding.

`CreateNativeControlCore(parent)`:

1. verifies Windows x64/ARM64 support;
2. ensures the native library is loaded;
3. calls `AvoidBuggyTSFConsoleFlags()` once per process before terminal creation;
4. calls `CreateTerminal(parent.Handle, out hwnd, out terminal)`;
5. registers callbacks;
6. installs the window subclass;
7. returns `new PlatformHandle(hwnd, "HWND")`.

`DestroyNativeControlCore`:

1. removes the window subclass;
2. clears managed callback references only after native registration is no longer reachable;
3. calls `DestroyTerminal(terminal)` exactly once;
4. zeros the terminal/HWND fields;
5. does not call `DestroyWindow` separately.

Creation/destruction is idempotent from SourceGit's perspective; duplicate cleanup paths must be harmless.

## Windows Message Handling

Microsoft's WPF wrapper demonstrates the required message forwarding. SourceGit reproduces only that native-control adapter behavior, not WPF itself.

Use `SetWindowSubclass`/`RemoveWindowSubclass` from `comctl32` rather than replacing the WndProc with `SetWindowLongPtr`.

The subclass handles:

- `WM_SETFOCUS` -> `TerminalSetFocused(terminal, true)`;
- `WM_KILLFOCUS` -> `TerminalSetFocused(terminal, false)`;
- `WM_MOUSEACTIVATE` -> focus the child HWND;
- `WM_KEYDOWN` / `WM_SYSKEYDOWN` -> unpack vkey/scan-code/flags and call `TerminalSendKeyEvent(..., keyDown: true)`;
- `WM_KEYUP` / `WM_SYSKEYUP` -> call `TerminalSendKeyEvent(..., keyDown: false)`;
- `WM_CHAR` -> call `TerminalSendCharEvent`;
- `WM_WINDOWPOSCHANGED` -> when size is non-zero, call `TerminalTriggerResize` and resize the PTY to the returned columns/rows.

After SourceGit's forwarding logic, call `DefSubclassProc` so the native terminal window still receives normal Win32 processing.

Do not install global keyboard or mouse hooks.

Mouse selection, terminal mouse-reporting mode, right-click behavior, renderer hit testing, and native accessibility remain owned by `Microsoft.Terminal.Control.dll`.

## PTY Process and Data Flow

`WindowsTerminalDevSpaceSurface` owns one `IPtyConnection` from Porta.Pty.

### Start

Use the existing `DevSpaceLaunchSpec` without duplicating shell-resolution logic.

Create:

```csharp
new PtyOptions
{
    App = spec.Process,
    CommandLine = spec.Arguments,
    Cwd = spec.WorkingDirectory,
    Cols = initialColumns,
    Rows = initialRows,
    UseAsyncIo = true,
}
```

Then call:

```csharp
PtyProvider.SpawnAsync(options, cancellationToken)
```

The terminal picker and `LocalDevSpaceSessionLauncher` remain the authority for deciding the process path and arguments.

### PTY -> renderer

Read `IPtyConnection.ReaderStream` asynchronously with UTF-8 decoding that preserves split multibyte sequences. For each decoded text chunk:

```csharp
WindowsTerminal.TerminalSendOutput(terminal, text);
```

Only one reader loop exists per session. Cancellation and disposal must end it without surfacing expected shutdown exceptions as UI failures.

### Renderer -> PTY

Register `TerminalRegisterWriteCallback`.

The callback receives UTF-16 terminal input from the native control. Encode it as UTF-8 and write it asynchronously to `IPtyConnection.WriterStream` through a serialized input channel so concurrent native callbacks cannot interleave writes.

The native terminal itself owns bracketed paste, keyboard translation, selection-aware Ctrl+C behavior, and mouse reporting. SourceGit does not reinterpret these input sequences.

### Resize

When `TerminalTriggerResize` reports a new terminal cell size `(columns, rows)`, call:

```csharp
pty.Resize(columns, rows);
```

Ignore zero-size layout transitions. Resize failures during normal shutdown are ignored; unexpected resize failures are logged but do not recreate the session.

### Exit

Subscribe to `IPtyConnection.ProcessExited`.

On exit:

- stop input/output loops;
- keep final native terminal content visible;
- raise `IDevSpaceTerminalSurface.Exited` with the PTY exit code;
- do not automatically destroy the pane until the existing DevSpaces close flow disposes it.

This preserves the current DevSpaces behavior where an exited terminal remains visible until the user closes it.

## Selection, Copy, Paste, and Clipboard

### Native Windows backend

Normal terminal interaction belongs to Windows Terminal.

Do not show the SourceGit Avalonia Copy/Paste/Select All context menu over the native renderer. Do not intercept Ctrl+C/Ctrl+V/Ctrl+Shift+C/Ctrl+Shift+V in the outer Avalonia view.

For any future SourceGit command that needs selected native text, `TerminalIsSelectionActive` and `TerminalGetSelection` are the supported native access points, but PR #7 does not add a second clipboard UX on top of the native renderer.

### Avalonia fallback

Keep the already implemented PR #7 behavior:

- whole terminal rectangle is hit-testable;
- Copy/Paste/Select All context menu outside TUI mouse-reporting mode;
- terminal-owned keyboard shortcuts remain intact;
- process/session lifetime remains persistent.

## Page Visibility, Layout, and Airspace

`NativeControlHost` tracks native child bounds and effective `IsVisible`, but DevSpaces currently keeps its overall page mounted and uses opacity to preserve terminal state. Native HWNDs are not hidden by Avalonia opacity.

Therefore add explicit page-active propagation:

- DevSpaces active -> native host `IsVisible = true`;
- History/Local Changes/Stashes active -> native host `IsVisible = false`;
- returning to DevSpaces -> `IsVisible = true`;
- never dispose the terminal merely because the repository page changed.

Adding a second terminal or changing Auto / 1x2 / 2x2 / 3x3 must reposition/resize the existing `NativeControlHost` instance, not recreate it.

Airspace rules:

- do not place Avalonia controls over the native terminal rectangle;
- terminal header/close controls stay outside the native HWND area;
- error/fallback UI is shown only after the failed native host has been removed;
- never depend on `Opacity=0` to hide native terminal content.

## Fallback and Error Handling

Native hosting is an optional enhancement, never a SourceGit startup dependency.

Before native session creation, fall back to the Avalonia renderer when any of these occur:

- not Windows x64/ARM64;
- native DLL missing;
- native DLL load failure;
- required export missing;
- `CreateTerminal` failure;
- window subclass installation failure;
- PTY spawn failure before the session is marked running;
- native callback registration failure.

Fallback uses the **same** `DevSpaceLaunchSpec`.

Cleanup of a failed native attempt must remove any subclass, destroy any created native terminal, dispose any partial PTY, cancel reader/writer loops, and then create the Avalonia surface.

After a native session is successfully running, later failures or process exit do not trigger fallback/restart because a new process would lose session state. Instead mark the session exited/failed through the existing DevSpaces state model.

No native terminal error may crash SourceGit or close sibling terminals.

## Packaging and Release

### Source package

`CI.Microsoft.Terminal.Wpf` 1.25.260303002 is restored only to obtain the native control binaries. SourceGit must not ship or reference `Microsoft.Terminal.Wpf.dll`.

### Staging

During build/publish, stage:

```text
native-terminal/win-x64/Microsoft.Terminal.Control.dll
native-terminal/win-arm64/Microsoft.Terminal.Control.dll
```

The exact RID-specific DLL comes from the pinned package's `runtimes/<rid>/native/` directory.

For a published artifact:

- `win-x64` must contain only the x64 native terminal directory;
- `win-arm64` must contain only the ARM64 native terminal directory;
- macOS/Linux artifacts must contain no `native-terminal` directory.

Development Windows builds may stage both x64 and ARM64 folders because the runtime resolver selects only the current architecture; release artifacts stay RID-minimal.

### NativeAOT invariant

The existing SourceGit publish command remains unchanged and must still show NativeAOT publishing.

Do not:

- set `<UseWPF>true</UseWPF>`;
- change SourceGit to `net10.0-windows`;
- disable `<PublishAot>true</PublishAot>`;
- add a WPF project reference;
- add a helper executable.

### Licensing

Update `THIRD-PARTY-LICENSES.md` for:

- `CI.Microsoft.Terminal.Wpf` / Microsoft Terminal source;
- `Microsoft.Terminal.Control.dll`;
- explicit `Porta.Pty 1.0.7` dependency if the repository's existing attribution convention requires it.

Record pinned versions and MIT license attribution without copying unnecessary full license bodies when the repository convention is attribution-only.

## Cleanup of the Superseded Helper Spike

The earlier helper-process spike is disposable and must not survive the new implementation.

Remove before final PR verification:

```text
tools/WindowsTerminalHostProbe/
tools/WindowsTerminalHostProbe.Helper/
```

Remove the temporary probe build/publish/upload steps from `.github/workflows/build.yml`.

Do not create `SourceGit.WindowsTerminalHost`.

The old helper-process implementation plan remains historical documentation only until the new plan is written; the new plan must explicitly mark it superseded or remove it to prevent accidental execution.

## Verification

The previously approved DevSpaces test-project exception continues: SourceGit still has no dedicated DevSpaces test project, and this change is dominated by native HWND integration. Verification therefore separates source/build/package evidence from interactive terminal evidence.

### Source audit

Required final invariants:

- no WPF project or `UseWPF` in SourceGit;
- no helper executable;
- no `wt.exe` process launch;
- no global input hooks;
- `Microsoft.Terminal.Control.dll` is loaded only from SourceGit's staged native-terminal directory;
- `Porta.Pty` version is 1.0.7;
- native callbacks are strongly rooted for the terminal lifetime;
- native terminal destruction happens once;
- existing Avalonia fallback remains available.

### CI

Required green checks on the exact final PR head:

- existing format check;
- SourceGit Windows x64 build + NativeAOT publish;
- SourceGit Windows ARM64 build + NativeAOT publish;
- macOS Intel build/publish;
- macOS Apple Silicon build/publish;
- Linux x64 build/publish;
- Linux ARM64 build/publish;
- win-x64 packaging assertion for x64 `Microsoft.Terminal.Control.dll`;
- win-arm64 packaging assertion for ARM64 `Microsoft.Terminal.Control.dll`;
- macOS/Linux assertion that no Windows native terminal payload is present.

CI proves compilation, AOT compatibility, and package composition. It does not prove native terminal feel.

### Manual Windows x64 acceptance

Using the final `win-x64` artifact:

1. Open a repository/worktree and DevSpaces.
2. Start Copilot and confirm a diagnostic identifies the Windows-native renderer.
3. Drag-select text and blank-space boundaries naturally.
4. Copy and paste using normal Windows Terminal interactions.
5. Verify Ctrl+C reaches Copilot/shell correctly when appropriate.
6. Verify PowerShell 7, Windows PowerShell, Command Prompt, and Git Bash picker entries still launch with the expected working directory.
7. Open a second terminal; the first process/output/selection state must not reload.
8. Change Auto / 1x2 / 2x2 / 3x3; existing sessions remain alive.
9. Switch to History/Stashes and back; the native HWND is completely hidden while inactive and session state remains intact.
10. Close one terminal; only that PTY/session closes.
11. Temporarily remove `native-terminal/win-x64/Microsoft.Terminal.Control.dll`; new sessions must fall back to the Avalonia renderer without crashing SourceGit.
12. Verify no `SourceGit.WindowsTerminalHost` or `wt.exe` child process is created.

### Manual Windows ARM64 acceptance

Using the final `win-arm64` artifact when ARM64 hardware is available:

1. confirm native renderer activation through diagnostics;
2. repeat basic typing, selection, copy/paste, resize, page hide/show, and close behavior;
3. remove the ARM64 native DLL and confirm Avalonia fallback.

Lack of ARM64 hardware does not allow claiming ARM64 UX manually verified; CI packaging/build evidence must be reported separately.

## Risks

1. **Native control ABI stability.** The reusable Windows Terminal control is not treated as a stable public ABI. Mitigation: pin `CI.Microsoft.Terminal.Wpf` 1.25.260303002 and upgrade intentionally with CI + manual acceptance.
2. **NativeAOT interop.** Dynamic/reflection-heavy interop could fail under trimming/AOT. Mitigation: source-generated `LibraryImport`, fixed delegate signatures, explicit DLL resolver, no managed WPF dependency.
3. **Window message integration.** Keyboard/focus behavior depends on forwarding the same message set Microsoft's wrapper handles. Mitigation: reproduce the minimal documented wrapper behavior with `SetWindowSubclass` and test on real Windows.
4. **HWND airspace.** Native windows do not obey Avalonia opacity/composition. Mitigation: explicit `IsVisible` page propagation and no Avalonia overlays over the terminal rectangle.
5. **PTY/renderer synchronization.** Renderer cell-size changes and PTY size must stay synchronized. Mitigation: every non-zero native resize result calls `IPtyConnection.Resize(columns, rows)`.
6. **Callback lifetime.** Native function pointers outlive a temporary managed delegate if it is not rooted. Mitigation: callback delegates are fields owned until after `DestroyTerminal`.
7. **Native DLL availability.** Packaging mistakes could break only Windows native rendering. Mitigation: native startup is optional, package composition is asserted in CI, and missing DLL falls back to Avalonia.

## Acceptance Criteria

PR #7's direct native Windows backend is ready for merge only when:

- the superseded helper probe and temporary CI probe steps are removed;
- SourceGit remains `net10.0`, Avalonia 11.3.20, and NativeAOT;
- no WPF runtime or helper process is introduced;
- Windows x64 and ARM64 artifacts include the correct pinned `Microsoft.Terminal.Control.dll`;
- macOS/Linux artifacts remain free of Windows native terminal payloads;
- Porta.Pty 1.0.7 drives native Windows sessions;
- native HWND sessions survive terminal add/layout/page-navigation without restart;
- native startup failure cleanly falls back to the existing Avalonia renderer;
- final PR CI is green on the exact head;
- manual Windows x64 acceptance passes;
- manual ARM64 status is reported accurately as verified or not verified;
- PR #7 is not merged until the user explicitly asks to merge.