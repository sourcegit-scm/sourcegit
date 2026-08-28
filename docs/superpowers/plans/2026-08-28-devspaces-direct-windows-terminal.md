# DevSpaces Direct Windows Terminal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the discarded WPF/helper-process experiment with an in-process Windows Terminal native HWND backend for DevSpaces on Windows x64/ARM64 while preserving SourceGit NativeAOT and the existing Avalonia terminal as fallback.

**Architecture:** `SourceGit.exe` directly P/Invokes the pinned flat C ABI in `Microsoft.Terminal.Control.dll`, hosts the returned HWND through Avalonia `NativeControlHost`, and connects it to `Porta.Pty 1.0.7`. Windows x64/ARM64 prefer the native renderer when its RID-specific DLL loads; all failures and all non-Windows platforms use the existing `DevSpaceTerminalControl` fallback from PR #7.

**Tech Stack:** .NET 10, Avalonia 11.3.20, NativeAOT, `Iciclecreek.Avalonia.Terminal` 1.0.12, `Porta.Pty` 1.0.7, `CI.Microsoft.Terminal.Wpf` 1.25.260303002 as a native-binary source only, Win32 `SetWindowSubclass`, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-native-terminal-input-design.md`

## Global Constraints

- Keep `SourceGit.exe` on `net10.0`, Avalonia `11.3.20`, and Release NativeAOT/trimming.
- Keep `Iciclecreek.Avalonia.Terminal` `1.0.12` as the portable fallback.
- Add direct `Porta.Pty` `1.0.7`; do not add a second ConPTY implementation.
- Obtain `Microsoft.Terminal.Control.dll` only from pinned package `CI.Microsoft.Terminal.Wpf` `1.25.260303002`; do not reference its managed WPF assembly.
- No WPF, WinUI, Windows App SDK, helper/broker process, installed `wt.exe`, terminal fork, or Avalonia 12 migration.
- Windows x64 and ARM64 prefer native rendering; macOS/Linux/unsupported Windows architectures always use Avalonia fallback.
- Preserve the current `IDevSpaceSessionLauncher` and `DevSpaceLaunchSpec` contract.
- Adding terminals, changing layouts, or switching repository subpages must not restart existing sessions.
- Native `HwndTerminal` owns mouse selection, double/triple click, right-click copy/paste, VT mouse behavior, UI Automation, and rendering.
- SourceGit adds only PTY lifecycle, keyboard shortcut forwarding, scrollback wrapper behavior, DPI/resize synchronization, and fallback selection.
- Keep the previously approved DevSpaces test-project exception: do not create a dedicated DevSpaces test assembly solely for this native UI integration. Verification is source audit + NativeAOT publish + six-platform PR CI + manual Windows acceptance.
- Do not call the native backend runtime-verified until the manual Windows x64 checklist passes; ARM64 runtime verification remains separate if no ARM64 machine is available.

---

### Task 1: Remove the obsolete helper-process probe and pin native binary acquisition

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
- Consumes: current PR #7 feature branch.
- Produces: no helper-process code; direct `Porta.Pty` contract; pinned native package download available for Windows publish RIDs.

- [ ] **Step 1: Remove all helper-probe files and the superseded helper-process plan**

Delete exactly the five files under `tools/WindowsTerminalHostProbe*` listed above and delete the superseded helper-process plan. Keep this direct-native plan and the approved spec.

- [ ] **Step 2: Remove the temporary helper-probe CI steps**

Delete the five Windows-x64 steps named:

```text
Build Windows terminal host probe helper
Build Windows terminal host probe
Publish Windows terminal host probe helper
Publish Windows terminal host probe
Upload Windows terminal host probe
```

Do not alter the normal six-platform matrix in this step.

- [ ] **Step 3: Add the explicit PTY dependency and native package download**

In `src/SourceGit.csproj`, keep:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.12" />
```

Add:

```xml
<PackageReference Include="Porta.Pty" Version="1.0.7" />
```

Add the native package download only when publishing the supported Windows RIDs:

