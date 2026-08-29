# DevSpaces Direct Windows Terminal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the discarded WPF/helper-process experiment with an in-process Windows Terminal native HWND backend for DevSpaces on Windows x64/ARM64 while preserving SourceGit NativeAOT and the existing Avalonia terminal as fallback.

**Architecture:** `SourceGit.exe` directly P/Invokes the pinned flat C ABI in `Microsoft.Terminal.Control.dll`, hosts the returned HWND through Avalonia `NativeControlHost`, and connects it to `Porta.Pty 1.0.7`. Windows x64/ARM64 prefer the native renderer when its RID-specific DLL loads; all failures and all non-Windows platforms use the existing `DevSpaceTerminalControl` fallback from PR #7.

**Tech Stack:** .NET 10, Avalonia 11.3.20, NativeAOT, `Iciclecreek.Avalonia.Terminal` 1.0.12, `Porta.Pty` 1.0.7, `CI.Microsoft.Terminal.Wpf` 1.25.260303002 as a native-binary source only, Win32 `SetWindowSubclass`, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-native-terminal-input-design.md`

## Global Constraints

- Keep `SourceGit.exe` on `net10.0`, Avalonia `11.3.20`, Release NativeAOT, and trimming.
- Keep `Iciclecreek.Avalonia.Terminal` `1.0.12` as the portable fallback.
- Add direct `Porta.Pty` `1.0.7`; do not add another ConPTY implementation.
- Obtain `Microsoft.Terminal.Control.dll` only from pinned package `CI.Microsoft.Terminal.Wpf` `1.25.260303002`; never reference `Microsoft.Terminal.Wpf.dll`.
- No WPF, WinUI, Windows App SDK, helper/broker process, installed `wt.exe`, terminal fork, or Avalonia 12 migration.
- Windows x64 and ARM64 prefer native rendering; macOS/Linux/unsupported Windows architectures use Avalonia fallback.
- Preserve `IDevSpaceSessionLauncher` / `DevSpaceLaunchSpec` as the only command-resolution authority.
- Adding terminals, changing layouts, or switching repository subpages must not restart existing sessions.
- Native `HwndTerminal` owns mouse selection, double/triple click, right-click copy/paste, VT mouse behavior, UI Automation, and rendering.
- SourceGit adds PTY lifecycle, keyboard shortcut forwarding, scrollback wrapper behavior, DPI/resize synchronization, diagnostics, and fallback selection only.
- Keep the previously approved DevSpaces no-new-test-project exception. Verification is source audit + NativeAOT publish + six-platform PR CI + package checks + manual Windows acceptance.
- Do not claim native runtime verification until the Windows x64 manual checklist passes. ARM64 runtime status is reported separately when hardware is unavailable.

---

### Task 1: Remove the helper-process spike and pin dependencies

**Files:**
- Delete: `tools/WindowsTerminalHostProbe.Helper/Program.cs`
- Delete: `tools/WindowsTerminalHostProbe.Helper/WindowsTerminalHostProbe.Helper.csproj`
- Delete: `tools/WindowsTerminalHostProbe/ProbeNativeHost.cs`
- Delete: `tools/WindowsTerminalHostProbe/Program.cs`
- Delete: `tools/WindowsTerminalHostProbe/WindowsTerminalHostProbe.csproj`
- Delete: `docs/superpowers/plans/2026-08-28-devspaces-windows-native-terminal.md`
- Modify: `.github/workflows/build.yml`
- Modify: `src/SourceGit.csproj`

**Interfaces:**
- Consumes: current PR #7 branch.
- Produces: helper-free branch, direct PTY dependency, pinned native package download.

- [ ] **Step 1: Delete the disposable helper probe and superseded helper plan**

Delete exactly the files listed above. Keep the approved spec and this plan.

- [ ] **Step 2: Remove temporary helper CI steps**

Delete the five build/publish/upload steps named:

```text
Build Windows terminal host probe helper
Build Windows terminal host probe
Publish Windows terminal host probe helper
Publish Windows terminal host probe
Upload Windows terminal host probe
```

Do not change the six-platform matrix.

- [ ] **Step 3: Add direct Porta.Pty and package download**

Keep:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.12" />
```

