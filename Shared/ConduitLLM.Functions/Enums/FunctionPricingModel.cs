namespace ConduitLLM.Functions.Enums;

/// <summary>
/// Defines the pricing strategy for calculating function execution costs.
/// Uses the Strategy pattern to support different billing models.
/// </summary>
public enum FunctionPricingModel
{
    /// <summary>
    /// Fixed cost per execution, regardless of results or usage
    /// </summary>
    FlatRate = 1,

    /// <summary>
    /// Cost per result returned (e.g., per search result)
    /// </summary>
    PerResult = 2,

    /// <summary>
    /// Cost per token (used for Answer functions that consume LLM tokens)
    /// </summary>
    PerToken = 3,

    /// <summary>
    /// Volume-based tiered pricing (different rates for different usage levels)
    /// Configuration stored in JSON format
    /// </summary>
    Tiered = 4,

    /// <summary>
    /// Time-based pricing (cost per minute or second of execution)
    /// </summary>
    TimeBased = 5,

    /// <summary>
    /// Hybrid/combination pricing model
    /// Configuration stored in JSON format for flexibility
    /// </summary>
    Hybrid = 6
}
