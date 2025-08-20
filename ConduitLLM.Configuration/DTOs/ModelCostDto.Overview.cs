using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for model cost overview data used in dashboards
    /// </summary>
    public class ModelCostOverviewDto
    {
        /// <summary>
        /// Model name or pattern
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Number of requests for this model
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Total cost for this model in USD
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Total input tokens processed
        /// </summary>
        public long InputTokens { get; set; }

        /// <summary>
        /// Total output tokens generated
        /// </summary>
        public long OutputTokens { get; set; }
    }
}