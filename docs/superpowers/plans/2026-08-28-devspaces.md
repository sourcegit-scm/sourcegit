# DevSpaces Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in DevSpaces repository page that launches multiple embedded GitHub Copilot CLI terminals in the current repository/worktree and displays them in Auto, 1x1, 2x2, 3x3, or 4x4 layouts without restarting running PTY sessions.

**Architecture:** Each `Repository` lazily owns one `ViewModels.DevSpaces` session manager. Session state is independent from Avalonia presentation: `DevSpaces` owns terminal metadata and grid-slot selection, while `Views.DevSpaces` keeps one persistent terminal control per session and reparents those controls when the layout changes. Terminal startup goes through a small local-launch abstraction so a later container-backed launcher can be added without changing repository navigation or the DevSpaces UI model.

**Tech Stack:** .NET 10, Avalonia 11.3.20, CommunityToolkit.Mvvm 8.4.2, `Iciclecreek.Avalonia.Terminal` 1.0.11, Porta.Pty transitively, SourceGit JSON source generation and XAML localization resources.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-design.md`

## Global Constraints

- `EnableDevSpaces` defaults to `false`; when false, existing SourceGit repository behavior and navigation remain unchanged.
- `DevSpacesDefaultCommand` defaults to `"copilot"`.
- `DevSpacesDefaultLayout` defaults to `Models.DevSpaceLayout.Auto`.
- DevSpaces is repository view index `3`, directly after Stashes.
- Every local terminal starts in the owning `Repository.FullPath`.
- The first DevSpaces visit creates one terminal automatically.
- Supported layouts are Auto, 1x1, 2x2, 3x3, and 4x4; grid capacity is at most 16 visible sessions.
- Changing layouts must never restart or recreate a PTY process.
- Closing one terminal must not stop any other terminal.
- Closing a repository/worktree tab terminates all DevSpaces sessions owned by that repository.
- Use `Iciclecreek.Avalonia.Terminal` exactly `1.0.11`; do not upgrade SourceGit to Avalonia 12.
- Do not add Docker, Podman, WSLC, container lifecycle, image, or mount behavior in this milestone.
- Do not add a new test framework solely for DevSpaces; use compile, publish, focused pure-logic verification, and manual functional checks described below.
- Do not modify the existing CI branch-trigger problem as part of this feature.

---

## File Structure

### New files

- `src/Models/DevSpaceLayout.cs` — layout enum and display/capacity helpers only.
- `src/DevSpaces/IDevSpaceSessionLauncher.cs` — terminal-launch contract and immutable launch specification.
- `src/DevSpaces/LocalDevSpaceSessionLauncher.cs` — converts a configured command into the platform shell invocation rooted at the repository/worktree path.
- `src/ViewModels/DevSpaceTerminal.cs` — one terminal session's state and lifecycle signals; no Avalonia terminal control dependency.
- `src/ViewModels/DevSpaces.cs` — per-repository terminal collection, active session, layout state, visible grid slots, session creation/close/dispose.
- `src/Views/DevSpaces.axaml` — DevSpaces toolbar and terminal-grid host.
- `src/Views/DevSpaces.axaml.cs` — persistent terminal-view dictionary, grid reparenting, tab/layout/empty-cell interactions.
- `src/Views/DevSpaceTerminal.axaml` — one embedded terminal pane/control.
- `src/Views/DevSpaceTerminal.axaml.cs` — PTY start/exit/kill adapter around `Iciclecreek.Terminal.TerminalControl`.

### Modified files

- `src/SourceGit.csproj` — pin `Iciclecreek.Avalonia.Terminal` 1.0.11.
- `src/ViewModels/Preferences.cs` — persisted DevSpaces feature, command, and default-layout properties.
- `src/Views/Preferences.axaml` — first-class DevSpaces Preferences tab.
- `src/ViewModels/Repository.cs` — fourth repository page, lazy DevSpaces owner, preference-change handling, cleanup.
- `src/Views/Repository.axaml` — DevSpaces navigation item under Stashes and main-content host.
- `src/Resources/Locales/en_US.axaml` — canonical DevSpaces strings.
- Any active locale that does not inherit `en_US.axaml` — add English fallback entries for the same DevSpaces keys so every configured locale resolves them.

---

### Task 1: Lock the PTY Dependency and Define the Launch Boundary

**Files:**
- Modify: `src/SourceGit.csproj`
- Create: `src/DevSpaces/IDevSpaceSessionLauncher.cs`
- Create: `src/DevSpaces/LocalDevSpaceSessionLauncher.cs`

**Interfaces:**
- Produces: `DevSpaceLaunchSpec(string Process, string[] Arguments, string WorkingDirectory)`
- Produces: `IDevSpaceSessionLauncher.Create(string command, string workingDirectory) -> DevSpaceLaunchSpec`
- Produces: `LocalDevSpaceSessionLauncher : IDevSpaceSessionLauncher`

- [ ] **Step 1: Add the exact terminal package version**

Add this package reference beside the existing Avalonia/UI package references in `src/SourceGit.csproj`:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.11" />
```

Do not change any existing Avalonia package version.

- [ ] **Step 2: Restore and verify the dependency does not force Avalonia 12**

Run from repository root after submodules are initialized:

```bash
git submodule update --init --recursive
dotnet restore SourceGit.slnx
dotnet list src/SourceGit.csproj package --include-transitive
```

Expected: restore succeeds; `Iciclecreek.Avalonia.Terminal 1.0.11` is present; resolved Avalonia remains on the repository's 11.3.x line rather than 12.x.

If restore resolves Avalonia 12 or produces a package downgrade/conflict, stop this task and do not proceed with the package.

- [ ] **Step 3: Create the launcher contract**

Create `src/DevSpaces/IDevSpaceSessionLauncher.cs`:

```csharp
namespace SourceGit.DevSpaces
{
    public readonly record struct DevSpaceLaunchSpec(
        string Process,
        string[] Arguments,
        string WorkingDirectory);

    public interface IDevSpaceSessionLauncher
    {
        DevSpaceLaunchSpec Create(string command, string workingDirectory);
    }
}
```

