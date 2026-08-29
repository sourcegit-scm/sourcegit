using SourceGit.AI.Hosting;
using Xunit;

namespace SourceGit.Tests;

public class AIRouterHostOptionsTests
{
    [Fact]
    public void ListenUrl_DefaultsToLoopbackOnly()
    {
        var options = new AIRouterHostOptions();

        Assert.Equal("http://127.0.0.1:11435", options.ListenUrl);
    }

    [Fact]
    public void Validate_RejectsEmptyApiKey()
    {
        var options = new AIRouterHostOptions { ApiKey = "" };

        Assert.Throws<System.InvalidOperationException>(() => options.Validate());
    }
}
