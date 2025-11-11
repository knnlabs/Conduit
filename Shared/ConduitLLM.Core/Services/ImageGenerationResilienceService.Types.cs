namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides self-healing and automatic failover capabilities for image generation - Type definitions
    /// </summary>
    public partial class ImageGenerationResilienceService
    {
        private class ProviderHealthState
        {
            public int ProviderId { get; set; }
            public bool IsHealthy { get; set; } = true;
            public double HealthScore { get; set; } = 1.0;
            public int ConsecutiveFailures { get; set; }
            public DateTime LastChecked { get; set; }
            public bool IsQuarantined { get; set; }
            public DateTime? QuarantinedAt { get; set; }
            public string? QuarantineReason { get; set; }
            public bool IsThrottled { get; set; }
            public double ThrottleLevel { get; set; } = 1.0;
            public DateTime? RecoveryStarted { get; set; }
            public bool IsPermanentlyFailed { get; set; }
        }

        private class FailoverState
        {
            public int FailedProviderId { get; set; }
            public int FailoverProviderId { get; set; }
            public DateTime InitiatedAt { get; set; }
            public FailoverStatus Status { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        private enum FailoverStatus
        {
            Initiated,
            Active,
            Recovering,
            Completed,
            NoAlternative
        }

        private class RecoveryAttempt
        {
            public int ProviderId { get; set; }
            public int AttemptCount { get; set; }
            public DateTime LastAttempt { get; set; } = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Configuration options for image generation resilience.
    /// </summary>
    public class ImageGenerationResilienceOptions
    {
        public bool Enabled { get; set; } = true;
        public int HealthCheckIntervalMinutes { get; set; } = 2;
        public int RecoveryCheckIntervalMinutes { get; set; } = 5;
        public int FailureThreshold { get; set; } = 3;
        public double SlowResponseThresholdMs { get; set; } = 30000;
        public double RecoveryHealthScoreThreshold { get; set; } = 0.8;
        public TimeSpan MinimumQuarantineTime { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan MaximumQuarantineTime { get; set; } = TimeSpan.FromHours(24);
        public int QueueDepthThreshold { get; set; } = 100;
    }

    #region Events

    public class ProviderQuarantined
    {
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime QuarantinedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class ProviderFailoverInitiated
    {
        public int FailedProviderId { get; set; }
        public string FailedProviderName { get; set; } = string.Empty;
        public int FailoverProviderId { get; set; }
        public string FailoverProviderName { get; set; } = string.Empty;
        public DateTime InitiatedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class ProviderRecoveryInitiated
    {
        public int ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public double ThrottleLevel { get; set; }
        public DateTime InitiatedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    public class ProviderFailoverReverted
    {
        public int OriginalProviderId { get; set; }
        public string OriginalProviderName { get; set; } = string.Empty;
        public int FailoverProviderId { get; set; }
        public string FailoverProviderName { get; set; } = string.Empty;
        public DateTime RevertedAt { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    #endregion
}