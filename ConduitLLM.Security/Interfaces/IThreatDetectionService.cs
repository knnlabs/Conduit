using ConduitLLM.Configuration.DTOs.Security;

namespace ConduitLLM.Security.Interfaces
{
    /// <summary>
    /// Service for detecting and analyzing security threats
    /// </summary>
    public interface IThreatDetectionService
    {
        /// <summary>
        /// Analyzes current security events to detect threats
        /// </summary>
        /// <param name="events">Recent security events to analyze</param>
        /// <returns>Detected threats and their severity</returns>
        Task<List<ThreatAlertDto>> AnalyzeThreatsAsync(List<SecurityEventDto> events);

        /// <summary>
        /// Gets the current threat level based on recent activity
        /// </summary>
        /// <returns>Current threat level</returns>
        Task<ThreatLevel> GetCurrentThreatLevelAsync();

        /// <summary>
        /// Checks if an IP address shows signs of malicious behavior
        /// </summary>
        /// <param name="ipAddress">IP address to check</param>
        /// <returns>True if the IP is considered malicious</returns>
        Task<bool> IsIpMaliciousAsync(string ipAddress);

        /// <summary>
        /// Detects distributed attack patterns
        /// </summary>
        /// <returns>List of detected distributed attacks</returns>
        Task<List<DistributedAttackDto>> DetectDistributedAttacksAsync();

        /// <summary>
        /// Gets risk score for a specific virtual key
        /// </summary>
        /// <param name="virtualKey">Virtual key to assess</param>
        /// <returns>Risk score (0-100)</returns>
        Task<int> GetVirtualKeyRiskScoreAsync(string virtualKey);

        /// <summary>
        /// Detects anomalous patterns in API usage
        /// </summary>
        /// <param name="virtualKey">Virtual key to analyze</param>
        /// <returns>List of detected anomalies</returns>
        Task<List<AnomalyDto>> DetectAnomaliesAsync(string virtualKey);
    }
}