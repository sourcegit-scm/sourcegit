using System;
using System.Text;
using System.Text.Json;

using DevBoard.DevSpaces.Terminal;
using DevBoard.Mcp;
using DevBoard.ViewModels;
using Xunit;

namespace DevBoard.Tests;

public class DevBoardMcpToolsTests
{
    [Fact]
    public void ListDevSpaces_returns_registered_devspaces()
    {
        var registry = new DevSpaceTerminalRegistry();
        registry.Register(CreateSession(@"C:\repo-a"));
        registry.Register(CreateSession(@"C:\repo-b"));
        var tools = CreateTools(registry);

        using var json = JsonDocument.Parse(tools.ListDevSpaces());
        var devSpaces = json.RootElement.GetProperty("devSpaces");

        Assert.Equal(2, devSpaces.GetArrayLength());
        Assert.Equal(@"C:\repo-a", devSpaces[0].GetProperty("id").GetString());
        Assert.Equal(1, devSpaces[0].GetProperty("terminalCount").GetInt32());
    }

    [Fact]
    public void ListTerminals_filters_by_devspace_and_returns_status()
    {
        var registry = new DevSpaceTerminalRegistry();
        var session = CreateSession(@"C:\repo-a");
        session.MarkRunning("Windows Terminal");
        registry.Register(session);
        registry.Register(CreateSession(@"C:\repo-b"));
        var tools = CreateTools(registry);

        using var json = JsonDocument.Parse(tools.ListTerminals(@"C:\repo-a"));
        var terminals = json.RootElement.GetProperty("terminals");

        var item = terminals[0];
        Assert.Equal(1, terminals.GetArrayLength());
        Assert.Equal(session.Id.ToString(), item.GetProperty("id").GetString());
        Assert.Equal("Windows Terminal", item.GetProperty("backend").GetString());
        Assert.Equal("Running", item.GetProperty("state").GetString());
    }

    [Fact]
    public void TerminalTail_returns_recent_output_and_cursor()
    {
        var registry = new DevSpaceTerminalRegistry();
        var session = CreateSession(@"C:\repo-a");
        session.MarkRunning("Windows Terminal");
        session.Transcript.AppendOutput("one\n");
        session.Transcript.AppendOutput("two\n");
        registry.Register(session);
        var tools = CreateTools(registry);

        using var json = JsonDocument.Parse(tools.TerminalTail(session.Id.ToString(), 200));
        var root = json.RootElement;

        Assert.Equal("one\ntwo\n", root.GetProperty("output").GetString());
        Assert.Equal(2, root.GetProperty("nextSequence").GetInt64());
        Assert.True(root.GetProperty("running").GetBoolean());
    }

    [Fact]
    public void TerminalRead_returns_only_output_after_cursor()
    {
        var registry = new DevSpaceTerminalRegistry();
        var session = CreateSession(@"C:\repo-a");
        var first = session.Transcript.AppendOutput("old\n");
        session.Transcript.AppendOutput("new\n");
        registry.Register(session);
        var tools = CreateTools(registry);

        using var json = JsonDocument.Parse(tools.TerminalRead(session.Id.ToString(), first, 32768));
        var root = json.RootElement;

        Assert.Equal("new\n", root.GetProperty("output").GetString());
        Assert.Equal(2, root.GetProperty("nextSequence").GetInt64());
        Assert.False(root.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public void TerminalRead_never_returns_more_than_64_kib_of_output()
    {
        var registry = new DevSpaceTerminalRegistry();
        var session = CreateSession(@"C:\repo-a");
        for (var i = 0; i < 100; i++)
            session.Transcript.AppendOutput(new string('x', 1024));
        registry.Register(session);
        var tools = CreateTools(registry);

        using var json = JsonDocument.Parse(tools.TerminalRead(session.Id.ToString(), null, 1024 * 1024));
        var output = json.RootElement.GetProperty("output").GetString() ?? string.Empty;

        Assert.True(Encoding.UTF8.GetByteCount(output) <= TerminalTranscriptStore.MaximumReadBytes);
    }

    [Fact]
    public void TerminalRead_returns_structured_error_when_output_sharing_is_disabled()
    {
        var registry = new DevSpaceTerminalRegistry();
        var session = CreateSession(@"C:\repo-a");
        registry.Register(session);
        var tools = new DevBoardMcpTools(registry, new DevBoardMcpOptions
        {
            ShareDevSpaceTerminalOutput = false,
        });

        using var json = JsonDocument.Parse(tools.TerminalRead(session.Id.ToString()));

        Assert.Equal(
            "terminal_output_sharing_disabled",
            json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void TerminalStatus_returns_structured_error_for_unknown_terminal()
    {
        var tools = CreateTools(new DevSpaceTerminalRegistry());
        var id = Guid.NewGuid().ToString();

        using var json = JsonDocument.Parse(tools.TerminalStatus(id));

        Assert.Equal("terminal_not_found", json.RootElement.GetProperty("error").GetString());
        Assert.Equal(id, json.RootElement.GetProperty("terminalId").GetString());
    }

    private static DevBoardMcpTools CreateTools(DevSpaceTerminalRegistry registry)
    {
        return new DevBoardMcpTools(registry, new DevBoardMcpOptions
        {
            ShareDevSpaceTerminalOutput = true,
        });
    }

    private static DevSpaceTerminal CreateSession(string devSpaceId)
    {
        return new DevSpaceTerminal(
            "PowerShell 1",
            "pwsh",
            devSpaceId,
            devSpaceId: devSpaceId);
    }
}