Add:

```xml
<PackageReference Include="Porta.Pty" Version="1.0.7" />
```

On Windows hosts, download the package only as a binary source:

```xml
<ItemGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <PackageDownload Include="CI.Microsoft.Terminal.Wpf" Version="[1.25.260303002]" />
</ItemGroup>
```

Do not add a `PackageReference` to `CI.Microsoft.Terminal.Wpf` and do not add `<UseWPF>true</UseWPF>`.

- [ ] **Step 4: Source-audit the dependency boundary**

Expected:

```text
Porta.Pty 1.0.7 = direct PackageReference
CI.Microsoft.Terminal.Wpf 1.25.260303002 = PackageDownload only
UseWPF = absent
EasyWindowsTerminalControl = absent
WindowsTerminalHostProbe = absent
```

- [ ] **Step 5: Commit**

```text
refactor: replace terminal helper probe with native package source
```

**Deliverable:** PR #7 is ready for direct native interop and contains no helper-process implementation.

---

### Task 2: Add the flat native ABI and Win32 subclass boundary

**Files:**
- Create: `src/Native/WindowsTerminal.cs`
- Create: `src/Native/WindowsTerminalSubclass.cs`

**Interfaces:**
- Consumes: `Microsoft.Terminal.Control.dll`, `comctl32.dll`, `user32.dll`.
- Produces: strict native DLL resolution, terminal ABI wrappers, managed HWND subclass lifecycle.

- [ ] **Step 1: Implement strict RID-local DLL resolution**

`WindowsTerminal.cs` resolves only:

```text
<AppContext.BaseDirectory>/native-terminal/win-x64/Microsoft.Terminal.Control.dll
<AppContext.BaseDirectory>/native-terminal/win-arm64/Microsoft.Terminal.Control.dll
```

Use `RuntimeInformation.ProcessArchitecture` and return unsupported for architectures other than X64/Arm64. Register one `NativeLibrary.SetDllImportResolver` for library name `Microsoft.Terminal.Control`. Never change `PATH`, call `SetDllDirectory`, or search installed Windows Terminal locations.

Expose:

```csharp
internal static bool IsSupported;
internal static string GetLibraryPath();
internal static void EnsureResolver();
```

- [ ] **Step 2: Declare the flat ABI exactly**

Use source-generated `[LibraryImport]` for fixed exports with `CallConvStdcall` and explicit UTF-16 string marshalling where required.

Required wrappers:

```csharp
AvoidBuggyTSFConsoleFlags();
int CreateTerminal(nint parent, out nint hwnd, out nint terminal);
void DestroyTerminal(nint terminal);
void TerminalSendOutput(nint terminal, string data);
int TerminalTriggerResize(nint terminal, int width, int height, out TilSize dimensions);
void TerminalDpiChanged(nint terminal, int dpi);
void TerminalUserScroll(nint terminal, int viewTop);
void TerminalSetFocused(nint terminal, byte focused);
void TerminalSendKeyEvent(nint terminal, ushort vkey, ushort scanCode, ushort flags, byte keyDown);
void TerminalSendCharEvent(nint terminal, char ch, ushort scanCode, ushort flags);
byte TerminalIsSelectionActive(nint terminal);
nint TerminalGetSelection(nint terminal);
```

