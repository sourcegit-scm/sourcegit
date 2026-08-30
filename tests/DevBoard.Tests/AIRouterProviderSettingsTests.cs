using System.IO;
using System.Text;
using System.Threading.Tasks;

using DevBoard.AI.Routing;

using Xunit;

namespace DevBoard.Tests;

public class AIRouterProviderSettingsTests
{
    [Fact]
    public async Task ExportAsync_ExcludesSecretsByDefault()
    {
        var provider = new AIRouterProviderSettings
        {
            Id = "openrouter",
            Name = "OpenRouter",
            BaseUrl = "https://openrouter.ai/api/v1",
            ApiKey = "secret-value",
            ApiKeyEnvironment = "OPENROUTER_API_KEY",
            DefaultModel = "anthropic/claude-sonnet-4.6",
        };

        await using var stream = new MemoryStream();
        await AIRouterProviderExchange.ExportAsync([provider], stream);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("\"version\": 1", json);
        Assert.Contains("openrouter", json);
        Assert.Contains("OPENROUTER_API_KEY", json);
        Assert.DoesNotContain("secret-value", json);
    }

    [Fact]
    public async Task ExportAsync_CanIncludeSecretsExplicitly()
    {
        var provider = new AIRouterProviderSettings
        {
            Id = "openai",
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "secret-value",
        };

        await using var stream = new MemoryStream();
        await AIRouterProviderExchange.ExportAsync([provider], stream, includeSecrets: true);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("secret-value", json);
    }

    [Fact]
    public async Task ImportAsync_ReadsVersionOneProviders()
    {
        const string json = """
        {
          "version": 1,
          "providers": [
            {
              "id": "local",
              "name": "LocalLLM",
              "baseUrl": "http://127.0.0.1:5032/v1",
              "defaultModel": "qwen3-coder",
              "priority": 10,
              "maxRetries": 1,
              "timeoutSeconds": 60,
              "isActive": true
            }
          ]
        }
        """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var providers = await AIRouterProviderExchange.ImportAsync(stream);

        var provider = Assert.Single(providers);
        Assert.Equal("local", provider.Id);
        Assert.Equal("LocalLLM", provider.Name);
        Assert.Equal("qwen3-coder", provider.DefaultModel);
        Assert.Equal(10, provider.Priority);
    }

    [Fact]
    public async Task ImportAsync_ReadsAIStudioProviderArray()
    {
        const string json = """
        [
          {
            "providerId": "opencode",
            "name": "OpenCode",
            "baseUrl": "https://opencode.ai/zen/v1",
            "apiKey": "",
            "priority": 10,
            "mode": "fallback",
            "models": "[\"deepseek-v4-flash-free\"]",
            "isActive": true,
            "extraHeaders": ""
          }
        ]
        """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var providers = await AIRouterProviderExchange.ImportAsync(stream);

        var provider = Assert.Single(providers);
        Assert.Equal("opencode", provider.Id);
        Assert.Equal("OpenCode", provider.Name);
        Assert.Equal("https://opencode.ai/zen/v1", provider.BaseUrl);
        Assert.Equal(["deepseek-v4-flash-free"], provider.Models);
        Assert.Equal("deepseek-v4-flash-free", provider.DefaultModel);
        Assert.Equal(10, provider.Priority);
        Assert.True(provider.IsActive);
        Assert.Empty(provider.ExtraHeaders);
    }

    [Fact]
    public async Task ImportAsync_RejectsUnsupportedVersion()
    {
        const string json = "{\"version\":2,\"providers\":[]}";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<InvalidDataException>(() => AIRouterProviderExchange.ImportAsync(stream));
    }

    [Fact]
    public void Clone_WithNewId_DoesNotReuseProviderId()
    {
        var provider = new AIRouterProviderSettings { Id = "openrouter", Name = "OpenRouter" };

        var copy = provider.Clone(createNewId: true);

        Assert.NotEqual(provider.Id, copy.Id);
        Assert.Equal(provider.Name, copy.Name);
    }
}