- [ ] **Step 4: Implement the local shell launcher**

Create `src/DevSpaces/LocalDevSpaceSessionLauncher.cs`:

```csharp
using System;

namespace SourceGit.DevSpaces
{
    public sealed class LocalDevSpaceSessionLauncher : IDevSpaceSessionLauncher
    {
        public DevSpaceLaunchSpec Create(string command, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("DevSpaces command must not be empty.", nameof(command));

            if (string.IsNullOrWhiteSpace(workingDirectory))
                throw new ArgumentException("DevSpaces working directory must not be empty.", nameof(workingDirectory));

            if (OperatingSystem.IsWindows())
            {
                return new DevSpaceLaunchSpec(
                    "pwsh",
                    ["-NoLogo", "-NoProfile", "-Command", command],
                    workingDirectory);
            }

            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrWhiteSpace(shell))
                shell = "/bin/sh";

            return new DevSpaceLaunchSpec(
                shell,
                ["-lc", command],
                workingDirectory);
        }
    }
}
```

Use PowerShell on Windows so npm/WinGet command shims such as `copilot` resolve in the user's normal shell environment instead of attempting to execute a `.cmd` shim directly through `Process`.

- [ ] **Step 5: Compile the new boundary**

Run:

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Expected: build succeeds with the package pinned and the new launcher types compiled.

- [ ] **Step 6: Commit**

```bash
git add src/SourceGit.csproj src/DevSpaces/IDevSpaceSessionLauncher.cs src/DevSpaces/LocalDevSpaceSessionLauncher.cs
git commit -m "feat: add DevSpaces terminal launch boundary"
```

---

### Task 2: Add DevSpace Layout Model and Persisted Preferences

**Files:**
- Create: `src/Models/DevSpaceLayout.cs`
- Modify: `src/ViewModels/Preferences.cs`
- Modify: `src/Views/Preferences.axaml`

**Interfaces:**
- Produces: `Models.DevSpaceLayout`
- Produces: `Preferences.EnableDevSpaces : bool`
- Produces: `Preferences.DevSpacesDefaultCommand : string`
- Produces: `Preferences.DevSpacesDefaultLayout : Models.DevSpaceLayout`
- Produces: `Preferences.DevSpacesDefaultLayoutIndex : int` for simple `ComboBox.SelectedIndex` binding

- [ ] **Step 1: Create the layout enum and helpers**

Create `src/Models/DevSpaceLayout.cs`:

```csharp
namespace SourceGit.Models
{
    public enum DevSpaceLayout
    {
        Auto = 0,
        OneByOne = 1,
        TwoByTwo = 2,
        ThreeByThree = 3,
        FourByFour = 4,
    }

    public static class DevSpaceLayoutExtensions
    {
        public static int GetDimension(this DevSpaceLayout layout, int sessionCount)
        {
            if (layout != DevSpaceLayout.Auto)
                return (int)layout;

            if (sessionCount <= 1)
                return 1;
            if (sessionCount <= 4)
                return 2;
            if (sessionCount <= 9)
                return 3;
            return 4;
        }

        public static int GetCapacity(this DevSpaceLayout layout, int sessionCount)
        {
            var dimension = layout.GetDimension(sessionCount);
            return dimension * dimension;
        }
    }
}
```

The helper intentionally clamps Auto to 4x4 for any count greater than 9.

- [ ] **Step 2: Add persisted Preferences properties**

Add properties in `src/ViewModels/Preferences.cs`:

```csharp
public bool EnableDevSpaces
{
    get => _enableDevSpaces;
    set => SetProperty(ref _enableDevSpaces, value);
}

public string DevSpacesDefaultCommand
{
    get => _devSpacesDefaultCommand;
    set => SetProperty(ref _devSpacesDefaultCommand, value);
}

public Models.DevSpaceLayout DevSpacesDefaultLayout
{
    get => _devSpacesDefaultLayout;
    set
    {
        if (SetProperty(ref _devSpacesDefaultLayout, value))
            OnPropertyChanged(nameof(DevSpacesDefaultLayoutIndex));
    }
}

[JsonIgnore]
public int DevSpacesDefaultLayoutIndex
{
    get => (int)_devSpacesDefaultLayout;
    set
    {
        if (value >= 0 && value <= 4)
            DevSpacesDefaultLayout = (Models.DevSpaceLayout)value;
    }
}
```

Add backing fields with exact defaults near the other preference fields:

```csharp
private bool _enableDevSpaces = false;
private string _devSpacesDefaultCommand = "copilot";
private Models.DevSpaceLayout _devSpacesDefaultLayout = Models.DevSpaceLayout.Auto;
```

`DevSpacesDefaultLayoutIndex` is ignored in JSON so only the typed enum is persisted.

- [ ] **Step 3: Add the top-level Preferences → DevSpaces tab**

Add one `TabItem` to the existing Preferences `TabControl` in `src/Views/Preferences.axaml`:

```xml
<TabItem>
  <TabItem.Header>
    <TextBlock Classes="tab_header" Text="{DynamicResource Text.DevSpaces}"/>
  </TabItem.Header>

  <Grid Margin="8"
        RowDefinitions="32,32,32"
        ColumnDefinitions="Auto,*">
    <CheckBox Grid.Row="0" Grid.Column="1"
              Height="32"
              Content="{DynamicResource Text.DevSpaces.Enable}"
              IsChecked="{Binding EnableDevSpaces, Mode=TwoWay}"/>

    <TextBlock Grid.Row="1" Grid.Column="0"
               Margin="0,0,16,0"
               HorizontalAlignment="Right"
               Text="{DynamicResource Text.DevSpaces.DefaultCommand}"/>
    <TextBox Grid.Row="1" Grid.Column="1"
             Height="28"
             CornerRadius="3"
             Text="{Binding DevSpacesDefaultCommand, Mode=TwoWay}"/>

    <TextBlock Grid.Row="2" Grid.Column="0"
               Margin="0,0,16,0"
               HorizontalAlignment="Right"
               Text="{DynamicResource Text.DevSpaces.DefaultLayout}"/>
    <ComboBox Grid.Row="2" Grid.Column="1"
              Height="28"
              SelectedIndex="{Binding DevSpacesDefaultLayoutIndex, Mode=TwoWay}">
      <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.Auto}"/>
      <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.1x1}"/>
      <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.2x2}"/>
      <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.3x3}"/>
      <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.4x4}"/>
    </ComboBox>
  </Grid>
</TabItem>
```

