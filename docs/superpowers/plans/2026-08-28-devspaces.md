# DevSpaces Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in DevSpaces repository page that launches multiple embedded GitHub Copilot CLI terminals in the current repository/worktree and displays them in Auto, 1x1, 2x2, 3x3, or 4x4 layouts without restarting running PTY sessions.

**Architecture:** Each `Repository` lazily owns one `ViewModels.DevSpaces` session manager. Session state is independent from presentation: the view model owns terminal metadata and visible grid-slot selection, while `Views.DevSpaces` keeps one persistent pane wrapper per session and reparents those wrappers when the layout changes. Terminal startup is resolved through `IDevSpaceSessionLauncher`, with a local shell implementation in this milestone and a clean seam for a later container launcher.

**Tech Stack:** .NET 10, Avalonia 11.3.20, CommunityToolkit.Mvvm 8.4.2, `Iciclecreek.Avalonia.Terminal` 1.0.11, Porta.Pty transitively, SourceGit JSON source generation, XAML localization resources.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-design.md`

## Global Constraints

- `EnableDevSpaces` defaults to `false`; when false, existing SourceGit repository behavior remains unchanged.
- `DevSpacesDefaultCommand` defaults to `"copilot"`.
- `DevSpacesDefaultLayout` defaults to `Models.DevSpaceLayout.Auto`.
- DevSpaces is repository view index `3`, directly after Stashes.
- Every terminal starts in the owning `Repository.FullPath`.
- First DevSpaces selection automatically creates one terminal.
- Supported layouts: Auto, 1x1, 2x2, 3x3, 4x4; at most 16 terminals are visible simultaneously.
- Changing layout must not restart, recreate, or kill a PTY.
- Closing one terminal must not affect any other terminal.
- Closing the repository/worktree tab terminates all DevSpaces sessions owned by that repository.
- Pin `Iciclecreek.Avalonia.Terminal` to exactly `1.0.11`; do not upgrade SourceGit to Avalonia 12.
- Do not implement Docker, Podman, WSLC, images, mounts, or container lifecycle in this milestone.
- Do not introduce a new test framework solely for this feature.
- Do not change the fork's existing `develop`/`master` CI trigger mismatch in this feature.

---

## File Structure

**Create**
- `src/Models/DevSpaceLayout.cs` — layout enum and dimension/capacity rules.
- `src/DevSpaces/IDevSpaceSessionLauncher.cs` — launch contract and immutable launch spec.
- `src/DevSpaces/LocalDevSpaceSessionLauncher.cs` — platform shell command resolution.
- `src/ViewModels/DevSpaceTerminal.cs` — one terminal session's non-visual state/lifecycle.
- `src/ViewModels/DevSpaces.cs` — per-repository sessions, active session, layout, visible slots.
- `src/Views/DevSpaceTerminal.axaml` / `.axaml.cs` — one persistent PTY terminal control.
- `src/Views/DevSpaces.axaml` / `.axaml.cs` — toolbar, session tabs, layout selector, persistent pane grid.

**Modify**
- `src/SourceGit.csproj` — terminal package.
- `src/ViewModels/Preferences.cs` — feature toggle, default command, default layout.
- `src/Views/Preferences.axaml` — first-class DevSpaces Preferences tab.
- `src/ViewModels/Repository.cs` — fourth page and lifecycle ownership.
- `src/Views/Repository.axaml` — sidebar item and main-content host.
- `src/Resources/Locales/en_US.axaml` and non-inheriting active locales — DevSpaces resources.

---

### Task 1: Lock the PTY Dependency and Launch Boundary

**Files:**
- Modify: `src/SourceGit.csproj`
- Create: `src/DevSpaces/IDevSpaceSessionLauncher.cs`
- Create: `src/DevSpaces/LocalDevSpaceSessionLauncher.cs`

**Produces:**
```csharp
DevSpaceLaunchSpec(string Process, string[] Arguments, string WorkingDirectory)
IDevSpaceSessionLauncher.Create(string command, string workingDirectory)
LocalDevSpaceSessionLauncher
```

- [ ] **Step 1: Pin the compatible terminal package**

Add beside the existing UI package references:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.11" />
```

