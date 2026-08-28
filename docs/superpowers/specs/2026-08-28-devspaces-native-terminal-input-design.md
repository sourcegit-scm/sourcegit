# DevSpaces Native Terminal Input Design

## Status

Approved in chat as the replacement architecture for the earlier helper-process design. Pending written-spec review before a new implementation plan is created.

This revision supersedes the earlier `SourceGit.WindowsTerminalHost.exe` / WPF helper-process architecture and the implementation plan at `docs/superpowers/plans/2026-08-28-devspaces-windows-native-terminal.md`.

PR #7 keeps the Avalonia 11 terminal improvements already implemented as the portable fallback and adds a Windows-native renderer by calling `Microsoft.Terminal.Control.dll` directly from `SourceGit.exe`.

## Problem

DevSpaces currently embeds `Iciclecreek.Avalonia.Terminal` 1.0.12. PR #7 already improves whole-surface selection and adds Copy/Paste/Select All behavior, but the Windows experience should use the same native terminal renderer/input surface as Windows Terminal rather than reproducing that behavior in Avalonia.

Microsoft's WPF terminal wrapper demonstrates that WPF is not required to use the renderer. Its managed `TerminalContainer` is a thin host around the native flat C ABI:

- `CreateTerminal(parentHwnd, out childHwnd, out terminal)` creates the native child window;
- `TerminalRegisterWriteCallback` returns terminal-generated input to the PTY connection;
- `TerminalSendOutput` sends PTY output into the renderer;
- focus/key/character/DPI/resize messages are forwarded through exported functions;
- `DestroyTerminal` owns native teardown.

The native `HwndTerminal` itself owns selection, double/triple click selection, VT mouse reporting, right-click copy/paste, UI Automation, rendering, and its system-clipboard integration for right-click. SourceGit therefore only needs to host the HWND, connect it to a PTY, supply the wrapper-level keyboard shortcuts/scrollbar behavior that is not part of the flat ABI, and preserve DevSpaces lifecycle.

## Goals

1. Use the Windows Terminal native renderer/input surface for DevSpaces on Windows x64 and Windows ARM64.
2. Keep `SourceGit.exe` on .NET 10, Avalonia 11.3.20, and NativeAOT in Release.
3. Do not introduce a helper terminal process, WPF runtime dependency, WinUI dependency, or installed `wt.exe` dependency.
4. Reuse Porta.Pty 1.0.7 for PTY/process ownership rather than introducing a second ConPTY implementation.
5. Keep `Iciclecreek.Avalonia.Terminal` 1.0.12 as the fallback on macOS, Linux, unsupported Windows architectures, or native initialization failure.
6. Preserve existing DevSpaces session lifetime: terminal add, grid changes, terminal activation, and repository-page navigation must not restart existing sessions.
7. Preserve the existing terminal picker and `IDevSpaceSessionLauncher` as the authority for process path, arguments, and working directory.
8. Automatically include the correct native terminal DLL in Windows release artifacts.
9. Preserve PR #7's existing Avalonia selection/copy/paste improvements for fallback sessions.

## Non-goals

- Embedding or reparenting installed `wt.exe`.
- Using WPF `HwndHost`, `Microsoft.Terminal.Wpf.dll`, or `EasyWindowsTerminalControl` at runtime.
- Creating `SourceGit.WindowsTerminalHost.exe` or another terminal broker process.
- Building/forking the complete Windows Terminal repository during SourceGit builds.
- Migrating SourceGit to Avalonia 12, WPF, WinUI 3, or Windows App SDK.
- Native renderer support on macOS/Linux in this PR.
- Copilot CLI session-ID persistence across SourceGit restarts.
- Replacing the terminal picker, grid model, worktree model, or repository-tab model.

## Platform Matrix

| Platform | Renderer |
| --- | --- |
| Windows x64 | Native Windows Terminal when available; Avalonia fallback on failure |
| Windows ARM64 | Native Windows Terminal when available; Avalonia fallback on failure |
| Other Windows architecture | Avalonia fallback |
| macOS x64/ARM64 | Avalonia fallback |
| Linux x64/ARM64 | Avalonia fallback |

The renderer choice is internal. The same Copilot/PowerShell/Command Prompt/Git Bash picker remains visible to the user.

## Dependency Strategy

### Avalonia fallback

