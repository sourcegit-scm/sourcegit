using SourceGit.Mcp;
using SourceGit.ViewModels;
using Xunit;

namespace SourceGit.Tests;

public class SourceGitMcpPreferencesTests
{
    [Fact]
    public void Mcp_defaults_are_safe()
    {
        var preferences = new Preferences();

        Assert.False(preferences.EnableMcpServer);
        Assert.Equal(SourceGitMcpOptions.DefaultPort, preferences.McpPort);
        Assert.True(preferences.McpShareDevSpaceTerminalOutput);
        Assert.Equal(
            $"http://127.0.0.1:{SourceGitMcpOptions.DefaultPort}/sse",
            preferences.McpEndpoint);
        Assert.Equal(string.Empty, preferences.McpAuthToken);
    }

    [Fact]
    public void Enabling_mcp_generates_auth_token_when_missing()
    {
        var preferences = new Preferences();

        preferences.EnableMcpServer = true;

        Assert.True(preferences.EnableMcpServer);
        Assert.False(string.IsNullOrWhiteSpace(preferences.McpAuthToken));
        Assert.True(preferences.McpAuthToken.Length >= 32);
    }

    [Fact]
    public void RegenerateMcpAuthToken_replaces_existing_token()
    {
        var preferences = new Preferences();
        preferences.EnableMcpServer = true;
        var first = preferences.McpAuthToken;

        preferences.RegenerateMcpAuthToken();

        Assert.NotEqual(first, preferences.McpAuthToken);
        Assert.True(preferences.McpAuthToken.Length >= 32);
    }

    [Fact]
    public void Service_maps_preferences_to_host_options()
    {
        var preferences = new Preferences
        {
            McpPort = 54321,
            McpShareDevSpaceTerminalOutput = false,
            McpAuthToken = "token",
        };

        var options = SourceGitMcpService.CreateOptions(preferences);

        Assert.Equal(54321, options.Port);
        Assert.False(options.ShareDevSpaceTerminalOutput);
        Assert.Equal("token", options.AuthToken);
        Assert.Equal(SourceGitMcpOptions.DefaultMaxConcurrentToolCalls, options.MaxConcurrentToolCalls);
    }
}