Do not change any Avalonia version.

- [ ] **Step 2: Restore and inspect resolved packages**

```bash
git submodule update --init --recursive
dotnet restore SourceGit.slnx
dotnet list src/SourceGit.csproj package --include-transitive
```

Expected: restore succeeds; terminal package is 1.0.11; Avalonia stays on 11.3.x. If the package forces Avalonia 12 or a package conflict, stop rather than upgrading Avalonia.

- [ ] **Step 3: Create the launch contract**

`src/DevSpaces/IDevSpaceSessionLauncher.cs`:

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

- [ ] **Step 4: Implement the local launcher**

`src/DevSpaces/LocalDevSpaceSessionLauncher.cs`:

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

            return new DevSpaceLaunchSpec(shell, ["-lc", command], workingDirectory);
        }
    }
}
```

Windows uses PowerShell so npm/WinGet command shims such as `copilot` resolve through a normal shell.

- [ ] **Step 5: Compile**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SourceGit.csproj src/DevSpaces
git commit -m "feat: add DevSpaces terminal launch boundary"
```

---

### Task 2: Add Layout Model and Persisted Preferences

**Files:**
- Create: `src/Models/DevSpaceLayout.cs`
- Modify: `src/ViewModels/Preferences.cs`
- Modify: `src/Views/Preferences.axaml`

**Produces:** `DevSpaceLayout`, `EnableDevSpaces`, `DevSpacesDefaultCommand`, `DevSpacesDefaultLayout`, `DevSpacesDefaultLayoutIndex`.

- [ ] **Step 1: Create layout rules**

`src/Models/DevSpaceLayout.cs`:

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
            var d = layout.GetDimension(sessionCount);
            return d * d;
        }
    }
}
```

- [ ] **Step 2: Add persisted Preferences properties**

Add to `ViewModels.Preferences`:

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

Backing fields:

```csharp
private bool _enableDevSpaces = false;
private string _devSpacesDefaultCommand = "copilot";
private Models.DevSpaceLayout _devSpacesDefaultLayout = Models.DevSpaceLayout.Auto;
```

- [ ] **Step 3: Add Preferences → DevSpaces**

Add a top-level `TabItem` to the existing Preferences `TabControl`:

```xml
<TabItem>
  <TabItem.Header>
    <TextBlock Classes="tab_header" Text="{DynamicResource Text.DevSpaces}"/>
  </TabItem.Header>
  <Grid Margin="8" RowDefinitions="32,32,32" ColumnDefinitions="Auto,*">
    <CheckBox Grid.Row="0" Grid.Column="1"
              Content="{DynamicResource Text.DevSpaces.Enable}"
              IsChecked="{Binding EnableDevSpaces, Mode=TwoWay}"/>
    <TextBlock Grid.Row="1" Grid.Column="0" Margin="0,0,16,0"
               HorizontalAlignment="Right"
               Text="{DynamicResource Text.DevSpaces.DefaultCommand}"/>
    <TextBox Grid.Row="1" Grid.Column="1" Height="28"
             Text="{Binding DevSpacesDefaultCommand, Mode=TwoWay}"/>
    <TextBlock Grid.Row="2" Grid.Column="0" Margin="0,0,16,0"
               HorizontalAlignment="Right"
               Text="{DynamicResource Text.DevSpaces.DefaultLayout}"/>
    <ComboBox Grid.Row="2" Grid.Column="1" Height="28"
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

No container configuration is added now.

- [ ] **Step 4: Compile and verify persistence**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Run SourceGit, verify defaults OFF / `copilot` / Auto, change the three settings, close Preferences, restart, and confirm they reload. Expected: only `DevSpacesDefaultLayout` is serialized, not the index helper.

- [ ] **Step 5: Commit**

```bash
git add src/Models/DevSpaceLayout.cs src/ViewModels/Preferences.cs src/Views/Preferences.axaml
git commit -m "feat: add DevSpaces preferences"
```

---

### Task 3: Implement Session and Grid-State View Models

**Files:**
- Create: `src/ViewModels/DevSpaceTerminal.cs`
- Create: `src/ViewModels/DevSpaces.cs`

