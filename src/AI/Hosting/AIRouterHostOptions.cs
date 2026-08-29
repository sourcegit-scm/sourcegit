using System;

namespace DevBoard.AI.Hosting;

public sealed class AIRouterHostOptions
{
    public bool Enabled { get; set; } = true;
    public string ListenUrl { get; set; } = "http://127.0.0.1:11435";
    public string ApiKey { get; set; } = "devboard-local";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ListenUrl))
            throw new InvalidOperationException("AI Router listen URL must not be empty.");

        if (!Uri.TryCreate(ListenUrl, UriKind.Absolute, out var uri) ||
            !uri.IsLoopback ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI Router must listen on a loopback HTTP URL.");

        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("AI Router API key must not be empty.");
    }
}
