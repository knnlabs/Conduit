using ConduitLLM.Core.Metrics;
using Prometheus;

namespace ConduitLLM.Admin.Metrics
{
    /// <summary>
    /// Prometheus metrics for Admin API authentication operations.
    /// Delegates to shared <see cref="AuthMetrics"/> with "admin" prefix.
    /// </summary>
    public static class AdminAuthMetrics
    {
        private static readonly AuthMetrics Instance = new("admin");

        public static Counter AuthAttempts => Instance.AuthAttempts;
        public static Histogram AuthDuration => Instance.AuthDuration;
        public static Counter AuthFailures => Instance.AuthFailures;

        public static void RecordSuccess(string scheme) => Instance.RecordSuccess(scheme);
        public static void RecordFailure(string scheme, string reason) => Instance.RecordFailure(scheme, reason);
        public static void RecordDuration(string scheme, double durationSeconds) => Instance.RecordDuration(scheme, durationSeconds);
    }
}