**Produces:** terminal state/lifecycle, session collection, active session, deterministic titles, grid slots, layout selection, launcher ownership.

- [ ] **Step 1: Create terminal state**

`src/ViewModels/DevSpaceTerminal.cs`:

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
        public DevSpaceTerminalState State { get => _state; private set => SetProperty(ref _state, value); }
        public int ExitCode { get => _exitCode; private set => SetProperty(ref _exitCode, value); }
        public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
        public event Action<DevSpaceTerminal> StopRequested;

        public DevSpaceTerminal(string title, string command, string workingDirectory)
        {
            Title = title;
            Command = command;
            WorkingDirectory = workingDirectory;
        }

        public void MarkRunning() => State = DevSpaceTerminalState.Running;
        public void MarkExited(int exitCode) { ExitCode = exitCode; State = DevSpaceTerminalState.Exited; }
        public void MarkFailed(string message) { ErrorMessage = message; State = DevSpaceTerminalState.Failed; }

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

No `TerminalControl` or visual object belongs here.

- [ ] **Step 2: Create `DevSpaces` with an injectable launcher**

The constructor and launcher boundary are:

```csharp
public DevSpaces(
    string workingDirectory,
    SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher = null)
{
    _workingDirectory = workingDirectory;
    Launcher = launcher ?? new SourceGit.DevSpaces.LocalDevSpaceSessionLauncher();
    _layout = Preferences.Instance.DevSpacesDefaultLayout;
    RebuildSlots();
}

public SourceGit.DevSpaces.IDevSpaceSessionLauncher Launcher { get; }
public AvaloniaList<DevSpaceTerminal> Sessions { get; } = [];
public AvaloniaList<DevSpaceGridSlot> VisibleSlots { get; } = [];
```

Add:

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

- [ ] **Step 3: Add active/layout properties**

```csharp
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

public int GridDimension => _layout.GetDimension(Sessions.Count);
```

- [ ] **Step 4: Implement deterministic creation**

Use `_nextSessionNumber = 1`; never derive titles from `Sessions.Count`.

```csharp
public void EnsureFirstSession()
{
    if (Sessions.Count == 0)
        CreateTerminal();
}

public DevSpaceTerminal CreateTerminal() => CreateTerminalAt(-1);

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
    _preferredSlot = preferredSlot;
    RebuildSlots();
    return terminal;
}
```

- [ ] **Step 5: Implement approved visibility rules**

`RebuildSlots()` must always create `GridDimension * GridDimension` slot objects. Fill from the first sessions in creation order. If `ActiveTerminal` is outside capacity, replace the final visible entry with it. For 1x1, always show `ActiveTerminal`. If `_preferredSlot` is a valid empty slot, place the newly-created active terminal there. Never dispose a session from this method.

Use `List<DevSpaceTerminal>` plus `Contains`/`IndexOf`; after rebuilding call:

```csharp
OnPropertyChanged(nameof(GridDimension));
OnPropertyChanged(nameof(VisibleSlots));
```

Exact Auto expectations:

```text
0-1 session  -> dimension 1
2-4 sessions -> dimension 2
5-9 sessions -> dimension 3
10+ sessions -> dimension 4
```

For 17+ sessions, capacity remains 16; activating a hidden session swaps it into the final visible slot without terminating the displaced session.

- [ ] **Step 6: Implement activation/close/all-stop**

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
    OnPropertyChanged(nameof(VisibleSlots));
}

public void Dispose() => StopAll();
```

- [ ] **Step 7: Build and commit**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
git add src/ViewModels/DevSpaceTerminal.cs src/ViewModels/DevSpaces.cs
git commit -m "feat: add DevSpaces session and layout state"
```

Expected build: PASS.

---

### Task 4: Build the Persistent PTY Terminal Adapter

**Files:**
- Create: `src/Views/DevSpaceTerminal.axaml`
- Create: `src/Views/DevSpaceTerminal.axaml.cs`

**Consumes:** `DevSpaceTerminal`, `IDevSpaceSessionLauncher`, `TerminalControl` 1.0.11.

