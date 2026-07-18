namespace ConduitLLM.Configuration.Constants;

/// <summary>
/// Valid billing units for provider tools.
/// Used for validation in the controller and cost calculation in the service.
/// </summary>
public static class ProviderToolBillingUnits
{
    public const string Requests = "requests";
    public const string Hours = "hours";
    public const string Minutes = "minutes";
    public const string Searches = "searches";
    public const string Executions = "executions";
    public const string Characters = "characters";
    public const string Tokens = "tokens";

    /// <summary>
    /// All valid billing units.
    /// </summary>
    public static readonly string[] All =
    {
        Requests, Hours, Minutes, Searches, Executions, Characters, Tokens
    };

    /// <summary>
    /// Returns true if the given billing unit is valid.
    /// </summary>
    public static bool IsValid(string? billingUnit)
    {
        if (string.IsNullOrEmpty(billingUnit))
            return true; // null/empty defaults to count-based billing

        return Array.Exists(All, u => u.Equals(billingUnit, StringComparison.OrdinalIgnoreCase));
    }
}