Define:

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct TilSize
{
    public int X;
    public int Y;
}
```

For `CreateTerminal` and `TerminalTriggerResize`, throw on negative HRESULT with `Marshal.ThrowExceptionForHR`.

- [ ] **Step 3: Define callback registrations and ownership**

Use rooted non-generic delegates:

```csharp
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate void WriteCallback(nint text);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate void ScrollCallback(int viewTop, int viewHeight, int bufferSize);
```

Declare `TerminalRegisterWriteCallback` and `TerminalRegisterScrollCallback` with these callback types. The host must store delegate instances in fields for the complete native-terminal lifetime.

The write callback receives a CoTaskMem UTF-16 pointer. Convert with `Marshal.PtrToStringUni` and always call `Marshal.FreeCoTaskMem` in `finally`.

- [ ] **Step 4: Implement safe HWND subclassing**

`WindowsTerminalSubclass` wraps:

```text
SetWindowSubclass
RemoveWindowSubclass
DefSubclassProc
GetKeyState
SetFocus
SystemParametersInfoW
```

Expose:

```csharp
internal sealed class WindowsTerminalSubclass : IDisposable
{
    internal static WindowsTerminalSubclass Attach(
        nint hwnd,
        Func<uint, nuint, nint, nint?> handler);
}
```

Use a static unmanaged subclass thunk plus `dwRefData` -> `GCHandle` to recover the wrapper instance. Do not replace the native WndProc with `SetWindowLongPtr`. On `WM_NCDESTROY`, detach idempotently before freeing the `GCHandle`.

- [ ] **Step 5: NativeAOT compile checkpoint**

Run on Windows x64:

```powershell
dotnet publish src/SourceGit.csproj -c Release -r win-x64 -o publish-native-abi
```

Expected: NativeAOT publish succeeds with no interop-generation/runtime-marshalling error for callback registration or subclass code.

If stored callback delegates are incompatible with NativeAOT, STOP and reopen the design for a tiny native C ABI shim that adds a callback context pointer. Do not introduce runtime-generated executable trampolines.

- [ ] **Step 6: Commit**

```text
feat: add Windows Terminal native ABI
```

**Deliverable:** SourceGit has a strict, AOT-compatible native interop boundary and no terminal process has been integrated yet.

---

### Task 3: Implement the native HWND host and Porta.Pty bridge

**Files:**
- Create: `src/Views/WindowsTerminalNativeHost.cs`
- Create: `src/DevSpaces/WindowsTerminalDevSpaceSurface.cs`

**Interfaces:**
- Consumes: `WindowsTerminal`, `WindowsTerminalSubclass`, `DevSpaceLaunchSpec`, `Porta.Pty`.
- Produces: one persistent native renderer HWND connected to one PTY session.

- [ ] **Step 1: Create `WindowsTerminalNativeHost : NativeControlHost`**

Own fields for:

```csharp
nint _terminal;
nint _hwnd;
WindowsTerminal.WriteCallback _writeCallback;
WindowsTerminal.ScrollCallback _scrollCallback;
WindowsTerminalSubclass _subclass;
int _viewTop;
int _viewHeight;
int _bufferSize;
int _cols = 80;
int _rows = 25;
```

Expose:

```csharp
internal event Action<string> InputGenerated;
internal event Action<int, int> TerminalResized;
internal bool NativeCreated { get; }
internal void SendOutput(string text);
internal Task CopySelectionAsync();
internal Task PasteAsync();
```

- [ ] **Step 2: Create the terminal only through `CreateNativeControlCore`**

Sequence:

```csharp
WindowsTerminal.EnsureResolver();
WindowsTerminal.AvoidBuggyTSFConsoleFlagsOnce();
WindowsTerminal.CreateTerminalChecked(parent.Handle, out _hwnd, out _terminal);
_writeCallback = OnNativeWrite;
_scrollCallback = OnScroll;
WindowsTerminal.TerminalRegisterWriteCallback(_terminal, _writeCallback);
WindowsTerminal.TerminalRegisterScrollCallback(_terminal, _scrollCallback);
_subclass = WindowsTerminalSubclass.Attach(_hwnd, HandleWindowMessage);
ApplyDpi();
return new PlatformHandle(_hwnd, "HWND");
```

Do not create/reparent a top-level `wt.exe` or WPF window.

- [ ] **Step 3: Convert and free native input ownership**

Use exactly:

```csharp
private void OnNativeWrite(nint text)
{
    if (text == 0)
        return;

    try
    {
        var value = Marshal.PtrToStringUni(text);
        if (!string.IsNullOrEmpty(value))
            InputGenerated?.Invoke(value);
    }
    finally
    {
        Marshal.FreeCoTaskMem(text);
    }
}
```

- [ ] **Step 4: Spawn the PTY from `DevSpaceLaunchSpec` only**

`WindowsTerminalDevSpaceSurface.StartAsync` creates:

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

_pty = await PtyProvider.SpawnAsync(options, _cts.Token);
```

