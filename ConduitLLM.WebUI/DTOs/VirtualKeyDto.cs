using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// Data Transfer Object representing a Virtual Key for API responses.
/// </summary>
public class VirtualKeyDto
{
    public int Id { get; set; }

    [Required]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// A prefix of the virtual key (e.g., "sk-...") for display purposes. 
    /// The full key is not exposed after creation.
    /// </summary>
    public string? KeyPrefix { get; set; }

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
