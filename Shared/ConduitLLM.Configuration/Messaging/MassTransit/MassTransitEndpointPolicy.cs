using MassTransit;

namespace ConduitLLM.Configuration.Messaging.MassTransit
{
    /// <summary>
    /// Translates the transport-agnostic resilience parts of an <see cref="EndpointPolicy"/>
    /// (retry, delayed redelivery, circuit breaker, rate limit) onto a MassTransit receive
    /// endpoint. These middleware live in the core <c>MassTransit</c> package, so this
    /// helper is transport-neutral (works for RabbitMQ and in-memory).
    /// </summary>
    /// <remarks>
    /// Transport-specific settings (prefetch, concurrency, quorum queue, queue arguments,
    /// single-active-consumer) live in <c>MassTransit.RabbitMQ</c> and are applied by the
    /// host's <c>Program.Messaging.cs</c> directly from the same descriptor, because
    /// <c>ConduitLLM.Configuration</c> intentionally does not reference the RabbitMQ package.
    /// </remarks>
    public static class MassTransitEndpointPolicy
    {
        /// <summary>
        /// Applies the descriptor's retry / delayed-redelivery / circuit-breaker / rate-limit
        /// middleware to a receive endpoint, in the same order the original imperative
        /// configuration used.
        /// </summary>
        public static void ApplyResiliencePolicies(IReceiveEndpointConfigurator endpoint, EndpointPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(policy);

            if (policy.Retry is { } retry)
            {
                endpoint.UseMessageRetry(r => ApplyRetry(r, retry));
            }

            if (policy.DelayedRedeliveryIntervals is { Count: > 0 } intervals)
            {
                endpoint.UseDelayedRedelivery(r => r.Intervals(intervals.ToArray()));
            }

            if (policy.CircuitBreaker is { } cb)
            {
                endpoint.UseCircuitBreaker(c =>
                {
                    c.TrackingPeriod = cb.TrackingPeriod;
                    c.TripThreshold = cb.TripThreshold;
                    c.ActiveThreshold = cb.ActiveThreshold;
                    c.ResetInterval = cb.ResetInterval;
                });
            }

            if (policy.RateLimit is { } rl)
            {
                endpoint.UseRateLimit(rl.MessageLimit, rl.Interval);
            }
        }

        /// <summary>Translates a <see cref="RetryPolicy"/> onto a MassTransit retry configurator.</summary>
        public static void ApplyRetry(IRetryConfigurator r, RetryPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(r);
            ArgumentNullException.ThrowIfNull(policy);

            switch (policy.Kind)
            {
                case RetryKind.Immediate:
                    r.Immediate(policy.RetryCount);
                    break;
                case RetryKind.Incremental:
                    r.Incremental(policy.RetryCount, policy.MinInterval!.Value, policy.IntervalStep!.Value);
                    break;
                case RetryKind.Exponential:
                    r.Exponential(policy.RetryCount, policy.MinInterval!.Value, policy.MaxInterval!.Value, policy.IntervalStep!.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(policy), policy.Kind, "Unknown retry kind.");
            }
        }
    }
}