Do not resolve shells/executables again inside the native surface.

- [ ] **Step 5: PTY output -> native renderer**

Run one async reader loop using a stateful `Encoding.UTF8.GetDecoder()` so split multibyte sequences survive reads. For each decoded non-empty chunk call `_host.SendOutput(text)`.

- [ ] **Step 6: Native/clipboard input -> serialized PTY writer**

Use one `Channel<string>` and one writer loop. Both `InputGenerated` and keyboard paste enqueue text. The writer loop UTF-8 encodes and writes to `IPtyConnection.WriterStream`, preventing interleaved writes.

- [ ] **Step 7: Resize and process exit**

Wire `_host.TerminalResized` to `_pty.Resize(cols, rows)`. Subscribe to `_pty.ProcessExited`; raise the surface's `Exited` event once and keep the pane visible until user close.

- [ ] **Step 8: Ordered shutdown**

Order:

```text
stop accepting input -> cancel loops -> kill/dispose PTY -> remove window subclass -> DestroyTerminal -> clear callback roots
```

`DestroyNativeControlCore` must call `DestroyTerminal` once and must not call `DestroyWindow` separately.

- [ ] **Step 9: Commit**

```text
feat: bridge Windows Terminal renderer to DevSpaces PTY
```

**Deliverable:** native renderer and PTY can exchange VT data without WPF/helper processes.

---

### Task 4: Match native host keyboard, focus, scroll, resize, and DPI semantics

**Files:**
- Modify: `src/Views/WindowsTerminalNativeHost.cs`

**Interfaces:**
- Consumes: native HWND/PTTY bridge.
- Produces: usable Windows Terminal-like interaction while leaving mouse/right-click ownership native.

- [ ] **Step 1: Mirror Microsoft's focus/key forwarding**

Subclass handling:

```text
WM_SETFOCUS       -> TerminalSetFocused(true)
WM_KILLFOCUS      -> TerminalSetFocused(false)
WM_MOUSEACTIVATE  -> SetFocus(native hwnd)
WM_KEYDOWN/WM_SYSKEYDOWN -> TerminalSendKeyEvent(..., true)
WM_KEYUP/WM_SYSKEYUP     -> TerminalSendKeyEvent(..., false)
WM_CHAR                    -> TerminalSendCharEvent(...)
```

Unpack scan code/flags from `lParam` exactly as Microsoft's WPF `TerminalContainer` does.

- [ ] **Step 2: Add only the DevSpaces shortcut layer**

Before normal forwarding:

```text
Ctrl+C + native selection       -> copy; consume
Ctrl+C + no selection           -> forward
Ctrl+Shift+C + selection        -> copy; consume
Ctrl+Shift+C + no selection     -> consume
Ctrl+Shift+V                    -> paste; consume
Shift+Insert                    -> paste; consume
Ctrl+V                          -> forward
Ctrl+A                          -> forward
```

Track consumed shortcut keys so the matching generated `WM_CHAR` and key-up do not also reach the PTY.

- [ ] **Step 3: Keyboard copy uses native selection**

Call `TerminalIsSelectionActive`; then call `TerminalGetSelection`, marshal the returned CoTaskMem UTF-16 text, free it in `finally`, and set Avalonia clipboard text on the UI dispatcher. Do not clear selection twice; native `TerminalGetSelection` already clears it.

- [ ] **Step 4: Keyboard paste uses the shared PTY input queue**

Read Avalonia clipboard on the UI dispatcher and enqueue text to the same channel used by the native write callback. Do not add bracketed-paste wrapping in this PR.

- [ ] **Step 5: Leave mouse/right-click/UIA native**