```xml
<ItemGroup Condition="'$(RuntimeIdentifier)' == 'win-x64' Or '$(RuntimeIdentifier)' == 'win-arm64'">
  <PackageDownload Include="CI.Microsoft.Terminal.Wpf" Version="[1.25.260303002]" />
</ItemGroup>
```

Do not add a `PackageReference` to `CI.Microsoft.Terminal.Wpf`; SourceGit must never compile against `Microsoft.Terminal.Wpf.dll`.

- [ ] **Step 4: Verify the cleanup and dependency graph**

Run/source-audit:

```powershell
Get-ChildItem tools -Recurse | Select-String 'WindowsTerminalHostProbe|EasyWindowsTerminalControl'
Select-String -Path src/SourceGit.csproj -Pattern 'Porta.Pty|CI.Microsoft.Terminal.Wpf|UseWPF'
```

Expected:

```text
- no WindowsTerminalHostProbe/EasyWindowsTerminalControl source remains
- Porta.Pty 1.0.7 is a PackageReference
- CI.Microsoft.Terminal.Wpf is PackageDownload only
- no UseWPF entry exists
```

- [ ] **Step 5: Commit**

```text
refactor: replace terminal helper probe with native package source
```

**Deliverable:** PR #7 no longer contains the abandoned helper-process architecture and is ready for direct native interop.

---

### Task 2: Add the Windows Terminal flat-ABI and Win32 interop layer

**Files:**
- Create: `src/Native/WindowsTerminal.cs`
- Create: `src/Native/WindowsTerminalSubclass.cs`

**Interfaces:**
- Consumes: `Microsoft.Terminal.Control.dll` flat ABI and Win32 `comctl32/user32`.
- Produces: `WindowsTerminal.IsSupported`, `CreateTerminal`, output/input/selection/resize/focus helpers, `WindowsTerminalSubclass` attach/detach callback routing.

- [ ] **Step 1: Create a strict native-library resolver**

`WindowsTerminal.cs` must register one resolver for library name `Microsoft.Terminal.Control` and only load:

```text
<AppContext.BaseDirectory>/native-terminal/win-x64/Microsoft.Terminal.Control.dll
<AppContext.BaseDirectory>/native-terminal/win-arm64/Microsoft.Terminal.Control.dll
```

Choose the folder with `RuntimeInformation.ProcessArchitecture`. Return unavailable for any architecture other than `X64` or `Arm64`.

Use this shape:

```csharp
internal static partial class WindowsTerminal
{
    internal const string LibraryName = "Microsoft.Terminal.Control";

    internal static bool IsSupported =>
        OperatingSystem.IsWindows() &&
        (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.Arm64) &&
        File.Exists(GetLibraryPath());

    internal static void EnsureResolver()
    {
        if (Interlocked.Exchange(ref _resolverRegistered, 1) != 0)
            return;

        NativeLibrary.SetDllImportResolver(
            typeof(WindowsTerminal).Assembly,
            static (name, assembly, path) =>
            {
                if (name != LibraryName)
                    return IntPtr.Zero;

                var file = GetLibraryPath();
                return File.Exists(file) && NativeLibrary.TryLoad(file, out var handle)
                    ? handle
                    : IntPtr.Zero;
            });
    }
}
```

Do not modify `PATH`, use `SetDllDirectory`, or search installed Windows Terminal locations.

- [ ] **Step 2: Declare the exact flat ABI**

Use `[LibraryImport]` for fixed exports and `UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])`.

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

For HRESULT-returning methods, call `Marshal.ThrowExceptionForHR(hr)` when `hr < 0`.

- [ ] **Step 3: Define and root native callbacks explicitly**

Because each terminal stores its own callback function pointer, define non-generic delegate types:

```csharp
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate void WriteCallback(nint text);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate void ScrollCallback(int viewTop, int viewHeight, int bufferSize);
```

Declare registration functions with these delegate parameters and keep the delegate instances in fields on the native host for the entire terminal lifetime. Do not create callback delegates inline at registration time.

`WriteCallback` receives CoTaskMem UTF-16 ownership. The eventual host callback must always call `Marshal.FreeCoTaskMem(text)` in `finally` after `Marshal.PtrToStringUni(text)`.

