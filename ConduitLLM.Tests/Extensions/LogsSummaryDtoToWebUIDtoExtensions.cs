// Using global namespace aliases from DTONamespaceAliases.cs

namespace ConduitLLM.Tests.Extensions
{
    /// <summary>
    /// Extension methods for converting between LogsSummaryDto types
    /// </summary>
    public static class LogsSummaryDtoToWebUIDtoExtensions
    {
        /// <summary>
        /// Converts a Configuration.DTOs.LogsSummaryDto to a WebUI.DTOs.LogsSummaryDto
        /// </summary>
        public static WebUIDTOs.LogsSummaryDto ToWebUILogsSummaryDto(this ConfigDTOs.LogsSummaryDto source)
        {
            return new WebUIDTOs.LogsSummaryDto
            {
                TotalRequests = source.TotalRequests,
                EstimatedCost = source.TotalCost,
                InputTokens = source.TotalInputTokens,
                OutputTokens = source.TotalOutputTokens,
                AverageResponseTime = source.AverageResponseTimeMs,
                SuccessfulRequests = source.SuccessfulRequests,
                FailedRequests = source.FailedRequests,
                LastRequestDate = source.LastRequestDate
                // StartDate and EndDate may need to be handled separately if needed
            };
        }
    }
}