For mouse messages and `WM_GETOBJECT`, return no managed handling so `DefSubclassProc` reaches the native `HwndTerminal`. Do not attach PR #7's Avalonia Copy/Paste/Select All menu to the native HWND.

- [ ] **Step 6: Resize renderer + PTY together**

On non-zero `WM_WINDOWPOSCHANGED`, call `TerminalTriggerResize`, clamp returned dimensions to at least 1x1, then raise `TerminalResized(cols, rows)`. Use an internal recursion guard because `TerminalTriggerResize` itself adjusts the child HWND size.

- [ ] **Step 7: Propagate DPI**

At creation and when `TopLevel.RenderScaling` changes:

```csharp
var dpi = (int)Math.Round(topLevel.RenderScaling * 96.0);
WindowsTerminal.TerminalDpiChanged(_terminal, dpi);
```

Unsubscribe scaling observers during detach/disposal.

- [ ] **Step 8: Mirror host scrollback behavior**

Store `viewTop/viewHeight/bufferSize` from `TerminalRegisterScrollCallback`. On `WM_MOUSEWHEEL`, preserve normal native processing, accumulate wheel delta in 120-unit steps, query Windows wheel-lines through `SystemParametersInfoW(SPI_GETWHEELSCROLLLINES)`, clamp the new viewport to `0..max(0, bufferSize-viewHeight)`, and call `TerminalUserScroll`.

- [ ] **Step 9: Commit**

```text
feat: add native terminal input and resize behavior
```

**Deliverable:** native terminal focus/keys/clipboard/scrollback/resize match the reviewed Windows Terminal host behavior.

---

### Task 5: Add a backend-neutral surface and preserve DevSpaces lifetime

**Files:**
- Create: `src/DevSpaces/IDevSpaceTerminalSurface.cs`
- Create: `src/DevSpaces/AvaloniaDevSpaceTerminalSurface.cs`
- Modify: `src/Views/DevSpaceTerminal.axaml`
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`
- Modify: `src/Views/DevSpaces.axaml.cs`
- Modify: `src/DevSpaces/DevSpacesBootstrap.cs`
- Modify: `src/ViewModels/DevSpaceTerminal.cs`
- Keep: `src/Views/DevSpaceTerminalControl.cs`

**Interfaces:**
- Consumes: native surface + current Avalonia terminal.
- Produces: native-first startup, safe fallback, explicit page visibility, backend diagnostic.

- [ ] **Step 1: Define backend-neutral surface types**

Create:

```csharp
internal sealed class DevSpaceTerminalExitedEventArgs : EventArgs
{
    internal DevSpaceTerminalExitedEventArgs(int exitCode) => ExitCode = exitCode;
    internal int ExitCode { get; }
}

internal interface IDevSpaceTerminalSurface : IDisposable
{
    Control View { get; }
    string BackendName { get; }
    event EventHandler<DevSpaceTerminalExitedEventArgs> Exited;
    Task StartAsync(DevSpaceLaunchSpec spec);
    void SetPageActive(bool active);
    void Stop();
}
```

Native backend name: `Windows Terminal`. Fallback backend name: `Avalonia Terminal`.

- [ ] **Step 2: Wrap the existing Avalonia terminal**

`AvaloniaDevSpaceTerminalSurface` owns one current `DevSpaceTerminalControl`, forwards `ProcessExited`, uses existing `LaunchProcess`/`Kill`, and preserves PR #7's current context menu and full-surface hit testing. It never recreates the control for layout/page changes.

- [ ] **Step 3: Convert `DevSpaceTerminal.axaml` to a persistent surface host**

Replace the hard-coded terminal element with:

```xml
<Grid>
  <Grid x:Name="SurfaceHost"/>
  <!-- existing error overlay remains unchanged -->
