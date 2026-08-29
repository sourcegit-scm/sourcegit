using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.AI.Routing;

public sealed record AIRouterRequest(string Model, string Payload, string Path = null);

public sealed record AIRouterResult(
    bool Success,
    int StatusCode,
    string ProviderId,
    string Model,
    string Payload,
    string Error = null);

public interface IAIProvider
{
    string Id { get; }
    Task<AIRouterResult> SendAsync(AIRouterRequest request, CancellationToken cancellationToken = default);
}

public sealed class AIRouter
{
    public AIRouter(IEnumerable<IAIProvider> providers)
    {
        _providers = providers?.ToArray() ?? [];
    }

    public async Task<AIRouterResult> RouteAsync(AIRouterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (providerId, model, automatic) = Resolve(request.Model);
        var candidates = automatic
            ? _providers
            : _providers.Where(x => string.Equals(x.Id, providerId, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (candidates.Count == 0)
            return new AIRouterResult(false, 400, providerId ?? string.Empty, model, null, $"Unknown AI provider '{providerId}'.");

        AIRouterResult last = null;
        foreach (var provider in candidates)
        {
            var result = await provider.SendAsync(request with { Model = model }, cancellationToken);
            if (result.Success)
                return result;

            last = result;
            if (!IsTransient(result.StatusCode))
                return result;
        }

        return last ?? new AIRouterResult(false, 503, string.Empty, model, null, "No AI providers are available.");
    }

    private static (string ProviderId, string Model, bool Automatic) Resolve(string value)
    {
        var model = value?.Trim() ?? string.Empty;
        if (string.Equals(model, "all", StringComparison.OrdinalIgnoreCase))
            return (null, model, true);

        var slash = model.IndexOf('/');
        if (slash < 0)
            return (model, string.Empty, false);

        return (model[..slash], model[(slash + 1)..], false);
    }

    private static bool IsTransient(int statusCode) => statusCode == 408 || statusCode == 429 || statusCode >= 500;

    private readonly IReadOnlyList<IAIProvider> _providers;
}
