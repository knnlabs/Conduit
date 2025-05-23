using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Services.Dtos
{
    /// <summary>
    /// Legacy compatibility class for logs summary statistics
    /// </summary>
    /// <remarks>
    /// This class is provided for backwards compatibility only and delegates all functionality 
    /// to ConduitLLM.Configuration.DTOs.LogsSummaryDto.
    /// </remarks>
    [Obsolete("This class is being consolidated with ConduitLLM.Configuration.DTOs.LogsSummaryDto. Use that version for new code.")]
    public class LogsSummaryDto
    {
        private ConduitLLM.Configuration.DTOs.LogsSummaryDto _innerDto;

        /// <summary>
        /// Creates a new instance of the LogsSummaryDto class
        /// </summary>
        public LogsSummaryDto()
        {
            _innerDto = new ConduitLLM.Configuration.DTOs.LogsSummaryDto();
        }

        /// <summary>
        /// Total number of requests in the period
        /// </summary>
        public int TotalRequests 
        { 
            get => _innerDto.TotalRequests; 
            set => _innerDto.TotalRequests = value; 
        }
        
        /// <summary>
        /// Total cost of all requests in the period
        /// </summary>
        public decimal TotalCost 
        { 
            get => _innerDto.TotalCost; 
            set => _innerDto.TotalCost = value; 
        }
        
        /// <summary>
        /// Total input tokens across all requests
        /// </summary>
        public int TotalInputTokens 
        { 
            get => _innerDto.TotalInputTokens; 
            set => _innerDto.TotalInputTokens = value; 
        }
        
        /// <summary>
        /// Total output tokens across all requests
        /// </summary>
        public int TotalOutputTokens 
        { 
            get => _innerDto.TotalOutputTokens; 
            set => _innerDto.TotalOutputTokens = value; 
        }
        
        /// <summary>
        /// Total tokens (input + output) for all requests
        /// </summary>
        public int TotalTokens => TotalInputTokens + TotalOutputTokens;
        
        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs 
        { 
            get => _innerDto.AverageResponseTimeMs; 
            set => _innerDto.AverageResponseTimeMs = value; 
        }
        
        /// <summary>
        /// Start date of the period
        /// </summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date of the period
        /// </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Request count by model name (legacy format)
        /// </summary>
        public Dictionary<string, int> RequestsByModelDict 
        { 
            get => _innerDto.RequestsByModel; 
            set => _innerDto.RequestsByModel = value; 
        }
        
        /// <summary>
        /// Cost by model name
        /// </summary>
        public Dictionary<string, decimal> CostByModel 
        { 
            get => _innerDto.CostByModel; 
            set => _innerDto.CostByModel = value; 
        }
        
        /// <summary>
        /// Request count by virtual key ID
        /// </summary>
        public Dictionary<int, KeySummary> RequestsByKey { get; set; } = new Dictionary<int, KeySummary>();
        
        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate 
        { 
            get => _innerDto.SuccessRate; 
            set => _innerDto.SuccessRate = value; 
        }
        
        /// <summary>
        /// Requests by status code
        /// </summary>
        public Dictionary<int, int> RequestsByStatus 
        { 
            get => _innerDto.RequestsByStatus; 
            set => _innerDto.RequestsByStatus = value; 
        }
        
        /// <summary>
        /// Number of successful requests
        /// </summary>
        public int SuccessfulRequests 
        { 
            get => _innerDto.SuccessfulRequests; 
            set => _innerDto.SuccessfulRequests = value; 
        }
        
        /// <summary>
        /// Number of failed requests
        /// </summary>
        public int FailedRequests 
        { 
            get => _innerDto.FailedRequests; 
            set => _innerDto.FailedRequests = value; 
        }
        
        /// <summary>
        /// Daily statistics
        /// </summary>
        public List<DailyStatsDto> DailyStats { get; set; } = new List<DailyStatsDto>();
        
        /// <summary>
        /// Model usage statistics
        /// </summary>
        public List<RequestsByModelDto> RequestsByModel { get; set; } = new List<RequestsByModelDto>();

        /// <summary>
        /// Converts this DTO to a DTOs.LogsSummaryDto
        /// </summary>
        /// <returns>A new DTOs.LogsSummaryDto with values from this instance</returns>
        public ConduitLLM.Configuration.DTOs.LogsSummaryDto? ToApiDto()
        {
            return _innerDto;
        }

        /// <summary>
        /// Creates a service DTO from an API DTO
        /// </summary>
        /// <param name="apiDto">The API DTO to convert</param>
        /// <returns>A new LogsSummaryDto instance with values from the API DTO, or null if the input is null</returns>
        public static LogsSummaryDto? FromApiDto(ConduitLLM.Configuration.DTOs.LogsSummaryDto? apiDto)
        {
            if (apiDto == null)
                return null;
            
            var dto = new LogsSummaryDto();
            dto._innerDto = apiDto;
            return dto;
        }
    }
    
    /// <summary>
    /// Summary statistics for a virtual key
    /// </summary>
    public class KeySummary
    {
        /// <summary>
        /// Name of the virtual key
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// Request count for this key
        /// </summary>
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Total cost for this key
        /// </summary>
        public decimal TotalCost { get; set; }
    }
}