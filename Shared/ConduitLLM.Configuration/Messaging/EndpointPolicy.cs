namespace ConduitLLM.Configuration.Messaging
{
    /// <summary>
    /// Transport-agnostic description of a tuned receive endpoint's reliability and
    /// concurrency policy. Captures — as data — everything the four hand-tuned
    /// MassTransit endpoints (webhook-delivery, video-generation-events,
    /// image-generation-events, spend-update-events) configure imperatively today, so
    /// each backend can translate the same descriptor into its own primitives without
    /// the domain knowing which library is underneath.
    /// </summary>
    /// <param name="Name">Queue / endpoint name (e.g. <c>spend-update-events</c>).</param>
    /// <param name="PrefetchCount">Transport prefetch, or null to use the bus default.</param>
    /// <param name="ConcurrentMessageLimit">Max concurrent handler invocations, or null for default.</param>
    /// <param name="SingleActiveConsumer">
    /// When true, only one consumer processes the queue at a time (strict ordering).
    /// Maps to RabbitMQ <c>x-single-active-consumer</c> / a sequential Wolverine listener.
    /// </param>
    /// <param name="QuorumQueue">Whether the queue is declared as a quorum queue.</param>
    /// <param name="ConfigureConsumeTopology">
    /// MassTransit-specific: bind the consumed message types to this endpoint's queue.
    /// </param>
    /// <param name="Retry">Immediate-retry policy for transient handler failures.</param>
    /// <param name="CircuitBreaker">Circuit-breaker policy, or null if none.</param>
    /// <param name="RateLimit">Rate-limit policy, or null if none.</param>
    /// <param name="DelayedRedeliveryIntervals">
    /// Second-level (delayed) redelivery intervals, or null to inherit the bus default.
    /// </param>
    /// <param name="QueueArguments">
    /// Extra transport queue arguments (e.g. <c>x-max-length</c>, <c>x-overflow</c>,
    /// <c>x-delivery-limit</c>). Keys are transport-native; backends apply what they support.
    /// </param>
    public sealed record EndpointPolicy(
        string Name,
        int? PrefetchCount = null,
        int? ConcurrentMessageLimit = null,
        bool SingleActiveConsumer = false,
        bool QuorumQueue = false,
        bool ConfigureConsumeTopology = false,
        RetryPolicy? Retry = null,
        CircuitBreakerPolicy? CircuitBreaker = null,
        RateLimitPolicy? RateLimit = null,
        IReadOnlyList<TimeSpan>? DelayedRedeliveryIntervals = null,
        IReadOnlyDictionary<string, object>? QueueArguments = null);

    /// <summary>Retry shape for a handler's transient failures.</summary>
    /// <param name="Kind">Backoff shape.</param>
    /// <param name="RetryCount">Number of retry attempts.</param>
    /// <param name="MinInterval">First/min interval (Incremental / Exponential).</param>
    /// <param name="MaxInterval">Max interval (Exponential).</param>
    /// <param name="IntervalStep">Per-attempt increment (Incremental / Exponential step).</param>
    public sealed record RetryPolicy(
        RetryKind Kind,
        int RetryCount,
        TimeSpan? MinInterval = null,
        TimeSpan? MaxInterval = null,
        TimeSpan? IntervalStep = null)
    {
        /// <summary>N immediate retries with no delay (spend-update-events).</summary>
        public static RetryPolicy Immediate(int count) => new(RetryKind.Immediate, count);

        /// <summary>Linear backoff: min, then +step each attempt.</summary>
        public static RetryPolicy Incremental(int count, TimeSpan min, TimeSpan step) =>
            new(RetryKind.Incremental, count, min, null, step);

        /// <summary>Exponential backoff between min and max, growing by step.</summary>
        public static RetryPolicy Exponential(int count, TimeSpan min, TimeSpan max, TimeSpan step) =>
            new(RetryKind.Exponential, count, min, max, step);
    }

    /// <summary>Retry backoff shapes supported by the descriptor.</summary>
    public enum RetryKind
    {
        /// <summary>Retry immediately with no delay.</summary>
        Immediate,

        /// <summary>Linear backoff (min + step * attempt).</summary>
        Incremental,

        /// <summary>Exponential backoff between min and max.</summary>
        Exponential
    }

    /// <summary>
    /// Circuit-breaker policy mirroring MassTransit's <c>UseCircuitBreaker</c> knobs.
    /// </summary>
    /// <param name="TrackingPeriod">Window over which failures are measured.</param>
    /// <param name="TripThreshold">Failure percentage that trips the breaker.</param>
    /// <param name="ActiveThreshold">Minimum attempts before the breaker can trip.</param>
    /// <param name="ResetInterval">How long the breaker stays open before half-opening.</param>
    public sealed record CircuitBreakerPolicy(
        TimeSpan TrackingPeriod,
        int TripThreshold,
        int ActiveThreshold,
        TimeSpan ResetInterval);

    /// <summary>Rate-limit policy: at most <paramref name="MessageLimit"/> per <paramref name="Interval"/>.</summary>
    /// <param name="MessageLimit">Maximum messages processed per interval.</param>
    /// <param name="Interval">The rate-limit window.</param>
    public sealed record RateLimitPolicy(int MessageLimit, TimeSpan Interval);
}
