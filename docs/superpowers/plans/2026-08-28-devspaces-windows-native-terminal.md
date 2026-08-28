# DevSpaces Windows-Native Terminal Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional Windows Terminal-backed DevSpaces renderer on Windows x64 while preserving SourceGit NativeAOT and the existing Avalonia terminal as the automatic fallback everywhere else.

**Architecture:** Keep `SourceGit.exe` cross-platform, `net10.0`, Avalonia 11.3.20, and NativeAOT. A separate x64-only WPF helper process hosts `EasyWindowsTerminalControl` 1.0.38 inside an `HwndSource` created directly as a child of Avalonia's `NativeControlHost` HWND. SourceGit communicates only the startup parent HWND and Base64URL JSON launch payload, receives one ready HWND line, and owns helper lifetime. A hard real-Windows probe must pass before production native-host wiring is kept.

**Tech Stack:** .NET 10, Avalonia 11.3.20, NativeAOT for SourceGit, WPF helper on `net10.0-windows`, EasyWindowsTerminalControl 1.0.38, Windows Terminal WPF backend, ConPTY, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-native-terminal-input-design.md`

## Global Constraints

- `SourceGit.exe` remains `net10.0`, Avalonia `11.3.20`, and NativeAOT in Release.
- Pin `EasyWindowsTerminalControl` to `1.0.38` for this PR.
- Native Windows Terminal rendering is Windows x64 only.
- Windows ARM64, macOS x64/ARM64, and Linux x64/ARM64 continue using the existing `Iciclecreek.Avalonia.Terminal` 1.0.12 fallback.
- Do not embed or reparent the installed `wt.exe` application window.
- Do not add WPF, EasyWindowsTerminalControl, or Windows-only package references to `src/SourceGit.csproj`.
- Do not add `SourceGit.WindowsTerminalHost` to `SourceGit.slnx`; non-Windows matrix jobs must never restore/build the WPF helper.
- Do not disable SourceGit NativeAOT.
- Do not introduce a network socket, persistent broker, global service, or global keyboard/mouse hook.
- One native helper process owns exactly one DevSpaces terminal session.
- Native startup failure falls back to the existing Avalonia terminal using the same `DevSpaceLaunchSpec`.
- Adding terminals, changing layout, or switching repository pages must not restart existing terminals.
- Existing PR #7 Avalonia selection/copy/paste behavior remains intact.
- The previously approved DevSpaces test-project exception continues; verification is source audit, builds/package assertions, a disposable real-Windows probe, and manual Windows runtime acceptance.

---

### Task 1: Prove cross-process HWND hosting on real Windows x64

**Files:**
- Create temporarily: `tools/WindowsTerminalHostProbe/WindowsTerminalHostProbe.csproj`
- Create temporarily: `tools/WindowsTerminalHostProbe/Program.cs`
- Create temporarily: `tools/WindowsTerminalHostProbe/ProbeNativeHost.cs`
- Create temporarily: `tools/WindowsTerminalHostProbe.Helper/WindowsTerminalHostProbe.Helper.csproj`
- Create temporarily: `tools/WindowsTerminalHostProbe.Helper/Program.cs`
- Delete all five probe files after the gate passes; if the gate fails, delete them and stop native-host implementation.

**Interfaces:**
- Consumes: Avalonia `NativeControlHost.CreateNativeControlCore(IPlatformHandle)`, WPF `HwndSource`, `EasyWindowsTerminalControl.EasyTerminalControl`.
- Produces: go/no-go evidence only. No probe type may become a production dependency.

- [ ] **Step 1: Create the probe helper project**

Create `tools/WindowsTerminalHostProbe.Helper/WindowsTerminalHostProbe.Helper.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PlatformTarget>x64</PlatformTarget>
    <UseWPF>true</UseWPF>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="EasyWindowsTerminalControl" Version="1.0.38" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create a child `HwndSource` directly under the Avalonia parent**

Create `tools/WindowsTerminalHostProbe.Helper/Program.cs` with this shape:

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;

