using System;
using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

using ModelContextProtocol.Server;

using SourceGit.DevSpaces.Terminal;
using SourceGit.ViewModels;

namespace SourceGit.Mcp
{
    [McpServerToolType]
    public sealed class SourceGitMcpTools
    {
        public SourceGitMcpTools(DevSpaceTerminalRegistry registry, SourceGitMcpOptions options)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        [McpServerTool(Name = "sourcegit_list_devspaces")]
        [Description("Lists SourceGit DevSpaces that currently have registered terminal sessions.")]
        public string ListDevSpaces()
        {
            return WriteJson(writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("devSpaces");
                writer.WriteStartArray();

                foreach (var devSpaceId in _registry.GetDevSpaces())
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", devSpaceId);
                    writer.WriteNumber("terminalCount", _registry.GetSessions(devSpaceId).Count);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }

        [McpServerTool(Name = "sourcegit_list_terminals")]
        [Description("Lists SourceGit DevSpace terminal sessions, optionally filtered by DevSpace path.")]
        public string ListTerminals(
            [Description("Optional DevSpace/worktree path to filter by.")] string devSpaceId = null)
        {
            return WriteJson(writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("terminals");
                writer.WriteStartArray();

                foreach (var session in _registry.GetSessions(devSpaceId))
                    WriteTerminalSummary(writer, session);

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
        }

        [McpServerTool(Name = "sourcegit_terminal_status")]
        [Description("Returns status and retained transcript sequence information for a SourceGit DevSpace terminal.")]
        public string TerminalStatus(
            [Description("SourceGit terminal session ID.")] string terminalId)
        {
            if (!TryGetTerminal(terminalId, out var session))
                return TerminalNotFound(terminalId);

            var retained = session.Transcript.Tail(1);
            return WriteJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("terminalId", session.Id.ToString());
                writer.WriteString("devSpaceId", session.DevSpaceId);
                writer.WriteString("title", session.Title);
                writer.WriteString("workingDirectory", session.WorkingDirectory);
                writer.WriteString("backend", session.BackendName);
                writer.WriteString("state", session.State.ToString());
                writer.WriteBoolean("running", session.State == DevSpaceTerminalState.Running);
                if (session.State == DevSpaceTerminalState.Exited)
                    writer.WriteNumber("exitCode", session.ExitCode);
                else
                    writer.WriteNull("exitCode");
                writer.WriteNumber("oldestSequence", retained.OldestSequence);
                writer.WriteNumber("latestSequence", retained.NextSequence);
                writer.WriteEndObject();
            });
        }

        [McpServerTool(Name = "sourcegit_terminal_tail")]
        [Description("Returns recent output retained for a SourceGit DevSpace terminal without exposing terminal input.")]
        public string TerminalTail(
            [Description("SourceGit terminal session ID.")] string terminalId,
            [Description("Maximum number of recent transcript events to read.")] int lines = 200)
        {
            if (!_options.ShareDevSpaceTerminalOutput)
                return Error("terminal_output_sharing_disabled");
            if (!TryGetTerminal(terminalId, out var session))
                return TerminalNotFound(terminalId);
            if (lines <= 0)
                return Error("invalid_lines");

            var read = session.Transcript.Tail(Math.Min(lines, 1000));
            return TerminalReadResponse(session, read);
        }

        [McpServerTool(Name = "sourcegit_terminal_read")]
        [Description("Incrementally reads retained SourceGit DevSpace terminal output after an optional sequence cursor.")]
        public string TerminalRead(
            [Description("SourceGit terminal session ID.")] string terminalId,
            [Description("Only return transcript events newer than this sequence number.")] long? afterSequence = null,
            [Description("Maximum UTF-8 output bytes to return, capped at 65536.")] int maxBytes = TerminalTranscriptStore.MaximumReadBytes)
        {
            if (!_options.ShareDevSpaceTerminalOutput)
                return Error("terminal_output_sharing_disabled");
            if (!TryGetTerminal(terminalId, out var session))
                return TerminalNotFound(terminalId);
            if (afterSequence < 0)
                return Error("invalid_cursor");
            if (maxBytes <= 0)
                return Error("invalid_max_bytes");

            var read = session.Transcript.Read(afterSequence, Math.Min(maxBytes, TerminalTranscriptStore.MaximumReadBytes));
            return TerminalReadResponse(session, read);
        }

        private bool TryGetTerminal(string terminalId, out DevSpaceTerminal session)
        {
            if (Guid.TryParse(terminalId, out var id) && _registry.TryGet(id, out session))
                return true;

            session = null;
            return false;
        }

        private static string TerminalNotFound(string terminalId)
        {
            return WriteJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("error", "terminal_not_found");
                writer.WriteString("terminalId", terminalId ?? string.Empty);
                writer.WriteEndObject();
            });
        }

        private static string Error(string error)
        {
            return WriteJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("error", error);
                writer.WriteEndObject();
            });
        }

        private static string TerminalReadResponse(DevSpaceTerminal session, TerminalReadResult read)
        {
            var output = new StringBuilder();
            foreach (var item in read.Events)
            {
                if (item.Kind == TerminalEventKind.Output)
                    output.Append(item.Text);
            }

            return WriteJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("terminalId", session.Id.ToString());
                writer.WriteBoolean("running", session.State == DevSpaceTerminalState.Running);
                writer.WriteString("backend", session.BackendName);
                writer.WriteString("workingDirectory", session.WorkingDirectory);
                writer.WriteBoolean("truncated", read.Truncated);
                writer.WriteNumber("oldestSequence", read.OldestSequence);
                writer.WriteNumber("nextSequence", read.NextSequence);
                writer.WriteString("output", output.ToString());
                writer.WriteEndObject();
            });
        }

        private static void WriteTerminalSummary(Utf8JsonWriter writer, DevSpaceTerminal session)
        {
            writer.WriteStartObject();
            writer.WriteString("id", session.Id.ToString());
            writer.WriteString("devSpaceId", session.DevSpaceId);
            writer.WriteString("title", session.Title);
            writer.WriteString("workingDirectory", session.WorkingDirectory);
            writer.WriteString("backend", session.BackendName);
            writer.WriteString("state", session.State.ToString());
            writer.WriteBoolean("running", session.State == DevSpaceTerminalState.Running);
            writer.WriteEndObject();
        }

        private static string WriteJson(Action<Utf8JsonWriter> write)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                write(writer);
                writer.Flush();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        private readonly DevSpaceTerminalRegistry _registry;
        private readonly SourceGitMcpOptions _options;
    }
}
