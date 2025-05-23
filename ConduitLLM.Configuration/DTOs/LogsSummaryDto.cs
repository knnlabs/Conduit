using System;
using System.Collections.Generic;
using System.Linq;
using ConduitLLM.Configuration.Services.Dtos;

#pragma warning disable CS0618 // Type or member is obsolete - We're managing the migration process
namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for logs summary statistics
    /// </summary>
    /// <remarks>
    /// IMPORTANT: There are two LogsSummaryDto classes in the project:
    /// 1. ConduitLLM.Configuration.DTOs.LogsSummaryDto (this one)
    /// 2. ConduitLLM.Configuration.Services.Dtos.LogsSummaryDto
    ///
    /// When referencing either class, use the fully qualified name to avoid ambiguity.
    /// This class is primarily for API/client consumption, while the Services.Dtos version
    /// is used internally by the RequestLogService.
    /// </remarks>
    public class LogsSummaryDto
    {
        /// <summary>
        /// Total number of requests in the period
        /// </summary>
        public int TotalRequests { get; set; }
        
        /// <summary>
        /// Total cost of all requests in the period
        /// </summary>
        public decimal EstimatedCost { get; set; }
        
        /// <summary>
        /// Total input tokens across all requests
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens across all requests
        /// </summary>
        public int OutputTokens { get; set; }
        
        /// <summary>
        /// Total tokens (input + output) for all requests
        /// </summary>
        public int TotalTokens => InputTokens + OutputTokens;
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTime { get; set; }
        
        /// <summary>
        /// Number of successful requests
        /// </summary>
        public int SuccessfulRequests { get; set; }
        
        /// <summary>
        /// Number of failed requests
        /// </summary>
        public int FailedRequests { get; set; }

        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate
        {
            get => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
            set { /* Setter for backward compatibility */ }
        }

        /// <summary>
        /// Date of the most recent request
        /// </summary>
        public DateTime? LastRequestDate { get; set; }
        
        /// <summary>
        /// Request count by model name
        /// </summary>
        public Dictionary<string, int> RequestsByModel { get; set; } = new Dictionary<string, int>();
        
        /// <summary>
        /// Cost by model name
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new Dictionary<string, decimal>();
        
        /// <summary>
        /// Request count by status code
        /// </summary>
        public Dictionary<int, int> RequestsByStatus { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// Daily statistics
        /// </summary>
        public List<DailyUsageStatsDto> DailyStats { get; set; } = new List<DailyUsageStatsDto>();
        
        // Backwards compatibility properties for Services.Dtos.LogsSummaryDto
        
        /// <summary>
        /// Total cost (alias for EstimatedCost)
        /// </summary>
        public decimal TotalCost
        {
            get => EstimatedCost;
            set => EstimatedCost = value;
        }
        
        /// <summary>
        /// Total input tokens (alias for InputTokens)
        /// </summary>
        public int TotalInputTokens
        {
            get => InputTokens;
            set => InputTokens = value;
        }
        
        /// <summary>
        /// Total output tokens (alias for OutputTokens)
        /// </summary>
        public int TotalOutputTokens
        {
            get => OutputTokens;
            set => OutputTokens = value;
        }
        
        /// <summary>
        /// Average response time (alias for AverageResponseTime)
        /// </summary>
        public double AverageResponseTimeMs
        {
            get => AverageResponseTime;
            set => AverageResponseTime = value;
        }

        /// <summary>
        /// Converts a Services.Dtos.LogsSummaryDto to a DTOs.LogsSummaryDto
        /// </summary>
        /// <param name="serviceDto">The service DTO to convert</param>
        /// <returns>A new LogsSummaryDto instance with values from the service DTO, or null if the input is null</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CS0618:Type or member is obsolete", Justification = "Necessary for the migration process")]
        public static LogsSummaryDto? FromServiceDto(Services.Dtos.LogsSummaryDto? serviceDto)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (serviceDto == null)
                return null;

            var result = new LogsSummaryDto
            {
                TotalRequests = serviceDto.TotalRequests,
                EstimatedCost = serviceDto.TotalCost,
                InputTokens = serviceDto.TotalInputTokens,
                OutputTokens = serviceDto.TotalOutputTokens,
                AverageResponseTime = serviceDto.AverageResponseTimeMs,
                SuccessfulRequests = serviceDto.SuccessfulRequests,
                FailedRequests = serviceDto.FailedRequests
            };

            // Copy status data
            foreach (var entry in serviceDto.RequestsByStatus)
            {
                result.RequestsByStatus[entry.Key] = entry.Value;
            }

            // Copy model data
            foreach (var entry in serviceDto.RequestsByModelDict)
            {
                result.RequestsByModel[entry.Key] = entry.Value;
            }

            foreach (var entry in serviceDto.CostByModel)
            {
                result.CostByModel[entry.Key] = entry.Value;
            }

            // Copy daily stats if available
            if (serviceDto.DailyStats != null)
            {
                foreach (var stat in serviceDto.DailyStats)
                {
                    result.DailyStats.Add(new DailyUsageStatsDto
                    {
                        Date = stat.Date,
                        ModelId = string.Empty, // Not available in the service DTO
                        RequestCount = stat.RequestCount,
                        InputTokens = stat.InputTokens,
                        OutputTokens = stat.OutputTokens,
                        Cost = stat.Cost
                    });
                }
            }

            // Copy model stats if available
            if (serviceDto.RequestsByModel != null)
            {
                foreach (var model in serviceDto.RequestsByModel)
                {
                    // Add to dictionary if not already present
                    if (!result.RequestsByModel.ContainsKey(model.ModelName))
                    {
                        result.RequestsByModel[model.ModelName] = model.RequestCount;
                    }

                    if (!result.CostByModel.ContainsKey(model.ModelName))
                    {
                        result.CostByModel[model.ModelName] = model.Cost;
                    }
                }
            }

            return result;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Converts this DTO to a Services.Dtos.LogsSummaryDto
        /// </summary>
        /// <returns>A new Services.Dtos.LogsSummaryDto with values from this instance</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CS0618:Type or member is obsolete", Justification = "Necessary for the migration process")]
        public 
#pragma warning disable CS0618 // Type or member is obsolete
        Services.Dtos.LogsSummaryDto 
#pragma warning restore CS0618
        ToServiceDto()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var result = new Services.Dtos.LogsSummaryDto
            {
                TotalRequests = this.TotalRequests,
                TotalCost = this.EstimatedCost,
                TotalInputTokens = this.InputTokens,
                TotalOutputTokens = this.OutputTokens,
                AverageResponseTimeMs = this.AverageResponseTime,
                SuccessfulRequests = this.SuccessfulRequests,
                FailedRequests = this.FailedRequests,
                SuccessRate = this.SuccessRate
            };

            // Copy status data
            foreach (var entry in this.RequestsByStatus)
            {
                result.RequestsByStatus[entry.Key] = entry.Value;
            }

            // Copy model data
            foreach (var entry in this.RequestsByModel)
            {
                result.RequestsByModelDict[entry.Key] = entry.Value;
            }

            foreach (var entry in this.CostByModel)
            {
                result.CostByModel[entry.Key] = entry.Value;
            }

            // Create model stats entries
            if (this.RequestsByModel != null)
            {
                foreach (var entry in this.RequestsByModel)
                {
                    var cost = this.CostByModel.ContainsKey(entry.Key) ? this.CostByModel[entry.Key] : 0;
                    
                    result.RequestsByModel.Add(new RequestsByModelDto
                    {
                        ModelName = entry.Key,
                        RequestCount = entry.Value,
                        Cost = cost,
                        Percentage = this.TotalRequests > 0 ? (double)entry.Value / this.TotalRequests * 100 : 0
                    });
                }
            }

            // Create daily stats
            if (this.DailyStats != null)
            {
                foreach (var stat in this.DailyStats)
                {
                    result.DailyStats.Add(new DailyStatsDto
                    {
                        Date = stat.Date,
                        RequestCount = stat.RequestCount,
                        InputTokens = stat.InputTokens,
                        OutputTokens = stat.OutputTokens,
                        Cost = stat.Cost,
                        AverageResponseTime = 0, // Not available in our DTO
                        SuccessfulRequests = 0,  // Not available in our DTO
                        FailedRequests = 0       // Not available in our DTO
                    });
                }
            }

            return result;
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
    
    /// <summary>
    /// Daily usage statistics for the logs summary
    /// </summary>
    public class DailyUsageStatsDto
    {
        /// <summary>
        /// Date for the statistics
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Model ID for this record
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Model name (compatibility property)
        /// </summary>
        public string ModelName
        {
            get => ModelId;
            set => ModelId = value;
        }

        /// <summary>
        /// Number of requests on this date
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Number of input tokens on this date
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of output tokens on this date
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Total cost for this date
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Total cost (compatibility property)
        /// </summary>
        public decimal TotalCost
        {
            get => Cost;
            set => Cost = value;
        }
    }
}
#pragma warning restore CS0618 // Type or member is obsolete