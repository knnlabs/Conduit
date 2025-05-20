// Using global namespace aliases from DTONamespaceAliases.cs

namespace ConduitLLM.Tests.Extensions
{
    /// <summary>
    /// Extension methods for converting between LogsSummaryDto types
    /// </summary>
    public static class LogsSummaryDtoToWebUIDtoExtensions
    {
        /// <summary>
        /// Converts a Configuration.Services.Dtos.LogsSummaryDto to a WebUI.DTOs.LogsSummaryDto
        /// </summary>
        public static WebUIDTOs.LogsSummaryDto ToWebUILogsSummaryDto(this ConfigServiceDtos.LogsSummaryDto source)
        {
            return new WebUIDTOs.LogsSummaryDto
            {
                TotalRequests = source.TotalRequests,
                EstimatedCost = source.TotalCost,
                InputTokens = source.TotalInputTokens,
                OutputTokens = source.TotalOutputTokens,
                AverageResponseTime = source.AverageResponseTimeMs,
                StartDate = source.StartDate,
                EndDate = source.EndDate,
                SuccessfulRequests = source.SuccessfulRequests,
                FailedRequests = source.FailedRequests
                // LastRequestDate is not present in the source DTO
            };
        }
    }
}