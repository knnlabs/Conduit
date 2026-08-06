using ConduitLLM.Configuration;

namespace ConduitLLM.Admin.DTOs;

/// <summary>
/// The configuration and presentation metadata a provider type declares, projected from the
/// backend provider registry so administrative UIs do not maintain a second provider catalog.
/// </summary>
public sealed class ProviderSettingsSchemaDto
{
    /// <summary>The provider type these settings belong to.</summary>
    public ProviderType ProviderType { get; init; }

    /// <summary>The stable numeric value used by legacy provider-association contracts.</summary>
    public int ProviderTypeId { get; init; }

    /// <summary>The operator-facing provider name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Whether credentials for this provider require an API key value.</summary>
    public bool RequiresApiKey { get; init; }

    /// <summary>Whether an explicit API endpoint is required.</summary>
    public bool RequiresEndpoint { get; init; }

    /// <summary>Whether an operator may override the provider's registered endpoint.</summary>
    public bool SupportsCustomEndpoint { get; init; }

    /// <summary>Optional provider documentation URL.</summary>
    public string? HelpUrl { get; init; }

    /// <summary>Optional provider-level configuration guidance.</summary>
    public string? HelpText { get; init; }

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

    /// <summary>Optional registry-defined value used when the operator supplies none.</summary>
    public string? DefaultValue { get; init; }

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