Do not put container/image/runtime fields on this page in this milestone.

- [ ] **Step 4: Compile preference bindings and JSON source generation**

Run:

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Expected: no compiled-binding or JSON source-generation errors for the enum/index properties.

- [ ] **Step 5: Manually verify persistence**

Run the app, open Preferences → DevSpaces, confirm defaults are OFF / `copilot` / Auto, change all three values, close Preferences, restart SourceGit, and reopen Preferences.

Expected: saved values are restored from `preference.json`; `DevSpacesDefaultLayoutIndex` itself is not serialized.

- [ ] **Step 6: Commit**

```bash
git add src/Models/DevSpaceLayout.cs src/ViewModels/Preferences.cs src/Views/Preferences.axaml
git commit -m "feat: add DevSpaces preferences"
```

---

### Task 3: Implement Terminal Session and Grid-State View Models

**Files:**
- Create: `src/ViewModels/DevSpaceTerminal.cs`
- Create: `src/ViewModels/DevSpaces.cs`

**Interfaces:**
- Produces: `DevSpaceTerminalState { Created, Running, Exited, Failed, Stopping }`
- Produces: `DevSpaceTerminal` with `Id`, `Title`, `Command`, `WorkingDirectory`, `State`, `ExitCode`, `ErrorMessage`, `StopRequested`, and state-transition methods
- Produces: `DevSpaceGridSlot(int Index, DevSpaceTerminal Terminal)` where `Terminal == null` represents an empty cell
- Produces: `DevSpaces.Sessions`, `VisibleSlots`, `ActiveTerminal`, `Layout`, `LayoutIndex`, `GridDimension`, `EnsureFirstSession()`, `CreateTerminal()`, `CreateTerminalAt(int)`, `ActivateTerminal(...)`, `CloseTerminal(...)`, `StopAll()`, `Dispose()`

- [ ] **Step 1: Create terminal session state**

Create `src/ViewModels/DevSpaceTerminal.cs` with this public contract:

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public enum DevSpaceTerminalState
    {
        Created,
        Running,
        Exited,
        Failed,
        Stopping,
    }

    public sealed class DevSpaceTerminal : ObservableObject, IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; }
        public string Command { get; }
        public string WorkingDirectory { get; }

        public DevSpaceTerminalState State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }

        public int ExitCode
        {
            get => _exitCode;
            private set => SetProperty(ref _exitCode, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public event Action<DevSpaceTerminal> StopRequested;

        public DevSpaceTerminal(string title, string command, string workingDirectory)
        {
            Title = title;
            Command = command;
            WorkingDirectory = workingDirectory;
        }

        public void MarkRunning() => State = DevSpaceTerminalState.Running;

        public void MarkExited(int exitCode)
        {
            ExitCode = exitCode;
            State = DevSpaceTerminalState.Exited;
        }

        public void MarkFailed(string message)
        {
            ErrorMessage = message;
            State = DevSpaceTerminalState.Failed;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            State = DevSpaceTerminalState.Stopping;
            StopRequested?.Invoke(this);
        }

        private DevSpaceTerminalState _state = DevSpaceTerminalState.Created;
        private int _exitCode;
        private string _errorMessage = string.Empty;
        private bool _disposed;
    }
}
```

Do not put `TerminalControl`, `Process`, or Avalonia visual objects in this view model.

- [ ] **Step 2: Create the DevSpaces session manager**

Create `src/ViewModels/DevSpaces.cs`. Use `AvaloniaList<T>` to match existing SourceGit collection patterns.

The constructor must be:

```csharp
public DevSpaces(string workingDirectory)
{
    _workingDirectory = workingDirectory;
    _layout = Preferences.Instance.DevSpacesDefaultLayout;
    _launcher = new SourceGit.DevSpaces.LocalDevSpaceSessionLauncher();
    RebuildSlots();
}
```

Expose:

```csharp
public AvaloniaList<DevSpaceTerminal> Sessions { get; } = [];
public AvaloniaList<DevSpaceGridSlot> VisibleSlots { get; } = [];

public DevSpaceTerminal ActiveTerminal
{
    get => _activeTerminal;
    private set => SetProperty(ref _activeTerminal, value);
}

public Models.DevSpaceLayout Layout
{
    get => _layout;
    set
    {
        if (SetProperty(ref _layout, value))
        {
            OnPropertyChanged(nameof(LayoutIndex));
            RebuildSlots();
        }
    }
}

public int LayoutIndex
{
    get => (int)_layout;
    set
    {
        if (value >= 0 && value <= 4)
            Layout = (Models.DevSpaceLayout)value;
    }
}

public int GridDimension
{
    get => _layout.GetDimension(Sessions.Count);
}
```

Declare the slot type in the same file:

```csharp
public sealed class DevSpaceGridSlot
{
    public int Index { get; }
    public DevSpaceTerminal Terminal { get; set; }

    public DevSpaceGridSlot(int index, DevSpaceTerminal terminal)
    {
        Index = index;
        Terminal = terminal;
    }
}
```

- [ ] **Step 3: Implement deterministic session naming and creation**

Use monotonically increasing `_nextSessionNumber`, never `Sessions.Count + 1`, so closing `Copilot 2` then adding another session produces `Copilot 3` or the next unused creation number rather than duplicate titles.

Implement:

```csharp
public void EnsureFirstSession()
{
    if (Sessions.Count == 0)
        CreateTerminal();
}

public DevSpaceTerminal CreateTerminal()
{
    return CreateTerminalAt(-1);
}