- [ ] **Step 4: Create the Win32 subclass wrapper using `SetWindowSubclass`**

`WindowsTerminalSubclass.cs` must declare:

```csharp
SetWindowSubclass
RemoveWindowSubclass
DefSubclassProc
GetKeyState
SetFocus
```

Use `SetWindowSubclass`, not `SetWindowLongPtr`.

Expose one disposable wrapper:

```csharp
internal sealed class WindowsTerminalSubclass : IDisposable
{
    public static WindowsTerminalSubclass Attach(
        nint hwnd,
        Func<uint, nuint, nint, nint?> handler);
}
```

The unmanaged subclass thunk uses the `dwRefData` value to recover a `GCHandle` for the managed wrapper. If the managed handler returns a value, return it; otherwise call `DefSubclassProc`.

On disposal remove the subclass before freeing the `GCHandle`. Treat `WM_NCDESTROY` as an idempotent detach path.

- [ ] **Step 5: NativeAOT compile checkpoint**

Run on Windows x64:

```powershell
dotnet publish src/SourceGit.csproj -c Release -r win-x64 -o publish-native-abi
```

Expected: NativeAOT publish succeeds with no interop generation error for the delegate callback registrations or subclass thunk.

If this fails specifically because NativeAOT cannot marshal the stored callback delegates, STOP production implementation and reopen the design for a tiny native C ABI shim that adds a callback context pointer. Do not use runtime-generated executable trampolines as a workaround.

- [ ] **Step 6: Commit**

```text
feat: add Windows Terminal native ABI
```

**Deliverable:** a strict, NativeAOT-compatible interop boundary exists without any terminal process or UI integration yet.

---

### Task 3: Implement the native HWND host and PTY data bridge

**Files:**
- Create: `src/Views/WindowsTerminalNativeHost.cs`
- Create: `src/DevSpaces/WindowsTerminalDevSpaceSurface.cs`

**Interfaces:**
- Consumes: `WindowsTerminal`, `WindowsTerminalSubclass`, `DevSpaceLaunchSpec`, `Porta.Pty`.
- Produces: one persistent native terminal HWND + one PTY session with `StartAsync`, `Stop`, `Exited`, and `SetPageActive` behavior.

- [ ] **Step 1: Implement `WindowsTerminalNativeHost : NativeControlHost`**

The host owns:

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

It exposes events/callbacks to its surface:

```csharp
internal event Action<string> InputGenerated;
internal event Action<int, int> TerminalResized;
```

- [ ] **Step 2: Create the native child HWND only through `NativeControlHost`**

In `CreateNativeControlCore(IPlatformHandle parent)`:

```csharp
WindowsTerminal.EnsureResolver();
WindowsTerminal.AvoidBuggyTSFConsoleFlags();
WindowsTerminal.CreateTerminalChecked(parent.Handle, out _hwnd, out _terminal);

_writeCallback = OnNativeWrite;
_scrollCallback = OnScroll;
WindowsTerminal.TerminalRegisterWriteCallback(_terminal, _writeCallback);
WindowsTerminal.TerminalRegisterScrollCallback(_terminal, _scrollCallback);
_subclass = WindowsTerminalSubclass.Attach(_hwnd, HandleWindowMessage);
ApplyDpi();
return new PlatformHandle(_hwnd, "HWND");
```

Call `AvoidBuggyTSFConsoleFlags` through a process-wide once guard.

- [ ] **Step 3: Decode/finally-free native input**

`OnNativeWrite(nint text)` must be:

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

- [ ] **Step 4: Implement `WindowsTerminalDevSpaceSurface` PTY ownership**

Spawn exactly from the existing launch spec:

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

Wire `_host.InputGenerated` to a single serialized writer channel and `_host.TerminalResized` to `_pty.Resize(cols, rows)`.

- [ ] **Step 5: Implement PTY -> renderer reading with a stateful UTF-8 decoder**

Use one reader task and one `Decoder`:

```csharp
var decoder = Encoding.UTF8.GetDecoder();
var bytes = new byte[8192];
var chars = new char[8192];

while (!_cts.IsCancellationRequested)
{
    var read = await _pty.ReaderStream.ReadAsync(bytes, _cts.Token);
    if (read == 0)
        break;

    var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
    if (count > 0)
        _host.SendOutput(new string(chars, 0, count));
}
```

Do not call the Avalonia dispatcher for `TerminalSendOutput`; the native terminal internally synchronizes renderer writes.

- [ ] **Step 6: Implement serialized PTY input writes**

Use a `Channel<string>` with one writer loop. Both native input callbacks and keyboard paste enqueue strings to this channel. The writer loop UTF-8 encodes each item and writes to `_pty.WriterStream`, preventing interleaved writes.

- [ ] **Step 7: Implement process exit and shutdown**

Subscribe to `IPtyConnection.ProcessExited`, raise the surface's own `Exited` event once, and do not start fallback after a running native process exits.

Shutdown order:

```text
cancel loops -> stop accepting native input -> kill/dispose PTY -> remove native subclass -> DestroyTerminal -> release callback roots
```

`DestroyNativeControlCore` must not call `DestroyWindow`; `DestroyTerminal` owns native HWND teardown.

- [ ] **Step 8: Commit**

```text
feat: bridge Windows Terminal renderer to DevSpaces PTY
```

**Deliverable:** one native terminal can render and exchange VT input/output with one Porta.Pty process while remaining owned by a normal Avalonia `NativeControlHost`.

---

### Task 4: Match Windows Terminal host keyboard, focus, resize, DPI, and scroll behavior

**Files:**
- Modify: `src/Views/WindowsTerminalNativeHost.cs`

**Interfaces:**
- Consumes: flat ABI and PTY bridge from Tasks 2-3.
- Produces: native-like input behavior and correct renderer/PTY dimensions.

- [ ] **Step 1: Forward focus and ordinary key messages**

In the subclass handler, mirror Microsoft's WPF adapter:

```text
WM_SETFOCUS       -> TerminalSetFocused(true)
WM_KILLFOCUS      -> TerminalSetFocused(false)
WM_MOUSEACTIVATE  -> SetFocus(native hwnd)
WM_KEYDOWN / WM_SYSKEYDOWN -> TerminalSendKeyEvent(..., keyDown=true)
WM_KEYUP / WM_SYSKEYUP     -> TerminalSendKeyEvent(..., keyDown=false)
WM_CHAR                     -> TerminalSendCharEvent(...)
```

Extract scan code and flags from `lParam` exactly as Microsoft's `NativeMethods.cs`/`TerminalContainer.cs` does.

- [ ] **Step 2: Add only the DevSpaces keyboard shortcut layer**

Before forwarding keydown:

```text
Ctrl+C + selection       -> CopySelectionAsync(); consume
Ctrl+C + no selection    -> forward normally
Ctrl+Shift+C + selection -> CopySelectionAsync(); consume
Ctrl+Shift+C + no selection -> consume
Ctrl+Shift+V             -> PasteAsync(); consume
Shift+Insert             -> PasteAsync(); consume
Ctrl+V                   -> forward normally
Ctrl+A                   -> forward normally
```

Track consumed shortcut keys so their generated `WM_CHAR`/key-up does not also reach the PTY.

- [ ] **Step 3: Implement keyboard copy using native selection ownership**

```csharp
if (WindowsTerminal.TerminalIsSelectionActive(_terminal) == 0)
    return;

var ptr = WindowsTerminal.TerminalGetSelection(_terminal);
try
{
    var text = ptr == 0 ? null : Marshal.PtrToStringUni(ptr);
    if (!string.IsNullOrEmpty(text))
        await TopLevel.GetTopLevel(this).Clipboard.SetTextAsync(text);
}
finally
{
    if (ptr != 0)
        Marshal.FreeCoTaskMem(ptr);
}
```

The native getter clears selection; do not add a second clear call.

- [ ] **Step 4: Implement keyboard paste through the shared PTY writer queue**

Read text from Avalonia clipboard on the UI dispatcher and enqueue it through the same serialized input channel used by `InputGenerated`. Do not add bracketed-paste wrapping in this PR.

