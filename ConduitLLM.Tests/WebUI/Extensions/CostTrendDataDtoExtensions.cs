using System;

using ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for CostTrendDataDto in tests
    /// </summary>
    public static class CostTrendDataDtoExtensions
    {
        /// <summary>
        /// Gets or adds the request count for backwards compatibility
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>A fixed request count for test purposes</returns>
        public static int RequestCount(this CostTrendDataDto dto)
        {
            // For testing purposes, return a fixed value 
            // This simulates the property that existed in the old DTO structure
            return 1;
        }

        /// <summary>
        /// Gets or adds the input tokens for backwards compatibility
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>A fixed input token count for test purposes</returns>
        public static int InputTokens(this CostTrendDataDto dto)
        {
            // For testing purposes, return a fixed value
            return 100;
        }

        /// <summary>
        /// Gets or adds the output tokens for backwards compatibility
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>A fixed output token count for test purposes</returns>
        public static int OutputTokens(this CostTrendDataDto dto)
        {
            // For testing purposes, return a fixed value
            return 50;
        }
    }
}
