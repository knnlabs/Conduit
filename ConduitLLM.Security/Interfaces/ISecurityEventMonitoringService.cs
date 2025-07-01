using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.Security;

namespace ConduitLLM.Security.Interfaces
{
    /// <summary>
    /// Service for monitoring security events and generating alerts
    /// </summary>
    public interface ISecurityEventMonitoringService
    {
        /// <summary>
        /// Records an authentication failure event
        /// </summary>
        /// <param name="ipAddress">IP address of the request</param>
        /// <param name="virtualKey">Virtual key used (if any)</param>
        /// <param name="endpoint">API endpoint accessed</param>
        void RecordAuthenticationFailure(string ipAddress, string virtualKey, string endpoint);

        /// <summary>
        /// Records an authentication success event
        /// </summary>
        /// <param name="ipAddress">IP address of the request</param>
        /// <param name="virtualKey">Virtual key used</param>
        /// <param name="endpoint">API endpoint accessed</param>
        void RecordAuthenticationSuccess(string ipAddress, string virtualKey, string endpoint);

        /// <summary>
        /// Records a rate limit violation event
        /// </summary>
        /// <param name="ipAddress">IP address of the request</param>
        /// <param name="virtualKey">Virtual key used (if any)</param>
        /// <param name="endpoint">API endpoint accessed</param>
        /// <param name="limitType">Type of rate limit violated</param>
        void RecordRateLimitViolation(string ipAddress, string virtualKey, string endpoint, string limitType);

        /// <summary>
        /// Records suspicious activity
        /// </summary>
        /// <param name="ipAddress">IP address of the request</param>
        /// <param name="activity">Type of suspicious activity</param>
        /// <param name="details">Additional details about the activity</param>
        void RecordSuspiciousActivity(string ipAddress, string activity, string details);

        /// <summary>
        /// Records potential data exfiltration attempt
        /// </summary>
        /// <param name="ipAddress">IP address of the request</param>
        /// <param name="virtualKey">Virtual key used</param>
        /// <param name="dataSize">Size of data requested</param>
        /// <param name="endpoint">API endpoint accessed</param>
        void RecordDataExfiltrationAttempt(string ipAddress, string virtualKey, long dataSize, string endpoint);

        /// <summary>
        /// Records anomalous access patterns
        /// </summary>
        /// <param name="ipAddress">IP address of the request</param>
        /// <param name="virtualKey">Virtual key used</param>
        /// <param name="anomaly">Type of anomaly detected</param>
        /// <param name="details">Additional details about the anomaly</param>
        void RecordAnomalousAccess(string ipAddress, string virtualKey, string anomaly, string details);

        /// <summary>
        /// Records an IP ban event
        /// </summary>
        /// <param name="ipAddress">IP address that was banned</param>
        /// <param name="reason">Reason for the ban</param>
        /// <param name="failedAttempts">Number of failed attempts before ban</param>
        void RecordIpBan(string ipAddress, string reason, int failedAttempts);

        /// <summary>
        /// Gets current security metrics
        /// </summary>
        /// <returns>Security metrics summary</returns>
        Task<SecurityMetricsDto> GetSecurityMetricsAsync();

        /// <summary>
        /// Gets recent security events
        /// </summary>
        /// <param name="minutes">Number of minutes to look back (default: 60)</param>
        /// <returns>List of recent security events</returns>
        Task<List<SecurityEventDto>> GetRecentSecurityEventsAsync(int minutes = 60);
    }
}