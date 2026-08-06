using System.ComponentModel.DataAnnotations;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.DTOs;

public sealed class CreateFunctionCredentialRequest
{
    [Required]
    public FunctionProviderType ProviderType { get; set; }
    public int? FunctionConfigurationId { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? Organization { get; set; }
    public short FunctionAccountGroup { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? KeyName { get; set; }
}