- [ ] **Step 1: Create terminal XAML**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:terminal="using:Iciclecreek.Terminal"
             xmlns:vm="using:SourceGit.ViewModels"
             xmlns:c="using:SourceGit.Converters"
             x:Class="SourceGit.Views.DevSpaceTerminal"
             x:DataType="vm:DevSpaceTerminal">
  <Grid>
    <terminal:TerminalControl x:Name="Terminal"
                              FontFamily="{DynamicResource Fonts.Monospace}"
                              BufferSize="3000"/>
    <Border Background="{DynamicResource Brush.Window}"
            IsVisible="{Binding ErrorMessage, Converter={x:Static c:StringConverters.IsNotNullOrEmpty}}">
      <TextBlock Margin="16" Text="{Binding ErrorMessage}" TextWrapping="Wrap"/>
    </Border>
  </Grid>
</UserControl>
```

- [ ] **Step 2: Implement start/stop with the verified package event API**

`src/Views/DevSpaceTerminal.axaml.cs`:

```csharp
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Iciclecreek.Terminal;

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

            try { Terminal.Kill(); }
            catch { /* The PTY may already have exited. */ }
        }

        public void Dispose() => Stop();

        private void OnStopRequested(ViewModels.DevSpaceTerminal _) => Stop();

        private void OnProcessExited(object sender, ProcessExitedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is ViewModels.DevSpaceTerminal session)
                    session.MarkExited(e.ExitCode);
            });
        }

        private bool _started;
        private bool _stopped;
    }
}
```

`ProcessExitedEventArgs.ExitCode` is the process exit source; `TerminalControl.Process` is an executable-name string and must not be treated as a `System.Diagnostics.Process`.

- [ ] **Step 3: Guard against accidental kill-on-reparent**

```bash
git grep -n "DetachedFromVisualTree\|OnUnloaded\|Unloaded" -- src/Views/DevSpaceTerminal*
```

Expected: no detach/unload handler calls `Kill()` or `Stop()`. Layout reparenting is presentation-only.

- [ ] **Step 4: Build and commit**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
git add src/Views/DevSpaceTerminal.axaml src/Views/DevSpaceTerminal.axaml.cs
git commit -m "feat: embed DevSpaces PTY terminal"
```

Expected build: PASS against 1.0.11.

---

### Task 5: Build Tabs and the Persistent Multi-Terminal Grid

**Files:**
- Create: `src/Views/DevSpaces.axaml`
- Create: `src/Views/DevSpaces.axaml.cs`

**Key invariant:** cache and reparent the whole pane wrapper, not just `TerminalControl`; this prevents a cached terminal visual from remaining parented to an abandoned wrapper.

- [ ] **Step 1: Create toolbar and grid host**

Use a two-row page. Row 0 contains horizontal session tabs, layout `ComboBox`, and `Icons.Plus`; Row 1 contains:

```xml
<UniformGrid x:Name="TerminalGrid" Margin="4"/>
```

The layout selector binds `LayoutIndex` and uses `Text.DevSpaces.Layout.Auto`, `.1x1`, `.2x2`, `.3x3`, `.4x4`. The plus button calls `OnCreateTerminal` and uses `Text.DevSpaces.NewTerminal` as its tooltip.

- [ ] **Step 2: Cache persistent pane handles**

In code-behind:

```csharp
private sealed record TerminalPaneHandle(Border Root, DevSpaceTerminal TerminalView);
private readonly Dictionary<Guid, TerminalPaneHandle> _panes = [];
```

`GetOrCreatePane(session)` must:

1. return `_panes[session.Id].Root` when already present;
2. create `var terminalView = new DevSpaceTerminal { DataContext = session };`;
3. create one `Border`/`Grid` wrapper containing a compact header, title, close button, and `terminalView`;
4. save both in `_panes`;
5. call `terminalView.Start(vm.Launcher)` exactly once;
6. return the cached wrapper.

Never construct a second `DevSpaceTerminal` for the same `session.Id`.

