using ConduitLLM.Configuration.DTOs.Security;

namespace ConduitLLM.Security.Interfaces
{
    /// <summary>
    /// Service for collecting and aggregating security metrics
    /// </summary>
    public interface ISecurityMetricsService
    {
        /// <summary>
        /// Gets aggregated security metrics for a time period
        /// </summary>
        /// <param name="startTime">Start of the time period</param>
        /// <param name="endTime">End of the time period</param>
        /// <returns>Aggregated security metrics</returns>
        Task<AggregatedSecurityMetricsDto> GetAggregatedMetricsAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Gets security metrics by component
        /// </summary>
        /// <returns>Dictionary of component names to their metrics</returns>
        Task<Dictionary<string, ComponentSecurityMetricsDto>> GetComponentMetricsAsync();

        /// <summary>
        /// Gets security trends over time
        /// </summary>
        /// <param name="days">Number of days to analyze</param>
        /// <returns>Security trend data</returns>
        Task<SecurityTrendsDto> GetSecurityTrendsAsync(int days = 7);

        /// <summary>
        /// Gets top security threats
        /// </summary>
        /// <param name="count">Number of top threats to return</param>
        /// <returns>List of top threats</returns>
        Task<List<TopThreatDto>> GetTopThreatsAsync(int count = 10);

        /// <summary>
        /// Gets security compliance metrics
        /// </summary>
        /// <returns>Compliance metrics and status</returns>
        Task<ComplianceMetricsDto> GetComplianceMetricsAsync();

        /// <summary>
        /// Exports security metrics in a specific format
        /// </summary>
        /// <param name="format">Export format (csv, json, xml)</param>
        /// <param name="startTime">Start of the time period</param>
        /// <param name="endTime">End of the time period</param>
        /// <returns>Exported data as string</returns>
        Task<string> ExportMetricsAsync(string format, DateTime startTime, DateTime endTime);
    }
}