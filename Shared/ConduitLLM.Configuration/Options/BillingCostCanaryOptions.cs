using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Options;

/// <summary>
/// Configuration for the synthetic model-cost canary.
/// </summary>
public sealed class BillingCostCanaryOptions
{
    public const string SectionName = "BillingCostCanary";

    /// <summary>
    /// Enables periodic validation of every active model mapping.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Delay between completed canary runs.
    /// </summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 5;
}
