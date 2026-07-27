using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Policies;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

public sealed record ScoredRoute(ModelProviderMapping Mapping, decimal Score);

/// <summary>Pure balanced scorer plus affinity-aware candidate ordering.</summary>
public static class BalancedRouteScorer
{
    public static IReadOnlyList<ScoredRoute> Score(
        IEnumerable<ModelProviderMapping> mappings,
        ModelRoutePolicy policy)
    {
        var eligible = mappings.Where(mapping => mapping.IsEnabled && mapping.Provider?.IsEnabled == true &&
            mapping.ModelProviderTypeAssociation?.IsEnabled == true && RouteCircuitRegistry.IsAvailable(mapping.Id)).ToList();
        if (eligible.Count == 0) return [];

        var prices = eligible.ToDictionary(mapping => mapping.Id, mapping => EffectivePrice(mapping));
        var speeds = eligible.ToDictionary(mapping => mapping.Id, mapping => mapping.ModelProviderTypeAssociation?.SpeedScore);
        var qualities = eligible.ToDictionary(mapping => mapping.Id, mapping => mapping.ModelProviderTypeAssociation?.QualityScore);

        return eligible.Select(mapping => new ScoredRoute(mapping,
                (policy.CostWeight * Normalize(prices, mapping.Id, invert: true) +
                 policy.SpeedWeight * Normalize(speeds, mapping.Id, invert: false) +
                 policy.QualityWeight * Normalize(qualities, mapping.Id, invert: false)) * mapping.RoutingWeight))
            .OrderByDescending(route => route.Score)
            .ThenBy(route => route.Mapping.RoutingPriority)
            .ThenBy(route => route.Mapping.Id)
            .ToArray();
    }

    private static decimal? EffectivePrice(ModelProviderMapping mapping)
    {
        var cost = mapping.ModelProviderTypeAssociation?.ModelCost;
        return cost is null ? null : cost.InputCostPerMillionTokens + 0.25m * cost.OutputCostPerMillionTokens;
    }

    private static decimal Normalize(IReadOnlyDictionary<int, decimal?> values, int id, bool invert)
    {
        if (values[id] is not decimal value) return 0.5m;
        var configured = values.Values.Where(item => item.HasValue).Select(item => item!.Value).ToArray();
        if (configured.Length < 2 || configured.Max() == configured.Min()) return 0.5m;
        var normalized = (value - configured.Min()) / (configured.Max() - configured.Min());
        return invert ? 1m - normalized : normalized;
    }
}

internal static class RouteCircuitRegistry
{
    private static readonly TimeSpan ProbeClaimTimeout = TimeSpan.FromSeconds(30);

    private sealed class State
    {
        public readonly Queue<DateTime> Failures = new();
        public DateTime? OpenUntil;
        public Guid? ProbeClaimToken;
        public DateTime ProbeClaimExpiresAt;
    }
    private static readonly ConcurrentDictionary<int, State> States = new();

    public static bool IsAvailable(int mappingId)
    {
        if (!States.TryGetValue(mappingId, out var state)) return true;

        lock (state)
        {
            if (state.OpenUntil is null) return true;
            if (state.OpenUntil > DateTime.UtcNow) return false;
            return state.ProbeClaimToken is null || state.ProbeClaimExpiresAt <= DateTime.UtcNow;
        }
    }

    public static bool TryBeginAttempt(int mappingId, out Guid? probeClaimToken) =>
        TryBeginAttempt(mappingId, DateTime.UtcNow, out probeClaimToken);

    internal static bool TryBeginAttempt(int mappingId, DateTime attemptedAt, out Guid? probeClaimToken)
    {
        probeClaimToken = null;
        if (!States.TryGetValue(mappingId, out var state)) return true;

        lock (state)
        {
            if (state.OpenUntil is null) return true;
            if (state.OpenUntil > attemptedAt) return false;
            if (state.ProbeClaimToken is not null && state.ProbeClaimExpiresAt > attemptedAt) return false;

            probeClaimToken = Guid.NewGuid();
            state.ProbeClaimToken = probeClaimToken;
            state.ProbeClaimExpiresAt = attemptedAt.Add(ProbeClaimTimeout);
            return true;
        }
    }

    public static void ReleaseProbe(int mappingId, Guid? probeClaimToken)
    {
        if (probeClaimToken is null || !States.TryGetValue(mappingId, out var state)) return;

        lock (state)
        {
            if (state.ProbeClaimToken != probeClaimToken) return;
            state.ProbeClaimToken = null;
            state.ProbeClaimExpiresAt = default;
        }
    }

    public static void Success(int mappingId) => States.TryRemove(mappingId, out _);

    public static void Failure(int mappingId) => Failure(mappingId, DateTime.UtcNow);

    internal static void Failure(int mappingId, DateTime failedAt)
    {
        var state = States.GetOrAdd(mappingId, _ => new State());
        lock (state)
        {
            var cutoff = failedAt.AddSeconds(-60);
            while (state.Failures.TryPeek(out var failure) && failure < cutoff) state.Failures.Dequeue();
            state.Failures.Enqueue(failedAt);
            if (state.ProbeClaimToken is not null || state.Failures.Count >= 5)
            {
                state.OpenUntil = failedAt.AddSeconds(30);
                state.ProbeClaimToken = null;
                state.ProbeClaimExpiresAt = default;
            }
        }
    }
}