public DevSpaceTerminal CreateTerminalAt(int preferredSlot)
{
    var command = Preferences.Instance.DevSpacesDefaultCommand;
    var number = _nextSessionNumber++;
    var prefix = command.Trim().Equals("copilot", StringComparison.OrdinalIgnoreCase)
        ? "Copilot"
        : "Terminal";

    var terminal = new DevSpaceTerminal($"{prefix} {number}", command, _workingDirectory);
    Sessions.Add(terminal);
    ActiveTerminal = terminal;

    if (preferredSlot >= 0)
        _preferredSlot = preferredSlot;

    RebuildSlots();
    return terminal;
}
```

Initialize `_nextSessionNumber = 1`.

- [ ] **Step 4: Implement active-session selection and grid slot rebuilding**

`RebuildSlots()` must satisfy all approved behaviors:

1. capacity is `GridDimension * GridDimension`;
2. fixed layouts always expose exactly capacity slots, including null terminals for empty cells;
3. Auto uses the calculated square capacity, also exposing empty cells within that square;
4. first `capacity` sessions are visible by default;
5. when `ActiveTerminal` is outside the first `capacity`, replace the final visible terminal with `ActiveTerminal`;
6. 1x1 always shows `ActiveTerminal` when one exists;
7. `preferredSlot` places a newly-created terminal in the clicked empty cell if valid;
8. sessions are never disposed during `RebuildSlots()`.

Use this shape:

```csharp
private void RebuildSlots()
{
    var dimension = _layout.GetDimension(Sessions.Count);
    var capacity = dimension * dimension;
    var visible = new List<DevSpaceTerminal>();

    for (var i = 0; i < Sessions.Count && visible.Count < capacity; i++)
        visible.Add(Sessions[i]);

    if (ActiveTerminal != null && !visible.Contains(ActiveTerminal))
    {
        if (visible.Count == capacity && visible.Count > 0)
            visible[visible.Count - 1] = ActiveTerminal;
        else
            visible.Add(ActiveTerminal);
    }

    if (capacity == 1 && ActiveTerminal != null)
    {
        visible.Clear();
        visible.Add(ActiveTerminal);
    }

    VisibleSlots.Clear();
    for (var i = 0; i < capacity; i++)
        VisibleSlots.Add(new DevSpaceGridSlot(i, i < visible.Count ? visible[i] : null));

    if (_preferredSlot >= 0 && _preferredSlot < VisibleSlots.Count && ActiveTerminal != null)
    {
        var current = VisibleSlots.FirstOrDefault(x => x.Terminal == ActiveTerminal);
        if (current != null)
            current.Terminal = VisibleSlots[_preferredSlot].Terminal;
        VisibleSlots[_preferredSlot].Terminal = ActiveTerminal;
        _preferredSlot = -1;
    }

    OnPropertyChanged(nameof(GridDimension));
    OnPropertyChanged(nameof(VisibleSlots));
}
```

Add required `using System.Collections.Generic;` and `using System.Linq;`.

- [ ] **Step 5: Implement close and dispose semantics**

Use:

```csharp
public void ActivateTerminal(DevSpaceTerminal terminal)
{
    if (terminal == null || !Sessions.Contains(terminal))
        return;

    ActiveTerminal = terminal;
    RebuildSlots();
}

public void CloseTerminal(DevSpaceTerminal terminal)
{
    if (terminal == null || !Sessions.Remove(terminal))
        return;

    terminal.Dispose();

    if (ActiveTerminal == terminal)
        ActiveTerminal = Sessions.Count > 0 ? Sessions[^1] : null;

    RebuildSlots();
}

public void StopAll()
{
    foreach (var terminal in Sessions.ToArray())
        terminal.Dispose();

    Sessions.Clear();
    VisibleSlots.Clear();
    ActiveTerminal = null;
    OnPropertyChanged(nameof(GridDimension));
}

public void Dispose() => StopAll();
```

- [ ] **Step 6: Compile the pure state layer**

Run:

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Expected: build succeeds; no Avalonia terminal control is referenced by the two new view models.

- [ ] **Step 7: Commit**

```bash
git add src/ViewModels/DevSpaceTerminal.cs src/ViewModels/DevSpaces.cs
git commit -m "feat: add DevSpaces session and layout state"
```

---

### Task 4: Build the Persistent Embedded Terminal Adapter

**Files:**
- Create: `src/Views/DevSpaceTerminal.axaml`
- Create: `src/Views/DevSpaceTerminal.axaml.cs`

**Interfaces:**
- Consumes: `ViewModels.DevSpaceTerminal`
- Consumes: `DevSpaceLaunchSpec` from `IDevSpaceSessionLauncher`
- Produces: `Views.DevSpaceTerminal.Start(IDevSpaceSessionLauncher launcher)`
- Produces: `Views.DevSpaceTerminal.Stop()`
- Requirement: one view/control instance per session for the lifetime of that session

- [ ] **Step 1: Create the terminal pane XAML**

Create `src/Views/DevSpaceTerminal.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:terminal="using:Iciclecreek.Terminal"
             xmlns:vm="using:SourceGit.ViewModels"
             x:Class="SourceGit.Views.DevSpaceTerminal"
             x:DataType="vm:DevSpaceTerminal">
  <Grid>
    <terminal:TerminalControl x:Name="Terminal"
                              FontFamily="{DynamicResource Fonts.Monospace}"/>

    <Border Background="{DynamicResource Brush.Window}"
            IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}">
      <TextBlock Margin="16"
                 Text="{Binding ErrorMessage}"
                 TextWrapping="Wrap"
                 Foreground="{DynamicResource Brush.FG2}"/>
    </Border>
  </Grid>