</Grid>
```

No native HWND may sit underneath an Avalonia error overlay; failed native startup must remove/dispose native host before fallback/error presentation.

- [ ] **Step 4: Native-first startup with one fallback attempt**

`DevSpaceTerminal.Start` creates the existing `DevSpaceLaunchSpec`, tries `WindowsTerminalDevSpaceSurface` only when `WindowsTerminal.IsSupported`, and on pre-running native startup failure disposes it and starts `AvaloniaDevSpaceTerminalSurface` using the exact same spec.

After a native PTY is running, later process exit/failure does not start fallback because that would lose session state.

- [ ] **Step 5: Record the selected backend for diagnostics**

Add `BackendName` to `ViewModels.DevSpaceTerminal` and change `MarkRunning` to accept the backend name:

```csharp
public void MarkRunning(string backendName)
{
    BackendName = backendName;
    State = DevSpaceTerminalState.Running;
}
```

Also emit:

```csharp
System.Diagnostics.Trace.WriteLine(
    $"[DevSpaces] {Title}: backend={BackendName}");
```

This provides the required diagnostic marker without changing terminal content.

- [ ] **Step 6: Preserve Stop/Dispose and sibling isolation**

`DevSpaceTerminal.Stop` unsubscribes session/surface events, calls current surface `Stop`/`Dispose` exactly once, and never touches sibling panes.

- [ ] **Step 7: Add explicit page-active propagation in the actual view/bootstrap path**

Add to `Views.DevSpaces`:

```csharp
internal void SetPageActive(bool active)
{
    foreach (var pane in _panes.Values)
        pane.TerminalView.SetPageActive(active);
}
```

Add to `Views.DevSpaceTerminal`:

```csharp
internal void SetPageActive(bool active) =>
    _surface?.SetPageActive(active);
```

In `DevSpacesBootstrap.RepositoryIntegration.Update()`, after computing:

```csharp
var active = _repository.SelectedViewIndex == 3;
```

call:

```csharp
(_host.Child as Views.DevSpaces)?.SetPageActive(active);
```

Keep the existing mounted/opacity strategy for Avalonia terminals, but native surface implements `SetPageActive` by setting its `NativeControlHost.IsVisible`. Never rely on opacity to hide an HWND.

- [ ] **Step 8: Verify layout persistence**

`Views.DevSpaces.RebuildGrid()` must continue reusing cached `TerminalPaneHandle` instances. Existing pane `DevSpaceTerminal` controls must not be removed/recreated when a new terminal is added or grid dimensions change.

- [ ] **Step 9: Commit**

```text
feat: prefer native Windows Terminal in DevSpaces
```

**Deliverable:** DevSpaces prefers native Windows rendering, falls back safely, keeps session state through layout/page changes, and exposes which backend is active.

---

### Task 6: Stage native binaries for development and package the correct RID automatically

**Files:**
- Modify: `src/SourceGit.csproj`
- Modify: `.github/workflows/build.yml`
- Modify: `THIRD-PARTY-LICENSES.md`

**Interfaces:**
- Consumes: pinned `PackageDownload`.
- Produces: Windows dev output with both native architectures available; published Windows artifacts with exactly one matching RID payload.

- [ ] **Step 1: Stage both native DLLs for Windows development builds**

Add an `AfterBuild` target conditioned on Windows host and empty `RuntimeIdentifier`:

```xml
<Target Name="StageWindowsTerminalNativeForDevelopment"
        AfterTargets="Build"
        Condition="$([MSBuild]::IsOSPlatform('Windows')) And '$(RuntimeIdentifier)' == ''">
  <PropertyGroup>
    <WindowsTerminalPackageRoot>$(NuGetPackageRoot)ci.microsoft.terminal.wpf\1.25.260303002</WindowsTerminalPackageRoot>
  </PropertyGroup>
  <MakeDir Directories="$(OutDir)native-terminal\win-x64;$(OutDir)native-terminal\win-arm64" />
  <Copy SourceFiles="$(WindowsTerminalPackageRoot)\runtimes\win-x64\native\Microsoft.Terminal.Control.dll"
        DestinationFolder="$(OutDir)native-terminal\win-x64" />
  <Copy SourceFiles="$(WindowsTerminalPackageRoot)\runtimes\win-arm64\native\Microsoft.Terminal.Control.dll"
        DestinationFolder="$(OutDir)native-terminal\win-arm64" />