internal sealed class RoutedChatClient : ILLMClient
{
    private readonly IReadOnlyList<(ModelProviderMapping Mapping, ILLMClient Client)> _routes;
    private readonly ChatCompletionRequest _request;
    private readonly IDistributedCache? _cache;
    private readonly ModelRoutePolicy _policy;
    private readonly ILogger? _logger;

    public RoutedChatClient(IReadOnlyList<(ModelProviderMapping, ILLMClient)> routes, ChatCompletionRequest request,
        IDistributedCache? cache, ModelRoutePolicy policy, ILogger? logger = null)
    {
        _routes = routes; _request = request; _cache = cache; _policy = policy; _logger = logger;
    }

    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request, string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        Exception? last = null;
        foreach (var (mapping, client) in _routes)
        {
            if (!TryBeginAttempt(mapping, out var probeClaimToken)) continue;

            try
            {
                Bind(mapping);
                var response = await client.CreateChatCompletionAsync(request, apiKey, cancellationToken);
                await SuccessAsync(mapping, cancellationToken);
                return response;
            }
            catch (Exception ex) when (TransientErrorPolicy.IsTransient(ex, cancellationToken))
            {
                RouteCircuitRegistry.Failure(mapping.Id); _request.RoutingFailoverCount++; last = ex;
            }
            finally { RouteCircuitRegistry.ReleaseProbe(mapping.Id, probeClaimToken); }
        }
        throw last ?? new ServiceUnavailableException($"No eligible provider route for '{request.Model}'.", "Routing");
    }

    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(ChatCompletionRequest request,
        string? apiKey = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Exception? last = null;
        foreach (var (mapping, client) in _routes)
        {
            if (!TryBeginAttempt(mapping, out var probeClaimToken)) continue;

            try
            {
                Bind(mapping);
                var enumerator = client.StreamChatCompletionAsync(request, apiKey, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                bool hasFirst;
                try
                {
                    hasFirst = await enumerator.MoveNextAsync();
                }
                catch (Exception ex) when (TransientErrorPolicy.IsTransient(ex, cancellationToken))
                {
                    await enumerator.DisposeAsync();
                    RouteCircuitRegistry.Failure(mapping.Id); _request.RoutingFailoverCount++; last = ex;
                    continue;
                }

                if (!hasFirst)
                {
                    try { await SuccessAsync(mapping, cancellationToken); }
                    finally { await enumerator.DisposeAsync(); }
                    yield break;
                }

                try
                {
                    yield return enumerator.Current;
                    while (true)
                    {
                        bool hasNext;
                        try
                        {
                            hasNext = await enumerator.MoveNextAsync();
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException ||
                            !cancellationToken.IsCancellationRequested)
                        {
                            RouteCircuitRegistry.Failure(mapping.Id);
                            throw;
                        }

                        if (!hasNext) break;
                        yield return enumerator.Current;
                    }

                    await SuccessAsync(mapping, cancellationToken);
                }
                finally { await enumerator.DisposeAsync(); }
                yield break;
            }
            finally { RouteCircuitRegistry.ReleaseProbe(mapping.Id, probeClaimToken); }
        }
        throw last ?? new ServiceUnavailableException($"No eligible provider route for '{request.Model}'.", "Routing");
    }

    private bool TryBeginAttempt(ModelProviderMapping mapping, out Guid? probeClaimToken)
    {
        if (RouteCircuitRegistry.TryBeginAttempt(mapping.Id, out probeClaimToken)) return true;
        _logger?.LogDebug(
            "Skipping provider mapping {MappingId} for model {ModelAlias} because its route circuit is open or a recovery probe is already in progress",
            mapping.Id,
            _request.Model);
        return false;
    }

    private void Bind(ModelProviderMapping mapping) => _request.SelectedMappingId = mapping.Id;
    private async Task SuccessAsync(ModelProviderMapping mapping, CancellationToken cancellationToken)
    {
        RouteCircuitRegistry.Success(mapping.Id);
        if (_cache is not null && _policy.CacheAffinityEnabled && !string.IsNullOrWhiteSpace(_request.RoutingAffinityKey))
            await _cache.SetStringAsync(AffinityCacheKey(_request.Model, _request.RoutingAffinityKey), mapping.Id.ToString(),
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(_policy.AffinityTtlSeconds) }, cancellationToken);
    }
    internal static string AffinityCacheKey(string alias, string key) => $"routing:affinity:{alias}:{key}";
    public Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default) => _routes[0].Client.ListModelsAsync(apiKey, cancellationToken);
    public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default) => _routes[0].Client.CreateEmbeddingAsync(request, apiKey, cancellationToken);
    public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default) => _routes[0].Client.CreateImageAsync(request, apiKey, cancellationToken);
    public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null) => _routes[0].Client.GetCapabilitiesAsync(modelId);
}
