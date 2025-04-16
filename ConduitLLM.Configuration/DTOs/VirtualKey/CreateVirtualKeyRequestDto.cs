using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.VirtualKey; // Updated namespace

/// <summary>
/// DTO for requesting the creation of a new virtual key.
/// </summary>
public class CreateVirtualKeyRequestDto
{
    [Required(ErrorMessage = "Key name is required.")]
    [MaxLength(100, ErrorMessage = "Key name cannot exceed 100 characters.")]
    public string KeyName { get; set; } = string.Empty;

    public string? AllowedModels { get; set; } // Comma-separated

    [Range(0.01, 1000000, ErrorMessage = "Max budget must be between 0.01 and 1,000,000, if specified.")]
    public decimal? MaxBudget { get; set; }

    [MaxLength(20)]
    public string? BudgetDuration { get; set; } // e.g., "Total", "Monthly"

    public DateTime? ExpiresAt { get; set; }

    public string? Metadata { get; set; } // Optional JSON metadata
}
