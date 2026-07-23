namespace ConduitLLM.Functions.DTOs;

public sealed class UpdateFunctionCredentialRequest
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? Organization { get; set; }
    public short? FunctionAccountGroup { get; set; }
    public bool? IsPrimary { get; set; }
    public bool? IsEnabled { get; set; }
    public string? KeyName { get; set; }
}