</Target>
```

Add `<Error>` checks for both source files before copying.

- [ ] **Step 2: Stage only the active RID during publish**

Add:

```xml
<Target Name="StageWindowsTerminalNativeControl"
        AfterTargets="Publish"
        Condition="'$(RuntimeIdentifier)' == 'win-x64' Or '$(RuntimeIdentifier)' == 'win-arm64'">
  <PropertyGroup>
    <WindowsTerminalPackageRoot>$(NuGetPackageRoot)ci.microsoft.terminal.wpf\1.25.260303002</WindowsTerminalPackageRoot>
    <WindowsTerminalNativeSource>$(WindowsTerminalPackageRoot)\runtimes\$(RuntimeIdentifier)\native\Microsoft.Terminal.Control.dll</WindowsTerminalNativeSource>
    <WindowsTerminalNativeTarget>$(PublishDir)native-terminal\$(RuntimeIdentifier)\Microsoft.Terminal.Control.dll</WindowsTerminalNativeTarget>
  </PropertyGroup>
  <Error Condition="!Exists('$(WindowsTerminalNativeSource)')"
         Text="Microsoft.Terminal.Control.dll was not restored for $(RuntimeIdentifier)." />
  <MakeDir Directories="$(PublishDir)native-terminal\$(RuntimeIdentifier)" />
  <Copy SourceFiles="$(WindowsTerminalNativeSource)"
        DestinationFiles="$(WindowsTerminalNativeTarget)" />
</Target>
```

Do not stage `Microsoft.Terminal.Wpf.dll`.

- [ ] **Step 3: Assert package composition in CI**

After normal publish, Windows jobs run:

```yaml
- name: Verify Windows Terminal native payload
  if: startsWith(matrix.runtime, 'win-')
  shell: pwsh
  run: |
    $expected = "publish/native-terminal/${{ matrix.runtime }}/Microsoft.Terminal.Control.dll"
    if (-not (Test-Path $expected)) { throw "Missing $expected" }
    $wrong = if ('${{ matrix.runtime }}' -eq 'win-x64') { 'win-arm64' } else { 'win-x64' }
    if (Test-Path "publish/native-terminal/$wrong") { throw "Wrong native RID packaged: $wrong" }
    if (Test-Path "publish/Microsoft.Terminal.Wpf.dll") { throw "WPF wrapper must not be packaged" }
```

macOS/Linux jobs assert `publish/native-terminal` does not exist before archive creation.

- [ ] **Step 4: Preserve NativeAOT**

Keep existing publish command unchanged:

```text
dotnet publish src/SourceGit.csproj -c Release -o publish -r <RID>
```

Do not set `DisableAOT`, change target framework, or add WPF build properties.

- [ ] **Step 5: Update third-party licenses**

Add:

```text
Microsoft Terminal / Microsoft.Terminal.Control.dll
Version source: CI.Microsoft.Terminal.Wpf 1.25.260303002
License: MIT
Source: https://github.com/microsoft/terminal

