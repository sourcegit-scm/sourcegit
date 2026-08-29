using System;
using System.Linq;

using DevBoard.DevSpaces.Terminal;
using DevBoard.ViewModels;
using Xunit;

namespace DevBoard.Tests;

public class DevSpaceTerminalRegistryTests
{
    [Fact]
    public void Register_and_lookup_session_by_id()
    {
        var registry = new DevSpaceTerminalRegistry();
        var session = CreateSession("C:\\repo-a", "C:\\repo-a");

        registry.Register(session);

        Assert.True(registry.TryGet(session.Id, out var found));
        Assert.Same(session, found);
    }

    [Fact]
    public void GetSessions_isolates_terminals_by_devspace()
    {
        var registry = new DevSpaceTerminalRegistry();
        var first = CreateSession("C:\\repo-a", "C:\\repo-a");
        var second = CreateSession("C:\\repo-b", "C:\\repo-b");
        registry.Register(first);
        registry.Register(second);

        var sessions = registry.GetSessions("C:\\repo-a");

        Assert.Single(sessions);
        Assert.Same(first, sessions[0]);
    }

    [Fact]
    public void GetDevSpaces_returns_distinct_registered_devspaces()
    {
        var registry = new DevSpaceTerminalRegistry();
        registry.Register(CreateSession("C:\\repo-a", "C:\\repo-a"));
        registry.Register(CreateSession("C:\\repo-a", "C:\\repo-a\\src"));
        registry.Register(CreateSession("C:\\repo-b", "C:\\repo-b"));

        var devSpaces = registry.GetDevSpaces();

        Assert.Equal(2, devSpaces.Count);
        Assert.Contains("C:\\repo-a", devSpaces);
        Assert.Contains("C:\\repo-b", devSpaces);
    }

    [Fact]
    public void Unregister_removes_only_target_session()
    {
        var registry = new DevSpaceTerminalRegistry();
        var first = CreateSession("C:\\repo-a", "C:\\repo-a");
        var second = CreateSession("C:\\repo-a", "C:\\repo-a\\src");
        registry.Register(first);
        registry.Register(second);

        Assert.True(registry.Unregister(first.Id));

        Assert.False(registry.TryGet(first.Id, out _));
        Assert.True(registry.TryGet(second.Id, out _));
        Assert.Single(registry.GetSessions("C:\\repo-a"));
    }

    [Fact]
    public void Session_exposes_owning_devspace_and_transcript()
    {
        var session = CreateSession("C:\\repo-a", "C:\\repo-a\\src");

        Assert.Equal("C:\\repo-a", session.DevSpaceId);
        Assert.NotNull(session.Transcript);
        Assert.NotSame(session.Transcript, CreateSession("C:\\repo-a", "C:\\repo-a").Transcript);
    }

    [Fact]
    public void Windows_devspace_matching_is_case_insensitive()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var registry = new DevSpaceTerminalRegistry();
        var session = CreateSession("C:\\Repo-A", "C:\\Repo-A");
        registry.Register(session);

        var sessions = registry.GetSessions("c:\\repo-a");

        Assert.Single(sessions);
        Assert.Same(session, sessions.Single());
    }

    private static DevSpaceTerminal CreateSession(string devSpaceId, string workingDirectory)
    {
        return new DevSpaceTerminal(
            "PowerShell 1",
            "powershell",
            workingDirectory,
            startupCommand: null,
            devSpaceId: devSpaceId);
    }
}
