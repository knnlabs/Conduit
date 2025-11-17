namespace ConduitLLM.Configuration.DTOs.IpFilter
{
    /// <summary>
    /// Result of checking if an IP address is allowed
    /// </summary>
    public class IpCheckResult
    {
        /// <summary>
        /// Whether the IP address is allowed
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Reason for denial if the IP is not allowed
        /// </summary>
        public string? DeniedReason { get; set; }
    }
}