- [ ] **Step 3: Rebuild only grid placement**

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
        TerminalGrid.Children.Add(
            slot.Terminal != null
                ? GetOrCreatePane(slot.Terminal)
                : CreateEmptySlot(slot.Index));
    }
}
```

Because the cached **wrapper** is removed by `Children.Clear()`, it can be added to a new cell without its terminal child changing parent.

- [ ] **Step 4: Implement interactions**

- Session-tab click: `vm.ActivateTerminal(session)` then `RebuildGrid()`.
- Occupied pane click/focus: activate that session.
- Empty cell: `vm.CreateTerminalAt(slotIndex)`; property change rebuilds grid.
- Close button:

```csharp
vm.CloseTerminal(session);
if (_panes.Remove(session.Id, out var pane))
    pane.TerminalView.Dispose();
RebuildGrid();
```

Closing a pane never iterates over or disposes other entries in `_panes`.

- [ ] **Step 5: Subscribe to view-model changes**

On `DataContextChanged`, unsubscribe the old `INotifyPropertyChanged`, dispose old cached panes only when changing to a different DevSpaces owner/null, subscribe the new owner, and rebuild. Rebuild on `VisibleSlots`, `GridDimension`, `ActiveTerminal`, or `Layout` changes. Dispatch UI changes through `Dispatcher.UIThread` when needed.

Ordinary right-page hiding does **not** dispose panes.

- [ ] **Step 6: Build and smoke-test reparenting**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Use a long-running interactive shell command as the temporary default command, create four sessions, enter recognizable text in each, then switch:

```text
Auto -> 1x1 -> 2x2 -> 3x3 -> 4x4 -> Auto
```

Expected: no process restarts; history remains; empty fixed-grid cells show `+ New Terminal`.

- [ ] **Step 7: Commit**

```bash
git add src/Views/DevSpaces.axaml src/Views/DevSpaces.axaml.cs
git commit -m "feat: add DevSpaces terminal grid"
```

---

### Task 6: Integrate DevSpaces into Repository Navigation and Lifecycle

**Files:**
- Modify: `src/ViewModels/Repository.cs`
- Modify: `src/Views/Repository.axaml`

- [ ] **Step 1: Add lazy repository ownership**

Add:

```csharp
public DevSpaces DevSpaces => _devSpaces;
public bool IsDevSpacesEnabled => Preferences.Instance.EnableDevSpaces;
public bool IsDevSpacesVisible => Preferences.Instance.EnableDevSpaces && SelectedViewIndex == 3;
private DevSpaces _devSpaces;
```

- [ ] **Step 2: Extend `SelectedViewIndex`**

Coerce index 3 to 0 while disabled. When selecting 3 while enabled:

```csharp
_devSpaces ??= new DevSpaces(FullPath);
_devSpaces.EnsureFirstSession();
OnPropertyChanged(nameof(DevSpaces));
```

Also notify `IsDevSpacesVisible` alongside the existing Histories/WorkingCopy/Stashes visibility properties.

- [ ] **Step 3: React immediately to the Preferences toggle**

In `Open()`:

```csharp
Preferences.Instance.PropertyChanged += OnPreferencesPropertyChanged;
```

Handler:

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

Import `System.ComponentModel` if required.

- [ ] **Step 4: Make `Repository.Close()` the hard lifetime boundary**

Before final watcher/timer disposal:

```csharp
Preferences.Instance.PropertyChanged -= OnPreferencesPropertyChanged;
_devSpaces?.Dispose();
_devSpaces = null;
```

- [ ] **Step 5: Add sidebar item directly after Stashes**

```xml
<ListBoxItem IsVisible="{Binding IsDevSpacesEnabled, Mode=OneWay}">
  <Grid ColumnDefinitions="4,Auto,*">
    <Rectangle Grid.Column="0" Classes="indicator" Width="4" Height="20" VerticalAlignment="Center"/>
    <Path Grid.Column="1" Classes="icon" Data="{StaticResource Icons.Terminal}"/>
    <TextBlock Grid.Column="2" Classes="header" Text="{DynamicResource Text.DevSpaces}"/>
  </Grid>
</ListBoxItem>
```

- [ ] **Step 6: Add main-content page after Stashes**

```xml
<Border IsVisible="{Binding IsDevSpacesVisible, Mode=OneWay}">
  <v:DevSpaces DataContext="{Binding DevSpaces, Mode=OneWay}"/>
