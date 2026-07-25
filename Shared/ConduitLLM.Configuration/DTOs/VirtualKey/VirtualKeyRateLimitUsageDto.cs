namespace ConduitLLM.Configuration.DTOs.VirtualKey
{
    /// <summary>
    /// What a virtual key is consuming right now, against each of its configured ceilings.
    /// </summary>
    /// <remarks>
    /// Every window is rolling, so these are the last minute and the last 24 hours from the
    /// moment of the call — not totals since a calendar boundary. Group figures are present only
    /// when the key belongs to a group with its own ceilings; absent means there is no group
    /// limit, which is not the same as a group sitting idle.
    /// </remarks>
    public class VirtualKeyRateLimitUsageDto
    {
        /// <summary>The key these figures describe.</summary>
        public int VirtualKeyId { get; set; }

        // --- current usage -------------------------------------------------
        public int RequestsThisMinute { get; set; }
        public int RequestsToday { get; set; }
        public long TokensThisMinute { get; set; }
        public int RequestsInFlight { get; set; }

        // --- the ceilings they are measured against ------------------------
        public int? RateLimitRpm { get; set; }
        public int? RateLimitRpd { get; set; }
        public int? RateLimitTpm { get; set; }
        public int? MaxParallelRequests { get; set; }

        // --- group scope, when one applies ---------------------------------
        public int? VirtualKeyGroupId { get; set; }
        public int? GroupRequestsThisMinute { get; set; }
        public int? GroupRequestsToday { get; set; }
        public long? GroupTokensThisMinute { get; set; }
        public int? GroupRequestsInFlight { get; set; }
        public int? GroupRateLimitRpm { get; set; }
        public int? GroupRateLimitRpd { get; set; }
        public int? GroupRateLimitTpm { get; set; }
        public int? GroupMaxParallelRequests { get; set; }

        /// <summary>
        /// True when the figures could not be read from the shared store, so they are zeroes
        /// rather than measurements.
        /// </summary>
        public bool Unavailable { get; set; }
    }
}
