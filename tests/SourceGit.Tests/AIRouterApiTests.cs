using SourceGit.AI.Hosting;
using Xunit;

namespace SourceGit.Tests;

public class AIRouterApiTests
{
    [Theory]
    [InlineData("/v1/chat/completions")]
    [InlineData("/v1/responses")]
    [InlineData("/v1/response")]
    public void IsCompletionEndpoint_AcceptsSupportedOpenAIPaths(string path)
    {
        Assert.True(AIRouterApi.IsCompletionEndpoint(path));
    }

    [Fact]
    public void GetModel_ReadsModelWithoutChangingPayload()
    {
        const string payload = "{\"model\":\"coding\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}";

        var model = AIRouterApi.GetModel(payload);

        Assert.Equal("coding", model);
        Assert.Contains("\"messages\"", payload);
    }

    [Theory]
    [InlineData("Bearer router-key", "router-key", true)]
    [InlineData("router-key", "router-key", true)]
    [InlineData("Bearer wrong", "router-key", false)]
    [InlineData(null, "router-key", false)]
    public void IsAuthorized_AcceptsBearerOrRawLocalKey(string authorization, string apiKey, bool expected)
    {
        Assert.Equal(expected, AIRouterApi.IsAuthorized(authorization, apiKey));
    }
}
