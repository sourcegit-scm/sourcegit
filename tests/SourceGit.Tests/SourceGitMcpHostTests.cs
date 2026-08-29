using System.Threading.Tasks;

using ModelContextProtocol.AspNetCore;

using SourceGit.DevSpaces.Terminal;
using SourceGit.Mcp;
using Xunit;

namespace SourceGit.Tests;

public class SourceGitMcpHostTests
{
    [Fact]
    public void Address_helpers_are_loopback_only_and_expose_sse_endpoint()
    {
        var options = new SourceGitMcpOptions
        {
            Port = 53921,
            AuthToken = "secret",
        };

        Assert.Equal("http://127.0.0.1:53921", SourceGitMcpHost.GetBaseAddress(options));
        Assert.Equal("http://127.0.0.1:53921/sse", SourceGitMcpHost.GetSseEndpoint(options));
    }

    [Fact]
    public void ConfigureTransport_enables_stateful_legacy_sse()
    {
        var options = new HttpServerTransportOptions();

        SourceGitMcpHost.ConfigureTransport(options);

        Assert.Equal(HttpServerSessionMode.Stateful, options.SessionMode);
#pragma warning disable MCP9004
        Assert.True(options.EnableLegacySse);
#pragma warning restore MCP9004
    }

    [Theory]
    [InlineData("Bearer secret", true)]
    [InlineData("secret", false)]
    [InlineData("Bearer wrong", false)]
    [InlineData("", false)]
    public void Authorization_requires_exact_bearer_token(string authorization, bool expected)
    {
        var options = new SourceGitMcpOptions { AuthToken = "secret" };

        Assert.Equal(expected, SourceGitMcpHost.IsAuthorized(options, authorization));
    }

    [Fact]
    public async Task Startup_failure_is_reported_without_throwing()
    {
        await using var host = new SourceGitMcpHost(new DevSpaceTerminalRegistry());
        var options = new SourceGitMcpOptions
        {
            Port = -1,
            AuthToken = "secret",
        };

        var started = await host.StartAsync(options);

        Assert.False(started);
        Assert.False(host.IsRunning);
        Assert.False(string.IsNullOrWhiteSpace(host.LastError));
    }

    [Fact]
    public async Task Host_can_start_on_dynamic_loopback_port_and_stop_cleanly()
    {
        await using var host = new SourceGitMcpHost(new DevSpaceTerminalRegistry());
        var options = new SourceGitMcpOptions
        {
            Port = 0,
            AuthToken = "secret",
        };

        var started = await host.StartAsync(options);

        Assert.True(started, host.LastError);
        Assert.True(host.IsRunning);
        Assert.StartsWith("http://127.0.0.1:", host.BaseAddress);
        Assert.EndsWith("/sse", host.SseEndpoint);

        await host.StopAsync();
        Assert.False(host.IsRunning);
    }
}
