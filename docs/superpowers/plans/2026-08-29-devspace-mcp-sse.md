# DevSpace MCP SSE Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only, localhost-only MCP server to SourceGit that lets MCP clients inspect DevSpace terminal sessions and incrementally read Windows terminal output over legacy SSE.

**Architecture:** DevSpace terminal sessions gain a bounded in-memory transcript store and are registered by repository/worktree path. The existing Windows PTY surface publishes decoded output into the session transcript while continuing to render it normally. An in-process ASP.NET Core MCP host exposes read-only tools over legacy SSE and is controlled by SourceGit preferences.

**Tech Stack:** .NET 10, Avalonia 11, Porta.Pty, `ModelContextProtocol.AspNetCore` 2.x, xUnit, ASP.NET Core/Kestrel, NativeAOT.

**Spec:** `docs/superpowers/specs/2026-08-29-devspace-mcp-sse-design.md`

## Global Constraints

- V1 is read-only: no command execution, terminal input injection, or process kill tools.
- MCP binds only to loopback (`127.0.0.1`).
- Use legacy SSE transport intentionally.
- Terminal transcripts remain in memory only and never go to SourceGit logs.
- MCP output reads are bounded to 64 KiB.
- Terminal history retention defaults to 3000 logical events/lines.
- Windows terminal capture ships first; Avalonia fallback capture is not implemented by screen scraping.
- SourceGit must remain usable if MCP startup fails.
- Release NativeAOT publish on `win-x64` is an acceptance gate.

---

### Task 1: Terminal transcript store

**Files:**
- Create: `src/DevSpaces/Terminal/DevSpaceTerminalEvent.cs`
- Create: `src/DevSpaces/Terminal/TerminalReadResult.cs`
- Create: `src/DevSpaces/Terminal/TerminalTranscriptStore.cs`
- Test: `tests/SourceGit.Tests/TerminalTranscriptStoreTests.cs`

**Interfaces:**
- Produces: `TerminalTranscriptStore(int capacity = 3000)`, `long AppendOutput(string text)`, `long AppendExit(int exitCode)`, `TerminalReadResult Read(long? afterSequence = null, int maxBytes = 65536)`, `TerminalReadResult Tail(int maxEvents = 200, int maxBytes = 65536)`.
- `TerminalReadResult` exposes `Events`, `OldestSequence`, `NextSequence`, and `Truncated`.

- [ ] **Step 1: Write failing transcript tests**

Cover monotonic sequencing, cursor reads, retention truncation, stale cursors, bounded UTF-8 output, tail reads, and independent readers.

```csharp
[Fact]
public void Read_returns_only_events_after_cursor()
{
    var store = new TerminalTranscriptStore(10);
    var first = store.AppendOutput("one\n");
    store.AppendOutput("two\n");

    var result = store.Read(first);

    Assert.Single(result.Events);
    Assert.Equal("two\n", result.Events[0].Text);
}
```

- [ ] **Step 2: Verify tests fail before implementation**

Run:

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter TerminalTranscriptStoreTests
```

Expected: build/test failure because transcript types do not exist.

- [ ] **Step 3: Implement the bounded store**

Use a private lock and queue/list. Sequence numbers are per store and start at 1. When capacity is exceeded, evict oldest events. `Read` marks `Truncated=true` when `afterSequence` predates retained history. Bound responses by UTF-8 byte count, never splitting an event string into invalid UTF-8.

- [ ] **Step 4: Re-run transcript tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/DevSpaces/Terminal tests/SourceGit.Tests/TerminalTranscriptStoreTests.cs
git commit -m "feat: add bounded DevSpace terminal transcripts"
```

### Task 2: Terminal session registry and DevSpace integration

**Files:**
- Create: `src/DevSpaces/Terminal/DevSpaceTerminalRegistry.cs`
- Modify: `src/ViewModels/DevSpaceTerminal.cs`
- Modify: `src/ViewModels/DevSpaces.cs`
- Test: `tests/SourceGit.Tests/DevSpaceTerminalRegistryTests.cs`