</UserControl>
```

If the package's actual XAML namespace resolves to a different CLR namespace, use the namespace exported by 1.0.11 while keeping the control type `TerminalControl`; do not change package version to make the sample compile.

- [ ] **Step 2: Implement one-time launch and explicit stop**

Create `src/Views/DevSpaceTerminal.axaml.cs` with a `_started` guard. The view must never kill the PTY merely because it is detached/reparented in the visual tree.

Use this structure:

```csharp
using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class DevSpaceTerminal : UserControl, IDisposable
    {
        public DevSpaceTerminal()
        {
            InitializeComponent();
        }

        public void Start(SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher)
        {
            if (_started || DataContext is not ViewModels.DevSpaceTerminal session)
                return;

            _started = true;
            session.StopRequested += OnStopRequested;

            try
            {
                var spec = launcher.Create(session.Command, session.WorkingDirectory);
                Terminal.ProcessExited += OnProcessExited;
                Terminal.LaunchProcess(spec.WorkingDirectory, spec.Process, spec.Arguments);
                session.MarkRunning();
            }
            catch (Exception ex)
            {
                session.MarkFailed(App.Text("DevSpaces.StartFailed", ex.Message));
            }
        }

        public void Stop()
        {
            if (_stopped)
                return;

            _stopped = true;
            if (DataContext is ViewModels.DevSpaceTerminal session)
                session.StopRequested -= OnStopRequested;

            Terminal.ProcessExited -= OnProcessExited;
            try
            {
                Terminal.Kill();
            }
            catch
            {
                // Process may already have exited.
            }
        }

        public void Dispose() => Stop();

        private void OnStopRequested(ViewModels.DevSpaceTerminal _)
        {
            Stop();
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is ViewModels.DevSpaceTerminal session)
                    session.MarkExited(Terminal.Process?.ExitCode ?? 0);
            });
        }

        private bool _started;
        private bool _stopped;
    }
}
```

Compile against the exact 1.0.11 API. If `ProcessExited` supplies a different event-args type, adapt only the handler signature; keep the lifecycle semantics above unchanged.

- [ ] **Step 3: Verify detach/reparent does not call `Stop()`**

Search the new code for visual-tree lifecycle hooks:

```bash
git grep -n "DetachedFromVisualTree\|OnUnloaded\|Unloaded" -- src/Views/DevSpaceTerminal*
```

Expected: no handler kills the terminal because of visual detach/unload. PTY termination occurs only through `StopRequested`, explicit `Stop()`, or whole-page disposal.

- [ ] **Step 4: Build**

Run:

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Expected: `TerminalControl`, `LaunchProcess`, `Kill`, and exit-event usage compile against 1.0.11.

- [ ] **Step 5: Commit**

```bash
git add src/Views/DevSpaceTerminal.axaml src/Views/DevSpaceTerminal.axaml.cs
git commit -m "feat: embed DevSpaces PTY terminal"
```

---

### Task 5: Build the DevSpaces Tabs and Multi-Terminal Grid Page

**Files:**
- Create: `src/Views/DevSpaces.axaml`
- Create: `src/Views/DevSpaces.axaml.cs`

**Interfaces:**
- Consumes: `ViewModels.DevSpaces.Sessions`, `VisibleSlots`, `LayoutIndex`, `GridDimension`
- Consumes: `Views.DevSpaceTerminal.Start(...)` and `Dispose()`
- Produces: one persistent `Views.DevSpaceTerminal` instance per session id
- Produces: empty-cell buttons that call `CreateTerminalAt(slot.Index)`

- [ ] **Step 1: Create the toolbar and grid host**

Create `src/Views/DevSpaces.axaml` with two rows. Keep the terminal grid itself in code-behind so controls can be reparented rather than recreated by a data template:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:SourceGit.ViewModels"
             x:Class="SourceGit.Views.DevSpaces"
             x:DataType="vm:DevSpaces">
  <Grid RowDefinitions="36,*">
    <Grid Grid.Row="0" ColumnDefinitions="*,Auto,Auto"
          BorderThickness="0,0,0,1"
          BorderBrush="{DynamicResource Brush.Border1}">
      <ListBox Grid.Column="0"
               Margin="4,0"
               Background="Transparent"
               ItemsSource="{Binding Sessions}"
               SelectedItem="{Binding ActiveTerminal, Mode=OneWay}">
        <ListBox.ItemsPanel>
          <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal"/>
          </ItemsPanelTemplate>
        </ListBox.ItemsPanel>
        <ListBox.ItemTemplate>
          <DataTemplate x:DataType="vm:DevSpaceTerminal">
            <Button Classes="icon_button"
                    Padding="8,0"
                    Click="OnSessionTabClicked">
              <TextBlock Text="{Binding Title}"/>
            </Button>
          </DataTemplate>
        </ListBox.ItemTemplate>
      </ListBox>

      <ComboBox Grid.Column="1"
                Width="88"
                Margin="4"
                SelectedIndex="{Binding LayoutIndex, Mode=TwoWay}">
        <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.Auto}"/>
        <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.1x1}"/>
        <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.2x2}"/>
        <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.3x3}"/>
        <ComboBoxItem Content="{DynamicResource Text.DevSpaces.Layout.4x4}"/>
      </ComboBox>

      <Button Grid.Column="2"
              Classes="icon_button"
              Width="32" Height="32"
              Margin="0,2,4,2"
              Click="OnCreateTerminal"
              ToolTip.Tip="{DynamicResource Text.DevSpaces.NewTerminal}">
        <Path Width="14" Height="14" Data="{StaticResource Icons.Plus}"/>
      </Button>
    </Grid>

    <UniformGrid Grid.Row="1"
                 x:Name="TerminalGrid"
                 Margin="4"/>
  </Grid>
</UserControl>
```

If SourceGit's icon resource for plus is named differently, use the existing plus/add geometry already used elsewhere; do not add a duplicate icon solely for DevSpaces.

- [ ] **Step 2: Keep one terminal view per session**

In `src/Views/DevSpaces.axaml.cs`, maintain:

```csharp
private readonly Dictionary<Guid, DevSpaceTerminal> _terminalViews = [];
private readonly SourceGit.DevSpaces.IDevSpaceSessionLauncher _launcher =
    new SourceGit.DevSpaces.LocalDevSpaceSessionLauncher();
```

`GetOrCreateTerminalView(session)` must:

1. return the cached view if it exists;
2. otherwise create `new DevSpaceTerminal { DataContext = session }`;
3. cache it by `session.Id`;
4. call `view.Start(_launcher)` exactly once.

- [ ] **Step 3: Rebuild only presentation when slots/layout change**

Implement `RebuildGrid()`:

```csharp
private void RebuildGrid()
{
    if (DataContext is not ViewModels.DevSpaces vm)
        return;

    TerminalGrid.Rows = vm.GridDimension;
    TerminalGrid.Columns = vm.GridDimension;
    TerminalGrid.Children.Clear();

    foreach (var slot in vm.VisibleSlots)
    {
        if (slot.Terminal != null)
        {
            TerminalGrid.Children.Add(CreatePane(slot.Terminal));
        }
        else
        {
            TerminalGrid.Children.Add(CreateEmptySlot(slot.Index));
        }
    }
}
```

`CreatePane` must place a compact header above the cached terminal view. The header must contain the terminal title and a close button. The terminal view itself is obtained only from `_terminalViews`, not constructed each rebuild.

`CreateEmptySlot(index)` must return a button with `Text.DevSpaces.NewTerminal` and an event handler that calls `vm.CreateTerminalAt(index)`.

- [ ] **Step 4: Wire active-session and close interactions**

`OnSessionTabClicked` activates the button's `DevSpaceTerminal` DataContext and rebuilds the grid.

Pane pointer/click activation calls `vm.ActivateTerminal(session)`.

Close calls:

```csharp
vm.CloseTerminal(session);
if (_terminalViews.Remove(session.Id, out var view))
    view.Dispose();
RebuildGrid();
```

The close action must dispose only the selected session's cached control.

- [ ] **Step 5: Observe view-model changes without recreating sessions**

On `DataContextChanged`, detach from the old `DevSpaces.PropertyChanged`, attach to the new one, and call `RebuildGrid()`.

On view-model property changes for `VisibleSlots`, `GridDimension`, `ActiveTerminal`, or `Layout`, call `RebuildGrid()` on the UI thread.

When the DevSpaces view itself is disposed because the repository is closing, dispose every cached terminal view and clear the dictionary. Do not dispose cached views on ordinary layout changes or when the repository right page becomes hidden.

- [ ] **Step 6: Build**

Run:

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Expected: compiled bindings and `UniformGrid` population code compile.

- [ ] **Step 7: Manual grid smoke check using a shell command**

Before depending on Copilot authentication, temporarily set Preferences → DevSpaces → Default command to a long-running interactive shell command appropriate to the OS, open four sessions, and switch Auto → 1x1 → 2x2 → 3x3 → 4x4.

Expected: terminal output/history remains intact and process identities remain alive while the controls move between cells; fixed layouts show `+ New Terminal` in empty cells.

Restore the preference to `copilot` after the check.

- [ ] **Step 8: Commit**

```bash
git add src/Views/DevSpaces.axaml src/Views/DevSpaces.axaml.cs
git commit -m "feat: add DevSpaces terminal grid"
```

---

### Task 6: Integrate DevSpaces into Repository Navigation and Lifecycle

**Files:**
- Modify: `src/ViewModels/Repository.cs`
- Modify: `src/Views/Repository.axaml`

**Interfaces:**
- Produces: `Repository.DevSpaces : ViewModels.DevSpaces`
- Produces: `Repository.IsDevSpacesEnabled : bool`
- Produces: `Repository.IsDevSpacesVisible : bool`
- Consumes: `Preferences.Instance.PropertyChanged`
- Consumes: `DevSpaces.EnsureFirstSession()` and `Dispose()`

- [ ] **Step 1: Add repository DevSpaces state**

Add fields:

```csharp
private DevSpaces _devSpaces;
```

Add properties:

```csharp
public DevSpaces DevSpaces => _devSpaces;

public bool IsDevSpacesEnabled => Preferences.Instance.EnableDevSpaces;

public bool IsDevSpacesVisible =>
    Preferences.Instance.EnableDevSpaces && SelectedViewIndex == 3;
```

- [ ] **Step 2: Extend `SelectedViewIndex`**

Update its setter so index 3 is rejected when the feature is disabled, and selecting index 3 lazily creates the page model and first terminal:

```csharp
public int SelectedViewIndex
{
    get => _selectedViewIndex;
    set
    {
        var next = value == 3 && !Preferences.Instance.EnableDevSpaces ? 0 : value;
        if (SetProperty(ref _selectedViewIndex, next))
        {
            if (next == 3)
            {
                _devSpaces ??= new DevSpaces(FullPath);
                _devSpaces.EnsureFirstSession();
                OnPropertyChanged(nameof(DevSpaces));
            }

            OnPropertyChanged(nameof(IsHistoriesVisible));
            OnPropertyChanged(nameof(IsWorkingCopyVisible));
            OnPropertyChanged(nameof(IsStashesVisible));
            OnPropertyChanged(nameof(IsDevSpacesVisible));
        }
    }
}
```

- [ ] **Step 3: React immediately when the preference is disabled**

At the end of `Open()`, subscribe:

```csharp
Preferences.Instance.PropertyChanged += OnPreferencesPropertyChanged;
```

Add:

```csharp
private void OnPreferencesPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName != nameof(Preferences.EnableDevSpaces))
        return;

    OnPropertyChanged(nameof(IsDevSpacesEnabled));
    OnPropertyChanged(nameof(IsDevSpacesVisible));

    if (!Preferences.Instance.EnableDevSpaces)
    {
        if (_selectedViewIndex == 3)
            SelectedViewIndex = 0;

        _devSpaces?.Dispose();
        _devSpaces = null;
        OnPropertyChanged(nameof(DevSpaces));
    }
}
```

Add `using System.ComponentModel;` if required.

- [ ] **Step 4: Clean up repository-owned terminals in `Close()`**

Before watcher/timer disposal completes, add:

```csharp
Preferences.Instance.PropertyChanged -= OnPreferencesPropertyChanged;
_devSpaces?.Dispose();
_devSpaces = null;
```

This makes the outer repository/worktree tab the definitive lifetime boundary.

- [ ] **Step 5: Add DevSpaces directly under Stashes in the left main-view list**

In `src/Views/Repository.axaml`, add a fourth `ListBoxItem` immediately after Stashes:

