using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Decorators
{
    /// <summary>
    /// Outermost decorator that retries a failed provider call against an ordered list of
    /// candidate (provider, key) chains before surfacing an error to the caller.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Each candidate is a fully decorated client chain bound to its own key, so per-key
    /// error tracking and auto-disable fire per attempt exactly as without failover; this
    /// decorator itself tracks nothing.</item>
    /// <item>Advancement is decided by <see cref="FailoverErrorClassifier"/>: key-scoped errors
    /// move to the next key; provider-scoped errors move to the next provider (or, within one
    /// provider, a sibling key on a different endpoint); user errors and cancellations abort
    /// with the original exception.</item>
    /// <item>Streaming: failover is only possible before the first chunk — once a chunk has
    /// been yielded the HTTP response is committed downstream and errors propagate as today.</item>
    /// <item>An explicit caller-supplied <c>apiKey</c> (BYOK) bypasses failover entirely.</item>
    /// <item>Image generation only fails over on auth-class errors unless
    /// <see cref="FailoverOptions.EnableFullMediaFailover"/> is set (double-generation risk).
    /// Video generation is not routed through this decorator at all (the video orchestrator
    /// walks the chain for <c>CreateVideoAsync</c>, which this class deliberately does not
    /// expose).</item>
    /// </list>
    /// </remarks>
    public class FailoverLLMClient : ILLMClient, ILLMClientDecorator, IAuthenticationVerifiable
    {
        private readonly IReadOnlyList<FailoverCandidate> _candidates;
        private readonly FailoverOptions _options;
        private readonly IFailoverAttributionAccessor? _attribution;
        private readonly ILogger<FailoverLLMClient>? _logger;

        public FailoverLLMClient(
            IReadOnlyList<FailoverCandidate> candidates,
            FailoverOptions options,
            IFailoverAttributionAccessor? attribution,
            ILogger<FailoverLLMClient>? logger)
        {
            if (candidates == null || candidates.Count == 0)
            {
                throw new ArgumentException("At least one failover candidate is required", nameof(candidates));
            }

            _candidates = candidates;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _attribution = attribution;
            _logger = logger;
        }

        /// <inheritdoc />
        public ILLMClient InnerClient => _candidates[0].GetClient();

        public Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
            => ExecuteWithFailoverAsync(
                (client, ct) => client.CreateChatCompletionAsync(request, apiKey, ct),
                mediaRestricted: false, apiKey, cancellationToken);

        public Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
            => ExecuteWithFailoverAsync(
                (client, ct) => client.CreateEmbeddingAsync(request, apiKey, ct),
                mediaRestricted: false, apiKey, cancellationToken);

        public Task<List<string>> ListModelsAsync(
            string? apiKey = null, CancellationToken cancellationToken = default)
            => ExecuteWithFailoverAsync(
                (client, ct) => client.ListModelsAsync(apiKey, ct),
                mediaRestricted: false, apiKey, cancellationToken);

        public Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
            => ExecuteWithFailoverAsync(
                (client, ct) => client.CreateImageAsync(request, apiKey, ct),
                mediaRestricted: !_options.EnableFullMediaFailover, apiKey, cancellationToken);

        public Task<ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            // Metadata lookup — no failover semantics needed.
            RecordAttempt(_candidates[0]);
            return _candidates[0].GetClient().GetCapabilitiesAsync(modelId);
        }

        public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var index = 0;
            Exception? lastError = null;

            while (index >= 0 && index < _candidates.Count)
            {
                var candidate = _candidates[index];
                RecordAttempt(candidate);

                var enumerator = candidate.GetClient()
                    .StreamChatCompletionAsync(request, apiKey, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);

                bool hasFirst;
                try
                {
                    hasFirst = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    await enumerator.DisposeAsync();
                    lastError = ex;
                    index = AdvanceOrThrow(ex, index, stopwatch, mediaRestricted: false, apiKey);
                    continue;
                }

                // First chunk obtained — the response is committed; stream through and let any
                // later error propagate unchanged.
                try
                {
                    while (hasFirst)
                    {
                        yield return enumerator.Current;
                        hasFirst = await enumerator.MoveNextAsync();
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }

                yield break;
            }

            // Unreachable: AdvanceOrThrow rethrows when no candidate remains. Defensive only.
            ExceptionDispatchInfo.Capture(lastError!).Throw();
        }

        /// <summary>
        /// Verifies authentication for the first candidate (auth verification targets one
        /// specific key by design).
        /// </summary>
        public Task<AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null, string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            if (_candidates[0].GetClient() is IAuthenticationVerifiable verifiable)
            {
                return verifiable.VerifyAuthenticationAsync(apiKey, baseUrl, cancellationToken);
            }

            return Task.FromResult(AuthenticationResult.Failure(
                "Provider does not support authentication verification",
                $"The {_candidates[0].GetClient().GetType().Name} client has not implemented authentication verification"));
        }

        /// <inheritdoc cref="IAuthenticationVerifiable.GetHealthCheckUrl" />
        public string GetHealthCheckUrl(string? baseUrl = null)
        {
            if (_candidates[0].GetClient() is IAuthenticationVerifiable verifiable)
            {
                return verifiable.GetHealthCheckUrl(baseUrl);
            }

            return baseUrl ?? "https://api.provider.com/health";
        }

        private async Task<T> ExecuteWithFailoverAsync<T>(
            Func<ILLMClient, CancellationToken, Task<T>> operation,
            bool mediaRestricted,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            // BYOK: rotating server-side keys under a caller-supplied key would be wrong.
            if (apiKey != null)
            {
                RecordAttempt(_candidates[0]);
                return await operation(_candidates[0].GetClient(), cancellationToken);
            }

            var stopwatch = Stopwatch.StartNew();
            var index = 0;

            while (true)
            {
                var candidate = _candidates[index];
                RecordAttempt(candidate);

                try
                {
                    return await operation(candidate.GetClient(), cancellationToken);
                }
                catch (Exception ex)
                {
                    index = AdvanceOrThrow(ex, index, stopwatch, mediaRestricted, apiKey);
                }
            }
        }

        /// <summary>
        /// Decides the next candidate index for a failed attempt, or rethrows the original
        /// exception (stack preserved) when failover must stop.
        /// </summary>
        private int AdvanceOrThrow(
            Exception ex, int currentIndex, Stopwatch stopwatch, bool mediaRestricted, string? apiKey)
        {
            var action = FailoverErrorClassifier.Classify(ex);

            if (apiKey != null
                || action == FailoverAction.Abort
                || (mediaRestricted && !FailoverErrorClassifier.IsAuthClassError(ex)))
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }

            // A new attempt that cannot plausibly finish within the budget only adds latency.
            var budget = TimeSpan.FromSeconds(_options.TotalBudgetSeconds);
            if (stopwatch.Elapsed > 0.6 * budget)
            {
                _logger?.LogWarning(
                    "Failover stopped after {Elapsed:F1}s (budget {Budget}s): not starting attempt {NextAttempt}",
                    stopwatch.Elapsed.TotalSeconds, _options.TotalBudgetSeconds, currentIndex + 2);
                ExceptionDispatchInfo.Capture(ex).Throw();
            }

            var next = NextIndex(currentIndex, action);
            if (next < 0)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }

            _logger?.LogWarning(
                ex,
                "Failover attempt {Attempt}: {Action} after error on key {KeyId} (provider {ProviderId}) — advancing to key {NextKeyId} (provider {NextProviderId})",
                currentIndex + 1,
                action,
                _candidates[currentIndex].KeyCredentialId,
                _candidates[currentIndex].ProviderId,
                _candidates[next].KeyCredentialId,
                _candidates[next].ProviderId);

            return next;
        }

        private int NextIndex(int currentIndex, FailoverAction action)
        {
            var current = _candidates[currentIndex];

            for (var j = currentIndex + 1; j < _candidates.Count; j++)
            {
                var next = _candidates[j];
                switch (action)
                {
                    case FailoverAction.NextKey:
                        return j;

                    case FailoverAction.NextProvider:
                        // A different provider always qualifies; within the same provider, a
                        // sibling key only helps if it points at a different endpoint.
                        if (next.ProviderId != current.ProviderId ||
                            !string.Equals(next.BaseUrl, current.BaseUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            return j;
                        }
                        continue;

                    default:
                        return -1;
                }
            }

            return -1;
        }

        private void RecordAttempt(FailoverCandidate candidate)
        {
            _attribution?.RecordAttempt(new FailoverAttribution(
                candidate.ProviderId,
                candidate.ProviderType,
                candidate.KeyCredentialId,
                candidate.ProviderModelId,
                candidate.MappingId,
                candidate.ModelCostId));
        }
    }
}
