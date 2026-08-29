using System;
using System.Collections.Generic;

namespace SourceGit.DevSpaces;

public static class DevSpaceAIRouterEnvironment
{
    public static IReadOnlyDictionary<string, string> Build(string baseUrl, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("AI router base URL must not be empty.", nameof(baseUrl));

        var normalized = baseUrl.Trim().TrimEnd('/');
        if (!normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            normalized += "/v1";

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPENAI_BASE_URL"] = normalized,
            ["OPENAI_API_KEY"] = apiKey ?? string.Empty,
            ["OPENAI_MODEL"] = string.IsNullOrWhiteSpace(model) ? "all" : model.Trim(),
        };
    }
}