```xml
<ListBoxItem IsVisible="{Binding IsDevSpacesEnabled, Mode=OneWay}">
  <Grid ColumnDefinitions="4,Auto,*">
    <Rectangle Grid.Column="0" Classes="indicator" Width="4" Height="20" VerticalAlignment="Center"/>
    <Path Grid.Column="1" Classes="icon" Data="{StaticResource Icons.Terminal}"/>
    <TextBlock Grid.Column="2" Classes="header" Text="{DynamicResource Text.DevSpaces}"/>
  </Grid>
</ListBoxItem>
```

Do not place DevSpaces in the branch/tag/worktree collapsible group.

- [ ] **Step 6: Add the DevSpaces right-page host**

After the Stashes right-page border add:

```xml
<Border IsVisible="{Binding IsDevSpacesVisible, Mode=OneWay}">
  <v:DevSpaces DataContext="{Binding DevSpaces, Mode=OneWay}"/>
</Border>
```

Do not call `OnRightPagePropertyChanged` unless the DevSpaces page needs the existing diff-hotkey behavior; it does not contain a `DiffView`.

- [ ] **Step 7: Build**

Run:

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Expected: repository compiled bindings resolve `DevSpaces`, `IsDevSpacesEnabled`, and `IsDevSpacesVisible`.

- [ ] **Step 8: Manually verify feature-toggle behavior**

Check all of these in one run:

1. Start with `Enable DevSpaces = false`: DevSpaces is absent under Stashes.
2. Enable it in Preferences: DevSpaces appears without restarting SourceGit.
3. Select it: first terminal is created automatically.
4. Disable it while selected: view returns to Histories, all DevSpaces PTYs stop, and the sidebar item disappears.
5. Re-enable and reopen: a fresh `Copilot 1` session is created.

- [ ] **Step 9: Commit**

```bash
git add src/ViewModels/Repository.cs src/Views/Repository.axaml
git commit -m "feat: integrate DevSpaces repository page"
```

---

### Task 7: Add Complete DevSpaces Localization Fallbacks

**Files:**
- Modify: `src/Resources/Locales/en_US.axaml`
- Modify only locale files that do not merge `avares://SourceGit/Resources/Locales/en_US.axaml`

**Interfaces:**
- Produces resource keys consumed by Preferences, Repository, DevSpaces, and terminal error/status views.

- [ ] **Step 1: Add canonical English keys**

Add these exact entries to `src/Resources/Locales/en_US.axaml`:

```xml
<x:String x:Key="Text.DevSpaces" xml:space="preserve">DevSpaces</x:String>
<x:String x:Key="Text.DevSpaces.Enable" xml:space="preserve">Enable DevSpaces</x:String>
<x:String x:Key="Text.DevSpaces.DefaultCommand" xml:space="preserve">Default command:</x:String>
<x:String x:Key="Text.DevSpaces.DefaultLayout" xml:space="preserve">Default layout:</x:String>
<x:String x:Key="Text.DevSpaces.Layout.Auto" xml:space="preserve">Auto</x:String>
<x:String x:Key="Text.DevSpaces.Layout.1x1" xml:space="preserve">1×1</x:String>
<x:String x:Key="Text.DevSpaces.Layout.2x2" xml:space="preserve">2×2</x:String>
<x:String x:Key="Text.DevSpaces.Layout.3x3" xml:space="preserve">3×3</x:String>
<x:String x:Key="Text.DevSpaces.Layout.4x4" xml:space="preserve">4×4</x:String>
<x:String x:Key="Text.DevSpaces.NewTerminal" xml:space="preserve">New terminal</x:String>
<x:String x:Key="Text.DevSpaces.CloseTerminal" xml:space="preserve">Close terminal</x:String>
<x:String x:Key="Text.DevSpaces.StartFailed" xml:space="preserve">Failed to start terminal: {0}</x:String>
<x:String x:Key="Text.DevSpaces.Exited" xml:space="preserve">Process exited with code {0}</x:String>
```

- [ ] **Step 2: Determine which locales already inherit English**

Run:

```bash
for f in src/Resources/Locales/*.axaml; do
  if [ "$(basename "$f")" != "en_US.axaml" ]; then
    grep -q 'Locales/en_US.axaml' "$f" || echo "$f"
  fi
done
```

On PowerShell use:

```powershell
Get-ChildItem src/Resources/Locales/*.axaml |
  Where-Object Name -ne 'en_US.axaml' |
  Where-Object { -not (Select-String -Quiet -Path $_.FullName -Pattern 'Locales/en_US.axaml') } |
  Select-Object -ExpandProperty FullName
```

The output is the exact set of locale files that need fallback entries.

- [ ] **Step 3: Add English fallback keys only to non-inheriting locale files**

For every file printed by Step 2, add the same `Text.DevSpaces*` keys with the English values from Step 1. Do not overwrite existing translations and do not duplicate keys in locale files that already inherit `en_US.axaml`.

- [ ] **Step 4: Verify every configured locale can resolve `Text.DevSpaces`**

Run a Debug build:

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Then start the app and switch through at least `en_US` plus one inheriting locale and one non-inheriting locale found in Step 2.

Expected: no `Text.DevSpaces...` resource-key placeholders appear in the UI.

- [ ] **Step 5: Commit**

```bash
git add src/Resources/Locales
git commit -m "feat: localize DevSpaces UI"
```

---

### Task 8: Functional and Cross-Runtime Verification

**Files:**
- No production files expected unless verification exposes a defect.
- If a defect is found, fix it in the owning task's file and rerun the affected verification before this task can complete.

**Interfaces:**
- Validates the complete spec and Release/AOT compatibility.

- [ ] **Step 1: Start from a clean dependency state**

Run:

```bash
git submodule sync --recursive
git submodule update --init --recursive
dotnet restore SourceGit.slnx
git status --short
```

Expected: restore succeeds; submodules are populated; no unintended generated files are staged.

