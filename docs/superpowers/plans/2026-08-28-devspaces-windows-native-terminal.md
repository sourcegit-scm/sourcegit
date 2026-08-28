# DevSpaces Windows-Native Terminal Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional Windows Terminal-backed DevSpaces renderer on Windows x64 while preserving SourceGit NativeAOT and the existing Avalonia terminal as the automatic fallback everywhere else.

**Architecture:** Keep `SourceGit.exe` cross-platform, `net10.0`, Avalonia 11.3.20, and NativeAOT. A separate x64-only WPF helper process hosts `EasyWindowsTerminalControl` 1.0.38 inside an `HwndSource` created directly as a child of Avalonia's `NativeControlHost` HWND. SourceGit passes one Base64URL JSON launch payload, receives one ready HWND line, and owns helper lifetime. A disposable interactive Windows x64 probe is a hard go/no-go gate before any production native-host integration is kept.

**Tech Stack:** .NET 10, Avalonia 11.3.20, NativeAOT for SourceGit, WPF `net10.0-windows` helper, EasyWindowsTerminalControl 1.0.38, Windows Terminal WPF backend, ConPTY, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-native-terminal-input-design.md`

## Global Constraints

- Keep `SourceGit.exe` on `net10.0`, Avalonia `11.3.20`, and NativeAOT in Release.
- Pin `EasyWindowsTerminalControl` to `1.0.38`.
- Native Windows Terminal rendering is Windows x64 only.
- Windows ARM64, macOS x64/ARM64, and Linux x64/ARM64 keep the existing `Iciclecreek.Avalonia.Terminal` 1.0.12 renderer.
- Do not embed or reparent installed `wt.exe`.
- Do not reference WPF, EasyWindowsTerminalControl, or Windows-only packages from `src/SourceGit.csproj`.
- Do not add `SourceGit.WindowsTerminalHost` to `SourceGit.slnx`; non-Windows matrix jobs must never restore/build it.
- Do not disable SourceGit NativeAOT.
- Do not add a network socket, global service, persistent broker, or global input hook.
- One native helper process owns one DevSpaces terminal session.
- Native startup failure falls back to the existing Avalonia renderer with the same `DevSpaceLaunchSpec`.
- Adding terminals, changing grid layout, and switching repository pages must not restart existing sessions.
- Preserve the already implemented PR #7 Avalonia full-surface selection and Copy/Paste/Select All behavior.
- Continue the previously approved DevSpaces test-project exception. Evidence comes from source audit, build/package checks, the disposable interactive probe, and manual runtime acceptance.

---

### Task 1: Prove cross-process HWND hosting on real Windows x64

**Files:**
- Create temporarily: `tools/WindowsTerminalHostProbe/WindowsTerminalHostProbe.csproj`
- Create temporarily: `tools/WindowsTerminalHostProbe/Program.cs`
- Create temporarily: `tools/WindowsTerminalHostProbe/ProbeNativeHost.cs`
- Create temporarily: `tools/WindowsTerminalHostProbe.Helper/WindowsTerminalHostProbe.Helper.csproj`
- Create temporarily: `tools/WindowsTerminalHostProbe.Helper/Program.cs`
- Delete both probe directories after the gate.

**Interfaces:**
- Consumes: Avalonia `NativeControlHost`, `IPlatformHandle`, `PlatformHandle`; WPF `HwndSource`; `EasyTerminalControl`.
- Produces: go/no-go evidence only. Probe code is never retained as production implementation.

- [ ] **Step 1: Create the WPF probe helper**

Create `tools/WindowsTerminalHostProbe.Helper/WindowsTerminalHostProbe.Helper.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
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

- [ ] **Step 2: Create the terminal child HWND directly under Avalonia's parent HWND**

