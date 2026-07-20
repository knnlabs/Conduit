using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Admin.DTOs;

public sealed class ResolveIndeterminateTaskDto
{
    [Required]
    public string Resolution { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ProviderOperationId { get; set; }
}