- [ ] **Step 5: Preserve native mouse/right-click/accessibility behavior**

For mouse messages, do not mark them handled in SourceGit. Call/allow `DefSubclassProc` so native `HwndTerminal` continues to own selection, right-click copy/paste, VT mouse forwarding, and `WM_GETOBJECT` accessibility.

Do not attach PR #7's Avalonia context menu to the native HWND.

- [ ] **Step 6: Implement resize synchronization**

On non-zero `WM_WINDOWPOSCHANGED`, guard against recursive resize and call:

```csharp
WindowsTerminal.TerminalTriggerResizeChecked(
    _terminal,
    windowPos.cx,
    windowPos.cy,
    out var size);

_cols = Math.Max(1, size.X);
_rows = Math.Max(1, size.Y);
TerminalResized?.Invoke(_cols, _rows);
```

Ignore zero-size transitions and disposal races.

- [ ] **Step 7: Implement DPI propagation**

At native creation and whenever the containing `TopLevel.RenderScaling` changes:

```csharp
var dpi = (int)Math.Round(topLevel.RenderScaling * 96.0);
WindowsTerminal.TerminalDpiChanged(_terminal, dpi);
```

Unsubscribe render-scaling observers during detach/disposal.

- [ ] **Step 8: Implement WPF-equivalent scrollback wheel behavior**

Store `viewTop/viewHeight/bufferSize` from `TerminalRegisterScrollCallback`. Accumulate `WM_MOUSEWHEEL` delta using 120 units and `SystemParametersInfo(SPI_GETWHEELSCROLLLINES)` or the equivalent Win32 query. Clamp new top to:

```csharp
0 .. Math.Max(0, _bufferSize - _viewHeight)
```

Then call `TerminalUserScroll(_terminal, newTop)`. Let the native HWND also receive the wheel message; alternate-buffer/TUI scrollback normally has no range and the native control retains VT mouse behavior.

- [ ] **Step 9: Commit**

```text
feat: add native terminal input and resize behavior
```

**Deliverable:** the hosted native HWND behaves like Microsoft's own WPF host for focus/input/resize while preserving native mouse semantics.

---

### Task 5: Introduce the terminal-surface boundary and automatic fallback

**Files:**
- Create: `src/DevSpaces/IDevSpaceTerminalSurface.cs`
- Create: `src/DevSpaces/AvaloniaDevSpaceTerminalSurface.cs`
- Modify: `src/Views/DevSpaceTerminal.axaml`
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`
- Keep: `src/Views/DevSpaceTerminalControl.cs`

**Interfaces:**
- Consumes: native surface from Tasks 3-4 and existing PR #7 Avalonia terminal.
- Produces: one backend-neutral DevSpaces terminal view with native-first startup on supported Windows and safe fallback.

- [ ] **Step 1: Define the surface contract without Iciclecreek event types**

Create:

```csharp
internal sealed class DevSpaceTerminalExitedEventArgs : EventArgs
{
    public DevSpaceTerminalExitedEventArgs(int exitCode) => ExitCode = exitCode;
    public int ExitCode { get; }
}

internal interface IDevSpaceTerminalSurface : IDisposable
{
    Control View { get; }
    event EventHandler<DevSpaceTerminalExitedEventArgs> Exited;
    Task StartAsync(DevSpaceLaunchSpec spec);
    void SetPageActive(bool active);
    void Stop();
}
```

- [ ] **Step 2: Wrap the existing Avalonia terminal without changing its behavior**

`AvaloniaDevSpaceTerminalSurface` owns one `DevSpaceTerminalControl`, forwards `ProcessExited`, calls the existing `LaunchProcess`/`Kill`, and retains PR #7's right-click Copy/Paste/Select All behavior for fallback sessions.

Do not recreate the control on visibility/layout changes.

- [ ] **Step 3: Make `DevSpaceTerminal.axaml` a backend host**

Replace the hard-coded terminal element with a persistent container:

```xml
<Grid>
  <Grid x:Name="SurfaceHost"/>
  <Border ... IsVisible="{Binding ErrorMessage, ...}">
    ...existing error text...
  </Border>
