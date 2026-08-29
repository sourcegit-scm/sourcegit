# DevSpace MCP SSE Design

Date: 2026-08-29
Status: Approved design, pending implementation-plan review

## Goal

Expose SourceGit DevSpace terminal context to MCP-capable LLM clients through a localhost-only SSE MCP server. The first version is read-only: clients can discover DevSpaces and terminal sessions, inspect terminal status, tail recent output, and incrementally read new output without being able to execute commands or inject terminal input.

## Scope

V1 includes:

- MCP server hosted in the running SourceGit desktop process.
- Legacy SSE transport for MCP clients that connect through a long-lived SSE stream.
- Loopback binding only.
- Explicit enable/disable setting.
- Per-terminal bounded transcript capture.
- DevSpace/worktree-aware terminal registry.
- Windows terminal backend transcript capture first.
- MCP tools for discovery, status, tail, and cursor-based reads.
- Authentication token for local MCP access.
- Unit and integration tests.
- Release NativeAOT publish verification.

V1 excludes:

- Running commands through MCP.
- Sending keys/input through MCP.
- Killing terminal processes through MCP.
- Persisting terminal transcripts to disk.
- Screen scraping terminal controls.
- Semantic command-boundary tracking.

## Transport

Use `ModelContextProtocol.AspNetCore` 2.x and enable the SDK's legacy SSE transport explicitly. The MCP server is stateful and hosted in-process with SourceGit.

Expected local endpoints:

- `GET http://127.0.0.1:<port>/sse`
- `POST http://127.0.0.1:<port>/message`

SourceGit must never bind this server to `0.0.0.0` or another externally reachable interface.

The default port may be configurable. A dynamically selected loopback port is acceptable if SourceGit exposes the resulting endpoint in Settings and provides a copyable client configuration.

## Architecture

```text
SourceGit
  DevSpace / Worktree
     Terminal PTY
       |---> Terminal UI renderer
       `---> TerminalTranscriptStore
                    |
             DevSpaceTerminalRegistry
                    |
              SourceGit MCP Host
                    |
            localhost SSE transport
                    |
                 LLM client
```

Terminal capture and MCP exposure are separate concerns. Terminal surfaces publish transcript events to a transcript sink/store. MCP tools read through registry/session abstractions rather than depending directly on Avalonia controls.

## Terminal Session Model

Add a terminal-session abstraction separate from `IDevSpaceTerminalSurface` so MCP concerns do not leak into the rendering interface.

Conceptual contract:

```csharp
interface IDevSpaceTerminalSession
{
    string Id { get; }
    string DevSpaceId { get; }
    string WorkingDirectory { get; }
    string BackendName { get; }
    TerminalSessionStatus Status { get; }
    TerminalReadResult Read(long? afterSequence, int maxBytes);
}
```

Each DevSpace/worktree owns its own terminal sessions. Switching tabs must not merge or lose terminal context.

## Transcript Store

Each terminal owns an in-memory bounded transcript store. No transcript is written to SourceGit logs or persisted to disk.

Suggested event shape:

```csharp
record DevSpaceTerminalEvent(
    long Sequence,
    DateTimeOffset Timestamp,
    TerminalEventKind Kind,
    string Text,
    int? ExitCode);
```

Initial event kinds:

- `Output`
- `Exit`

Terminal input is not shared in V1.

Behavior:

- Monotonic per-session sequence numbers.
- Bounded retention by line count and/or byte size.
- Default tail of 200 lines.
- Maximum MCP response size of approximately 64 KB.
- Cursor-based reads using `afterSequence`.
- A stale cursor returns the oldest retained sequence plus a truncation indicator instead of failing ambiguously.
- UTF-8 output split across stream reads must remain valid text.

Recommended starting limits:

- 3,000-10,000 retained lines per terminal.
- 64 KB maximum output per MCP read response.

## Windows Terminal Capture

`WindowsTerminalDevSpaceSurface` already reads decoded PTY output before sending it to the Windows terminal host. Capture transcript output at that boundary:

1. Read bytes from the PTY reader stream.
2. Decode UTF-8 safely across chunk boundaries.
3. Append output to the transcript sink/store.
4. Forward the same decoded output to the terminal UI host.
5. Record process exit/status changes.

This keeps the UI behavior unchanged and avoids screen scraping.

## Avalonia Terminal Capture

The existing Avalonia terminal control owns more of its process/PTY lifecycle. V1 must not scrape visible screen text.

Implementation order:

1. Ship Windows transcript capture first.
2. Investigate a clean raw-output hook in the Avalonia terminal control stack.
3. If no suitable hook exists, introduce a SourceGit-owned shared PTY session that feeds both transcript storage and renderer backends.

Long-term target:

```text
DevSpacePtySession
   |---> TerminalTranscriptStore
   |---> Windows renderer
   `---> Avalonia renderer
```

## MCP Surface

Initial tools:

### `sourcegit_list_devspaces`

Returns registered DevSpaces/worktrees that currently expose terminal context.

### `sourcegit_list_terminals`

