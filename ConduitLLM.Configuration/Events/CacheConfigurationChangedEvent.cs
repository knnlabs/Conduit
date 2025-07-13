using System;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Configuration.Events
{
    /// <summary>
    /// Event raised when cache configuration is changed.
    /// </summary>
    public class CacheConfigurationChangedEvent
    {
        /// <summary>
        /// Gets or sets the cache region that was changed.
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the action performed (Created, Updated, Deleted).
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the old configuration.
        /// </summary>
        public CacheRegionConfig? OldConfig { get; set; }

        /// <summary>
        /// Gets or sets the new configuration.
        /// </summary>
        public CacheRegionConfig? NewConfig { get; set; }

        /// <summary>
        /// Gets or sets who made the change.
        /// </summary>
        public string ChangedBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the change occurred.
        /// </summary>
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the reason for the change.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets whether the change should be applied immediately.
        /// </summary>
        public bool ApplyImmediately { get; set; } = true;

        /// <summary>
        /// Gets or sets the rollout percentage (0-100) for gradual rollout.
        /// </summary>
        public int RolloutPercentage { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether this is a rollback operation.
        /// </summary>
        public bool IsRollback { get; set; }

        /// <summary>
        /// Gets or sets the source system of the change.
        /// </summary>
        public string? ChangeSource { get; set; }
    }

    /// <summary>
    /// Event consumer interface for cache configuration changes.
    /// </summary>
    public interface ICacheConfigurationChangedConsumer : MassTransit.IConsumer<CacheConfigurationChangedEvent>
    {
    }
}