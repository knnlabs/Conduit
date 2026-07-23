using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Caching.Distributed;

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
            mapping.ModelProviderTypeAssociation?.IsEnabled == true && RouteCircuitRegistry.CanAttempt(mapping.Id)).ToList();
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
    private sealed class State
    {
        public readonly Queue<DateTime> Failures = new();
        public DateTime? OpenUntil;
        public bool ProbeClaimed;
    }
    private static readonly ConcurrentDictionary<int, State> States = new();

    public static bool CanAttempt(int mappingId)
    {
        var state = States.GetOrAdd(mappingId, _ => new State());
        lock (state)
        {
            if (state.OpenUntil is null) return true;
            if (state.OpenUntil > DateTime.UtcNow) return false;
            if (state.ProbeClaimed) return false;
            state.ProbeClaimed = true;
            return true;
        }
    }

    public static void Success(int mappingId) => States.TryRemove(mappingId, out _);

    public static void Failure(int mappingId)
    {
        var state = States.GetOrAdd(mappingId, _ => new State());
        lock (state)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-60);
            while (state.Failures.TryPeek(out var failure) && failure < cutoff) state.Failures.Dequeue();
            state.Failures.Enqueue(DateTime.UtcNow);
            if (state.ProbeClaimed || state.Failures.Count >= 5)
            {
                state.OpenUntil = DateTime.UtcNow.AddSeconds(30);
                state.ProbeClaimed = false;
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

    public RoutedChatClient(IReadOnlyList<(ModelProviderMapping, ILLMClient)> routes, ChatCompletionRequest request,
        IDistributedCache? cache, ModelRoutePolicy policy)
    {
        _routes = routes; _request = request; _cache = cache; _policy = policy;
    }

    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request, string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        Exception? last = null;
        foreach (var (mapping, client) in _routes)
        {
            Bind(mapping);
            try
            {
                var response = await client.CreateChatCompletionAsync(request, apiKey, cancellationToken);
                await SuccessAsync(mapping, cancellationToken);
                return response;
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
            {
                RouteCircuitRegistry.Failure(mapping.Id); _request.RoutingFailoverCount++; last = ex;
            }
        }
        throw last ?? new ServiceUnavailableException($"No eligible provider route for '{request.Model}'.", "Routing");
    }

    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(ChatCompletionRequest request,
        string? apiKey = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Exception? last = null;
        foreach (var (mapping, client) in _routes)
        {
            Bind(mapping);
            var enumerator = client.StreamChatCompletionAsync(request, apiKey, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            bool hasFirst;
            try
            {
                hasFirst = await enumerator.MoveNextAsync();
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
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
        throw last ?? new ServiceUnavailableException($"No eligible provider route for '{request.Model}'.", "Routing");
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
    private static bool IsRetryable(Exception exception, CancellationToken callerToken)
    {
        if (exception is OperationCanceledException) return !callerToken.IsCancellationRequested;
        if (exception is HttpRequestException or RequestTimeoutException) return true;
        if (exception is LLMCommunicationException communication)
            return communication.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                   communication.StatusCode is >= HttpStatusCode.InternalServerError;
        return false;
    }

    public Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default) => _routes[0].Client.ListModelsAsync(apiKey, cancellationToken);
    public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default) => _routes[0].Client.CreateEmbeddingAsync(request, apiKey, cancellationToken);
    public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default) => _routes[0].Client.CreateImageAsync(request, apiKey, cancellationToken);
    public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null) => _routes[0].Client.GetCapabilitiesAsync(modelId);
}
