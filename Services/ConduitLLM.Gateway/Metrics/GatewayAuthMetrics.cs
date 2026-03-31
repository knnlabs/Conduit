using ConduitLLM.Core.Metrics;
using Prometheus;

namespace ConduitLLM.Gateway.Metrics
{
    /// <summary>
    /// Prometheus metrics for Gateway authentication operations.
    /// Delegates to shared <see cref="AuthMetrics"/> with "gateway" prefix.
    /// </summary>
    public static class GatewayAuthMetrics
    {
        private static readonly AuthMetrics Instance = new("gateway");

        public static Counter AuthAttempts => Instance.AuthAttempts;
        public static Histogram AuthDuration => Instance.AuthDuration;
        public static Counter AuthFailures => Instance.AuthFailures;

        public static void RecordSuccess(string scheme) => Instance.RecordSuccess(scheme);
        public static void RecordFailure(string scheme, string reason) => Instance.RecordFailure(scheme, reason);
        public static void RecordNoResult(string scheme) => Instance.RecordNoResult(scheme);
        public static void RecordError(string scheme) => Instance.RecordError(scheme);
        public static void RecordDuration(string scheme, double durationSeconds) => Instance.RecordDuration(scheme, durationSeconds);
    }
}
