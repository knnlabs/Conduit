using JasperFx;
using JasperFx.CodeGeneration;

using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.Runtime.Handlers;

namespace ConduitLLM.Configuration.Messaging.Wolverine
{
    /// <summary>
    /// Translates the transport-agnostic <see cref="EndpointPolicy"/> descriptors
    /// (<see cref="ConduitEndpointPolicies"/>) into Wolverine primitives — the Wolverine
    /// analogue of the Gateway's <c>ApplyRabbitMqEndpointPolicy</c> resilience
    /// configuration in the previous backend (epic #909, I2.3/#926).
    /// </summary>
    /// <remarks>
    /// Mapping notes:
    /// <list type="bullet">
    /// <item><c>ConcurrentMessageLimit = 1</c> → <c>ListenWithStrictOrdering</c>: exactly
    /// one node listens with sequential handling (spend-update-events).
    /// <c>SingleActiveConsumer</c> alone → <c>ExclusiveNodeWithParallelism</c>: one node
    /// listens (failover semantics of RabbitMQ's <c>x-single-active-consumer</c>) but
    /// handles messages in parallel, matching the previous backend's concurrent processing.</item>
    /// <item><c>ConcurrentMessageLimit</c> → <c>MaximumParallelMessages</c>; null inherits
    /// Wolverine's default. <c>PrefetchCount</c> → <c>MaximumMessagesToReceive</c> (the
    /// per-poll receive batch), polled every 250ms instead of the 5s default.</item>
    /// <item><c>Retry</c> → failure rules scoped to the endpoint's message types (see
    /// <see cref="ComputeRetryCooldowns"/>); <c>DelayedRedeliveryIntervals</c> →
    /// scheduled retries after the inline attempts. Exhausted messages land in
    /// Wolverine's Postgres dead-letter storage.</item>
    /// <item><c>CircuitBreaker</c> → the listener circuit breaker (pauses the listener).</item>
    /// <item><c>QuorumQueue</c>/<c>QueueArguments</c> are RabbitMQ-native and do not apply;
    /// <c>RateLimit</c> (webhook 100/s) is intentionally NOT translated — the app-level
    /// webhook rate limiting/circuit breaking is retained, per the Phase 2 plan.</item>
    /// </list>
    /// </remarks>
    public static class WolverineEndpointPolicy
    {
        /// <summary>
        /// Parallelism for single-active-consumer endpoints whose descriptor leaves
        /// <c>ConcurrentMessageLimit</c> null ("inherit the bus/config default") —
        /// mirrors the <c>ConduitLLM:RabbitMQ</c> ConcurrentMessageLimit default (50)
        /// that the previous backend applies to those same endpoints.
        /// </summary>
        private const int DefaultSingleActiveParallelism = 50;

        /// <summary>
        /// Per-poll receive batch for endpoints whose descriptor leaves
        /// <c>PrefetchCount</c> null — mirrors the RabbitMQ prefetch the same endpoints
        /// get from the <c>ConduitLLM:RabbitMQ</c> defaults.
        /// </summary>
        private const int DefaultReceiveBatchSize = 50;

        /// <summary>
        /// Configures a listener for the policy's queue on this host and registers the
        /// policy's retry rules for the given event types. Call only on the host that
        /// consumes the queue; publishers need only the routing rules
        /// (<c>PublishMessage(...).ToPostgresqlQueue(policy.Name)</c>).
        /// </summary>
        /// <param name="options">The Wolverine options under configuration.</param>
        /// <param name="policy">The tuned endpoint descriptor.</param>
        /// <param name="eventTypes">The event types consumed on this endpoint.</param>
        public static void ListenWithPolicy(this WolverineOptions options, EndpointPolicy policy, IReadOnlyList<Type> eventTypes)
        {
            var listener = options.ListenToPostgresqlQueue(policy.Name);

            // The Postgres queue listener defaults to 20 messages per poll on the 5s
            // ScheduledJobPollingTime cadence — a ~4 msg/s ceiling per queue (#929 parity
            // gate finding W4). Poll aggressively and map PrefetchCount to the per-poll
            // batch size, which IS its Postgres-transport analogue.
            listener.PollingInterval(TimeSpan.FromMilliseconds(250));
            listener.MaximumMessagesToReceive(policy.PrefetchCount ?? DefaultReceiveBatchSize);

            if (policy.ConcurrentMessageLimit == 1)
            {
                // Cluster-wide single active listener + sequential handling: strict
                // ordering across all nodes (spend-update-events).
                listener.ListenWithStrictOrdering();
            }
            else if (policy.SingleActiveConsumer)
            {
                // RabbitMQ x-single-active-consumer without ConcurrentMessageLimit=1 is
                // one consumer *instance* with parallel handling (image-generation-events
                // runs 50-concurrent on the previous backend via the ConduitLLM:RabbitMQ default) —
                // exclusive node for failover semantics, but NOT sequential. Mapping SAC
                // to ListenWithStrictOrdering serialized the queue and cut media
                // throughput ~10x (#929 parity gate finding W3).
                listener.ExclusiveNodeWithParallelism(
                    policy.ConcurrentMessageLimit ?? DefaultSingleActiveParallelism);
            }
            else if (policy.ConcurrentMessageLimit is int limit)
            {
                listener.MaximumParallelMessages(limit);
            }

            if (policy.CircuitBreaker is { } breaker)
            {
                listener.CircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = breaker.TrackingPeriod;
                    cb.FailurePercentageThreshold = breaker.TripThreshold;
                    cb.MinimumThreshold = breaker.ActiveThreshold;
                    cb.PauseTime = breaker.ResetInterval;
                });
            }