Keep:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.12" />
```

### Explicit PTY dependency

Add the same PTY version used by Iciclecreek 1.0.12 as a direct SourceGit contract:

```xml
<PackageReference Include="Porta.Pty" Version="1.0.7" />
```

Do not rely on a transitive dependency for types used directly by SourceGit.

### Native Windows Terminal source package

Use the reviewed Windows Terminal WPF CI package only as a **native binary source**:

```xml
<PackageDownload Include="CI.Microsoft.Terminal.Wpf" Version="[1.25.260303002]" />
```

Do not reference `Microsoft.Terminal.Wpf.dll` and do not enable WPF in `SourceGit.csproj`.

The version is pinned because the reusable flat native ABI is not treated as a stable public API. The Windows Terminal WPF packaging project places `Microsoft.Terminal.Control.dll` in RID-specific native directories for x86, x64, and ARM64. PR #7 uses x64 and ARM64 only.

## SourceGit Architecture

`SourceGit.exe` remains one cross-platform project:

- `TargetFramework`: `net10.0`;
- Avalonia: 11.3.20;
- NativeAOT and trimming remain enabled in Release;
- no WPF project reference;
- no helper executable.

Introduce a small terminal-surface boundary:

```csharp
internal interface IDevSpaceTerminalSurface : IDisposable
{
    Control View { get; }
    event EventHandler<int> Exited;
    Task StartAsync(DevSpaceLaunchSpec spec);
    void SetPageActive(bool active);
}
```

Implementations:

1. `AvaloniaDevSpaceTerminalSurface`
   - wraps the existing `DevSpaceTerminalControl` from PR #7;
   - retains Iciclecreek rendering/PTTY behavior and PR #7 clipboard menu behavior.

2. `WindowsTerminalDevSpaceSurface`
   - used only on supported Windows when the native control can be loaded;
   - owns one `WindowsTerminalNativeHost` and one `IPtyConnection`;
   - uses Windows Terminal for rendering, mouse selection, right-click clipboard, accessibility, and VT mouse handling;
   - uses SourceGit only for PTY lifecycle and the wrapper-level keyboard shortcuts/scrollback behavior described below.

`Views.DevSpaceTerminal` attempts the Windows-native surface first on Windows x64/ARM64. A startup failure disposes the partial native surface and starts the Avalonia surface with the same `DevSpaceLaunchSpec`.

After a native PTY has successfully started, later exit/failure does not create a fallback process because that would lose session state. It is reported through the existing DevSpaces exited/failed state.

## Native ABI Layer

Create `src/Native/WindowsTerminal.cs`. Its only responsibilities are native ABI definitions and native DLL resolution.

### DLL resolution

Register one `NativeLibrary.SetDllImportResolver` for the SourceGit assembly before the first Windows Terminal call.

Resolve library name `Microsoft.Terminal.Control` only from:

- x64: `<AppContext.BaseDirectory>/native-terminal/win-x64/Microsoft.Terminal.Control.dll`
- ARM64: `<AppContext.BaseDirectory>/native-terminal/win-arm64/Microsoft.Terminal.Control.dll`

Do not modify `PATH`, call `SetDllDirectory`, search the installed Windows Terminal application, or load an arbitrary DLL from the system.

Unsupported architecture, missing file, load failure, or missing export means native support is unavailable and the session uses the Avalonia fallback.

### AOT-safe imports

Use source-generated `LibraryImport` declarations and fixed unmanaged signatures. Do not use reflection or runtime-generated interop.

Required exports:

- `AvoidBuggyTSFConsoleFlags`
- `CreateTerminal`
- `DestroyTerminal`
- `TerminalRegisterScrollCallback`
- `TerminalRegisterWriteCallback`
- `TerminalSendOutput`
- `TerminalTriggerResize`
- `TerminalDpiChanged`
- `TerminalUserScroll`
- `TerminalSetFocused`
- `TerminalSendKeyEvent`
- `TerminalSendCharEvent`
- `TerminalIsSelectionActive`
- `TerminalGetSelection`

For HRESULT-returning exports (`CreateTerminal`, `TerminalTriggerResize`), declare the integer HRESULT and call `Marshal.ThrowExceptionForHR` on negative values.

Use exact StdCall calling conventions. Represent C++ `bool` parameters/returns with one-byte marshalling (or a byte in the managed ABI wrapper) rather than assuming Win32 `BOOL` layout.

The terminal write callback and scroll callback are stored as fields for the full native-terminal lifetime. The native code transfers a CoTaskMem UTF-16 string to the registered write callback; the callback must convert it and free the pointer in `finally`. `TerminalGetSelection` likewise returns CoTaskMem text that SourceGit converts and frees.

## Native HWND Host

`WindowsTerminalNativeHost : NativeControlHost` owns:

- terminal object pointer;
- child HWND;
- terminal write callback delegate;
- terminal scroll callback delegate;
- Win32 window-subclass delegate;
- current scroll state (`viewTop`, `viewHeight`, `bufferSize`);
- current renderer columns/rows;
- a reference back to its `WindowsTerminalDevSpaceSurface` for PTY input/resize.

### Create

`CreateNativeControlCore(parent)`:

1. verifies Windows x64/ARM64 and native-DLL availability;
2. calls `AvoidBuggyTSFConsoleFlags()` once per SourceGit process;
3. calls `CreateTerminal(parent.Handle, out hwnd, out terminal)`;
4. registers write and scroll callbacks;
5. installs a per-window subclass with `SetWindowSubclass` from `comctl32`;
6. applies the current Avalonia render scaling via `TerminalDpiChanged` (`round(RenderScaling * 96)`);
7. returns `new PlatformHandle(hwnd, "HWND")`.

Do not use `SetWindowLongPtr` to replace the terminal WndProc. `SetWindowSubclass`/`RemoveWindowSubclass` keeps the native control's own WndProc in the chain.

### Destroy

`DestroyNativeControlCore` is idempotent and ordered:

1. remove the window subclass if installed;
2. prevent new PTY writes/resizes from reaching native code;
3. call `DestroyTerminal(terminal)` exactly once;
4. zero the terminal/HWND fields;
5. release managed callback roots after native destruction;
6. do not call `DestroyWindow` separately.

The native `DestroyTerminal` path owns the native child window teardown.

## Native Mouse, Selection, Right-Click, and Accessibility

Do not recreate these features in SourceGit.

`HwndTerminal` already:

- handles left-button selection and drag selection;
- handles double-click word selection and triple-click line selection;
- forwards mouse input to terminal applications when VT mouse reporting is active (with Shift as the selection override);
- performs right-click copy when a selection is active;
- performs right-click paste when there is no selection;
- exposes native UI Automation through `WM_GETOBJECT`;
- renders through the Windows Terminal native renderer.

Therefore the native surface must not attach PR #7's Avalonia Copy/Paste/Select All context menu. It lets the native HWND process mouse/right-click messages through `DefSubclassProc`.

## Keyboard Shortcut Layer

The flat native ABI accepts key/character events but does not expose Windows Terminal application's keybinding/action layer. SourceGit must provide only the small wrapper-level shortcut set required by DevSpaces.

Before forwarding a key message to the terminal engine, the window subclass reads Ctrl/Shift state with `GetKeyState` and applies these rules:

- `Ctrl+C` with active selection -> copy selection; consume the shortcut.
- `Ctrl+C` with no selection -> do not intercept; forward normally so the PTY receives Ctrl+C/SIGINT semantics.
- `Ctrl+Shift+C` with active selection -> copy selection; consume the shortcut.
- `Ctrl+Shift+C` with no selection -> consume it without sending Ctrl+C to the application.
- `Ctrl+Shift+V` -> paste clipboard text to the PTY; consume the shortcut.
- `Shift+Insert` -> paste clipboard text to the PTY; consume the shortcut.
- `Ctrl+V` -> application-owned; forward normally.
- `Ctrl+A` -> application-owned; forward normally.
- all other keys -> match Microsoft's WPF adapter behavior by forwarding `WM_KEYDOWN`/`WM_SYSKEYDOWN`, `WM_KEYUP`/`WM_SYSKEYUP`, and `WM_CHAR` through `TerminalSendKeyEvent`/`TerminalSendCharEvent`.

### Keyboard copy

Copy uses the native selection domain:

1. confirm `TerminalIsSelectionActive(terminal)`;
2. call `TerminalGetSelection(terminal)`;
3. convert the returned CoTaskMem UTF-16 pointer to a managed string;
4. free the CoTaskMem pointer;
5. set the text through the current Avalonia `IClipboard` on the UI dispatcher.

`TerminalGetSelection` clears the selection; that matches the native HwndTerminal copy behavior.

### Keyboard paste

Paste reads text from Avalonia `IClipboard` and writes that text to the PTY input queue. This matches HwndTerminal's own right-click paste path, which reads `CF_UNICODETEXT` and sends the text directly to its write callback.

No bracketed-paste parser is added in SourceGit for this PR; the native HwndTerminal right-click path itself is also a direct text write.

### Suppressing translated characters

Because Win32 `TranslateMessage` can queue a `WM_CHAR` for a shortcut before the subclass consumes `WM_KEYDOWN`, the host tracks intercepted virtual keys and suppresses the corresponding generated character/key-up messages for copy/paste shortcuts. This prevents a consumed copy/paste shortcut from also reaching the PTY as a control character.

Clipboard failures are caught and logged; they do not stop or recreate the PTY session.

## Scrollback and Resize Layer

The flat HwndTerminal native window owns its text buffer but expects the host wrapper to provide scrollback-wheel movement. SourceGit mirrors the minimal WPF wrapper behavior.

### Scroll state

Register `TerminalRegisterScrollCallback`. The callback updates:

- `viewTop`;
- `viewHeight`;
- `bufferSize`.

### Mouse wheel

On `WM_MOUSEWHEEL`, after preserving the native window's normal processing, accumulate wheel delta using the Windows wheel constant (120) and move `viewTop` by the system wheel-line setting, clamped to:

```text
0 .. max(0, bufferSize - viewHeight)
```

Call `TerminalUserScroll(terminal, newViewTop)` for the resulting position.

The native HWND still receives the mouse event through `DefSubclassProc`; in alternate-screen/mouse-aware TUIs the scrollback range is normally zero, so wrapper scrollback movement is a no-op while the native control can emit VT mouse input.

### Renderer + PTY resize

On non-zero `WM_WINDOWPOSCHANGED` size:

1. call `TerminalTriggerResize(terminal, pixelWidth, pixelHeight, out dimensions)`;
2. store returned columns/rows;
3. call `IPtyConnection.Resize(columns, rows)` when a PTY exists.

The initial renderer/PTTY size is 80 columns x 25 rows, matching HwndTerminal initialization, until Avalonia supplies the actual host size.

Ignore zero-size layout transitions. Expected resize errors during disposal are ignored; unexpected resize failures are logged without restarting the session.

### DPI

At creation, use the containing Avalonia `TopLevel.RenderScaling` to call `TerminalDpiChanged`.

While attached, observe `TopLevel.RenderScalingProperty`; when it changes, call `TerminalDpiChanged(round(RenderScaling * 96))` and let the following layout resize synchronize renderer and PTY dimensions. Unsubscribe when the native host detaches/disposes.

## PTY Process and Data Flow

`WindowsTerminalDevSpaceSurface` owns one Porta.Pty `IPtyConnection` and cancellation state.

### Spawn

Use the existing `DevSpaceLaunchSpec` directly:

```csharp
var options = new PtyOptions
{
    App = spec.Process,
    CommandLine = spec.Arguments,
    Cwd = spec.WorkingDirectory,
    Cols = 80,
    Rows = 25,
    UseAsyncIo = true,
};
```

Then:

```csharp
_pty = await PtyProvider.SpawnAsync(options, cancellationToken);
```

Do not duplicate executable/shell resolution inside the native surface.

### PTY -> native renderer

Run exactly one asynchronous reader loop over `IPtyConnection.ReaderStream`. Decode UTF-8 with a stateful decoder so a multibyte character split across reads is preserved. For every decoded non-empty chunk call:

```csharp
TerminalSendOutput(terminal, text);
```

The native terminal's internal locking/render path is designed to accept connection output without WPF dispatcher marshalling, matching Microsoft's wrapper.

### Native renderer -> PTY

The native write callback receives terminal-generated UTF-16 text. Convert it to a managed string, free the callback's CoTaskMem pointer, then enqueue the text to a single serialized writer loop.

The writer loop UTF-8 encodes queued input and writes it to `IPtyConnection.WriterStream`. A single writer avoids interleaving concurrent callback/clipboard writes.

Keyboard paste goes through this same serialized PTY-input queue.

### Process exit

Subscribe to `IPtyConnection.ProcessExited`.

On exit:

- stop accepting new input;
- allow already-read final output to render;
- raise `IDevSpaceTerminalSurface.Exited` with the PTY exit code once;
- keep the native pane/scrollback visible until the existing user close action disposes it.

Closing a session cancels loops, kills/disposes the PTY if still running, then destroys the native terminal.

## Page Visibility, Layout, and Airspace

Avalonia `NativeControlHost` handles child HWND positioning, but native HWNDs do not obey Avalonia opacity.

Add explicit page-active propagation:

- DevSpaces active -> native host `IsVisible = true`;
- History/Local Changes/Stashes active -> native host `IsVisible = false`;
- returning to DevSpaces -> native host `IsVisible = true`;
- repository page switches never dispose the terminal surface.

Adding another terminal or changing Auto / 1x2 / 2x2 / 3x3 reuses the existing `NativeControlHost`; Avalonia changes its bounds and the native resize path updates renderer + PTY.

Airspace rules:

- no Avalonia overlay may cover the native terminal rectangle;
- terminal pane header/close controls remain outside that rectangle;
- error UI appears only after a failed native host is removed and fallback is active;
- never use `Opacity=0` as the only mechanism to hide a native terminal.

## Fallback and Error Handling

Native hosting is optional and must never become a SourceGit startup dependency.

Before a native session is established, fall back to the Avalonia surface when any of these occur:

- unsupported platform/architecture;
- native DLL missing or load failure;
- required export missing;
- `CreateTerminal` failure;
- callback registration failure;
- window subclass installation failure;
- PTY spawn failure;
- native host initialization exception.

Fallback reuses the exact same `DevSpaceLaunchSpec`.

Partial native cleanup removes the subclass, stops/cancels PTY work, disposes any PTY, destroys any created terminal once, releases callbacks, then starts the Avalonia surface.

After a native PTY is running, later process exit or runtime failure is reported to the current DevSpaces session and does not auto-restart with the fallback.

No native terminal failure may crash SourceGit or close sibling terminals.

## Packaging and Release

### Package source

`CI.Microsoft.Terminal.Wpf` 1.25.260303002 is restored only to obtain RID-native `Microsoft.Terminal.Control.dll`. SourceGit does not ship/reference `Microsoft.Terminal.Wpf.dll`.

### Development staging

Windows development builds stage:

```text
native-terminal/win-x64/Microsoft.Terminal.Control.dll
native-terminal/win-arm64/Microsoft.Terminal.Control.dll
```

This allows the runtime resolver to select the current process architecture without changing `PATH`.

### Release staging

Published artifacts are RID-minimal:

- `win-x64` -> only `native-terminal/win-x64/Microsoft.Terminal.Control.dll`;
- `win-arm64` -> only `native-terminal/win-arm64/Microsoft.Terminal.Control.dll`;
- macOS/Linux -> no `native-terminal` directory.

CI must fail if the expected native package path does not exist or the wrong architecture directory appears in an artifact.

### NativeAOT invariant

The existing SourceGit publish command remains the authoritative application publish. Do not:

- set `<UseWPF>true</UseWPF>`;
- change SourceGit to `net10.0-windows`;
- disable `PublishAot`/trimming;
- add a WPF project;
- add a helper executable.

### Licensing

Update `THIRD-PARTY-LICENSES.md` with pinned package/upstream/license attribution for:

- `CI.Microsoft.Terminal.Wpf` / Microsoft Terminal and `Microsoft.Terminal.Control.dll`;
- explicit `Porta.Pty` 1.0.7 if the repository's current attribution convention requires it.

## Cleanup of Superseded Helper Spike

The prior helper-process probe is disposable and must be removed before final verification:

```text
tools/WindowsTerminalHostProbe/
tools/WindowsTerminalHostProbe.Helper/
```

Remove its temporary build/publish/upload steps from `.github/workflows/build.yml`.

Do not create `SourceGit.WindowsTerminalHost`.

The new implementation plan must mark `docs/superpowers/plans/2026-08-28-devspaces-windows-native-terminal.md` superseded or remove it so it cannot be executed accidentally.

## Verification

The previously approved DevSpaces no-new-test-project exception remains in effect. Verification separates source/build/package evidence from interactive native behavior.

### Source audit

Final branch must show:

- no WPF project/`UseWPF`;
- no helper executable;
- no `wt.exe` launch;
- no global input hooks;
- native DLL resolution restricted to SourceGit's staged directory;
- `Porta.Pty` pinned to 1.0.7;
- native callback delegates rooted for terminal lifetime;
- CoTaskMem callback/selection pointers freed;
- window subclass removed before native destruction;
- `DestroyTerminal` executed once;
- Avalonia fallback retained.

### CI

Required green checks on the exact final PR head:

- format check;
- Windows x64 SourceGit build + NativeAOT publish;
- Windows ARM64 SourceGit build + NativeAOT publish;
- macOS Intel build/publish;
- macOS Apple Silicon build/publish;
- Linux x64 build/publish;
- Linux ARM64 build/publish;
- win-x64 artifact contains only x64 `Microsoft.Terminal.Control.dll` payload;
- win-arm64 artifact contains only ARM64 payload;
- macOS/Linux artifacts contain no Windows native-terminal payload;
- temporary helper-probe artifact is gone.

CI proves compilation, AOT compatibility, and package composition only.

### Manual Windows x64 acceptance

Using the final `win-x64` artifact:

1. Open a repository/worktree and DevSpaces.
2. Start Copilot and confirm a diagnostic identifies the Windows-native renderer.
3. Drag-select text; double-click selects a word; triple-click selects a line.
4. Right-click selection copies; right-click without selection pastes.
5. Ctrl+C copies when selection exists and interrupts the process when no selection exists.
6. Ctrl+Shift+C copies; Ctrl+Shift+V and Shift+Insert paste; Ctrl+A/Ctrl+V remain application-owned as specified.
7. Mouse wheel scrolls normal scrollback; a mouse-aware TUI still receives its mouse interaction.
8. PowerShell 7, Windows PowerShell, Command Prompt, and Git Bash launch in the expected working directory.
9. Add a second terminal; the first terminal process/output state does not reload.
10. Change Auto / 1x2 / 2x2 / 3x3; sessions remain alive and resize correctly.
11. Switch to History/Stashes and back; the HWND is completely hidden while inactive and state persists.
12. Close one terminal; only that PTY/session closes.
13. Remove `native-terminal/win-x64/Microsoft.Terminal.Control.dll`; new sessions fall back to Avalonia without crashing.
14. Confirm no `SourceGit.WindowsTerminalHost` or `wt.exe` child process is created.

### Manual Windows ARM64 acceptance

When ARM64 hardware is available, repeat native activation, typing, selection, clipboard, resize, hide/show, close, and missing-DLL fallback. If ARM64 hardware is unavailable, report only CI build/package evidence and do not claim ARM64 UX verification.

## Risks

1. **Native ABI stability.** The reusable flat control ABI is not treated as stable. Mitigation: pin `CI.Microsoft.Terminal.Wpf` 1.25.260303002 and upgrade intentionally.
2. **NativeAOT interop.** Mitigation: fixed `LibraryImport` signatures, explicit resolver, no managed WPF dependency, and Windows NativeAOT CI.
3. **Keyboard wrapper semantics.** The flat control lacks Windows Terminal application's keybinding layer. Mitigation: keep SourceGit's wrapper shortcuts intentionally small and test Ctrl+C/copy/paste behavior manually.
4. **Scrollback wrapper behavior.** Mouse wheel scrollback is a wrapper responsibility while VT mouse handling is native. Mitigation: mirror Microsoft's host behavior and verify full-screen TUI + normal scrollback separately.
5. **HWND airspace.** Mitigation: explicit `IsVisible` propagation and no Avalonia overlays over native HWNDs.
6. **PTY/renderer synchronization.** Mitigation: every non-zero native resize updates the Porta.Pty dimensions.
7. **Callback/memory lifetime.** Mitigation: root delegates, free transferred CoTaskMem pointers, remove subclass before native destruction.
8. **Package composition.** Mitigation: exact RID assertions in CI and automatic Avalonia fallback when native DLL loading fails.

## Acceptance Criteria

PR #7's direct native Windows backend is merge-ready only when:

- superseded helper probe/code/workflow steps are removed;
- SourceGit remains `net10.0`, Avalonia 11.3.20, NativeAOT;
- no WPF runtime/helper process/`wt.exe` embedding is introduced;
- Windows x64 and ARM64 release artifacts automatically include their correct pinned native control DLL;
- macOS/Linux artifacts do not include Windows native payloads;
- Porta.Pty 1.0.7 owns native Windows PTY sessions;
- keyboard shortcuts, native mouse selection/right-click, scrollback, resize, and page visibility follow this spec;
- terminal add/layout/page navigation does not restart sessions;
- native startup failure falls back cleanly to the existing Avalonia renderer;
- final PR CI is green on the exact final head;
- manual Windows x64 acceptance passes;
- ARM64 manual status is reported accurately;
- PR #7 is not merged until the user explicitly asks to merge.