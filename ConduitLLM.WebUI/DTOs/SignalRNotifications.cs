using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Spend update notification DTO
    /// </summary>
    public class SpendUpdateNotification
    {
        public DateTime Timestamp { get; set; }
        public decimal NewSpend { get; set; }
        public decimal TotalSpend { get; set; }
        public decimal? RemainingBudget { get; set; }
        public decimal? BudgetPercentage { get; set; }
        public decimal? Budget { get; set; }
        public string? Model { get; set; }
        public string? TaskType { get; set; }
    }

    /// <summary>
    /// Budget alert notification DTO
    /// </summary>
    public class BudgetAlertNotification
    {
        public decimal Percentage { get; set; }
        public decimal Remaining { get; set; }
        public string Severity { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
        public DateTime BudgetPeriodEnd { get; set; }
    }

    /// <summary>
    /// Spend summary notification DTO
    /// </summary>
    public class SpendSummaryNotification
    {
        public string PeriodType { get; set; } = string.Empty;
        public decimal TotalSpend { get; set; }
        public Dictionary<string, decimal> ModelBreakdown { get; set; } = new();
        public List<ModelSpend> TopModels { get; set; } = new();
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        
        public class ModelSpend
        {
            public string Model { get; set; } = string.Empty;
            public decimal Spend { get; set; }
            public decimal Percentage { get; set; }
        }
    }

    /// <summary>
    /// Unusual spending notification DTO
    /// </summary>
    public class UnusualSpendingNotification
    {
        public string PatternType { get; set; } = string.Empty;
        public decimal CurrentSpend { get; set; }
        public decimal AverageSpend { get; set; }
        public decimal PercentageIncrease { get; set; }
        public string Timeframe { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
    }
}