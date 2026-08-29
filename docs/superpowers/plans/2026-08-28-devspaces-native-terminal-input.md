# DevSpaces Native Terminal Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make DevSpaces terminal selection, copy, and paste feel native without changing SourceGit's Avalonia 11 platform or its persistent PTY lifecycle.

**Architecture:** Upgrade `Iciclecreek.Avalonia.Terminal` to 1.0.12, then wrap its existing `TerminalControl`/`TerminalView` with DevSpaces-specific subclasses. The derived view supplies full-surface hit testing; the derived control exposes safe clipboard/selection operations and uses the existing `PART_TerminalView` template contract. SourceGit owns only context-menu UX and never reimplements PTY or emulator behavior.

**Tech Stack:** .NET 10, Avalonia 11.3.20, Iciclecreek.Avalonia.Terminal 1.0.12, XTerm.NET 1.x, existing SourceGit GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-28-devspaces-native-terminal-input-design.md`

## Global Constraints

- Keep Avalonia packages at `11.3.20`.
- Upgrade only `Iciclecreek.Avalonia.Terminal` from `1.0.11` to `1.0.12`.
- Do not add a terminal fork, submodule, source vendoring, reflection, or Avalonia 12 dependency.
- Existing DevSpaces terminal controls must remain parented for their session lifetime.
- `Ctrl+C` with no selection remains process-owned.
- Do not intercept terminal keyboard shortcuts in the outer SourceGit view.
- Suppress the SourceGit context menu whenever XTerm mouse tracking is active.
- No new DevSpaces test project; use the previously approved exception, source audit, six-platform PR build/format checks, and manual Copilot acceptance.

---

### Task 1: Add the DevSpaces terminal adapter

**Files:**
- Modify: `src/SourceGit.csproj`
- Create: `src/Views/DevSpaceTerminalControl.cs`

**Interfaces:**
- Consumes: `Iciclecreek.Terminal.TerminalControl`, `Iciclecreek.Terminal.TerminalView`, `XTerm.Input.MouseTrackingMode`.
- Produces: `DevSpaceTerminalControl.CopyAsync()`, `PasteAsync()`, `SelectAll()`, `HasSelection`, `IsMouseReportingActive`, and `DevSpaceTerminalView`.

- [ ] **Step 1: Update the package reference**

Change:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.11" />
```

to:

```xml
<PackageReference Include="Iciclecreek.Avalonia.Terminal" Version="1.0.12" />
```

Do not change any Avalonia package version.

- [ ] **Step 2: Create the derived terminal view and control**

Create `src/Views/DevSpaceTerminalControl.cs` with this structure:

```csharp
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

using Iciclecreek.Terminal;

namespace SourceGit.Views
{
    public sealed class DevSpaceTerminalView : TerminalView, ICustomHitTest
    {
        public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);
    }

    public sealed class DevSpaceTerminalControl : TerminalControl
    {
        public bool HasSelection => _view?.Terminal.Selection.HasSelection == true;

        public bool IsMouseReportingActive =>
            _view?.Terminal.MouseTrackingMode != XTerm.Input.MouseTrackingMode.None;

        public Task<bool> CopyAsync() =>
            _view?.CopyAsync() ?? Task.FromResult(false);

        public Task PasteAsync() =>
            _view?.PasteAsync() ?? Task.CompletedTask;

        public void SelectAll()
        {
            _view?.Terminal.Selection.SelectAll();
            _view?.InvalidateVisual();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _view = e.NameScope.Find<DevSpaceTerminalView>("PART_TerminalView");
        }

        private DevSpaceTerminalView? _view;
    }
}
```

If the 1.0.12 XTerm selection API spells `SelectAll` differently, stop and inspect that exact package/upstream 1.x API before changing the design; do not guess.

- [ ] **Step 3: Source-audit the adapter before integrating it**

Verify manually from the source diff:

```text
- adapter derives from upstream controls
- no reflection
- no PTY creation or process-launch code
- hit testing uses Bounds only
- mouse tracking comes from Terminal.MouseTrackingMode
- copy/paste delegate to TerminalView
```

- [ ] **Step 4: Commit**

Commit message:

```text
feat: add native DevSpaces terminal adapter
```

**Deliverable:** SourceGit has a small adapter over the Avalonia-11 terminal package with no lifecycle changes.

---

### Task 2: Use the adapter without changing PTY lifetime

**Files:**
- Modify: `src/Views/DevSpaceTerminal.axaml`

**Interfaces:**
- Consumes: `SourceGit.Views.DevSpaceTerminalControl`.
- Produces: the same `x:Name="Terminal"` field used by `DevSpaceTerminal.axaml.cs`, so existing launch/stop code remains unchanged.

- [ ] **Step 1: Replace the terminal control and provide its template**

Remove the terminal XML namespace and use the local SourceGit namespace:

```xml
xmlns:local="using:SourceGit.Views"
```

Replace the existing terminal element with:

```xml
<local:DevSpaceTerminalControl x:Name="Terminal"
                               FontFamily="{DynamicResource Fonts.Monospace}"
                               BufferSize="3000">
  <local:DevSpaceTerminalControl.Template>
    <ControlTemplate>
      <Grid ColumnDefinitions="*,Auto">
        <local:DevSpaceTerminalView x:Name="PART_TerminalView"
                                    FontFamily="{TemplateBinding FontFamily}"
                                    FontSize="{TemplateBinding FontSize}"
                                    FontStyle="{TemplateBinding FontStyle}"
                                    FontWeight="{TemplateBinding FontWeight}"
                                    Foreground="{TemplateBinding Foreground}"
                                    Background="{TemplateBinding Background}"
                                    SelectionBrush="{TemplateBinding SelectionBrush}"
                                    Process="{TemplateBinding Process}"
                                    StartingDirectory="{TemplateBinding StartingDirectory}"
                                    Args="{TemplateBinding Args}"
                                    Options="{TemplateBinding Options}"/>
        <ScrollBar x:Name="PART_ScrollBar"
                   Grid.Column="1"
                   Orientation="Vertical"
                   AllowAutoHide="False"/>
      </Grid>
    </ControlTemplate>
  </local:DevSpaceTerminalControl.Template>
</local:DevSpaceTerminalControl>
```

Keep the existing error overlay unchanged.

- [ ] **Step 2: Audit lifecycle invariants**

Confirm `src/Views/DevSpaceTerminal.axaml.cs` still launches via:

```csharp
Terminal.ProcessExited += OnProcessExited;
Terminal.LaunchProcess(spec.WorkingDirectory, spec.Process, spec.Arguments);
```

and stops via:

```csharp
Terminal.Kill();
```

No code in this task may recreate the terminal control on add/layout/navigation.

- [ ] **Step 3: Commit**

Commit message:

```text
feat: use full-surface DevSpaces terminal view
```

**Deliverable:** existing DevSpaces session lifecycle now renders through the full-surface terminal view.

---

### Task 3: Add native Copy/Paste/Select All menu

**Files:**
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`

**Interfaces:**
- Consumes: `DevSpaceTerminalControl.HasSelection`, `IsMouseReportingActive`, `CopyAsync()`, `PasteAsync()`, `SelectAll()`.
- Produces: a normal-shell right-click menu while leaving mouse-aware TUI right-click untouched.

- [ ] **Step 1: Register a handled-events-too tunneling handler in the constructor**

After `InitializeComponent()` add:

```csharp
AddHandler(
    PointerPressedEvent,
    OnTerminalPointerPressed,
    Avalonia.Interactivity.RoutingStrategies.Tunnel,
    handledEventsToo: true);
```

Add the required `Avalonia.Input` and `Avalonia.Interactivity` usings.

- [ ] **Step 2: Add the right-click gate**

Implement:

```csharp
private void OnTerminalPointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (!e.GetCurrentPoint(Terminal).Properties.IsRightButtonPressed ||
        Terminal.IsMouseReportingActive)
        return;

    var menu = new ContextMenu();
    var copy = new MenuItem { Header = "Copy", IsEnabled = Terminal.HasSelection };
    var paste = new MenuItem { Header = "Paste" };
    var selectAll = new MenuItem { Header = "Select All" };

    copy.Click += async (_, _) => await Terminal.CopyAsync();
    paste.Click += async (_, _) => await Terminal.PasteAsync();
    selectAll.Click += (_, _) => Terminal.SelectAll();

    menu.Items.Add(copy);
    menu.Items.Add(paste);
    menu.Items.Add(selectAll);
    menu.Open(Terminal);
    e.Handled = true;
}
```

Do not clear the selection after copy. Do not add outer key handlers.

- [ ] **Step 3: Keep clipboard failures isolated**

Wrap the two async menu delegates with a small local/static helper or `try/catch` so a platform clipboard exception does not escape into the UI event loop. The catch intentionally performs no PTY action and does not stop the session.

- [ ] **Step 4: Commit**

Commit message:

```text
feat: add DevSpaces terminal clipboard menu
```

**Deliverable:** Copy/Paste/Select All is discoverable without stealing TUI mouse input or terminal shortcuts.

---

### Task 4: Verify and open the SourceGit PR

**Files:**
- Audit: all files changed from `master` to `feat/devspaces-native-terminal-input`.

- [ ] **Step 1: Compare branch against master**

Expected production scope:

```text
src/SourceGit.csproj
src/Views/DevSpaceTerminalControl.cs
src/Views/DevSpaceTerminal.axaml
src/Views/DevSpaceTerminal.axaml.cs
```

Plus this spec and plan only.

- [ ] **Step 2: Open a PR to `master`**

Title:

```text
feat: improve native DevSpaces terminal input
```

Body must state:

```text
- upgrade terminal package 1.0.11 -> 1.0.12 on Avalonia 11
- make the whole terminal rectangle hit-testable
- add Copy/Paste/Select All context menu
- preserve terminal keyboard and TUI mouse ownership
- no PTY/session lifecycle change
- no dedicated DevSpaces test project; verification is PR Check + manual Copilot acceptance
```

- [ ] **Step 3: Verify PR Check**

Wait for the existing PR Check to finish. Required evidence is successful build/publish matrix for Windows x64/ARM64, macOS x64/ARM64, Linux x64/ARM64, plus format check.

If any job fails, invoke `superpowers:systematic-debugging`, inspect the failing job logs, reproduce the root cause from evidence, then fix on the same branch.

- [ ] **Step 4: Report manual acceptance separately**

CI green proves build/format compatibility only. Before calling native interaction fully verified, run or ask the user to run the spec's Windows Copilot checklist. Do not claim mouse/clipboard feel was manually verified by CI.

**Deliverable:** scoped PR with green cross-platform CI and an explicit remaining/manual runtime verification status.