using EasyWindowsTerminalControl;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 ||
            !long.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentValue))
            return 2;

        var parameters = new HwndSourceParameters("SourceGit Windows Terminal Probe")
        {
            ParentWindow = new IntPtr(parentValue),
            WindowStyle = unchecked((int)0x50000000), // WS_CHILD | WS_VISIBLE
            Width = 800,
            Height = 480,
        };

        using var source = new HwndSource(parameters);
        var terminal = new EasyTerminalControl
        {
            StartupCommandLine = "cmd.exe",
            WorkingDirectory = Environment.CurrentDirectory,
        };
        source.RootVisual = terminal;

        Console.WriteLine($"SOURCEGIT_TERMINAL_READY {source.Handle.ToInt64()}");
        Console.Out.Flush();

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Run();
        return 0;
    }
}
```

Do not create a top-level `Window` and call `SetParent` afterward. The child HWND must be born with `ParentWindow` set to Avalonia's native parent.

- [ ] **Step 3: Create the Avalonia probe host**

Create `tools/WindowsTerminalHostProbe/WindowsTerminalHostProbe.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.20" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.20" />
  </ItemGroup>
</Project>
```

Create `ProbeNativeHost : NativeControlHost`. `CreateNativeControlCore` must start the helper with redirected stdout, wait at most five seconds for exactly `SOURCEGIT_TERMINAL_READY <hwnd>`, and return:

```csharp
return new PlatformHandle(new IntPtr(hwndValue), "HWND");
```

`DestroyNativeControlCore` must request helper termination and must not call `DestroyWindow` on the foreign-process HWND.

- [ ] **Step 4: Build both probes on Windows x64**

Run:

```powershell
 dotnet build tools/WindowsTerminalHostProbe.Helper/WindowsTerminalHostProbe.Helper.csproj -c Release
 dotnet build tools/WindowsTerminalHostProbe/WindowsTerminalHostProbe.csproj -c Release
```

Expected: both exit 0.

- [ ] **Step 5: Run the real Windows acceptance gate**

On an interactive Windows x64 desktop, start the Avalonia probe and verify all seven spec requirements:

```text
1. cmd.exe renders inside the Avalonia host rectangle.
2. Click/focus and keyboard typing work naturally.
3. Mouse drag selection works.
4. Resizing the Avalonia window resizes the native terminal.
5. Hiding/showing the host does not terminate cmd.exe.
6. Closing the host terminates the helper cleanly.
7. Hidden native content does not bleed over unrelated Avalonia content.
```

This step cannot be replaced by GitHub Actions because hosted runners do not provide the interactive desktop evidence required by the spec.

- [ ] **Step 6: Apply the go/no-go rule**

If any requirement needs a global hook, `wt.exe` reparenting, a top-level-window reparent hack, disabling NativeAOT, or an unbounded focus workaround, delete the probe files and stop. Keep PR #7 as the Avalonia-terminal improvement only.

If all seven pass, record the result in the PR conversation and continue.

- [ ] **Step 7: Remove the disposable probe**

Delete both temporary `tools/WindowsTerminalHostProbe*` directories before production implementation is merged.

**Deliverable:** explicit real-Windows proof that the helper HWND architecture is viable, or a clean no-go with no production native-host code retained.

---

### Task 2: Add the shared launch protocol and Windows command-line encoder

**Files:**
- Create: `src/DevSpaces/WindowsTerminalHostProtocol.cs`

**Interfaces:**
- Produces: `WindowsTerminalLaunchPayload`, `WindowsTerminalHostProtocol.Encode(...)`, `TryDecode(...)`, `ReadyPrefix`, `BuildWindowsCommandLine(...)`.
- Consumed by: `SourceGit.exe` and linked into `SourceGit.WindowsTerminalHost` without a project reference.

- [ ] **Step 1: Define the payload and protocol constants**

Create:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SourceGit.DevSpaces
{
    public sealed record WindowsTerminalLaunchPayload(
        string Process,
        string[] Arguments,
        string WorkingDirectory);

    public static class WindowsTerminalHostProtocol
    {
        public const string ReadyPrefix = "SOURCEGIT_TERMINAL_READY ";
        public const int StartupTimeoutMilliseconds = 5000;

        public static string Encode(WindowsTerminalLaunchPayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static bool TryDecode(string encoded, out WindowsTerminalLaunchPayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(encoded))
                return false;

            try
            {
                var value = encoded.Replace('-', '+').Replace('_', '/');
                value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(value));
                payload = JsonSerializer.Deserialize<WindowsTerminalLaunchPayload>(json);
                return payload != null &&
                    !string.IsNullOrWhiteSpace(payload.Process) &&
                    payload.Arguments != null &&
                    !string.IsNullOrWhiteSpace(payload.WorkingDirectory);
            }
            catch
            {
                return false;
            }
        }
```