Create `tools/WindowsTerminalHostProbe.Helper/Program.cs`:

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

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
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

        Console.Out.WriteLine($"SOURCEGIT_TERMINAL_READY {source.Handle.ToInt64()}");
        Console.Out.Flush();
        app.Run();
        return 0;
    }
}
```

Do not create a normal top-level WPF `Window` and reparent it later.

- [ ] **Step 3: Create the Avalonia probe**

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

`ProbeNativeHost : NativeControlHost` starts the helper with:

```csharp
var psi = new ProcessStartInfo(helperPath)
{
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
psi.ArgumentList.Add(parent.Handle.ToInt64().ToString(CultureInfo.InvariantCulture));
```

It waits at most five seconds for one line matching `SOURCEGIT_TERMINAL_READY <decimal-hwnd>` and returns:

```csharp
return new PlatformHandle(new IntPtr(hwndValue), "HWND");
```

`DestroyNativeControlCore` terminates/disposes the helper and never calls `DestroyWindow` on the foreign-process HWND.

- [ ] **Step 4: Build both probe projects on Windows x64**

```powershell
 dotnet build tools/WindowsTerminalHostProbe.Helper/WindowsTerminalHostProbe.Helper.csproj -c Release
 dotnet build tools/WindowsTerminalHostProbe/WindowsTerminalHostProbe.csproj -c Release
```

Expected: both commands exit 0.

- [ ] **Step 5: Run the interactive go/no-go gate**

On a real interactive Windows x64 desktop verify:

```text
1. cmd.exe renders inside the Avalonia host rectangle.
2. Click-to-focus and normal typing work.
3. Mouse drag selection works.
4. Resizing the Avalonia window resizes the terminal.
5. Hide/show preserves the cmd.exe process and terminal state.
6. Closing the host terminates the helper cleanly.
7. Hidden native content does not bleed over unrelated Avalonia content.
```

GitHub hosted runners cannot substitute for this interaction gate.

- [ ] **Step 6: Apply the hard gate**

If any item requires `wt.exe` reparenting, a top-level-window reparent hack, global input hooks, disabling NativeAOT, or recurring focus hacks, delete the probe directories and stop this native-host plan. PR #7 remains the improved Avalonia terminal only.

If all seven pass, record the result on PR #7 and continue.

- [ ] **Step 7: Delete the probe directories**

Remove both temporary probe projects before production commits continue.

**Deliverable:** real Windows evidence that cross-process child-HWND hosting is viable, or a clean no-go with no native production code retained.

---

### Task 2: Add the NativeAOT-safe launch protocol

**Files:**
- Create: `src/DevSpaces/WindowsTerminalHostProtocol.cs`

**Interfaces:**
- Produces: `WindowsTerminalLaunchPayload`, `WindowsTerminalHostProtocol.ReadyPrefix`, `StartupTimeoutMilliseconds`, `Encode`, `TryDecode`, `BuildWindowsCommandLine`.
- Consumed by `SourceGit.exe` and linked as source into `SourceGit.WindowsTerminalHost`.

- [ ] **Step 1: Define the source-generated JSON contract**

Create:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGit.DevSpaces
{
    public sealed record WindowsTerminalLaunchPayload(
        string Process,
        string[] Arguments,
        string WorkingDirectory);

    [JsonSerializable(typeof(WindowsTerminalLaunchPayload))]
    internal partial class WindowsTerminalHostJsonContext : JsonSerializerContext
    {
    }

    public static class WindowsTerminalHostProtocol
    {
        public const string ReadyPrefix = "SOURCEGIT_TERMINAL_READY ";
        public const int StartupTimeoutMilliseconds = 5000;

        public static string Encode(WindowsTerminalLaunchPayload payload)
        {
            var json = JsonSerializer.Serialize(
                payload,
                WindowsTerminalHostJsonContext.Default.WindowsTerminalLaunchPayload);
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
                payload = JsonSerializer.Deserialize(
                    json,
                    WindowsTerminalHostJsonContext.Default.WindowsTerminalLaunchPayload);
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

Using a generated `JsonSerializerContext` is required because the same source is compiled into the NativeAOT SourceGit process.

- [ ] **Step 2: Add deterministic CreateProcess-compatible command-line quoting**

Add to the same class:

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
            var slashes = 0;
            foreach (var ch in value)
            {
                if (ch == '\\')
                {
                    slashes++;
                    continue;
                }

                if (ch == '"')
                {
                    builder.Append('\\', slashes * 2 + 1);
                    builder.Append('"');
                    slashes = 0;
                    continue;
                }

                builder.Append('\\', slashes);
                slashes = 0;
                builder.Append(ch);
            }

            builder.Append('\\', slashes * 2);
            builder.Append('"');
        }
    }
}
```

Do not join raw arguments with spaces.

- [ ] **Step 3: Audit the protocol before integration**

Confirm from the source diff:

```text
- JSON contains only Process, Arguments, WorkingDirectory.
- JSON serialization uses the generated context in both directions.
- malformed Base64URL/JSON returns false.
- no shell is involved in payload decoding.
- command-line quoting handles spaces, embedded quotes, empty args, and trailing backslashes.
```

- [ ] **Step 4: Commit**

```text
feat: add Windows terminal host protocol
```

**Deliverable:** one AOT-safe neutral launch contract shared by both processes without an assembly reference.

---

### Task 3: Add the standalone Windows Terminal helper

**Files:**
- Create: `src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj`
- Create: `src/SourceGit.WindowsTerminalHost/Program.cs`
- Create: `src/SourceGit.WindowsTerminalHost/WindowsTerminalHost.cs`
- Do not modify: `SourceGit.slnx`

**Interfaces:**
- Consumes linked `../DevSpaces/WindowsTerminalHostProtocol.cs` and EasyWindowsTerminalControl 1.0.38.
- Produces `SourceGit.WindowsTerminalHost.exe`, one ready line on stdout, stderr diagnostics, and helper process exit matching the child terminal exit when available.

- [ ] **Step 1: Create the x64 WPF project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
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

- [ ] **Step 2: Parse exactly the approved startup arguments**

`Program.Main` accepts exactly:

```text
--parent-hwnd <decimal-hwnd> --launch-payload <base64url-json>
```

Reject malformed or extra arguments with exit code 2 and a stderr message. Decode using `WindowsTerminalHostProtocol.TryDecode`.

- [ ] **Step 3: Create the `HwndSource` and `EasyTerminalControl`**

In `WindowsTerminalHost`:

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

Only after the source/root visual exists emit:

```csharp
Console.Out.WriteLine(
    $"{WindowsTerminalHostProtocol.ReadyPrefix}{_source.Handle.ToInt64()}");
Console.Out.Flush();
```

All other diagnostics go to stderr.

- [ ] **Step 4: Exit the helper when the ConPTY child exits**

The reviewed 1.0.38 source exposes `TermPTY.Process : IProcess`, where `IProcess.HasExited` is public, and the default implementation is public `ProcessFactory.WrappedProcess` with a public `System.Diagnostics.Process Process` property.

After the terminal starts, monitor without blocking the WPF dispatcher:

```csharp
private async Task MonitorTerminalProcessAsync()
{
    while (_terminal.ConPTYTerm?.Process == null)
        await Task.Delay(50);

    var process = _terminal.ConPTYTerm.Process;
    if (process is ProcessFactory.WrappedProcess wrapped)
    {
        await wrapped.Process.WaitForExitAsync();
        var exitCode = wrapped.Process.ExitCode;
        _application.Dispatcher.Invoke(() => _application.Shutdown(exitCode));
        return;
    }

    await Task.Run(process.WaitForExit);
    _application.Dispatcher.Invoke(() => _application.Shutdown(0));
}
```

Start this monitor once. Do not poll forever after the process is known.

- [ ] **Step 5: Dispose only this terminal session during helper shutdown**

```csharp
try { _terminal?.ConPTYTerm?.CloseStdinToApp(); } catch { }
try { _terminal?.ConPTYTerm?.StopExternalTermOnly(); } catch { }
try { _source?.Dispose(); } catch { }
```

- [ ] **Step 6: Build and publish independently on Windows x64**

```powershell
 dotnet build src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj -c Release
 dotnet publish src/SourceGit.WindowsTerminalHost/SourceGit.WindowsTerminalHost.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows-terminal-host
```

Expected: both exit 0 and `artifacts/windows-terminal-host/SourceGit.WindowsTerminalHost.exe` exists.

- [ ] **Step 7: Commit**

```text
feat: add Windows Terminal helper process
```

**Deliverable:** an x64-only JIT/WPF helper hosting one Windows Terminal/ConPTY session, isolated from SourceGit's NativeAOT executable.

---

### Task 4: Introduce terminal surfaces and the Avalonia `NativeControlHost`

**Files:**
- Create: `src/DevSpaces/IDevSpaceTerminalSurface.cs`
- Create: `src/DevSpaces/AvaloniaDevSpaceTerminalSurface.cs`
- Create: `src/DevSpaces/WindowsNativeDevSpaceTerminalSurface.cs`
- Create: `src/Views/WindowsTerminalNativeHost.cs`
- Modify: `src/Views/DevSpaceTerminal.axaml`
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`

**Interfaces:**
- Produces `IDevSpaceTerminalSurface.View`, `Exited`, `Start(DevSpaceLaunchSpec)`, `SetPageActive(bool)`, `Dispose()`.
- `WindowsTerminalNativeHost` additionally produces `event EventHandler<int> HelperExited`.

- [ ] **Step 1: Define the shared surface boundary**

Create:

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

- [ ] **Step 2: Move the existing fallback terminal behavior into `AvaloniaDevSpaceTerminalSurface`**

The class owns one `Views.DevSpaceTerminalControl`, subscribes to its `ProcessExited`, and launches exactly as PR #7 does now:

```csharp
_terminal.LaunchProcess(spec.WorkingDirectory, spec.Process, spec.Arguments);
```

Move the existing tunneling right-click handler and `TryClipboardAsync` helper into this surface so native and Avalonia backends do not share clipboard logic.

`SetPageActive(bool)` must not destroy/recreate the control. Preserve all existing PR #7 shortcut and TUI mouse-reporting behavior.

- [ ] **Step 3: Implement `WindowsTerminalNativeHost`**

Subclass `NativeControlHost`. Keep one `DevSpaceLaunchSpec`, one `System.Diagnostics.Process`, and one stopped flag.

`CreateNativeControlCore(IPlatformHandle parent)` must first enforce:

```csharp
if (!OperatingSystem.IsWindows() ||
    RuntimeInformation.ProcessArchitecture != Architecture.X64)
    throw new PlatformNotSupportedException();
```

Resolve the helper from:

```csharp
Path.Combine(
    AppContext.BaseDirectory,
    "native-terminal",
    "win-x64",
    "SourceGit.WindowsTerminalHost.exe")
```

Start it with:

```csharp
var psi = new ProcessStartInfo(helperPath)
{
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
psi.ArgumentList.Add("--parent-hwnd");
psi.ArgumentList.Add(parent.Handle.ToInt64().ToString(CultureInfo.InvariantCulture));
psi.ArgumentList.Add("--launch-payload");
psi.ArgumentList.Add(WindowsTerminalHostProtocol.Encode(payload));
```

Wait at most `StartupTimeoutMilliseconds` for the first stdout line. Accept only `ReadyPrefix + decimal HWND`. Reject zero/malformed handles. On any startup failure, call one cleanup method that kills the helper process tree if still alive, disposes it, then throws.

After the ready line, subscribe to helper process exit and raise:

```csharp
public event EventHandler<int> HelperExited;
```

exactly once with `_helper.ExitCode`.

Return:

```csharp
return new PlatformHandle(childHwnd, "HWND");
```

`DestroyNativeControlCore` calls helper cleanup only. Never call Win32 `DestroyWindow(control.Handle)` because the helper owns that HWND.

- [ ] **Step 4: Implement `WindowsNativeDevSpaceTerminalSurface`**

`View` returns one persistent `WindowsTerminalNativeHost` instance. `Start(spec)` assigns the launch spec before the native host is attached and subscribes to `HelperExited`. `SetPageActive(active)` sets `View.IsVisible = active`; it does not dispose the helper.

Availability is exactly:

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

Forward `HelperExited` to `Exited` once.

- [ ] **Step 5: Replace hard-coded terminal XAML with a neutral surface slot**

`src/Views/DevSpaceTerminal.axaml` becomes:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:SourceGit.ViewModels"
             xmlns:c="using:SourceGit.Converters"
             x:Class="SourceGit.Views.DevSpaceTerminal"
             x:DataType="vm:DevSpaceTerminal">
  <Grid>
    <Grid x:Name="SurfaceHost" />
    <Border Background="{DynamicResource Brush.Window}"
            IsVisible="{Binding ErrorMessage, Converter={x:Static c:StringConverters.IsNotNullOrWhitespace}}">
      <TextBlock Margin="16"
                 Text="{Binding ErrorMessage}"
                 TextWrapping="Wrap"/>
    </Border>
  </Grid>
</UserControl>
```

- [ ] **Step 6: Make `DevSpaceTerminal` select native then fall back**

`Start` obtains the existing launch spec once, then:

```csharp
var spec = launcher.Create(session.Command, session.WorkingDirectory);
_surface = CreatePreferredSurface();
try
{
    AttachAndStart(_surface, spec);
}
catch (Exception ex) when (_surface is DevSpaces.WindowsNativeDevSpaceTerminalSurface)
{
    _surface.Dispose();
    _surface = new DevSpaces.AvaloniaDevSpaceTerminalSurface();
    AttachAndStart(_surface, spec);
    Trace.WriteLine($"[DevSpaces] native-terminal=fallback error={ex}");
}

Trace.WriteLine(
    $"[DevSpaces] terminal-backend={(_surface is DevSpaces.WindowsNativeDevSpaceTerminalSurface ? "windows-terminal" : "avalonia")}");
session.MarkRunning();
```

`AttachAndStart` adds exactly one `surface.View` to `SurfaceHost`, subscribes to `surface.Exited`, applies the current page-active state, then calls `surface.Start(spec)`.

Add:

```csharp
public void SetPageActive(bool active)
{
    _pageActive = active;
    _surface?.SetPageActive(active);
}
```

`Stop` unsubscribes `Exited`, removes the view, disposes the surface, and remains idempotent.

If the native helper exits after successful startup, forward the exit to `session.MarkExited(exitCode)`. Do not auto-start another process after a live native session exits.

- [ ] **Step 7: Verify SourceGit still compiles without Windows/WPF references**

Run the normal SourceGit build on the current platform. Inspect `src/SourceGit.csproj` to confirm no WPF/EasyWindowsTerminalControl package or project reference was added.

- [ ] **Step 8: Commit**

```text
feat: add DevSpaces native terminal backend
```

**Deliverable:** SourceGit can instantiate the helper-backed renderer when packaged/supported and transparently use the existing Avalonia renderer when native startup is unavailable.

---

### Task 5: Propagate repository-page activity without destroying sessions

**Files:**
- Modify: `src/Views/DevSpaces.axaml.cs`
- Modify: `src/DevSpaces/DevSpaceRegistry.cs`
- Modify: `src/DevSpaces/DevSpacesBootstrap.cs`

**Interfaces:**
- Produces `Views.DevSpaces.SetPageActive(bool)` and `DevSpaceRegistry.SetPageActive(ViewModels.Repository, bool)`.
- Consumes `DevSpaceTerminal.SetPageActive(bool)`.

- [ ] **Step 1: Propagate activity through the cached panes**

In `Views.DevSpaces` add:

```csharp
public void SetPageActive(bool active)
{
    _pageActive = active;
    foreach (var pane in _panes.Values)
        pane.TerminalView.SetPageActive(active);
}
```

After `terminalView.Start(_owner.Launcher)` in `GetOrCreatePane`, call:

```csharp
terminalView.SetPageActive(_pageActive);
```

Add `private bool _pageActive;`.

- [ ] **Step 2: Add registry forwarding**

In `DevSpaceRegistry` add:

```csharp
public static void SetPageActive(ViewModels.Repository repository, bool active)
{
    if (repository != null && _spaces.TryGetValue(repository, out var entry))
        entry.View.SetPageActive(active);
}
```

- [ ] **Step 3: Update bootstrap page switching**

Keep the existing fallback subtree mounted:

```csharp
_host.IsVisible = true;
var active = _repository.SelectedViewIndex == 3;
_host.Opacity = active ? 1 : 0;
_host.IsHitTestVisible = active;
DevSpaceRegistry.SetPageActive(_repository, active);
```

When DevSpaces is disabled, call `DevSpaceRegistry.SetPageActive(_repository, false)` before detaching.

Native child HWND visibility must come from its `NativeControlHost.IsVisible`; never rely on host opacity to hide a native HWND.

- [ ] **Step 4: Verify persistence manually**

With two terminals open, switch DevSpaces -> History -> Stashes -> DevSpaces. Confirm both process IDs and output state are unchanged.

- [ ] **Step 5: Commit**

```text
fix: preserve native terminals across repository pages
```

**Deliverable:** repository navigation hides native child HWNDs without terminating or recreating helper/ConPTY sessions.

---

### Task 6: Package the helper only for Windows x64

**Files:**
- Modify: `.github/workflows/build.yml`
- Inspect/modify only if required: `build/scripts/package.win.ps1`
- Modify: `THIRD-PARTY-LICENSES.md`

**Interfaces:**
- Produces `publish/native-terminal/win-x64/SourceGit.WindowsTerminalHost.exe` only for the `win-x64` matrix artifact.

- [ ] **Step 1: Format and publish the helper only in the Windows x64 job**

Add before artifact upload:

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

Do not change the existing SourceGit publish command; it remains the independent NativeAOT publish evidence.

- [ ] **Step 2: Stage helper output into the win-x64 SourceGit artifact**

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

- [ ] **Step 3: Add explicit helper absence checks to every other runtime**

For Windows ARM64 add a PowerShell assertion:

```yaml
      - name: Assert Windows native terminal is x64-only
        if: matrix.runtime == 'win-arm64'
        shell: pwsh
        run: |
          if (Test-Path 'publish/native-terminal') {
            throw 'Windows ARM64 artifact must not contain the x64 terminal helper.'
          }
```

For macOS/Linux add a shell assertion:

```yaml
      - name: Assert Windows native terminal is absent
        if: startsWith(matrix.runtime, 'osx-') || startsWith(matrix.runtime, 'linux-')
        shell: bash
        run: test ! -e publish/native-terminal
```

- [ ] **Step 4: Verify Windows packaging preserves nested helper files**

Inspect `build/scripts/package.win.ps1`. If it packages the complete staged publish directory recursively, make no change. If it enumerates top-level files, change that enumeration to include `native-terminal/win-x64/**`. Do not otherwise refactor packaging.

- [ ] **Step 5: Add third-party attribution**

Update `THIRD-PARTY-LICENSES.md` for `EasyWindowsTerminalControl` 1.0.38 and the Windows Terminal/ConPTY packages resolved by the helper restore. Follow the repository's existing attribution format and record exact resolved package versions from `dotnet list ... package --include-transitive` or `obj/project.assets.json` on the Windows helper build.

- [ ] **Step 6: Commit**

```text
build: package Windows terminal host on x64
```

**Deliverable:** the native helper and its dependencies exist only in win-x64 artifacts while SourceGit remains independently NativeAOT-published.

---

### Task 7: Update PR #7 and verify the exact final head

**Files:**
- Modify: PR #7 body
- Audit: branch diff from `master` to `feat/devspaces-native-terminal-input`

**Interfaces:**
- Consumes all previous tasks.
- Produces merge-ready evidence only after the final CI gate plus manual Windows x64 acceptance.

- [ ] **Step 1: Audit final branch scope**

After the disposable probe is deleted, expected native-host production scope is:

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

Plus the existing PR #7 fallback terminal-input files and approved spec/plans. `SourceGit.slnx` and SourceGit AOT settings remain unchanged.

- [ ] **Step 2: Update PR #7 body**

Add:

```text
## Windows native backend

- Windows x64 release artifacts include a separate WPF/Windows Terminal helper process.
- SourceGit.exe remains Avalonia 11 + NativeAOT and has no WPF dependency.
- Windows ARM64/macOS/Linux continue using the Avalonia terminal fallback.
- Native startup is optional; startup failure falls back with the same launch spec.
- EasyWindowsTerminalControl is pinned to 1.0.38 and wraps unofficial/beta Windows Terminal packaging.
- Interactive Windows x64 HWND hosting is a separately recorded manual verification gate.
```

- [ ] **Step 3: Follow the final PR Check on the exact final head**

Required successful evidence:

```text
Format Check
Windows x64 SourceGit build + NativeAOT publish
Windows ARM64 SourceGit build/publish
macOS Intel build/publish
macOS Apple Silicon build/publish
Linux x64 build/publish
Linux arm64 build/publish
Windows x64 helper format/build/publish
win-x64 helper staging assertion
win-arm64/macOS/Linux helper-absence assertions
```

If any job fails, invoke `superpowers:systematic-debugging`, inspect the failing job logs, identify the root cause, make the smallest fix, and rerun the complete gate on the new final head.

- [ ] **Step 4: Perform final Windows x64 runtime acceptance**

Using the final win-x64 artifact:

```text
1. Start Copilot and confirm `SourceGit.WindowsTerminalHost.exe` is the active child helper; capture the `[DevSpaces] terminal-backend=windows-terminal` diagnostic while running under the debugger/trace listener.
2. Drag-select across text and blank-space boundaries.
3. Copy/paste with normal Windows Terminal interactions.
4. With no selected text, Ctrl+C reaches Copilot as expected.
5. Open a second terminal; the first helper PID and state remain unchanged.
6. Change Auto / 1x2 / 2x2 / 3x3 layouts; both sessions persist.
7. Switch History/Stashes and back; HWNDs do not bleed over other pages and terminal state persists.
8. Close one terminal; only its helper/ConPTY child exits.
9. Temporarily remove `native-terminal/win-x64/SourceGit.WindowsTerminalHost.exe`; creating a new terminal uses the Avalonia fallback without crashing.
10. Confirm the SourceGit publish step still reports NativeAOT and the app executable comes from that unchanged publish path.
```

- [ ] **Step 5: Keep ARM64 manual status separate**

When Windows ARM64 hardware is available, confirm DevSpaces uses the Avalonia fallback. Do not claim ARM64 native-terminal runtime testing without that hardware; CI build/package evidence is separate.

- [ ] **Step 6: Do not merge automatically**

Report PR #7 head SHA, mergeability, complete CI result, disposable probe result, final Windows x64 runtime result, and any remaining unverified manual items. Merge only after an explicit user merge request.

**Deliverable:** PR #7 contains the Windows-native x64 backend only if the interactive HWND gate passes, has green cross-platform CI on the exact final head, preserves SourceGit NativeAOT, and clearly separates automated from manual evidence.
