using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// DTO for requesting updates to an existing Virtual Key.
/// </summary>
public class UpdateVirtualKeyRequestDto
{
    /// <summary>
    /// Optional new name for the key.
    /// </summary>
    [MaxLength(100)]
    public string? KeyName { get; set; }

    /// <summary>
    /// Optional updated comma-separated list of allowed model aliases.
    /// </summary>
    public string? AllowedModels { get; set; }

    /// <summary>
    /// Optional updated budget. Use null to remove budget.
    /// </summary>
    public decimal? MaxBudget { get; set; }

    /// <summary>
    /// Optional updated budget duration.
    /// </summary>
    [MaxLength(20)]
    public string? BudgetDuration { get; set; }

    /// <summary>
    /// Optional flag to enable/disable the key.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Optional updated expiry date. Use null to remove expiry.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Optional updated JSON string for metadata.
    /// </summary>
    public string? Metadata { get; set; }

    // Note: CurrentSpend and BudgetStartDate are typically managed by the system, not directly updated by the user.
}