- [ ] **Step 2: Add a deterministic Windows command-line builder**

In the same class add:

```csharp
        public static string BuildWindowsCommandLine(string process, IReadOnlyList<string> arguments)
        {
            var builder = new StringBuilder();
            AppendQuoted(builder, process);
            foreach (var argument in arguments)
            {
                builder.Append(' ');
                AppendQuoted(builder, argument ?? string.Empty);
            }
            return builder.ToString();
        }

        private static void AppendQuoted(StringBuilder builder, string value)
        {
            if (value.Length > 0 && value.IndexOfAny([' ', '\t', '"']) < 0)
            {
                builder.Append(value);
                return;
            }

            builder.Append('"');
            var slashCount = 0;
            foreach (var ch in value)
            {
                if (ch == '\\')
                {
                    slashCount++;
                    continue;
                }

                if (ch == '"')
                {
                    builder.Append('\\', slashCount * 2 + 1);
                    builder.Append('"');
                    slashCount = 0;
                    continue;
                }

                builder.Append('\\', slashCount);
                slashCount = 0;
                builder.Append(ch);
            }

            builder.Append('\\', slashCount * 2);
            builder.Append('"');
        }
    }
}
```

This is CreateProcess/CRT-compatible quoting. Do not join raw arguments with spaces.

- [ ] **Step 3: Source-audit protocol behavior**

Verify from the diff:

```text
- only Process, Arguments, WorkingDirectory are serialized;
- Base64URL is one command-line argument;
- malformed payload returns false;
- no shell executable is used to decode or launch;
- argument quoting handles whitespace, embedded quotes, and trailing backslashes.
```

- [ ] **Step 4: Commit**

```text
feat: add Windows terminal host protocol
```

**Deliverable:** one platform-neutral source file defines the exact cross-process startup contract.

---

### Task 3: Add `SourceGit.WindowsTerminalHost`

**Files:**
- Create: `src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj`
- Create: `src/SourceGit.WindowsTerminalHost/Program.cs`
- Create: `src/SourceGit.WindowsTerminalHost/WindowsTerminalHost.cs`
- Do not modify: `SourceGit.slnx`

**Interfaces:**
- Consumes: linked `../DevSpaces/WindowsTerminalHostProtocol.cs`, `EasyTerminalControl` 1.0.38.
- Produces: executable `SourceGit.WindowsTerminalHost.exe`; stdout ready line; helper process lifetime equals one terminal lifetime.

- [ ] **Step 1: Create the x64-only WPF helper project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PlatformTarget>x64</PlatformTarget>
    <UseWPF>true</UseWPF>
    <Nullable>disable</Nullable>
    <AssemblyName>SourceGit.WindowsTerminalHost</AssemblyName>
    <RootNamespace>SourceGit.WindowsTerminalHost</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="EasyWindowsTerminalControl" Version="1.0.38" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="../DevSpaces/WindowsTerminalHostProtocol.cs"
             Link="WindowsTerminalHostProtocol.cs" />
  </ItemGroup>
</Project>
```

Do not add a `ProjectReference` from SourceGit.

- [ ] **Step 2: Parse only the approved startup arguments**

`Program.Main` accepts exactly:

```text
--parent-hwnd <decimal-hwnd> --launch-payload <base64url-json>
```

Reject missing/extra values with exit code 2 and stderr diagnostics. Decode using `WindowsTerminalHostProtocol.TryDecode`.

- [ ] **Step 3: Create the terminal HWND directly under Avalonia's parent**

Implement `WindowsTerminalHost` so construction does:

```csharp
var parameters = new HwndSourceParameters("SourceGit Windows Terminal")
{
    ParentWindow = parentHwnd,
    WindowStyle = unchecked((int)0x50000000), // WS_CHILD | WS_VISIBLE
    Width = 800,
    Height = 480,
};
_source = new HwndSource(parameters);

