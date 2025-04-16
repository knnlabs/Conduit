using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// DTO for requesting the creation of a new Virtual Key.
/// </summary>
public class CreateVirtualKeyRequestDto
{
    [Required]
    [MaxLength(100)]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Optional comma-separated list of allowed model aliases.
    /// If null/empty, all models are allowed.
    /// </summary>
    public string? AllowedModels { get; set; }

    public decimal? MaxBudget { get; set; }

    [MaxLength(20)]
    public string? BudgetDuration { get; set; } // e.g., "Total", "Monthly"

    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Optional JSON string for metadata.
    /// </summary>
    public string? Metadata { get; set; }
}
