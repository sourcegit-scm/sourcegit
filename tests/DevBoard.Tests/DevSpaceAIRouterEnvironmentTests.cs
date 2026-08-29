using DevBoard.DevSpaces;
using Xunit;

namespace DevBoard.Tests;

public class DevSpaceAIRouterEnvironmentTests
{
    [Fact]
    public void Build_CreatesOpenAICompatibleEnvironment()
    {
        var environment = DevSpaceAIRouterEnvironment.Build("http://127.0.0.1:11435", "router-key", "coding");

        Assert.Equal("http://127.0.0.1:11435/v1", environment["OPENAI_BASE_URL"]);
        Assert.Equal("router-key", environment["OPENAI_API_KEY"]);
        Assert.Equal("coding", environment["OPENAI_MODEL"]);
    }

    [Fact]
    public void Build_DoesNotDuplicateV1Suffix()
    {
        var environment = DevSpaceAIRouterEnvironment.Build("http://127.0.0.1:11435/v1/", "key", "all");

        Assert.Equal("http://127.0.0.1:11435/v1", environment["OPENAI_BASE_URL"]);
    }
}