_terminal = new EasyTerminalControl
{
    StartupCommandLine = WindowsTerminalHostProtocol.BuildWindowsCommandLine(
        payload.Process,
        payload.Arguments),
    WorkingDirectory = payload.WorkingDirectory,
};
_source.RootVisual = _terminal;
```

Then emit exactly:

```csharp
Console.Out.WriteLine($"{WindowsTerminalHostProtocol.ReadyPrefix}{_source.Handle.ToInt64()}");
Console.Out.Flush();
```

All diagnostics use `Console.Error`.

- [ ] **Step 4: Tie helper shutdown to terminal process exit**

After `EasyTerminalControl` is loaded and `ConPTYTerm.TermProcIsStarted` becomes true, poll `ConPTYTerm.Process.HasExited` on a background task at 250 ms intervals. When it becomes true, dispatch `Application.Current.Shutdown(0)`. If the package API at 1.0.38 does not expose `Process.HasExited` publicly at compile time, stop and inspect the exact public package API before changing lifecycle semantics; do not guess or silently leave the helper alive forever.

- [ ] **Step 5: Dispose ConPTY before helper exit**

On normal shutdown:

```csharp
try { _terminal?.ConPTYTerm?.CloseStdinToApp(); } catch { }
try { _terminal?.ConPTYTerm?.StopExternalTermOnly(); } catch { }
try { _source?.Dispose(); } catch { }
```

Do not kill unrelated processes.

- [ ] **Step 6: Build the helper independently**

On Windows x64:

```powershell
 dotnet build src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj -c Release
 dotnet publish src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows-terminal-host
```

Expected: exit 0 and `artifacts/windows-terminal-host/SourceGit.WindowsTerminalHost.exe` exists.

- [ ] **Step 7: Commit**

```text
feat: add Windows Terminal helper process
```

**Deliverable:** a standalone x64 WPF helper that hosts one Windows Terminal surface and does not affect SourceGit's target framework or AOT settings.

---

### Task 4: Add the SourceGit native-host and terminal-surface boundary

**Files:**
- Create: `src/DevSpaces/IDevSpaceTerminalSurface.cs`
- Create: `src/DevSpaces/AvaloniaDevSpaceTerminalSurface.cs`
- Create: `src/DevSpaces/WindowsNativeDevSpaceTerminalSurface.cs`
- Create: `src/Views/WindowsTerminalNativeHost.cs`
- Modify: `src/Views/DevSpaceTerminal.axaml`
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`

**Interfaces:**
- Produces:
  - `IDevSpaceTerminalSurface.View : Control`
  - `IDevSpaceTerminalSurface.Exited : event EventHandler<int>`
  - `Start(DevSpaceLaunchSpec spec)`
  - `SetPageActive(bool active)`
  - `Dispose()`
- Consumes: existing `DevSpaceLaunchSpec`, existing `DevSpaceTerminalControl`, new helper executable.

- [ ] **Step 1: Define the surface interface**

```csharp
using System;
using Avalonia.Controls;

namespace SourceGit.DevSpaces
{
    public interface IDevSpaceTerminalSurface : IDisposable
    {
        Control View { get; }
        event EventHandler<int> Exited;
        void Start(DevSpaceLaunchSpec spec);
        void SetPageActive(bool active);
    }
}
```

- [ ] **Step 2: Move the current Avalonia terminal behavior behind the interface**

`AvaloniaDevSpaceTerminalSurface` owns one existing `Views.DevSpaceTerminalControl`, subscribes to `ProcessExited`, calls `LaunchProcess(spec.WorkingDirectory, spec.Process, spec.Arguments)`, and keeps the current Copy/Paste/Select All tunneling handler behavior. `SetPageActive` must not destroy the control; it only keeps the Avalonia surface available for the current parent layout.

Do not change PR #7 keyboard semantics or TUI mouse-reporting gate.

- [ ] **Step 3: Implement `WindowsTerminalNativeHost`**

Subclass `NativeControlHost`. It stores one `DevSpaceLaunchSpec`, one helper `Process`, and the returned child `PlatformHandle`.

`CreateNativeControlCore` must:

```csharp
if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
    throw new PlatformNotSupportedException();
```

Resolve:

```text
<AppContext.BaseDirectory>/native-terminal/win-x64/SourceGit.WindowsTerminalHost.exe
```

Create payload:

```csharp
var payload = new WindowsTerminalLaunchPayload(
    _spec.Process,
    _spec.Arguments,
    _spec.WorkingDirectory);
```

Start helper with `UseShellExecute=false`, `RedirectStandardOutput=true`, `RedirectStandardError=true`, `CreateNoWindow=true`, and `ArgumentList` entries for `--parent-hwnd`, decimal parent handle, `--launch-payload`, and `WindowsTerminalHostProtocol.Encode(payload)`.

