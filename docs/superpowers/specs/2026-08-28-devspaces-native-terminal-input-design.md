# DevSpaces Native Terminal Input Design

## Status

Approved revised architecture. This replaces the earlier fork/submodule approach because the required writable terminal fork is unavailable in the connected GitHub environment.

## Problem

DevSpaces embeds `Iciclecreek.Avalonia.Terminal` 1.0.11. The PTY lifecycle now persists correctly, but selecting Copilot output, copying, and pasting still feels less reliable than a native desktop terminal. The main UX gap is that pointer hit testing does not reliably cover blank terminal pixels, and DevSpaces has no discoverable Copy/Paste/Select All menu.

## Goals

1. Make the whole visible terminal rectangle a pointer input surface, including blank cells and padding.
2. Preserve existing PTY/session lifetime behavior: adding terminals, changing DevSpaces layout, or switching repository pages must not restart another terminal.
3. Keep terminal-native keyboard semantics: `Ctrl+C` copies only when there is a selection and otherwise reaches the running process; `Ctrl+Shift+C` copies; `Ctrl+Shift+V` pastes.
4. Add a right-click DevSpaces menu with Copy, Paste, and Select All.
5. Preserve TUI/application mouse ownership when terminal mouse tracking is active.
6. Keep SourceGit on Avalonia 11.3.20.
7. Avoid reflection, source vendoring, a custom terminal fork, and an Avalonia 12 migration.

## Non-goals

- Copilot CLI session-ID persistence across app restarts.
- Terminal themes, profiles, fonts, or shell configuration.
- Replacing XTerm.NET or Porta.Pty.
- Migrating SourceGit to Avalonia 12.
- Changing the terminal-picker work delivered by PR #6.

## Architecture

Upgrade `Iciclecreek.Avalonia.Terminal` from 1.0.11 to 1.0.12, the upstream Avalonia-11 maintenance line. Version 1.0.12 keeps `net8.0`, Avalonia 11.x, Porta.Pty 1.x, and XTerm.NET 1.x while exposing the terminal behavior DevSpaces needs, including public `TerminalView.CopyAsync()` and `PasteAsync()`, selection state through the public XTerm terminal, and re-parent support.

SourceGit adds two small DevSpaces-only controls:

- `DevSpaceTerminalView : TerminalView, ICustomHitTest` implements full-rectangle pointer hit testing with `new Rect(Bounds.Size).Contains(point)`.
- `DevSpaceTerminalControl : TerminalControl` supplies a SourceGit-specific control template whose `PART_TerminalView` is `DevSpaceTerminalView`. It stores the typed template part after `base.OnApplyTemplate()` and exposes DevSpaces-facing operations: `CopyAsync()`, `PasteAsync()`, `SelectAll()`, `HasSelection`, and `IsMouseReportingActive`.

The base `TerminalControl` remains responsible for process launch, PTY lifetime, scrolling, focus forwarding, and process-exit events. SourceGit does not reimplement terminal emulation or selection algorithms.

## Pointer and Selection Behavior

`DevSpaceTerminalView` makes the full terminal bounds hit-testable. Existing upstream 1.0.12 pointer logic remains authoritative for drag selection, double-click word selection, triple-click line selection, and terminal mouse reporting.

SourceGit must not steal pointer input from a running TUI. The context menu is shown only when `Terminal.MouseTrackingMode == XTerm.Input.MouseTrackingMode.None`. When mouse tracking is active, right-click continues to the terminal application.

## Clipboard and Shortcuts

The terminal library keeps ownership of keyboard shortcuts. SourceGit does not intercept `Ctrl+C`, `Ctrl+Shift+C`, or `Ctrl+Shift+V` in the outer `UserControl`.

Required behavior:

- `Ctrl+C`: copy selected text; otherwise send Ctrl+C to the process.
- `Ctrl+Shift+C`: copy selected text.
- `Ctrl+Shift+V`: paste clipboard text.
- Copy must leave the selection visible where the terminal library supports it. SourceGit itself never clears the selection after copy.

The DevSpaces context menu contains:

- Copy — enabled only when a selection exists.
- Paste — always available; clipboard-unavailable/no-text behavior is a safe no-op in the terminal library.
- Select All — selects the visible terminal buffer through the public XTerm selection API. It must not restart the PTY.

## Integration

`Views/DevSpaceTerminal.axaml` switches from `TerminalControl` to `DevSpaceTerminalControl`. The custom control owns its template; the existing `DevSpaceTerminal` view continues to call `LaunchProcess`, subscribe to `ProcessExited`, and call `Kill` exactly as before.

`Views/DevSpaceTerminal.axaml.cs` attaches a tunneling pointer handler with `handledEventsToo: true` so SourceGit can offer the context menu before inner terminal handlers consume right-click. The handler exits immediately if terminal mouse reporting is active.

## Error Handling

Clipboard operations are awaited through small async menu handlers and catch/report no user-facing exception; an unavailable clipboard remains a no-op. Context-menu failures must not terminate the PTY or affect sibling DevSpaces sessions.

If the 1.0.12 package creates a build/API incompatibility, stop and diagnose the exact compiler/restore error rather than importing Avalonia-12 code.

## Verification

SourceGit currently has no DevSpaces test project; the previously approved DevSpaces exception remains in effect rather than adding a test assembly solely for this integration. Verification consists of:

1. Source audit confirming only DevSpaces terminal integration + package version + this spec/plan changed.
2. Existing PR Check across Windows x64/ARM64, macOS x64/ARM64, Linux x64/ARM64, plus format check.
3. Manual Windows Copilot acceptance:
   - start Copilot;
   - drag-select across text and blank terminal areas;
   - double-click a word and triple-click a line;
   - copy with Ctrl+Shift+C;
   - paste with Ctrl+Shift+V;
   - with no selection, Ctrl+C still interrupts/reaches Copilot;
   - right-click shows Copy/Paste/Select All in a normal shell/Copilot screen;
   - a mouse-aware TUI keeps right-click ownership;
   - adding/switching terminals and History/Stashes does not restart the terminal.

## Acceptance Criteria

The feature is complete when the terminal full surface is selectable, copy/paste is reliable and discoverable, terminal-native shortcuts and TUI ownership remain intact, no DevSpaces lifecycle regression is introduced, SourceGit remains on Avalonia 11.3.20, the PR check is green, and manual Copilot acceptance is reported separately from CI.