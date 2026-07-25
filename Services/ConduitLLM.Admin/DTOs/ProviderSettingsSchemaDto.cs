using ConduitLLM.Configuration;

namespace ConduitLLM.Admin.DTOs;

/// <summary>
/// The structured settings a provider type declares, projected from the backend provider registry
/// so administrative UIs render and validate the same fields the backend enforces.
/// </summary>
public sealed class ProviderSettingsSchemaDto
{
    /// <summary>The provider type these settings belong to.</summary>
    public ProviderType ProviderType { get; init; }

    /// <summary>The declared settings, in the order they should be presented.</summary>
    public IReadOnlyList<ProviderSettingFieldDto> Settings { get; init; } = Array.Empty<ProviderSettingFieldDto>();
}

/// <summary>
/// A single structured, provider-scoped setting an operator supplies in addition to the API key
/// (for example a Cloudflare account ID).
/// </summary>
public sealed class ProviderSettingFieldDto
{
    /// <summary>Stable machine key; also the storage key in the provider's settings map.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable field label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Optional help text describing where to find the value.</summary>
    public string? HelpText { get; init; }

    /// <summary>Optional example value shown as the input placeholder.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Whether the operator must supply this setting.</summary>
    public bool Required { get; init; }

    /// <summary>Whether the value is sensitive and should be entered masked.</summary>
    public bool Secret { get; init; }

    /// <summary>
    /// Optional regular-expression source the value must match. The backend remains authoritative;
    /// clients use this only to fail fast before submitting.
    /// </summary>
    public string? ValidationRegex { get; init; }
}
