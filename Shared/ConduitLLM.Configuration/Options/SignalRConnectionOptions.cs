using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Configuration options for SignalR connection limits
    /// </summary>
    public class SignalRConnectionOptions
    {
        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string SectionName = "SignalR:ConnectionLimits";

        /// <summary>
        /// Maximum concurrent connections per virtual key
        /// </summary>
        [Range(1, 10000)]
        public int MaxConnectionsPerVirtualKey { get; set; } = 100;

        /// <summary>
        /// Maximum total connections across all virtual keys
        /// </summary>
        [Range(1, 100000)]
        public int MaxTotalConnections { get; set; } = 10000;

        /// <summary>
        /// Whether to enforce connection limits (can disable for debugging)
        /// </summary>
        public bool EnforceLimits { get; set; } = true;
    }
}
