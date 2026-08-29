using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SourceGit.AI.Routing;

public sealed class AIRouterProviderSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyEnvironment { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public List<string> Models { get; set; } = [];
    public int Priority { get; set; } = 100;
    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 120;
    public bool IsActive { get; set; } = true;
    public Dictionary<string, string> ExtraHeaders { get; set; } = [];

    public AIRouterProviderSettings Clone(bool createNewId = false, bool includeSecret = true)
    {
        return new AIRouterProviderSettings
        {
            Id = createNewId ? Guid.NewGuid().ToString("D") : Id,
            Name = Name,
            BaseUrl = BaseUrl,
            ApiKey = includeSecret ? ApiKey : string.Empty,
            ApiKeyEnvironment = ApiKeyEnvironment,
            DefaultModel = DefaultModel,
            Models = [.. Models],
            Priority = Priority,
            MaxRetries = MaxRetries,
            TimeoutSeconds = TimeoutSeconds,
            IsActive = IsActive,
            ExtraHeaders = new Dictionary<string, string>(ExtraHeaders, StringComparer.OrdinalIgnoreCase),
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new ArgumentException("Provider ID must not be empty.");
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Provider name must not be empty.");
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Provider Base URL must be a valid HTTP or HTTPS URL.");
        if (MaxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRetries));
        if (TimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds));
    }
}

public sealed class AIRouterSettings
{
    public static AIRouterSettings Instance => _instance ??= Load();

    public bool Enabled { get; set; } = true;
    public string ListenUrl { get; set; } = "http://127.0.0.1:11435";
    public string ApiKey { get; set; } = Guid.NewGuid().ToString("N");
    public List<AIRouterProviderSettings> Providers { get; set; } = [];

    public void Save()
    {
        try
        {
            var data = new AIRouterSettingsData
            {
                Version = 1,
                Enabled = Enabled,
                ListenUrl = ListenUrl,
                ApiKey = ApiKey,
                Providers = Providers.Select(x => x.Clone()).ToList(),
            };
            var file = Path.Combine(Native.OS.DataDir, "ai-router.json");
            using var stream = File.Create(file);
            JsonSerializer.Serialize(stream, data, AIRouterSettingsJsonContext.Default.AIRouterSettingsData);
        }
        catch
        {
            // Router settings persistence must not prevent SourceGit from running.
        }
    }

    private static AIRouterSettings Load()
    {
        var settings = new AIRouterSettings();
        var file = Path.Combine(Native.OS.DataDir, "ai-router.json");
        if (!File.Exists(file))
            return settings;

        try
        {
            using var stream = File.OpenRead(file);
            var data = JsonSerializer.Deserialize(stream, AIRouterSettingsJsonContext.Default.AIRouterSettingsData);
            if (data == null || data.Version != 1)
                return settings;

            settings.Enabled = data.Enabled;
            settings.ListenUrl = string.IsNullOrWhiteSpace(data.ListenUrl) ? settings.ListenUrl : data.ListenUrl;
            settings.ApiKey = string.IsNullOrWhiteSpace(data.ApiKey) ? settings.ApiKey : data.ApiKey;
            settings.Providers = data.Providers?.Select(x => x.Clone()).ToList() ?? [];
        }
        catch
        {
            // Fall back to safe defaults when persisted settings are invalid.
        }

        return settings;
    }

    private static AIRouterSettings _instance;
}

public static class AIRouterProviderExchange
{
    public static async Task ExportAsync(
        IEnumerable<AIRouterProviderSettings> providers,
        Stream stream,
        bool includeSecrets = false)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(stream);

        var export = new AIRouterProviderExport
        {
            Version = 1,
            Providers = providers.Select(x => x.Clone(includeSecret: includeSecrets)).ToList(),
        };
        await JsonSerializer.SerializeAsync(stream, export, AIRouterSettingsJsonContext.Default.AIRouterProviderExport);
    }

    public static async Task<List<AIRouterProviderSettings>> ImportAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var export = await JsonSerializer.DeserializeAsync(stream, AIRouterSettingsJsonContext.Default.AIRouterProviderExport);
        if (export == null || export.Version != 1)
            throw new InvalidDataException("Unsupported AI Router provider JSON version.");

        var providers = export.Providers ?? [];
        foreach (var provider in providers)
            provider.Validate();

        return providers.Select(x => x.Clone()).ToList();
    }
}

internal sealed class AIRouterSettingsData
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string ListenUrl { get; set; } = "http://127.0.0.1:11435";
    public string ApiKey { get; set; } = string.Empty;
    public List<AIRouterProviderSettings> Providers { get; set; } = [];
}

internal sealed class AIRouterProviderExport
{
    public int Version { get; set; } = 1;
    public List<AIRouterProviderSettings> Providers { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AIRouterSettingsData))]
[JsonSerializable(typeof(AIRouterProviderExport))]
internal partial class AIRouterSettingsJsonContext : JsonSerializerContext
{
}
