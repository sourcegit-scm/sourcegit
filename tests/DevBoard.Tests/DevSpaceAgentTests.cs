using System.Linq;

using DevBoard.DevSpaces;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceAgentTests
{
    [Fact]
    public void BuiltInAgentsContainExpectedCliCommands()
    {
        var agents = DevSpaceAgent.BuiltIn.ToDictionary(x => x.Name, x => x.Command);

        Assert.Equal("copilot", agents["Copilot"]);
        Assert.Equal("codex", agents["Codex"]);
        Assert.Equal("agy", agents["Antigravity"]);
    }

    [Theory]
    [InlineData("Copilot", "copilot")]
    [InlineData("Codex", "codex")]
    [InlineData("Antigravity", "agy")]
    public void BuiltInAgentCanBeResolvedByName(string name, string command)
    {
        var agent = DevSpaceAgent.BuiltIn.Single(x => x.Name == name);

        Assert.Equal(command, agent.Command);
    }
}