Porta.Pty
Version: 1.0.7
License: MIT
Source: https://github.com/tomlm/Porta.Pty
```

Do not describe `Microsoft.Terminal.Wpf.dll` as a runtime dependency.

- [ ] **Step 6: Commit**

```text
build: package native Windows Terminal renderer
```

**Deliverable:** Windows dev builds can resolve the native control locally, and every Windows release artifact automatically contains only its matching native DLL.

---

### Task 7: Final verification and PR #7 cleanup

**Files:**
- Audit: complete diff from `master` to `feat/devspaces-native-terminal-input`.
- Modify: PR #7 description.

**Interfaces:**
- Consumes: completed implementation.
- Produces: exact-head CI/package evidence plus explicit manual-runtime status.

- [ ] **Step 1: Audit forbidden remnants**

Search branch for:

```text
WindowsTerminalHostProbe
SourceGit.WindowsTerminalHost
EasyWindowsTerminalControl
UseWPF
Microsoft.Terminal.Wpf.dll
wt.exe
SetWindowLongPtr
```

Expected: no production/helper architecture remnants. Historical docs may mention superseded approaches only when clearly labeled superseded.

- [ ] **Step 2: Audit exact dependency/platform invariants**

Expected:

```text
TargetFramework net10.0
Avalonia 11.3.20
Iciclecreek.Avalonia.Terminal 1.0.12
Porta.Pty 1.0.7
CI.Microsoft.Terminal.Wpf PackageDownload 1.25.260303002
PublishAot unchanged
no Avalonia 12 / WPF / WinUI / Windows App SDK
```

Also verify callback delegates remain rooted, CoTaskMem input/selection pointers are freed, subclass removal precedes `DestroyTerminal`, and `DestroyTerminal` is idempotent.

- [ ] **Step 3: Run final PR Check on exact head**

Required green jobs:

```text
Format Check
Windows x64 build/publish
Windows ARM64 build/publish
macOS Intel build/publish
macOS Apple Silicon build/publish
Linux x64 build/publish
Linux ARM64 build/publish
```

On any failure invoke `superpowers:systematic-debugging`, inspect the exact failed job log, establish root cause, fix only that cause, and rerun on the new head.

- [ ] **Step 4: Inspect artifacts**

Required:

```text
win-x64   -> native-terminal/win-x64/Microsoft.Terminal.Control.dll only
win-arm64 -> native-terminal/win-arm64/Microsoft.Terminal.Control.dll only
macOS/Linux -> no native-terminal directory
no windows-terminal-host-probe artifact
```

- [ ] **Step 5: Manual Windows x64 acceptance**

Using final Release artifact verify all 14 spec checks:

```text
1. Open worktree/DevSpaces and start Copilot; diagnostic shows backend=Windows Terminal.
2. Drag selection works, including blank boundaries.
3. Double-click selects word; triple-click selects line.
4. Right-click selection copies; right-click with no selection pastes.
5. Ctrl+C copies selection; with no selection it reaches the process.
6. Ctrl+Shift+C copies; Ctrl+Shift+V and Shift+Insert paste; Ctrl+A/Ctrl+V remain app-owned.
7. Normal scrollback wheel works; mouse-aware TUI still receives mouse input.
8. PowerShell 7, Windows PowerShell, Command Prompt, Git Bash use expected working directory.
9. Add second terminal; first state does not reload.
10. Switch Auto/1x2/2x2/3x3; sessions remain alive and resize.
11. Switch History/Stashes and back; HWND fully hides and state persists.
12. Close one terminal; sibling remains alive.
13. Remove native-terminal/win-x64/Microsoft.Terminal.Control.dll; new terminal falls back to Avalonia without crash.
14. Confirm no SourceGit.WindowsTerminalHost or wt.exe child process exists.
```

Do not report native runtime behavior verified until all 14 pass.

- [ ] **Step 6: Windows ARM64 runtime status**

When ARM64 hardware exists, repeat native activation, typing, selection, clipboard, resize, page hide/show, close, and missing-DLL fallback. If hardware is unavailable, report only CI build/package evidence and explicitly say ARM64 UX is not manually verified.

- [ ] **Step 7: Update PR #7 body**

State:

```text
- Windows x64/ARM64 prefer Microsoft.Terminal.Control.dll hosted directly by Avalonia NativeControlHost
- SourceGit remains net10.0/Avalonia 11.3.20/NativeAOT
- Porta.Pty 1.0.7 owns native PTY processes
- no WPF/helper process/installed wt.exe dependency
- Avalonia terminal remains automatic fallback
- native DLL is staged automatically per Windows RID
- CI proves build/package compatibility; manual native interaction is tracked separately
```

- [ ] **Step 8: Invoke verification-before-completion**

Use `superpowers:verification-before-completion` and freshly verify final PR head, CI, artifacts, mergeability, and unresolved review threads before claiming completion.

Do not merge PR #7 unless the user explicitly asks.

**Deliverable:** PR #7 contains only the approved direct-native architecture, stays NativeAOT/cross-platform, packages the correct native DLL automatically, and has honest runtime verification status.
