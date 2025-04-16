namespace ConduitLLM.Configuration.Constants;

/// <summary>
/// Constants related to virtual keys used across the application
/// </summary>
public static class VirtualKeyConstants
{
    /// <summary>
    /// Standard prefix for all virtual keys
    /// </summary>
    public const string KeyPrefix = "condt_";
    
    /// <summary>
    /// Default budget periods for virtual keys
    /// </summary>
    public static class BudgetPeriods
    {
        public const string Total = "Total";
        public const string Monthly = "Monthly";
        public const string Daily = "Daily";
    }
    
    /// <summary>
    /// Threshold percentages for budget warnings
    /// </summary>
    public static class BudgetWarningThresholds
    {
        public const decimal High = 95m;
        public const decimal Medium = 90m;
        public const decimal Low = 80m;
    }
}
