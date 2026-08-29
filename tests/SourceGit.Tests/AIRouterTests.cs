using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SourceGit.AI.Routing;
using Xunit;

namespace SourceGit.Tests;

public class AIRouterTests
{
    [Fact]
    public async Task RouteAsync_FallsBackToNextProvider_WhenFirstProviderFails()
    {
        var first = new StubProvider("first", false, 503);
        var second = new StubProvider("second", true, 200);
        var router = new AIRouter([first, second]);

        var result = await router.RouteAsync(new AIRouterRequest("all", "{}"));

        Assert.True(result.Success);
        Assert.Equal("second", result.ProviderId);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task RouteAsync_UsesExplicitProviderAndModel()
    {
        var first = new StubProvider("openai", true, 200);
        var second = new StubProvider("openrouter", true, 200);
        var router = new AIRouter([first, second]);

        var result = await router.RouteAsync(new AIRouterRequest("openrouter/anthropic/claude-sonnet", "{}"));

        Assert.True(result.Success);
        Assert.Equal("openrouter", result.ProviderId);
        Assert.Equal("anthropic/claude-sonnet", second.LastModel);
        Assert.Equal(0, first.Calls);
    }

    [Fact]
    public async Task RouteAsync_DoesNotFallbackForClientErrors()
    {
        var first = new StubProvider("first", false, 400);
        var second = new StubProvider("second", true, 200);
        var router = new AIRouter([first, second]);

        var result = await router.RouteAsync(new AIRouterRequest("all", "{}"));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, second.Calls);
    }

    private sealed class StubProvider : IAIProvider
    {
        private readonly bool _success;
        private readonly int _statusCode;

        public StubProvider(string id, bool success, int statusCode)
        {
            Id = id;
            _success = success;
            _statusCode = statusCode;
        }

        public string Id { get; }
        public int Calls { get; private set; }
        public string LastModel { get; private set; } = string.Empty;

        public Task<AIRouterResult> SendAsync(AIRouterRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastModel = request.Model;
            return Task.FromResult(new AIRouterResult(_success, _statusCode, Id, request.Model, _success ? "{}" : null));
        }
    }
}
