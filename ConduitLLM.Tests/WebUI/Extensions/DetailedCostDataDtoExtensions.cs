using System;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Extensions for working with different DetailedCostDataDto classes in tests
    /// </summary>
    public static class DetailedCostDataDtoExtensions
    {
        /// <summary>
        /// Creates a legacy-style DetailedCostDataDto for tests
        /// </summary>
        /// <returns>A new DetailedCostDataDto for tests</returns>
        public static ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto CreateLegacyDetailedCostDataDto()
        {
            return new ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto
            {
                Name = "gpt-4",
                Cost = 0.025m,
                Percentage = 10.0m
            };
        }

        /// <summary>
        /// Gets the request count for a detailed cost data item
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>A fixed request count for test purposes</returns>
        public static int RequestCount(this ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto dto)
        {
            // For testing purposes, return a fixed value
            return 2;
        }

        /// <summary>
        /// Gets the input tokens for a detailed cost data item
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>A fixed input token count for test purposes</returns>
        public static int InputTokens(this ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto dto)
        {
            // For testing purposes, return a fixed value
            return 250;
        }

        /// <summary>
        /// Gets the output tokens for a detailed cost data item
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>A fixed output token count for test purposes</returns>
        public static int OutputTokens(this ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto dto)
        {
            // For testing purposes, return a fixed value
            return 125;
        }
        
        /// <summary>
        /// Gets the model name for a detailed cost data item
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>The model name</returns>
        public static string Model(this ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto dto)
        {
            // The Name property in the new DTO should contain the model name
            return dto.Name;
        }
        
        /// <summary>
        /// Gets the virtual key name for a detailed cost data item
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>The virtual key name</returns>
        public static string KeyName(this ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto dto)
        {
            // The Name property in the new DTO should contain the key name
            return dto.Name;
        }
    }
}