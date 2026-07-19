using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Providers.Configuration;

/// <summary>
/// Configuration for the provider HTTP resilience pipeline
/// (total timeout → retry → circuit breaker → per-attempt timeout).
/// </summary>
/// <remarks>
/// Bound from the <c>Conduit:ProviderHttp</c> section. Defaults are chosen so that going live
/// does not tighten any bound that existed before the pipeline: fast-fail behavior comes from
/// the small retry budget, the Retry-After cap, and the circuit breaker — not from shrinking
/// timeouts.
/// </remarks>
public class ProviderResilienceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Conduit:ProviderHttp";

    /// <summary>Retry behavior shared by all operation classes.</summary>
    public RetrySettings Retry { get; set; } = new();

    /// <summary>Circuit breaker behavior (partitioned per named client × URI authority).</summary>
    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();

    /// <summary>Per-operation-class timeout/attempt budgets.</summary>
    public BudgetSettings Budgets { get; set; } = new();

    /// <summary>Streaming-specific settings.</summary>
    public StreamingSettings Streaming { get; set; } = new();

    /// <summary>
    /// Resolves the budget for an operation class string (see <see cref="Http.ConduitHttpOptions"/>).
    /// Unknown classes fall back to the chat budget.
    /// </summary>
    public OperationBudget BudgetFor(string operationClass) => operationClass switch
    {
        Http.ConduitHttpOptions.Images => Budgets.Images,
        Http.ConduitHttpOptions.Auth => Budgets.Auth,
        Http.ConduitHttpOptions.Video => Budgets.Video,
        _ => Budgets.Chat, // chat, chat-stream, and anything unrecognized
    };

    public class RetrySettings
    {
        /// <summary>Base delay for exponential backoff between retries.</summary>
        [Range(0.05, 30)]
        public double BaseDelaySeconds { get; set; } = 0.5;

        /// <summary>Maximum backoff delay between retries.</summary>
        [Range(0.1, 120)]
        public double MaxDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Maximum honored <c>Retry-After</c> value. A 429/503 whose Retry-After exceeds this
        /// cap is not retried at all — failing fast beats stalling an interactive request.
        /// </summary>
        [Range(0, 120)]
        public double RetryAfterCapSeconds { get; set; } = 5;
    }

    public class CircuitBreakerSettings
    {
        /// <summary>Escape hatch: disables the circuit breaker strategy entirely.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Failure ratio within the sampling window that opens the circuit.</summary>
        [Range(0.01, 1.0)]
        public double FailureRatio { get; set; } = 0.5;

        /// <summary>Minimum requests in the sampling window before the breaker can act.</summary>
        [Range(2, 1000)]
        public int MinimumThroughput { get; set; } = 10;

        /// <summary>Sampling window for the failure ratio.</summary>
        [Range(1, 600)]
        public double SamplingDurationSeconds { get; set; } = 30;

        /// <summary>How long the circuit stays open before probing.</summary>
        [Range(1, 600)]
        public double BreakDurationSeconds { get; set; } = 30;
    }

    public class BudgetSettings
    {
        /// <summary>Interactive chat/completions/embeddings (and chat streaming: the attempt
        /// timeout bounds time-to-first-token since the HTTP call completes at headers).</summary>
        public OperationBudget Chat { get; set; } = new()
        {
            AttemptTimeoutSeconds = 100,
            TotalTimeoutSeconds = 120,
            MaxRetryAttempts = 2,
        };

        /// <summary>Image generation — longer single attempts, same small retry budget.</summary>
        public OperationBudget Images { get; set; } = new()
        {
            AttemptTimeoutSeconds = 180,
            TotalTimeoutSeconds = 240,
            MaxRetryAttempts = 2,
        };

        /// <summary>Key/auth verification — fail fast, never retry (a wrong key stays wrong,
        /// and admins are waiting on the result interactively).</summary>
        public OperationBudget Auth { get; set; } = new()
        {
            AttemptTimeoutSeconds = 10,
            TotalTimeoutSeconds = 30,
            MaxRetryAttempts = 0,
        };

        /// <summary>Video generation — very long attempts, no retries (retrying an accepted
        /// generation job risks double-generation and double cost).</summary>
        public OperationBudget Video { get; set; } = new()
        {
            AttemptTimeoutSeconds = 600,
            TotalTimeoutSeconds = 1800,
            MaxRetryAttempts = 0,
        };
    }

    public class OperationBudget
    {
        /// <summary>Timeout for a single attempt (connect + time to response headers).</summary>
        [Range(1, 3600)]
        public double AttemptTimeoutSeconds { get; set; }

        /// <summary>Total budget across all attempts including backoff delays.</summary>
        [Range(1, 7200)]
        public double TotalTimeoutSeconds { get; set; }

        /// <summary>Retries after the initial attempt (0 = single attempt).</summary>
        [Range(0, 10)]
        public int MaxRetryAttempts { get; set; }
    }

    public class StreamingSettings
    {
        /// <summary>
        /// Maximum time between SSE reads before a stalled stream is aborted
        /// (consumed by the streaming idle-read watchdog).
        /// </summary>
        [Range(5, 3600)]
        public double IdleReadTimeoutSeconds { get; set; } = 90;
    }
}