Inputs:

- optional `devSpaceId`

Returns terminal session IDs and metadata such as backend, working directory, and status.

### `sourcegit_terminal_status`

Inputs:

- `terminalId`

Returns current status, backend, working directory, retained sequence range, and exit code when available.

### `sourcegit_terminal_tail`

Inputs:

- `terminalId`
- optional `lines` with a safe upper bound

Returns recent terminal output and the latest sequence cursor.

### `sourcegit_terminal_read`

Inputs:

- `terminalId`
- optional `afterSequence`
- optional `maxBytes`

Returns only retained events newer than the supplied cursor, plus `nextSequence` and a truncation indicator.

Example input:

```json
{
  "terminalId": "terminal-2",
  "afterSequence": 940,
  "maxBytes": 32768
}
```

Example response shape:

```json
{
  "running": true,
  "backend": "WindowsTerminal",
  "workingDirectory": "D:\\Development\\sourcegit",
  "truncated": false,
  "nextSequence": 955,
  "output": "..."
}
```

## MCP Resources and Notifications

Expose read-only resources where useful:

- `sourcegit://devspaces`
- `sourcegit://devspaces/{id}/terminals`
- `sourcegit://terminals/{sessionId}`
- `sourcegit://terminals/{sessionId}/output`

Terminal output changes may trigger resource-updated notifications over the SSE connection, but notifications must be debounced. Do not emit one MCP notification per PTY byte or character.

Recommended notification debounce: approximately 200 ms.

## Backpressure and Concurrency

Legacy SSE lacks the stronger HTTP-level backpressure behavior of newer Streamable HTTP. SourceGit must therefore bound work explicitly:

- Bounded transcript retention.
- Bounded MCP response sizes.
- Debounced resource-update notifications.
- A small maximum number of concurrent MCP tool calls, initially 4-8.
- Cancellation support for client disconnects and application shutdown.

## Security

The feature is disabled by default until explicitly enabled in SourceGit Settings.

Requirements:

- Bind only to `127.0.0.1`/loopback.
- Require a generated token for MCP requests.
- Allow token regeneration.
- Never expose terminal input in V1.
- Never persist terminal output to normal application logs.
- Clearly indicate that terminal output can contain secrets.
- Shut down the MCP host with SourceGit.

Suggested settings:

```text
Settings > MCP
[x] Enable MCP Server
Endpoint: http://127.0.0.1:53921/sse
[Copy MCP Configuration]
[x] Share DevSpace terminal output
Retention: [3000 lines]
Authentication token: ******** [Regenerate]
```

## Error Handling

MCP tools return structured errors for:

- Unknown DevSpace.
- Unknown terminal session.
- Terminal sharing disabled.
- Invalid cursor/maxBytes/line limits.
- Session already disposed.

A terminal exiting is not an MCP error; the status response should report the exit state and retained output remains readable until the session is evicted.

MCP server startup failure must not crash SourceGit. Surface the failure in settings/status and keep the desktop app usable.

## Testing

### Transcript store tests

- Append and preserve order.
- Sequence numbers are monotonic.
- Cursor reads return only newer events.
- Retention truncates oldest events correctly.
- Stale cursor reports truncation deterministically.
- UTF-8 split across stream reads remains valid.
- Multiple readers do not mutate session state.
- Session isolation.

### Registry tests

- Register/remove sessions.
- Multiple terminals per DevSpace.
- Per-DevSpace/worktree isolation.
- Session lookup by ID.

### MCP tool tests

- List DevSpaces.
- List terminals.
- Tail terminal.
- Incremental read.
- Status for running/exited terminals.
- Unknown terminal errors.
- Sharing-disabled errors.
- Maximum response size enforcement.

### MCP host tests

- Loopback-only binding configuration.
- Authentication token enforcement.
- SSE transport enabled.
- Graceful SourceGit shutdown.
- Server startup failure does not terminate the desktop application.

### Integration verification

- Run normal test suite.
- Build SourceGit.
- Publish Release NativeAOT for at least `win-x64`:

```bash
dotnet publish src/SourceGit.csproj -c Release -r win-x64
```

## Delivery Sequence

1. Add transcript event/store/session abstractions with tests.
2. Add DevSpace terminal registry with tests.
3. Capture Windows terminal PTY output into the transcript store.
4. Add MCP ASP.NET host with legacy SSE enabled, loopback binding, authentication, and bounded concurrency.
5. Add read-only MCP tools/resources and tests.
6. Add SourceGit MCP settings/status UI.
7. Verify tests, build, and NativeAOT publish.
8. Follow up with Avalonia raw-output support if it cannot cleanly share the existing Windows capture path.

## Success Scenario

A developer runs `dotnet test` in a SourceGit DevSpace terminal. An MCP-connected LLM can discover that DevSpace, locate the terminal, read the recent output through SSE-backed MCP tools, incrementally retrieve newer output, and explain the build/test failure without the user copying terminal text into the conversation.

The LLM cannot run commands or type into the terminal in V1.
