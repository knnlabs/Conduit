using System;
using System.Collections.Concurrent;
using Prometheus;

namespace ConduitLLM.Http.Metrics
{
    /// <summary>
    /// Prometheus metrics for billing system monitoring
    /// </summary>
    public static class BillingMetrics
    {
        // Redis Circuit Breaker Metrics
        
        /// <summary>
        /// Gauge for circuit breaker state (0=closed, 1=open, 2=half-open)
        /// </summary>
        private static readonly Gauge CircuitBreakerState = Prometheus.Metrics
            .CreateGauge("conduit_redis_circuit_breaker_state",
                "Current state of Redis circuit breaker (0=closed, 1=open, 2=half-open)");

        /// <summary>
        /// Counter for circuit breaker state changes
        /// </summary>
        private static readonly Counter CircuitBreakerStateChanges = Prometheus.Metrics
            .CreateCounter("conduit_redis_circuit_breaker_state_changes_total",
                "Total number of circuit breaker state changes",
                labelNames: new[] { "from_state", "to_state" });

        /// <summary>
        /// Counter for requests rejected by circuit breaker
        /// </summary>
        private static readonly Counter CircuitBreakerRejections = Prometheus.Metrics
            .CreateCounter("conduit_redis_circuit_breaker_rejections_total",
                "Total number of requests rejected by circuit breaker",
                labelNames: new[] { "path", "method" });

        /// <summary>
        /// Gauge for time until circuit breaker recovery attempt
        /// </summary>
        private static readonly Gauge CircuitBreakerTimeUntilRecovery = Prometheus.Metrics
            .CreateGauge("conduit_redis_circuit_breaker_recovery_seconds",
                "Seconds until circuit breaker will attempt recovery");

        /// <summary>
        /// Counter for circuit breaker trips
        /// </summary>
        private static readonly Counter CircuitBreakerTrips = Prometheus.Metrics
            .CreateCounter("conduit_redis_circuit_breaker_trips_total",
                "Total number of times circuit breaker has tripped",
                labelNames: new[] { "reason" });
        /// <summary>
        /// Counter for total spend update attempts
        /// </summary>
        private static readonly Counter SpendUpdateAttempts = Prometheus.Metrics
            .CreateCounter("conduit_spend_update_attempts_total",
                "Total number of spend update attempts to Redis",
                labelNames: new[] { "virtual_key_id", "status" });

        /// <summary>
        /// Counter for spend update failures
        /// </summary>
        private static readonly Counter SpendUpdateFailures = Prometheus.Metrics
            .CreateCounter("conduit_spend_update_failures_total",
                "Total number of failed spend updates to Redis",
                labelNames: new[] { "virtual_key_id", "error_type" });

        /// <summary>
        /// Gauge for pending spend updates in Redis
        /// </summary>
        private static readonly Gauge PendingSpendUpdates = Prometheus.Metrics
            .CreateGauge("conduit_pending_spend_updates",
                "Number of pending spend updates in Redis",
                labelNames: new[] { "virtual_key_group_id" });

        /// <summary>
        /// Counter for Redis flush operations
        /// </summary>
        private static readonly Counter RedisFlushOperations = Prometheus.Metrics
            .CreateCounter("conduit_redis_flush_operations_total",
                "Total number of Redis flush operations",
                labelNames: new[] { "status" });

        /// <summary>
        /// Histogram for spend update latency
        /// </summary>
        private static readonly Histogram SpendUpdateLatency = Prometheus.Metrics
            .CreateHistogram("conduit_spend_update_latency_seconds",
                "Latency of spend update operations in seconds",
                labelNames: new[] { "operation" });

        /// <summary>
        /// Summary for potential revenue loss
        /// </summary>
        private static readonly Summary PotentialRevenueLoss = Prometheus.Metrics
            .CreateSummary("conduit_potential_revenue_loss_dollars",
                "Potential revenue loss from failed billing operations",
                labelNames: new[] { "reason" });

        // Track failure rates
        private static readonly ConcurrentDictionary<string, (long success, long failure, DateTime lastReset)> _failureRates = new();

        /// <summary>
        /// Record a successful spend update
        /// </summary>
        public static void RecordSpendUpdateSuccess(int virtualKeyId)
        {
            SpendUpdateAttempts.WithLabels(virtualKeyId.ToString(), "success").Inc();
            UpdateFailureRate(virtualKeyId.ToString(), true);
        }

