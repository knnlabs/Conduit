using Prometheus;

namespace ConduitLLM.Gateway.Metrics
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
        /// Counter for spend update failures
        /// </summary>
        private static readonly Counter SpendUpdateFailures = Prometheus.Metrics
            .CreateCounter("conduit_spend_update_failures_total",
                "Total number of failed spend update persistence attempts",
                labelNames: new[] { "error_type" });

        /// <summary>
        /// Summary for potential revenue loss
        /// </summary>
        private static readonly Summary PotentialRevenueLoss = Prometheus.Metrics
            .CreateSummary("conduit_potential_revenue_loss_dollars",
                "Potential revenue loss from failed billing operations",
                labelNames: new[] { "reason" });

        /// <summary>
        /// Record a failed spend update
        /// </summary>
        public static void RecordSpendUpdateFailure(string errorType = "unknown")
        {
            SpendUpdateFailures.WithLabels(errorType).Inc();
        }

        /// <summary>
        /// Record potential revenue loss
        /// </summary>
        public static void RecordPotentialRevenueLoss(decimal amount, string reason)
        {
            PotentialRevenueLoss.WithLabels(reason).Observe((double)amount);
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
