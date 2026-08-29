using SourceGit.Mcp;
using Xunit;

namespace SourceGit.Tests;

public class SourceGitMcpPreferencesTests
{
    [Fact]
    public void Mcp_defaults_are_safe()
    {
        var settings = new SourceGitMcpSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(SourceGitMcpOptions.DefaultPort, settings.Port);
        Assert.True(settings.ShareDevSpaceTerminalOutput);
        Assert.Equal(
            $"http://127.0.0.1:{SourceGitMcpOptions.DefaultPort}/sse",
            settings.Endpoint);
        Assert.Equal(string.Empty, settings.AuthToken);
        Assert.Equal("Stopped", settings.RuntimeStatus);
        Assert.Equal(string.Empty, settings.RuntimeError);
    }

    [Fact]
    public void Enabling_mcp_generates_auth_token_when_missing()
    {
        var settings = new SourceGitMcpSettings();

        settings.Enabled = true;

        Assert.True(settings.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(settings.AuthToken));
        Assert.True(settings.AuthToken.Length >= 32);
    }

    [Fact]
    public void RegenerateAuthToken_replaces_existing_token()
    {
        var settings = new SourceGitMcpSettings();
        settings.Enabled = true;
        var first = settings.AuthToken;

        settings.RegenerateAuthToken();

        Assert.NotEqual(first, settings.AuthToken);
        Assert.True(settings.AuthToken.Length >= 32);
    }

    [Fact]
    public void Runtime_state_exposes_running_endpoint_and_startup_errors()
    {
        var settings = new SourceGitMcpSettings();

        settings.UpdateRuntimeState(true, "http://127.0.0.1:54321/sse", string.Empty);

        Assert.Equal("Running", settings.RuntimeStatus);
        Assert.Equal("http://127.0.0.1:54321/sse", settings.RuntimeEndpoint);
        Assert.Equal(string.Empty, settings.RuntimeError);

        settings.UpdateRuntimeState(false, string.Empty, "Address already in use");

        Assert.Equal("Error", settings.RuntimeStatus);
        Assert.Equal(string.Empty, settings.RuntimeEndpoint);
        Assert.Equal("Address already in use", settings.RuntimeError);
    }

    [Theory]
    [InlineData(nameof(SourceGitMcpSettings.Enabled), true)]
    [InlineData(nameof(SourceGitMcpSettings.Port), true)]
    [InlineData(nameof(SourceGitMcpSettings.ShareDevSpaceTerminalOutput), true)]
    [InlineData(nameof(SourceGitMcpSettings.AuthToken), true)]
    [InlineData(nameof(SourceGitMcpSettings.Endpoint), false)]
    [InlineData(nameof(SourceGitMcpSettings.RuntimeStatus), false)]
    [InlineData(nameof(SourceGitMcpSettings.RuntimeEndpoint), false)]
    [InlineData(nameof(SourceGitMcpSettings.RuntimeError), false)]
    public void Service_only_reapplies_for_configuration_properties(string propertyName, bool expected)
    {
        Assert.Equal(expected, SourceGitMcpService.IsConfigurationProperty(propertyName));
    }

    [Fact]
    public void Service_maps_settings_to_host_options()
    {
        var settings = new SourceGitMcpSettings
        {
            Port = 54321,
            ShareDevSpaceTerminalOutput = false,
            AuthToken = "token",
        };

        var options = SourceGitMcpService.CreateOptions(settings);

        Assert.Equal(54321, options.Port);
        Assert.False(options.ShareDevSpaceTerminalOutput);
        Assert.Equal("token", options.AuthToken);
        Assert.Equal(SourceGitMcpOptions.DefaultMaxConcurrentToolCalls, options.MaxConcurrentToolCalls);
    }
}