        /// <summary>
        /// Record a failed spend update
        /// </summary>
        public static void RecordSpendUpdateFailure(int virtualKeyId, string errorType = "unknown")
        {
            SpendUpdateAttempts.WithLabels(virtualKeyId.ToString(), "failure").Inc();
            SpendUpdateFailures.WithLabels(virtualKeyId.ToString(), errorType).Inc();
            UpdateFailureRate(virtualKeyId.ToString(), false);
        }

        /// <summary>
        /// Set the number of pending spend updates
        /// </summary>
        public static void SetPendingSpendUpdates(int groupId, double count)
        {
            PendingSpendUpdates.WithLabels(groupId.ToString()).Set(count);
        }

        /// <summary>
        /// Record a Redis flush operation
        /// </summary>
        public static void RecordFlushOperation(bool success)
        {
            RedisFlushOperations.WithLabels(success ? "success" : "failure").Inc();
        }

        /// <summary>
        /// Record spend update latency
        /// </summary>
        public static IDisposable MeasureSpendUpdateLatency(string operation = "queue")
        {
            return SpendUpdateLatency.WithLabels(operation).NewTimer();
        }

        /// <summary>
        /// Record potential revenue loss
        /// </summary>
        public static void RecordPotentialRevenueLoss(decimal amount, string reason)
        {
            PotentialRevenueLoss.WithLabels(reason).Observe((double)amount);
        }

        /// <summary>
        /// Get the current failure rate for spend updates
        /// </summary>
        public static double GetFailureRate(string? keyId = null)
        {
            if (keyId != null)
            {
                if (_failureRates.TryGetValue(keyId, out var rate))
                {
                    var total = rate.success + rate.failure;
                    return total > 0 ? (double)rate.failure / total : 0;
                }
                return 0;
            }

            // Calculate overall failure rate
            long totalSuccess = 0;
            long totalFailure = 0;

            foreach (var rate in _failureRates.Values)
            {
                totalSuccess += rate.success;
                totalFailure += rate.failure;
            }

            var overallTotal = totalSuccess + totalFailure;
            return overallTotal > 0 ? (double)totalFailure / overallTotal : 0;
        }

        private static void UpdateFailureRate(string keyId, bool success)
        {
            var now = DateTime.UtcNow;
            _failureRates.AddOrUpdate(keyId,
                _ => success ? (1, 0, now) : (0, 1, now),
                (_, current) =>
                {
                    // Reset counters if more than 1 hour has passed
                    if (now - current.lastReset > TimeSpan.FromHours(1))
                    {
                        return success ? (1, 0, now) : (0, 1, now);
                    }

                    return success
                        ? (current.success + 1, current.failure, current.lastReset)
                        : (current.success, current.failure + 1, current.lastReset);
                });
        }

        /// <summary>
        /// Reset all failure rate counters
        /// </summary>
        public static void ResetFailureRates()
        {
            _failureRates.Clear();
        }

        // Circuit Breaker Metric Methods

        /// <summary>
        /// Update circuit breaker state metric
        /// </summary>
        public static void UpdateCircuitBreakerState(string state)
        {
            var stateValue = state.ToLowerInvariant() switch
            {
                "closed" => 0,
                "open" => 1,
                "halfopen" or "half-open" => 2,
                _ => 0
            };
            CircuitBreakerState.Set(stateValue);
        }

        /// <summary>
        /// Record a circuit breaker state change
        /// </summary>
        public static void RecordCircuitBreakerStateChange(string fromState, string toState)
        {
            CircuitBreakerStateChanges.WithLabels(fromState.ToLowerInvariant(), toState.ToLowerInvariant()).Inc();
        }

        /// <summary>
        /// Record a request rejected by circuit breaker
        /// </summary>
        public static void RecordCircuitBreakerRejection(string path, string method)
        {
            CircuitBreakerRejections.WithLabels(path, method).Inc();
        }

        /// <summary>
        /// Update time until circuit breaker recovery
        /// </summary>
        public static void UpdateTimeUntilRecovery(double seconds)
        {
            CircuitBreakerTimeUntilRecovery.Set(seconds);
        }

        /// <summary>
        /// Record a circuit breaker trip
        /// </summary>
        public static void RecordCircuitBreakerTrip(string reason)
        {
            CircuitBreakerTrips.WithLabels(reason).Inc();
        }
    }
}