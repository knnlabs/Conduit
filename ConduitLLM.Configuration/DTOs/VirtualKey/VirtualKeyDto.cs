namespace ConduitLLM.Configuration.DTOs.VirtualKey; // Updated namespace

/// <summary>
/// Data Transfer Object representing the details of a Virtual Key, excluding sensitive hash.
/// </summary>
public class VirtualKeyDto
{
    public int Id { get; set; }
    public string KeyName { get; set; } = string.Empty;
    // Note: KeyHash is intentionally excluded
    // public string? KeyPrefix { get; set; } // Consider adding this if needed for display
    public string? AllowedModels { get; set; }
    public decimal? MaxBudget { get; set; }
    public decimal CurrentSpend { get; set; }
    public string? BudgetDuration { get; set; }
    public DateTime? BudgetStartDate { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Metadata { get; set; }
}
