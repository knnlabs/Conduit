namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Derives a health status string from how stale a service's heartbeat is (#1067). Kept
    /// pure and side-effect-free so the staleness thresholds are unit-testable.
    /// </summary>
    public static class ServiceHeartbeatEvaluator
    {
        /// <summary>Fallback cadence used when a heartbeat did not report its own interval.</summary>
        public const double DefaultIntervalSeconds = 30;

        /// <summary>A heartbeat is "healthy" while its age is within this many intervals.</summary>
        public const int HealthyIntervalMultiple = 2;

        /// <summary>
        /// A heartbeat is "degraded" while its age is within this many intervals; anything older
        /// is "unhealthy" (heartbeats lost — the service is likely down or unreachable).
        /// </summary>
        public const int DegradedIntervalMultiple = 4;

        /// <summary>
        /// Maps a heartbeat age to a status: "healthy" while fresh (≤2 intervals), "degraded"
        /// while delayed (≤4 intervals), otherwise "unhealthy".
        /// </summary>
        /// <param name="ageSeconds">How long ago the heartbeat was received, in seconds.</param>
        /// <param name="intervalSeconds">The reporting service's heartbeat cadence; falls back to <see cref="DefaultIntervalSeconds"/> when not positive.</param>
        public static string EvaluateStatus(double ageSeconds, double intervalSeconds)
        {
            var interval = intervalSeconds > 0 ? intervalSeconds : DefaultIntervalSeconds;

            if (ageSeconds <= interval * HealthyIntervalMultiple)
            {
                return "healthy";
            }

            return ageSeconds <= interval * DegradedIntervalMultiple ? "degraded" : "unhealthy";
        }
    }
}