</Border>
```

Do not add diff hotkey behavior to this border.

- [ ] **Step 7: Build and verify toggle lifecycle**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Manual expected sequence:
1. Disabled: no DevSpaces item.
2. Enable: item appears without app restart.
3. Select: `Copilot 1` auto-starts.
4. Disable while selected: Histories becomes active, all DevSpaces PTYs stop, item disappears.
5. Re-enable/reopen: a fresh session manager starts at `Copilot 1`.

- [ ] **Step 8: Commit**

```bash
git add src/ViewModels/Repository.cs src/Views/Repository.axaml
git commit -m "feat: integrate DevSpaces repository page"
```

---

### Task 7: Add Localization Fallbacks

**Files:**
- Modify: `src/Resources/Locales/en_US.axaml`
- Modify: each active locale that does not merge `avares://SourceGit/Resources/Locales/en_US.axaml`

- [ ] **Step 1: Add canonical English keys**

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

- [ ] **Step 2: Identify non-inheriting locales**

Bash:

```bash
for f in src/Resources/Locales/*.axaml; do
  if [ "$(basename "$f")" != "en_US.axaml" ]; then
    grep -q 'Locales/en_US.axaml' "$f" || echo "$f"
  fi
done
```

PowerShell:

```powershell
Get-ChildItem src/Resources/Locales/*.axaml |
  Where-Object Name -ne 'en_US.axaml' |
  Where-Object { -not (Select-String -Quiet -Path $_.FullName -Pattern 'Locales/en_US.axaml') } |
  Select-Object -ExpandProperty FullName
```

- [ ] **Step 3: Add fallback keys to exactly those files**

Copy the English values from Step 1 into every file printed by Step 2. Do not overwrite an existing DevSpaces translation and do not duplicate resources in locale files already inheriting `en_US.axaml`.

- [ ] **Step 4: Build and spot-check resources**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
```

Switch the app through `en_US`, one locale that inherits English, and one locale from Step 2. Expected: no literal `Text.DevSpaces...` missing-resource labels.

- [ ] **Step 5: Commit**

```bash
git add src/Resources/Locales
git commit -m "feat: localize DevSpaces UI"
```

---

### Task 8: Full Functional and Runtime Verification

**Files:** no planned production changes; any discovered defect is fixed in its owning file and its failed gate rerun.

- [ ] **Step 1: Clean dependency setup**

```bash
git submodule sync --recursive
git submodule update --init --recursive
dotnet restore SourceGit.slnx
git status --short
```

Expected: restore succeeds; submodules populated; no unintended generated files staged.

- [ ] **Step 2: Debug and Release builds**

```bash
dotnet build src/SourceGit.csproj -c Debug --no-restore
dotnet build src/SourceGit.csproj -c Release --no-restore
```

Expected: both PASS.

- [ ] **Step 3: Publish the existing six-RID matrix**

Run separately:

```bash
dotnet publish src/SourceGit.csproj -c Release -r win-x64 -o .artifacts/devspaces/win-x64
dotnet publish src/SourceGit.csproj -c Release -r win-arm64 -o .artifacts/devspaces/win-arm64
dotnet publish src/SourceGit.csproj -c Release -r osx-x64 -o .artifacts/devspaces/osx-x64
dotnet publish src/SourceGit.csproj -c Release -r osx-arm64 -o .artifacts/devspaces/osx-arm64
dotnet publish src/SourceGit.csproj -c Release -r linux-x64 -o .artifacts/devspaces/linux-x64
dotnet publish src/SourceGit.csproj -c Release -r linux-arm64 -o .artifacts/devspaces/linux-arm64
```

Expected: all six PASS, including trimming/NativeAOT. If the current host cannot cross-publish a RID, mark that RID `UNVERIFIED: host cannot execute this publish` and do not call the feature fully verified until a capable environment runs it.

- [ ] **Step 4: Verify worktree directory isolation**

Open a Git worktree in its SourceGit outer tab, enable/open DevSpaces, then print the current directory in the terminal. Expected: exact worktree `Repository.FullPath`, never the main repository path.

- [ ] **Step 5: Verify Copilot session behavior**

Set Default command to `copilot`. Expected:
- first selection starts `Copilot 1` automatically;
- `+` creates independently interactive `Copilot 2`, `Copilot 3`, ...;
- one terminal's input/output does not appear in another.

- [ ] **Step 6: Verify layout preservation**

With four running sessions, leave recognizable terminal history in each and switch:

```text
Auto -> 1x1 -> 2x2 -> 3x3 -> 4x4 -> Auto
```

Expected: no PTY restart; all histories survive; empty cells in larger fixed grids show `+ New Terminal`.

- [ ] **Step 7: Verify capacity boundaries**

A lightweight long-running shell command may be used instead of 17 authenticated Copilot processes.

Expected:
- 1 -> Auto 1x1;
- 2-4 -> Auto 2x2;
- 5-9 -> Auto 3x3;
- 10-16 -> Auto 4x4;
- 17th remains in the tab strip;
- selecting the 17th swaps it into a visible slot without stopping the displaced session.

- [ ] **Step 8: Verify close semantics**

With at least three sessions running, close one pane. Expected: only that process stops. Close the outer repository/worktree tab. Expected: every remaining DevSpaces PTY owned by that repository stops.

- [ ] **Step 9: Verify start failure containment**

Set Default command to:

```text
sourcegit-devspaces-command-does-not-exist
```

Create a terminal. Expected: in-pane `Failed to start terminal: ...`; SourceGit and other sessions remain responsive. Restore command to `copilot` afterward.

- [ ] **Step 10: Verify disabling feature kills owned sessions**

Disable DevSpaces while it is selected and sessions are running. Expected: switch to Histories, all owned PTYs stop, DevSpaces sidebar item disappears immediately.

- [ ] **Step 11: Scope review**

```bash
git status --short
git diff master...HEAD --stat
git diff master...HEAD -- . \
  ':(exclude)docs/superpowers/specs/2026-08-28-devspaces-design.md' \
  ':(exclude)docs/superpowers/plans/2026-08-28-devspaces.md'
