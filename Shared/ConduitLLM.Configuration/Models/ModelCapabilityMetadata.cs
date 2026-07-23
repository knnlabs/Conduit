using System.Text.Json;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Models;

/// <summary>
/// Describes where model capability metadata originated.
/// </summary>
public enum ModelCapabilitySource
{
    Unknown = 0,
    LegacyInferred = 1,
    Curated = 2,
    ProviderApi = 3,
    Manual = 4
}

/// <summary>
/// Canonical modality names used in storage and API contracts.
/// </summary>
public static class ModelModalities
{
    public const string Text = "text";
    public const string Image = "image";
    public const string Audio = "audio";
    public const string Video = "video";
    public const string File = "file";

    private static readonly HashSet<string> Known =
        new(StringComparer.OrdinalIgnoreCase) { Text, Image, Audio, Video, File };

    public static IReadOnlyList<string>? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<string[]>(json) ?? []);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Serialize(IEnumerable<string>? modalities) =>
        modalities is null ? null : JsonSerializer.Serialize(Normalize(modalities));

    public static IReadOnlyList<string> Normalize(IEnumerable<string> modalities) =>
        modalities
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    public static bool IsKnown(string modality) => Known.Contains(modality);
}

/// <summary>
/// Nullable provider-specific overrides for operational model capabilities.
/// A null member inherits the canonical model value.
/// </summary>
public sealed class ProviderOperationalCapabilities
{
    public bool? SupportsChat { get; set; }
    public bool? SupportsStreaming { get; set; }
    public bool? SupportsVision { get; set; }
    public bool? SupportsImageGeneration { get; set; }
    public bool? SupportsVideoGeneration { get; set; }
    public bool? SupportsEmbeddings { get; set; }
    public bool? SupportsFunctionCalling { get; set; }
    public bool? SupportsSpeechToText { get; set; }
    public bool? SupportsTextToSpeech { get; set; }
    public bool? SupportsRerank { get; set; }
}

/// <summary>
/// Effective capabilities after canonical metadata and provider overrides are combined.
/// </summary>
public sealed record EffectiveModelCapabilities(
    IReadOnlyList<string>? InputModalities,
    IReadOnlyList<string>? OutputModalities,
    bool SupportsChat,
    bool SupportsStreaming,
    bool SupportsVision,
    bool SupportsImageGeneration,
    bool SupportsVideoGeneration,
    bool SupportsEmbeddings,
    bool SupportsFunctionCalling,
    bool SupportsSpeechToText,
    bool SupportsTextToSpeech,
    bool SupportsRerank,
    ModelCapabilitySource Source,
    DateTime? LastVerifiedAt)
{
    public bool SupportsImageInput => InputModalities?.Contains(ModelModalities.Image, StringComparer.Ordinal) == true;
    public bool SupportsVideoInput => InputModalities?.Contains(ModelModalities.Video, StringComparer.Ordinal) == true;
    public bool SupportsAudioInput => InputModalities?.Contains(ModelModalities.Audio, StringComparer.Ordinal) == true;
    public bool SupportsFileInput => InputModalities?.Contains(ModelModalities.File, StringComparer.Ordinal) == true;
    public bool SupportsVideoUnderstanding =>
        SupportsVideoInput &&
        OutputModalities?.Contains(ModelModalities.Text, StringComparer.Ordinal) == true;
}

/// <summary>
/// Resolves canonical and provider-specific model capability metadata.
/// </summary>
public static class ModelCapabilityResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static EffectiveModelCapabilities Resolve(
        Model model,
        ModelProviderTypeAssociation? association = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var canonicalInputs = ModelModalities.Parse(model.InputModalitiesJson);
        var canonicalOutputs = ModelModalities.Parse(model.OutputModalitiesJson);
        if (model.CapabilitySource != ModelCapabilitySource.Unknown)
        {
            canonicalInputs ??= InferLegacyInputs(model);
            canonicalOutputs ??= InferLegacyOutputs(model);
        }

        var inputs = association?.InputModalitiesJson is null
            ? canonicalInputs
            : ModelModalities.Parse(association.InputModalitiesJson);
        var outputs = association?.OutputModalitiesJson is null
            ? canonicalOutputs
            : ModelModalities.Parse(association.OutputModalitiesJson);

        var operationOverrides = DeserializeOverrides(association?.OperationalCapabilitiesJson);

        return new EffectiveModelCapabilities(
            inputs,
            outputs,
            operationOverrides?.SupportsChat ?? model.SupportsChat,
            operationOverrides?.SupportsStreaming ?? model.SupportsStreaming,
            inputs?.Contains(ModelModalities.Image, StringComparer.Ordinal) == true,
            operationOverrides?.SupportsImageGeneration ?? model.SupportsImageGeneration,
            operationOverrides?.SupportsVideoGeneration ?? model.SupportsVideoGeneration,
            operationOverrides?.SupportsEmbeddings ?? model.SupportsEmbeddings,
            operationOverrides?.SupportsFunctionCalling ?? model.SupportsFunctionCalling,
            operationOverrides?.SupportsSpeechToText ?? model.SupportsSpeechToText,
            operationOverrides?.SupportsTextToSpeech ?? model.SupportsTextToSpeech,
            operationOverrides?.SupportsRerank ?? model.SupportsRerank,
            association?.CapabilitySource ?? model.CapabilitySource,
            association?.CapabilitiesLastVerifiedAt ?? model.CapabilitiesLastVerifiedAt);
    }

    public static string? SerializeOverrides(ProviderOperationalCapabilities? capabilities) =>
        capabilities is null ? null : JsonSerializer.Serialize(capabilities, JsonOptions);

    public static ProviderOperationalCapabilities? DeserializeOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProviderOperationalCapabilities>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> InferLegacyInputs(Model model)
    {
        var modalities = new List<string>();
        if (model.SupportsChat ||
            model.SupportsEmbeddings ||
            model.SupportsImageGeneration ||
            model.SupportsVideoGeneration ||
            model.SupportsTextToSpeech ||
            model.SupportsRerank)
        {
            modalities.Add(ModelModalities.Text);
        }
        if (model.SupportsVision)
        {
            modalities.Add(ModelModalities.Image);
        }
        if (model.SupportsSpeechToText)
        {
            modalities.Add(ModelModalities.Audio);
        }
        return ModelModalities.Normalize(modalities);
    }

    private static IReadOnlyList<string> InferLegacyOutputs(Model model)
    {
        var modalities = new List<string>();
        if (model.SupportsChat || model.SupportsSpeechToText || model.SupportsRerank)
        {
            modalities.Add(ModelModalities.Text);
        }
        if (model.SupportsImageGeneration)
        {
            modalities.Add(ModelModalities.Image);
        }
        if (model.SupportsVideoGeneration)
        {
            modalities.Add(ModelModalities.Video);
        }
        if (model.SupportsTextToSpeech)
        {
            modalities.Add(ModelModalities.Audio);
        }
        return ModelModalities.Normalize(modalities);
    }
}
