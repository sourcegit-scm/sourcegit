using System;
using System.Text.Json;

namespace SourceGit.AI.Hosting;

public static class AIRouterApi
{
    public const string ChatCompletionsPath = "/v1/chat/completions";
    public const string ResponsesPath = "/v1/responses";
    public const string ResponseAliasPath = "/v1/response";

    public static bool IsCompletionEndpoint(string path) =>
        string.Equals(path, ChatCompletionsPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, ResponsesPath, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, ResponseAliasPath, StringComparison.OrdinalIgnoreCase);

    public static string GetModel(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "all";

        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("model", out var model) &&
            model.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(model.GetString()))
            return model.GetString();

        return "all";
    }

    public static bool IsAuthorized(string authorization, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(authorization))
            return false;

        const string bearer = "Bearer ";
        var supplied = authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearer.Length..].Trim()
            : authorization.Trim();

        return string.Equals(supplied, apiKey, StringComparison.Ordinal);
    }
}