Read one stdout line with a 5-second timeout. Accept only `ReadyPrefix + decimal HWND`. On any failure, kill/dispose the helper and throw so the caller can fall back.

Return `new PlatformHandle(childHwnd, "HWND")`.

`DestroyNativeControlCore` must stop the helper process; it must not call Win32 `DestroyWindow(control.Handle)` because the HWND belongs to the helper process.

- [ ] **Step 4: Implement `WindowsNativeDevSpaceTerminalSurface`**

`View` returns one persistent `WindowsTerminalNativeHost`. `Start(spec)` assigns the spec before the host is attached. `SetPageActive(active)` sets `View.IsVisible = active`; it must not dispose the helper. Monitor the helper process exit and raise `Exited` once.

Provide:

```csharp
public static bool IsSupported =>
    OperatingSystem.IsWindows() &&
    RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
    File.Exists(Path.Combine(
        AppContext.BaseDirectory,
        "native-terminal",
        "win-x64",
        "SourceGit.WindowsTerminalHost.exe"));
```

- [ ] **Step 5: Convert `DevSpaceTerminal.axaml` to a neutral surface host**

Replace the hard-coded terminal control with:

```xml
<Grid>
  <Grid x:Name="SurfaceHost" />
  <Border Background="{DynamicResource Brush.Window}"
          IsVisible="{Binding ErrorMessage, Converter={x:Static c:StringConverters.IsNotNullOrWhitespace}}">
    <TextBlock Margin="16" Text="{Binding ErrorMessage" TextWrapping="Wrap"/>
  </Border>
</Grid>
```

Correct the binding syntax when editing; the intended existing expression is `Text="{Binding ErrorMessage}"`.

- [ ] **Step 6: Make `DevSpaceTerminal` choose native then fall back**

`Start(launcher)` does:

```csharp
var spec = launcher.Create(session.Command, session.WorkingDirectory);
_surface = CreatePreferredSurface();
try
{
    AttachAndStart(_surface, spec);
}
catch (Exception ex) when (_surface is WindowsNativeDevSpaceTerminalSurface)
{
    _surface.Dispose();
    _surface = new AvaloniaDevSpaceTerminalSurface();
    AttachAndStart(_surface, spec);
    System.Diagnostics.Trace.WriteLine($"DevSpaces native terminal fallback: {ex}");
}
session.MarkRunning();
```

`CreatePreferredSurface()` returns Windows native only when `WindowsNativeDevSpaceTerminalSurface.IsSupported`; otherwise Avalonia.

`Stop()` unsubscribes `Exited`, removes/disposes the current surface, and remains idempotent.

If the native helper exits after successful startup, do not auto-create a fallback process; forward the exit to `session.MarkExited(...)` because restarting would lose terminal state.

- [ ] **Step 7: Build SourceGit on all available platforms**

Run normal SourceGit build. The new main-project code must compile without referencing WPF types or EasyWindowsTerminalControl assemblies.

- [ ] **Step 8: Commit**

```text
feat: add DevSpaces terminal backend abstraction
```

**Deliverable:** SourceGit can select the native helper on supported packaged Windows x64 builds and cleanly fall back to the existing terminal without changing session commands.

---

### Task 5: Propagate repository-page activity without destroying native terminals

**Files:**
- Modify: `src/Views/DevSpaces.axaml.cs`
- Modify: `src/DevSpaces/DevSpaceRegistry.cs`
- Modify: `src/DevSpaces/DevSpacesBootstrap.cs`

**Interfaces:**
- Produces: `Views.DevSpaces.SetPageActive(bool)` and `DevSpaceRegistry.SetPageActive(ViewModels.Repository, bool)`.
- Consumes: `DevSpaceTerminal.SetPageActive(bool)` which forwards to the active surface.

- [ ] **Step 1: Add page-active propagation to pane views**

In `Views.DevSpaces` add:

```csharp
public void SetPageActive(bool active)
{
    _pageActive = active;
    foreach (var pane in _panes.Values)
        pane.TerminalView.SetPageActive(active);
}
```

When creating a pane, call `terminalView.SetPageActive(_pageActive)` after `Start`.

Add `private bool _pageActive;`.

- [ ] **Step 2: Expose registry forwarding**

Add:

```csharp
public static void SetPageActive(ViewModels.Repository repository, bool active)
{
    if (repository != null && _spaces.TryGetValue(repository, out var entry))
        entry.View.SetPageActive(active);
}
```

- [ ] **Step 3: Update bootstrap page switching**

Keep the existing mounted/measured fallback behavior:

```csharp
_host.IsVisible = true;
var active = _repository.SelectedViewIndex == 3;
_host.Opacity = active ? 1 : 0;
_host.IsHitTestVisible = active;
DevSpaceRegistry.SetPageActive(_repository, active);
```

When disabled, call `DevSpaceRegistry.SetPageActive(_repository, false)` before detaching.

The key invariant is: Avalonia fallback remains mounted with opacity behavior; native child HWND visibility is controlled through its `NativeControlHost.IsVisible`, never opacity alone.

- [ ] **Step 4: Verify persistence manually**

With two terminal sessions, switch DevSpaces -> History -> Stashes -> DevSpaces and confirm process IDs/session output remain unchanged.

- [ ] **Step 5: Commit**

```text
fix: preserve native terminals across repository pages
```

**Deliverable:** native HWNDs hide when DevSpaces is inactive without terminating helper/ConPTY sessions.

---

### Task 6: Package the helper only in Windows x64 artifacts

**Files:**
- Modify: `.github/workflows/build.yml`
- Modify: `build/scripts/package.win.ps1` only if the packaging script copies from a staged publish directory and otherwise drops nested helper content.
- Modify: `THIRD-PARTY-LICENSES.md`

**Interfaces:**
- Produces artifact path: `publish/native-terminal/win-x64/SourceGit.WindowsTerminalHost.exe` for `win-x64` only.

- [ ] **Step 1: Add Windows x64 helper format/build/publish steps before SourceGit artifact upload**

In `.github/workflows/build.yml`, add steps conditioned on `matrix.runtime == 'win-x64'`:

```yaml
      - name: Format Windows terminal host
        if: matrix.runtime == 'win-x64'
        run: dotnet format --verify-no-changes src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj

      - name: Publish Windows terminal host
        if: matrix.runtime == 'win-x64'
        run: >-
          dotnet publish src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj
          -c Release
          -r win-x64
          --self-contained true
          -o native-terminal-host
```

Keep the existing SourceGit `dotnet publish` command unchanged so its NativeAOT evidence remains separate.

- [ ] **Step 2: Stage the helper into the SourceGit publish output**

After SourceGit publish and before artifact packaging/upload:

```yaml
      - name: Stage Windows terminal host
        if: matrix.runtime == 'win-x64'
        shell: pwsh
        run: |
          $target = 'publish/native-terminal/win-x64'
          New-Item -ItemType Directory -Force -Path $target | Out-Null
          Copy-Item 'native-terminal-host/*' $target -Recurse -Force
          if (-not (Test-Path "$target/SourceGit.WindowsTerminalHost.exe")) {
            throw 'Windows terminal host executable was not staged.'
          }
```

- [ ] **Step 3: Assert non-x64 artifacts do not contain the helper**

Add a cross-platform assertion before upload:

```yaml
      - name: Assert native terminal packaging
        shell: pwsh
        run: |
          $helper = 'publish/native-terminal/win-x64/SourceGit.WindowsTerminalHost.exe'
          if ('${{ matrix.runtime }}' -eq 'win-x64') {
            if (-not (Test-Path $helper)) { throw 'Missing Windows x64 terminal helper.' }
          } elseif (Test-Path 'publish/native-terminal') {
            throw 'Native Windows terminal helper must not be included for this runtime.'
          }
```

If PowerShell is unavailable in Linux container jobs, make this assertion Windows-only plus add shell-native `test ! -e publish/native-terminal` assertions to macOS/Linux jobs. Do not remove the assertion.

- [ ] **Step 4: Confirm Windows packaging preserves the nested helper directory**

Inspect `build/scripts/package.win.ps1`. If it packages the complete `publish` tree unchanged, make no code change. If it enumerates only top-level files, update it to recursively include `native-terminal/win-x64/**`.

- [ ] **Step 5: Add third-party attribution**

Update `THIRD-PARTY-LICENSES.md` with `EasyWindowsTerminalControl` and the Windows Terminal WPF package chain used by 1.0.38. Record package names, pinned versions resolved by restore, upstream project URLs, and their licenses. Do not paste full license bodies if the repository's existing convention is attribution-only.