```

Expected: only DevSpaces, its pinned dependency, Preferences, Repository integration, and localization. No container implementation and no CI-trigger edits.

- [ ] **Step 12: Commit verification-driven fixes only when needed**

For each discovered defect: fix the owning file, rerun its focused check, rerun Steps 2-3, then commit with a message describing that defect. Do not create an empty verification commit.

---

### Task 9: Final Review and Pull Request

- [ ] **Step 1: Verify branch state**

```bash
git status --short
git log --oneline --decorate master..HEAD
```

Expected: clean worktree and only DevSpaces/spec/plan commits.

- [ ] **Step 2: Re-run final Release gate**

```bash
dotnet build src/SourceGit.csproj -c Release --no-restore
```

Expected: PASS. Rerun any RID publish that failed and was fixed in Task 8.

- [ ] **Step 3: Prepare PR body from observed evidence**

Use title:

```text
feat: add DevSpaces terminal workspace
```

Body must contain these six feature bullets:

```text
- add opt-in DevSpaces directly under Stashes
- launch embedded Copilot CLI terminals in the current repository/worktree path
- support Auto/1x1/2x2/3x3/4x4 layouts with up to 16 visible terminals
- preserve PTY sessions when changing layouts
- terminate sessions with their owning repository tab
- keep container-backed DevSpaces out of this milestone behind a launcher boundary
```

Then add a verification section using **only actual Task 8 observations**. For every RID, write exactly one of `PASS`, `FAIL`, or `UNVERIFIED: <specific reason>`. Do not infer success from the absence of GitHub checks.

If all six publish commands passed, the matrix line is exactly:

```text
RID publish: win-x64 PASS; win-arm64 PASS; osx-x64 PASS; osx-arm64 PASS; linux-x64 PASS; linux-arm64 PASS
```

Also state the existing repository limitation:

```text
Existing CI limitation: PR workflows target develop while this fork targets master; this feature does not change those triggers.
```

- [ ] **Step 4: Open and inspect the PR**

Target `master` from `feat/devspaces`. Confirm no merge conflict, spec and plan are included, and the diff has no container or unrelated CI changes. If GitHub has no checks because of the existing trigger mismatch, report **no checks ran** rather than **CI passed**.