**Interfaces:**
- `DevSpaceTerminal` gains `TerminalTranscriptStore Transcript` and `string DevSpaceId`.
- `DevSpaceTerminalRegistry.Register(DevSpaceTerminal session)`, `Unregister(Guid id)`, `TryGet(Guid id, out DevSpaceTerminal session)`, `GetDevSpaces()`, and `GetSessions(string devSpaceId = null)`.

- [ ] **Step 1: Write failing registry tests**

Verify multiple terminals per DevSpace, worktree isolation, lookup by ID, unregister behavior, and stable path comparison on Windows.

- [ ] **Step 2: Verify registry tests fail**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpaceTerminalRegistryTests
```

- [ ] **Step 3: Add session metadata and registration lifecycle**

Pass the owning DevSpace path into `DevSpaceTerminal`. Register immediately after creation in `ViewModels.DevSpaces.CreateTerminalAt` and unregister when a terminal is closed or all terminals are stopped.

- [ ] **Step 4: Re-run registry and existing tests**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/DevSpaces/Terminal src/ViewModels/DevSpaceTerminal.cs src/ViewModels/DevSpaces.cs tests/SourceGit.Tests/DevSpaceTerminalRegistryTests.cs
git commit -m "feat: register DevSpace terminal sessions"
```

### Task 3: Capture Windows PTY output

**Files:**
- Modify: `src/DevSpaces/WindowsTerminalDevSpaceSurface.cs`
- Modify: `src/Views/DevSpaceTerminal.axaml.cs`
- Test: `tests/SourceGit.Tests/DevSpaceTerminalCaptureTests.cs`

**Interfaces:**
- `WindowsTerminalDevSpaceSurface` constructor accepts `TerminalTranscriptStore transcript`.
- Output is appended immediately after UTF-8 decoding and before `_host.SendOutput`.
- Exit is appended once at the same boundary that raises the existing `Exited` event.

- [ ] **Step 1: Add a capture seam test**

Test a small internal helper that accepts decoded output and writes to a transcript store, so PTY behavior itself does not need a native terminal in unit tests.

- [ ] **Step 2: Verify the capture test fails**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --filter DevSpaceTerminalCaptureTests
```

- [ ] **Step 3: Wire transcript capture into the Windows surface**

Create the preferred Windows surface with `session.Transcript`. Preserve the existing incremental `Encoding.UTF8.GetDecoder()` path and do not capture input.

- [ ] **Step 4: Re-run all tests**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/DevSpaces/WindowsTerminalDevSpaceSurface.cs src/Views/DevSpaceTerminal.axaml.cs tests/SourceGit.Tests/DevSpaceTerminalCaptureTests.cs
git commit -m "feat: capture Windows DevSpace terminal output"
```

### Task 4: Read-only MCP tools and SSE host

**Files:**
- Modify: `src/SourceGit.csproj`
- Create: `src/Mcp/SourceGitMcpOptions.cs`
- Create: `src/Mcp/SourceGitMcpTools.cs`
- Create: `src/Mcp/SourceGitMcpHost.cs`
- Test: `tests/SourceGit.Tests/SourceGitMcpToolsTests.cs`
- Test: `tests/SourceGit.Tests/SourceGitMcpHostTests.cs`

**Interfaces:**
- `SourceGitMcpTools.ListDevSpaces()`
- `SourceGitMcpTools.ListTerminals(string devSpaceId = null)`
- `SourceGitMcpTools.TerminalStatus(string terminalId)`
- `SourceGitMcpTools.TerminalTail(string terminalId, int lines = 200)`
- `SourceGitMcpTools.TerminalRead(string terminalId, long? afterSequence = null, int maxBytes = 65536)`
- `SourceGitMcpHost.StartAsync(SourceGitMcpOptions options, CancellationToken)` and `StopAsync()`.

- [ ] **Step 1: Add `ModelContextProtocol.AspNetCore` 2.x**