- [ ] **Step 2: Run Debug and Release builds**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
dotnet build src/SourceGit.csproj -c Release --no-restore
```

Expected: both builds succeed with zero errors.

- [ ] **Step 3: Publish every runtime used by the existing build workflow**

Run each command separately so a failing RID is obvious:

```bash
dotnet publish src/SourceGit.csproj -c Release -r win-x64 -o .artifacts/devspaces/win-x64
dotnet publish src/SourceGit.csproj -c Release -r win-arm64 -o .artifacts/devspaces/win-arm64
dotnet publish src/SourceGit.csproj -c Release -r osx-x64 -o .artifacts/devspaces/osx-x64
dotnet publish src/SourceGit.csproj -c Release -r osx-arm64 -o .artifacts/devspaces/osx-arm64
dotnet publish src/SourceGit.csproj -c Release -r linux-x64 -o .artifacts/devspaces/linux-x64
dotnet publish src/SourceGit.csproj -c Release -r linux-arm64 -o .artifacts/devspaces/linux-arm64
```

Expected: every publish succeeds, including NativeAOT/trimming. If the host cannot cross-publish a platform/RID, record that RID explicitly as unverified and rely on a capable runner before merge; do not label the feature fully verified while any required RID remains unknown.

- [ ] **Step 4: Verify worktree path isolation**

Create/open a Git worktree in SourceGit, enable DevSpaces, select DevSpaces, and in the first terminal print the current directory:

Windows PowerShell:

```powershell
(Get-Location).Path
```

Unix shell:

```bash
pwd
```

Expected: the printed directory is exactly that worktree's `Repository.FullPath`, not the main repository path.

- [ ] **Step 5: Verify Copilot auto-start and independent sessions**

Set default command back to `copilot`, open DevSpaces, and create at least four sessions.

Expected:

- first visit automatically starts `Copilot 1`;
- `+` creates `Copilot 2`, `Copilot 3`, `Copilot 4`;
- each session is an independent interactive Copilot CLI process;
- input/output in one session does not appear in another.

- [ ] **Step 6: Verify all layout transitions preserve PTYs**

With four running sessions, enter recognizable text/history in each. Switch through:

```text
Auto -> 1x1 -> 2x2 -> 3x3 -> 4x4 -> Auto
```

Expected: no terminal restarts, each session retains its own screen/history, and empty cells in 3x3/4x4 show `+ New Terminal`.

- [ ] **Step 7: Verify 9, 16, and >16 behavior**

Use a lightweight long-running shell command if opening 17 Copilot sessions is undesirable during the verification pass.

Expected:

- 9 sessions in Auto resolves to 3x3;
- 10 sessions in Auto resolves to 4x4;
- 16 sessions fit in 4x4;
- the 17th session remains accessible from the session tabs;
- selecting session 17 swaps it into the visible grid without killing the displaced session.

- [ ] **Step 8: Verify targeted close semantics**

Close one visible terminal while at least three others are running.

Expected: only the chosen PTY exits; all remaining sessions continue accepting input.

Close the outer repository/worktree tab.

Expected: every DevSpaces PTY owned by that repository exits.

- [ ] **Step 9: Verify command-start failure is contained**

Set Default command to a deliberately nonexistent command such as:

```text
sourcegit-devspaces-command-does-not-exist
```

Open a new DevSpaces terminal.

Expected: the terminal pane shows `Text.DevSpaces.StartFailed` with the launch error; SourceGit remains responsive and other terminal sessions remain alive.

Restore Default command to `copilot` after the check.

- [ ] **Step 10: Verify feature-toggle cleanup**

While DevSpaces contains running sessions, disable `Enable DevSpaces` in Preferences.

Expected: repository switches to Histories if necessary, all its DevSpaces processes terminate, and the DevSpaces item disappears immediately.

- [ ] **Step 11: Inspect final diff for scope**

Run:

```bash
git status --short
git diff master...HEAD --stat
git diff master...HEAD -- . ':(exclude)docs/superpowers/specs/2026-08-28-devspaces-design.md' ':(exclude)docs/superpowers/plans/2026-08-28-devspaces.md'
```

Expected: changes are limited to the approved DevSpaces feature, its dependency, Preferences, Repository integration, and localization. No Docker/Podman/WSLC/container implementation and no unrelated CI workflow changes are present.

- [ ] **Step 12: Commit any verification-driven fixes**

If verification required code changes, commit each coherent fix with a descriptive message, rerun its failed verification, then rerun Steps 2 and 3 before proceeding.

If no fixes were needed, no empty verification commit is required.

---

### Task 9: Final Review and Pull Request

**Files:**
- No new production files expected.

**Interfaces:**
- Produces a reviewable PR from `feat/devspaces` to `master` only after the verification gates above are satisfied or any environment-limited RIDs are explicitly disclosed.

- [ ] **Step 1: Verify branch state**

Run:

```bash
git status --short
git log --oneline --decorate master..HEAD
```

Expected: clean working tree and only DevSpaces/spec/plan commits on the feature branch.

- [ ] **Step 2: Re-run the final build gate**

At minimum:

```bash
dotnet build src/SourceGit.csproj -c Release --no-restore
```

Also rerun any publish command that was previously fixed during Task 8.

Expected: all locally executable gates are green.

- [ ] **Step 3: Open the PR**

Use:

```text
Title: feat: add DevSpaces terminal workspace

Body summary:
- add opt-in DevSpaces repository page directly under Stashes
- launch embedded Copilot CLI terminals in each repository/worktree path
- support Auto/1x1/2x2/3x3/4x4 layouts with up to 16 visible terminals
- preserve PTY sessions while changing layouts
- terminate sessions with their owning repository tab
- keep container-backed DevSpaces out of this milestone behind a launcher boundary

Verification:
- Debug build: <record actual result>
- Release build: <record actual result>
- RID publish matrix: <record actual results per RID>
- manual DevSpaces/worktree/grid/lifecycle checks: <record actual result>

Known repository CI limitation:
- existing PR CI workflows target `develop` while this fork uses `master`; this feature does not change those triggers
```

Replace the verification-result markers with actual observed results before creating the PR; never claim a check succeeded without evidence.

- [ ] **Step 4: Review PR diff and status**

Confirm the PR targets `master`, the feature branch is `feat/devspaces`, the spec and plan are included, and GitHub reports no merge conflict. If repository CI still does not trigger because of the pre-existing branch mismatch, state that explicitly rather than interpreting the absence of checks as success.