            if (policy.Retry is not null || policy.DelayedRedeliveryIntervals is not null)
            {
                options.Policies.Add(new EndpointRetryHandlerPolicy(
                    eventTypes,
                    policy.Retry is { } retry ? ComputeRetryCooldowns(retry) : Array.Empty<TimeSpan>(),
                    policy.DelayedRedeliveryIntervals?.ToArray()));
            }
        }

        /// <summary>
        /// Expands a <see cref="RetryPolicy"/> descriptor into the per-attempt cooldowns
        /// Wolverine's <c>RetryWithCooldown</c> expects. Immediate → zero delays;
        /// Incremental → min + step·attempt; Exponential → min + step·(2ⁿ−1) capped at max
        /// (the same shape the previous backend's <c>Exponential</c> produces).
        /// </summary>
        public static TimeSpan[] ComputeRetryCooldowns(RetryPolicy retry)
        {
            var cooldowns = new TimeSpan[retry.RetryCount];
            for (var attempt = 0; attempt < retry.RetryCount; attempt++)
            {
                cooldowns[attempt] = retry.Kind switch
                {
                    RetryKind.Immediate => TimeSpan.Zero,
                    RetryKind.Incremental =>
                        (retry.MinInterval ?? TimeSpan.Zero) + (retry.IntervalStep ?? TimeSpan.Zero) * attempt,
                    RetryKind.Exponential => Cap(
                        (retry.MinInterval ?? TimeSpan.Zero) + (retry.IntervalStep ?? TimeSpan.Zero) * (Math.Pow(2, attempt) - 1),
                        retry.MaxInterval),
                    _ => TimeSpan.Zero,
                };
            }

            return cooldowns;
        }

        private static TimeSpan Cap(TimeSpan value, TimeSpan? max)
            => max is { } m && value > m ? m : value;

        /// <summary>
        /// Applies an endpoint descriptor's retry shape as failure rules on the handler
        /// chains of that endpoint's message types only — Wolverine's failure-rule DSL is
        /// either global or generically typed, and the endpoint policies carry runtime
        /// <see cref="Type"/> lists, hence this <see cref="IHandlerPolicy"/>.
        /// </summary>
        internal sealed class EndpointRetryHandlerPolicy : IHandlerPolicy
        {
            private readonly HashSet<Type> _messageTypes;
            private readonly TimeSpan[] _cooldowns;
            private readonly TimeSpan[]? _scheduledRetries;

            public EndpointRetryHandlerPolicy(
                IEnumerable<Type> messageTypes,
                TimeSpan[] cooldowns,
                TimeSpan[]? scheduledRetries)
            {
                _messageTypes = new HashSet<Type>(messageTypes);
                _cooldowns = cooldowns;
                _scheduledRetries = scheduledRetries;
            }

            public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
            {
                foreach (var chain in chains)
                {
                    if (!_messageTypes.Contains(chain.MessageType))
                    {
                        continue;
                    }

                    if (_cooldowns.Length > 0)
                    {
                        chain.OnException<Exception>().RetryWithCooldown(_cooldowns);
                    }

                    if (_scheduledRetries is { Length: > 0 })
                    {
                        chain.OnException<Exception>().ScheduleRetry(_scheduledRetries);
                    }
                }
            }
        }
    }
}