Pin the current compatible 2.x package version and keep Release NativeAOT enabled.

- [ ] **Step 2: Write failing MCP tool tests**

Seed the registry with test sessions and verify discovery, status, tail, incremental reads, invalid terminal IDs, and the 64 KiB limit.

- [ ] **Step 3: Implement tool DTOs and read-only MCP methods**

Use explicit method names `sourcegit_list_devspaces`, `sourcegit_list_terminals`, `sourcegit_terminal_status`, `sourcegit_terminal_tail`, and `sourcegit_terminal_read`. Do not expose mutating terminal operations.

- [ ] **Step 4: Write failing host configuration tests**

Verify loopback address, token validation, legacy SSE enabled, and graceful handling of startup errors.

- [ ] **Step 5: Implement the host**

Build an in-process ASP.NET Core host with Kestrel bound to `127.0.0.1:<port>`. Configure MCP as stateful and enable legacy SSE. Add token validation middleware before MCP endpoints. A startup exception is captured as host status rather than propagated into the desktop app.

- [ ] **Step 6: Re-run MCP and full tests**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/SourceGit.csproj src/Mcp tests/SourceGit.Tests/SourceGitMcp*Tests.cs
git commit -m "feat: add read-only DevSpace MCP SSE server"
```

### Task 5: Preferences and application lifecycle

**Files:**
- Modify: `src/ViewModels/Preferences.cs`
- Modify: `src/Views/DevSpacesPreferences.axaml`
- Modify: `src/Views/DevSpacesPreferences.axaml.cs`
- Modify: `src/DevSpaces/DevSpacesBootstrap.cs`
- Create: `tests/SourceGit.Tests/SourceGitMcpPreferencesTests.cs`

**Interfaces:**
- Preferences: `EnableMcpServer`, `McpPort` (default 53921), `McpShareDevSpaceTerminalOutput` (default true), `McpAuthToken`.
- Token is generated on first enable when empty.
- Enabling/disabling MCP starts/stops the host without restarting SourceGit.

- [ ] **Step 1: Add failing preference/default tests**

Verify safe defaults: disabled server, loopback-only port configuration, terminal sharing enabled only as a read capability, non-empty generated token after enable.

- [ ] **Step 2: Extend DevSpaces preferences UI**

Add MCP enable toggle, endpoint display, terminal-output sharing toggle, masked token, and regenerate action. Include a short warning that terminal output may contain secrets.

- [ ] **Step 3: Wire host lifecycle**

`DevSpacesBootstrap` observes MCP preference changes, starts the host when enabled, stops it when disabled, and ensures shutdown/disposal does not crash the app.

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/ViewModels/Preferences.cs src/Views/DevSpacesPreferences.axaml src/Views/DevSpacesPreferences.axaml.cs src/DevSpaces/DevSpacesBootstrap.cs tests/SourceGit.Tests/SourceGitMcpPreferencesTests.cs
git commit -m "feat: add SourceGit MCP preferences"
```

### Task 6: Verification and PR readiness

**Files:**
- Modify only files required by verification fixes.

**Interfaces:**
- No new product surface; this task proves the feature is safe to ship.

- [ ] **Step 1: Run the complete test project**

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --configuration Release
```

Expected: PASS.

- [ ] **Step 2: Build SourceGit**

```bash
dotnet build src/SourceGit.csproj --configuration Release -p:DisableAOT=true
```

Expected: PASS.

- [ ] **Step 3: Verify NativeAOT publish**

```bash
dotnet publish src/SourceGit.csproj --configuration Release --runtime win-x64
```

Expected: PASS with MCP assemblies/configuration surviving trimming/AOT.

- [ ] **Step 4: Inspect branch diff for V1 scope**

Confirm there is no MCP command execution/input API, no non-loopback binding, no persisted transcript, and no Avalonia screen scraping.

- [ ] **Step 5: Commit verification fixes if any**

```bash
git add -A
git commit -m "fix: harden DevSpace MCP integration"
```
