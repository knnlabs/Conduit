namespace ConduitLLM.Admin.DTOs;

/// <summary>Server-owned metadata for a known global setting.</summary>
public sealed class GlobalSettingDefinitionDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = "string";
    public string Category { get; init; } = "General";
    public string DefaultValue { get; init; } = string.Empty;
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public int? MaxLength { get; init; }
    public string? FeatureRoute { get; init; }
    public bool IsFeatureOwned { get; init; }
}
