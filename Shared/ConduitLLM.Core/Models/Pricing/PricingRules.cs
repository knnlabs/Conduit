using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models.Pricing;

/// <summary>
/// Root configuration for the pricing rules engine.
/// Stored in ModelCost.PricingConfiguration as JSON.
/// </summary>
public class PricingRulesConfig
{
    /// <summary>
    /// Schema version for future compatibility.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// How the rate is applied: "per_unit", "per_second", or "per_step".
    /// </summary>
    [JsonPropertyName("pricingType")]
    public string PricingType { get; set; } = "per_unit";

    /// <summary>
    /// Field name from Usage that provides the quantity.
    /// Examples: "ImageCount", "VideoDurationSeconds", "InferenceSteps".
    /// </summary>
    [JsonPropertyName("unitField")]
    public string? UnitField { get; set; }

    /// <summary>
    /// Fallback rate if no rules match.
    /// </summary>
    [JsonPropertyName("defaultRate")]
    public decimal DefaultRate { get; set; }

    /// <summary>
    /// List of pricing rules evaluated in priority order.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<PricingRule> Rules { get; set; } = new();

    /// <summary>
    /// Optional validation constraints for input parameters.
    /// </summary>
    [JsonPropertyName("constraints")]
    public PricingConstraints? Constraints { get; set; }
}

/// <summary>
/// A single pricing rule with conditions and rate.
/// Rules are evaluated in priority order (highest first).
/// </summary>
public class PricingRule
{
    /// <summary>
    /// Key-value pairs that must all match (AND logic).
    /// Keys are parameter names (e.g., "resolution", "with_audio").
    /// Values can be strings, booleans, or numbers.
    /// </summary>
    [JsonPropertyName("conditions")]
    public Dictionary<string, object> Conditions { get; set; } = new();

    /// <summary>
    /// The rate to apply when all conditions match.
    /// </summary>
    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    /// <summary>
    /// Higher priority rules are evaluated first (default: 0).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Human-readable description of this rule.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Validation constraints for pricing parameters.
/// </summary>
public class PricingConstraints
{
    /// <summary>
    /// Minimum allowed duration in seconds.
    /// </summary>
    [JsonPropertyName("minDuration")]
    public double? MinDuration { get; set; }

    /// <summary>
    /// Maximum allowed duration in seconds.
    /// </summary>
    [JsonPropertyName("maxDuration")]
    public double? MaxDuration { get; set; }

    /// <summary>
    /// Minimum allowed inference steps.
    /// </summary>
    [JsonPropertyName("minSteps")]
    public int? MinSteps { get; set; }

    /// <summary>
    /// Maximum allowed inference steps.
    /// </summary>
    [JsonPropertyName("maxSteps")]
    public int? MaxSteps { get; set; }

    /// <summary>
    /// List of allowed resolution values.
    /// </summary>
    [JsonPropertyName("allowedResolutions")]
    public List<string>? AllowedResolutions { get; set; }
}

/// <summary>
/// Context information passed to the pricing evaluator.
/// Used for audit logging and correlation.
/// </summary>
public class PricingContext
{
    /// <summary>
    /// Virtual key that will be charged.
    /// </summary>
    public int VirtualKeyId { get; set; }

    /// <summary>
    /// Model identifier.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Model cost configuration ID being used.
    /// </summary>
    public int ModelCostId { get; set; }

    /// <summary>
    /// Request ID for correlation with request logs.
    /// </summary>
    public string? RequestId { get; set; }
}

/// <summary>
/// Result of pricing rule evaluation.
/// </summary>
public class PricingEvaluationResult
{
    /// <summary>
    /// Final calculated cost.
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// The rate that was applied.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// The quantity (duration, count, steps) used in calculation.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The rule that matched, or null if default rate was used.
    /// </summary>
    public PricingRule? MatchedRule { get; set; }

    /// <summary>
    /// Whether the default rate was used (no rule matched).
    /// </summary>
    public bool UsedDefaultRate { get; set; }
}

/// <summary>
/// Parameter definition from ModelSeries.Parameters schema.
/// Used for validation and UI generation.
/// </summary>
public class ParameterDefinition
{
    /// <summary>
    /// Parameter type from the persisted UI schema. Pricing validation recognizes both the
    /// canonical types ("enum", "boolean", "integer", "number", "string") and UI control
    /// aliases such as "select", "checkbox", "toggle", "slider", and "resolution".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>
    /// Allowed values for enum types.
    /// </summary>
    [JsonPropertyName("options")]
    [JsonConverter(typeof(ParameterOptionValuesConverter))]
    public List<string>? Options { get; set; }

    /// <summary>
    /// Minimum value for numeric types.
    /// </summary>
    [JsonPropertyName("min")]
    public decimal? Min { get; set; }

    /// <summary>
    /// Maximum value for numeric types.
    /// </summary>
    [JsonPropertyName("max")]
    public decimal? Max { get; set; }

    /// <summary>
    /// Default value for this parameter.
    /// </summary>
    [JsonPropertyName("default")]
    public object? Default { get; set; }

    /// <summary>
    /// Step increment for numeric inputs.
    /// </summary>
    [JsonPropertyName("step")]
    public decimal? Step { get; set; }
}

/// <summary>
/// Normalizes persisted UI-schema options, which may be primitive values or
/// <c>{ "value": ..., "label": ... }</c> objects, into their comparable values.
/// </summary>
public sealed class ParameterOptionValuesConverter : JsonConverter<List<string>>
{
    /// <inheritdoc />
    public override List<string>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Parameter options must be an array.");

        using var document = JsonDocument.ParseValue(ref reader);
        var values = new List<string>();
        foreach (var option in document.RootElement.EnumerateArray())
        {
            var value = option.ValueKind == JsonValueKind.Object &&
                option.TryGetProperty("value", out var objectValue)
                    ? objectValue
                    : option;

            var normalized = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

            if (normalized != null)
                values.Add(normalized);
        }

        return values;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        List<string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var option in value)
            writer.WriteStringValue(option);
        writer.WriteEndArray();
    }
}