- [ ] **Step 6: Commit**

```text
build: package Windows terminal host on x64
```

**Deliverable:** win-x64 artifacts contain the helper and its required runtime/native dependencies; every other artifact remains unchanged.

---

### Task 7: Update PR #7 metadata and run the complete verification gate

**Files:**
- Modify: PR #7 body
- Audit: all files changed from `master` to `feat/devspaces-native-terminal-input`

**Interfaces:**
- Consumes all previous tasks.
- Produces merge-ready evidence only after CI plus manual Windows x64 acceptance.

- [ ] **Step 1: Audit branch scope**

Expected production additions/modifications after removing the disposable probe:

```text
src/DevSpaces/WindowsTerminalHostProtocol.cs
src/DevSpaces/IDevSpaceTerminalSurface.cs
src/DevSpaces/AvaloniaDevSpaceTerminalSurface.cs
src/DevSpaces/WindowsNativeDevSpaceTerminalSurface.cs
src/Views/WindowsTerminalNativeHost.cs
src/Views/DevSpaceTerminal.axaml
src/Views/DevSpaceTerminal.axaml.cs
src/Views/DevSpaces.axaml.cs
src/DevSpaces/DevSpaceRegistry.cs
src/DevSpaces/DevSpacesBootstrap.cs
src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj
src/SourceGit.WindowsTerminalHost/Program.cs
src/SourceGit.WindowsTerminalHost/WindowsTerminalHost.cs
.github/workflows/build.yml
THIRD-PARTY-LICENSES.md
```

Plus the existing PR #7 terminal-input files/spec/plans. `SourceGit.slnx` and SourceGit AOT settings must remain unchanged unless CI proves a separately justified packaging-only edit is required.

- [ ] **Step 2: Update PR #7 body**

Add a `Windows native backend` section stating:

```text
- Windows x64 release artifacts include a separate WPF/Windows Terminal helper process.
- SourceGit.exe remains Avalonia 11 + NativeAOT and does not reference WPF.
- Windows ARM64/macOS/Linux remain on the Avalonia terminal fallback.
- Native host startup is optional; failure falls back without restarting sibling terminals.
- EasyWindowsTerminalControl is pinned to 1.0.38 and is an unofficial/beta packaging surface around Windows Terminal.
```

Keep the existing manual-vs-CI verification distinction.

- [ ] **Step 3: Run/follow the final PR Check on the exact final head**

Required green evidence:

```text
Format Check
Build Windows x64 + SourceGit NativeAOT publish
Build Windows ARM64
Build macOS Intel
Build macOS Apple Silicon
Build Linux x64
Build Linux arm64
Windows x64 helper format/build/publish
win-x64 helper packaging assertion
non-x64 absence assertions
```

If any job fails, invoke `superpowers:systematic-debugging`, inspect the exact failed job log, identify root cause, and rerun the complete gate after the fix.

- [ ] **Step 4: Perform final manual Windows x64 acceptance**

Using the final win-x64 artifact, verify:

```text
1. Copilot uses the native backend; confirm via diagnostic marker/log.
2. Mouse drag selection behaves like Windows Terminal.
3. Native copy/paste shortcuts work.
4. Ctrl+C reaches Copilot when no selection should be copied.
5. A second terminal does not reload the first.
6. Auto/1x2/2x2/3x3 layout changes preserve both sessions.
7. History/Stashes hide the HWND completely and returning preserves output/process state.
8. Closing one terminal exits only its helper/process.
9. Renaming/removing native-terminal/win-x64/SourceGit.WindowsTerminalHost.exe causes clean Avalonia fallback.
10. SourceGit itself remains the NativeAOT executable from the standard publish step.
```

- [ ] **Step 5: Perform Windows ARM64 fallback acceptance when an ARM64 machine/artifact is available**

Confirm the helper is absent and DevSpaces uses the existing Avalonia terminal. Do not block x64 behavior claims on unavailable ARM64 hardware; CI package/build evidence is separate from manual ARM64 UX evidence.

- [ ] **Step 6: Do not merge automatically**

Report PR #7 head SHA, mergeability, full CI result, Windows x64 manual probe result, and any unverified manual items. Merge only after the user explicitly requests it.

**Deliverable:** PR #7 contains the Windows-native x64 terminal backend only if the real HWND probe passes, has green cross-platform CI on its final head, and clearly separates automated and manual evidence.