</Grid>
```

Keep the error overlay unchanged.

- [ ] **Step 4: Choose native first, then fallback once on startup failure**

In `DevSpaceTerminal.Start(...)`:

```csharp
var spec = launcher.Create(session.Command, session.WorkingDirectory);

if (WindowsTerminal.IsSupported)
{
    try
    {
        await StartSurfaceAsync(new WindowsTerminalDevSpaceSurface(), spec);
        session.MarkRunning();
        return;
    }
    catch
    {
        DisposeCurrentSurface();
    }
}

await StartSurfaceAsync(new AvaloniaDevSpaceTerminalSurface(), spec);
session.MarkRunning();
```

Use `async void` only for the UI entry point if the existing call site cannot await; all backend work remains `Task`-returning. Catch native startup failures before PTY startup is considered successful. After a native session is running, later failure is reported as exit/failure and must not spawn a replacement process.

- [ ] **Step 5: Preserve existing session lifetime and Stop semantics**

`Stop()` unsubscribes `StopRequested`, unsubscribes current surface exit, calls `Stop()/Dispose()` exactly once, and leaves sibling terminals untouched.

Adding another terminal or changing the grid must not replace `SurfaceHost.Children[0]` for existing sessions.

- [ ] **Step 6: Add the page-active visibility bridge for native HWNDs**

The existing DevSpaces bootstrap keeps the overall view mounted using opacity when navigating to History/Stashes. Native HWNDs cannot be hidden by Avalonia opacity.

Wire the existing DevSpaces active/inactive transition to call `surface.SetPageActive(false/true)` for every session. Native implementation sets its `NativeControlHost.IsVisible`; Avalonia fallback may use normal control visibility without stopping its process.

Do not destroy/recreate the native HWND on page navigation.

- [ ] **Step 7: Commit**

```text
feat: prefer native Windows Terminal in DevSpaces
```

**Deliverable:** DevSpaces transparently uses native Windows Terminal on supported Windows and preserves the current terminal everywhere else or on native startup failure.

---

### Task 6: Package the correct native DLL automatically and gate it in CI

**Files:**
- Modify: `src/SourceGit.csproj`
- Modify: `.github/workflows/build.yml`
- Modify: `THIRD-PARTY-LICENSES.md`

**Interfaces:**
- Consumes: `PackageDownload` from Task 1.
- Produces: RID-correct release payload under `native-terminal/<rid>/Microsoft.Terminal.Control.dll` for win-x64/win-arm64 only.

- [ ] **Step 1: Add a publish target that stages only the current RID native DLL**

In `src/SourceGit.csproj` add:

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

Do not copy the managed WPF DLL.

- [ ] **Step 2: Add Windows artifact assertions to CI**

Immediately after SourceGit publish in `.github/workflows/build.yml` add:

```yaml
- name: Verify Windows Terminal native payload
  if: startsWith(matrix.runtime, 'win-')
  shell: pwsh
  run: |
    $dll = "publish/native-terminal/${{ matrix.runtime }}/Microsoft.Terminal.Control.dll"
    if (-not (Test-Path $dll)) { throw "Missing $dll" }
    if (Test-Path "publish/Microsoft.Terminal.Wpf.dll") { throw "WPF wrapper must not be packaged" }
```

For macOS/Linux add a lightweight assertion that `publish/native-terminal` does not exist before archive creation.

- [ ] **Step 3: Preserve the normal NativeAOT publish path**

Do not set `DisableAOT=true`. The existing command remains:

```text
dotnet publish src/SourceGit.csproj -c Release -o publish -r <RID>
```

Windows x64 and ARM64 jobs must still show NativeAOT compilation of `SourceGit.exe`.

- [ ] **Step 4: Update third-party licenses**

Add entries for:

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

Do not claim the WPF wrapper is a runtime dependency.

- [ ] **Step 5: Commit**

```text
build: package native Windows Terminal renderer
```

**Deliverable:** normal Windows release artifacts automatically contain the matching native renderer DLL, while ARM/macOS/Linux artifacts stay clean and SourceGit stays NativeAOT.

---

### Task 7: Final verification and PR #7 cleanup

**Files:**
- Audit: all files changed from `master` to `feat/devspaces-native-terminal-input`.
- Modify: PR #7 description if needed.

**Interfaces:**
- Consumes: completed implementation.
- Produces: final green PR head with explicit runtime-verification status.

- [ ] **Step 1: Source-audit forbidden architecture remnants**

Search the entire PR branch for:

```text
WindowsTerminalHostProbe
SourceGit.WindowsTerminalHost
EasyWindowsTerminalControl
UseWPF
Microsoft.Terminal.Wpf.dll
wt.exe
SetWindowLongPtr
```

Expected: no production/helper architecture remnants. Documentation may mention the superseded design only where explicitly labeled historical/superseded.

- [ ] **Step 2: Verify exact dependency versions and no Avalonia 12**

Expected:

```text
Avalonia 11.3.20
Iciclecreek.Avalonia.Terminal 1.0.12
Porta.Pty 1.0.7
CI.Microsoft.Terminal.Wpf PackageDownload 1.25.260303002
```

No Avalonia 12, WPF, WinUI, or Windows App SDK reference may be introduced.

- [ ] **Step 3: Run the full PR Check on the final head**

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

If any job fails, invoke `superpowers:systematic-debugging`, inspect the exact failed job log, determine root cause before changing code, and rerun the gate on the new final head.

- [ ] **Step 4: Inspect release artifacts**

Windows x64 must contain:

```text
native-terminal/win-x64/Microsoft.Terminal.Control.dll
```

Windows ARM64 must contain:

```text
native-terminal/win-arm64/Microsoft.Terminal.Control.dll
```

macOS/Linux artifacts must not contain `native-terminal/`.

- [ ] **Step 5: Manual Windows x64 acceptance gate**

Run the Release artifact and verify all of these:

```text
1. Open a worktree and DevSpaces; Copilot starts in the native renderer.
2. Drag selection over text and blank terminal area.
3. Double-click selects a word; triple-click selects a line.
4. Right-click with selection copies; right-click with no selection pastes.
5. Ctrl+Shift+C copies; Ctrl+Shift+V and Shift+Insert paste.
6. Ctrl+C with no selection reaches Copilot/process; Ctrl+A and Ctrl+V remain application-owned.
7. Mouse-aware TUI input still works; Shift allows selection override.
8. Mouse wheel scrollback works in normal buffer.
9. Add a second terminal: first session state does not reload.
10. Switch layouts: existing sessions persist and resize correctly.
11. Switch History/Stashes and back: native HWND does not bleed over other pages and process state persists.
12. Close one terminal: sibling terminal remains alive.
13. Temporarily rename/remove native-terminal/win-x64/Microsoft.Terminal.Control.dll: a new DevSpaces terminal falls back to the Avalonia renderer instead of crashing.
```

Do not call native runtime behavior verified until all 13 pass.

- [ ] **Step 6: Windows ARM64 runtime acceptance when hardware is available**

Repeat the native-start/basic-selection/persistence/fallback checks on ARM64. Until this is done, report ARM64 as CI publish-verified but not manually runtime-verified.

- [ ] **Step 7: Update PR #7 description**

The PR body must state:

```text
- Windows x64/ARM64 prefer Microsoft.Terminal.Control.dll hosted directly by Avalonia NativeControlHost
- SourceGit remains net10.0/Avalonia 11.3.20/NativeAOT
- Porta.Pty 1.0.7 owns PTY processes
- no WPF/helper process/installed wt.exe dependency
- Avalonia terminal remains the automatic fallback
- native DLL is packaged automatically per Windows RID
- CI proves build/publish compatibility; manual Windows interaction is tracked separately
```

- [ ] **Step 8: Invoke verification-before-completion**

Before reporting completion, use `superpowers:verification-before-completion` and re-check the exact final PR head, required jobs, artifacts, and unresolved review threads.

Do not merge PR #7 unless the user explicitly asks.

**Deliverable:** PR #7 contains only the approved direct-native architecture, is green on the exact final head, packages the correct native DLL automatically, and has honest manual-runtime verification status.
