using System.Text.Json;
using ConduitLLM.Core.Models.Pricing;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Interface for validating pricing rules configurations.
/// </summary>
public interface IPricingRulesValidator
{
    /// <summary>
    /// Validates a pricing rules configuration.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <param name="parameterSchema">Optional parameter schema JSON from ModelSeries.Parameters.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    ValidationResult Validate(PricingRulesConfig config, string? parameterSchema = null);

    /// <summary>
    /// Parses and validates a pricing configuration JSON string.
    /// </summary>
    /// <param name="json">The JSON string to parse and validate.</param>
    /// <param name="parameterSchema">Optional parameter schema JSON from ModelSeries.Parameters.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    ValidationResult ValidateJson(string json, string? parameterSchema = null);
}

/// <summary>
/// Result of pricing rules validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the configuration is valid (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// List of validation errors that must be fixed.
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// List of warnings that don't prevent saving but should be addressed.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// The parsed configuration if JSON parsing succeeded.
    /// </summary>
    public PricingRulesConfig? ParsedConfig { get; set; }
}

/// <summary>
/// A single validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// The field that has the error.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Description of the error.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Index of the rule if the error is rule-specific.
    /// </summary>
    public int? RuleIndex { get; set; }
}

/// <summary>
/// Validates pricing rules configurations against schema and business rules.
/// </summary>
public class PricingRulesValidator : IPricingRulesValidator
{
    private readonly ILogger<PricingRulesValidator> _logger;

    private static readonly string[] ValidPricingTypes = { "per_unit", "per_second", "per_step" };
    private static readonly string[] ValidUnitFields = { "ImageCount", "VideoCount", "VideoDurationSeconds", "InferenceSteps" };

    public PricingRulesValidator(ILogger<PricingRulesValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ValidationResult ValidateJson(string json, string? parameterSchema = null)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(json))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "json",
                Message = "Pricing configuration JSON is required"
            });
            return result;
        }

        try
        {
            var config = JsonSerializer.Deserialize<PricingRulesConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "json",
                    Message = "Failed to parse pricing configuration JSON"
                });
                return result;
            }

            result.ParsedConfig = config;
            return Validate(config, parameterSchema);
        }
        catch (JsonException ex)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "json",
                Message = $"Invalid JSON: {ex.Message}"
            });
            return result;
        }
    }

    /// <inheritdoc />
    public ValidationResult Validate(PricingRulesConfig config, string? parameterSchema = null)
    {
        var result = new ValidationResult { ParsedConfig = config };

        // 1. Validate required fields
        ValidateRequiredFields(config, result);

        // 2. Validate pricing type
        ValidatePricingType(config, result);

        // 3. Validate unit field
        ValidateUnitField(config, result);

        // 4. Validate rules structure
        ValidateRules(config, result);

        // 5. Validate against parameter schema (if provided)
        if (!string.IsNullOrEmpty(parameterSchema))
        {
            ValidateAgainstSchema(config, parameterSchema, result);
        }

        // 6. Check for duplicate rules
        CheckDuplicateRules(config, result);

        // 7. Validate constraints
        ValidateConstraints(config, result);

        // 8. Check for potential issues
        CheckPotentialIssues(config, result);

        return result;
    }

    private void ValidateRequiredFields(PricingRulesConfig config, ValidationResult result)
    {
        if (string.IsNullOrEmpty(config.Version))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "version",
                Message = "Version is required"
            });
        }
        else if (config.Version != "1.0")
        {
            result.Warnings.Add($"Unknown schema version '{config.Version}'. Expected '1.0'.");
        }

        if (string.IsNullOrEmpty(config.PricingType))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "pricingType",
                Message = "Pricing type is required"
            });
        }

        if (config.DefaultRate <= 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "defaultRate",
                Message = "Default rate must be greater than zero so unmatched usage cannot be free"
            });
        }
    }

    private void ValidatePricingType(PricingRulesConfig config, ValidationResult result)
    {
        if (!string.IsNullOrEmpty(config.PricingType) && !ValidPricingTypes.Contains(config.PricingType))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "pricingType",
                Message = $"Invalid pricing type '{config.PricingType}'. Must be one of: {string.Join(", ", ValidPricingTypes)}"
            });
        }
    }

    private void ValidateUnitField(PricingRulesConfig config, ValidationResult result)
    {
        if (!string.IsNullOrEmpty(config.UnitField) && !ValidUnitFields.Contains(config.UnitField))
        {
            result.Warnings.Add($"Unit field '{config.UnitField}' is not a standard field. Valid options: {string.Join(", ", ValidUnitFields)}");
        }

        // Recommend unit field based on pricing type
        if (string.IsNullOrEmpty(config.UnitField))
        {
            var recommendedField = config.PricingType switch
            {
                "per_second" => "VideoDurationSeconds",
                "per_step" => "InferenceSteps",
                "per_unit" => "ImageCount",
                _ => null
            };

            if (recommendedField != null)
            {
                result.Warnings.Add($"No unitField specified. Consider setting it to '{recommendedField}' for {config.PricingType} pricing.");
            }
        }
    }

    private void ValidateRules(PricingRulesConfig config, ValidationResult result)
    {
        if (config.Rules.Count == 0)
        {
            result.Warnings.Add("No rules defined. Only the default rate will be used.");
            return;
        }

        for (int i = 0; i < config.Rules.Count; i++)
        {
            var rule = config.Rules[i];

            if (rule.Rate < 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "rate",
                    Message = "Rate cannot be negative",
                    RuleIndex = i
                });
            }

            if (rule.Conditions == null)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "conditions",
                    Message = "Conditions object is required (use {} for catch-all)",
                    RuleIndex = i
                });
            }
            else if (rule.Conditions.Count == 0)
            {
                result.Warnings.Add($"Rule {i + 1} has no conditions - will always match at priority {rule.Priority}");
            }

            if (rule.Priority < 0)
            {
                result.Warnings.Add($"Rule {i + 1} has negative priority ({rule.Priority}). Consider using non-negative values.");
            }
        }

        // Check for rules with same priority and overlapping conditions
        var priorityGroups = config.Rules.GroupBy(r => r.Priority);
        foreach (var group in priorityGroups.Where(g => g.Count() > 1))
        {
            result.Warnings.Add($"Multiple rules ({group.Count()}) have priority {group.Key}. Order among them may be unpredictable.");
        }
    }

    private void ValidateAgainstSchema(PricingRulesConfig config, string parameterSchema, ValidationResult result)
    {
        Dictionary<string, ParameterDefinition>? schema;
        try
        {
            schema = JsonSerializer.Deserialize<Dictionary<string, ParameterDefinition>>(parameterSchema, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse parameter schema for validation");
            result.Warnings.Add("Could not parse parameter schema for validation");
            return;
        }

        if (schema == null || schema.Count == 0)
        {
            return;
        }

        foreach (var rule in config.Rules)
        {
            foreach (var condition in rule.Conditions)
            {
                // Check if condition key exists in schema
                if (!schema.TryGetValue(condition.Key, out var paramDef))
                {
                    result.Warnings.Add($"Condition '{condition.Key}' not found in model parameters schema. Available: {string.Join(", ", schema.Keys)}");
                    continue;
                }

                // Validate value against parameter definition
                ValidateConditionValue(condition.Key, condition.Value, paramDef, result);
            }
        }
    }

    private void ValidateConditionValue(string key, object value, ParameterDefinition paramDef, ValidationResult result)
    {
        var valueStr = ExtractStringValue(value);

        switch (paramDef.Type?.ToLowerInvariant())
        {
            case "enum":
                if (paramDef.Options != null && paramDef.Options.Count > 0)
                {
                    if (!paramDef.Options.Contains(valueStr, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Errors.Add(new ValidationError
                        {
                            Field = key,
                            Message = $"Value '{valueStr}' not in allowed options: {string.Join(", ", paramDef.Options)}"
                        });
                    }
                }
                break;

            case "boolean":
                if (!bool.TryParse(valueStr, out _) &&
                    !valueStr.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                    !valueStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Field = key,
                        Message = $"Value '{valueStr}' is not a valid boolean"
                    });
                }
                break;

            case "integer":
                if (!long.TryParse(valueStr, out var intValue))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Field = key,
                        Message = $"Value '{valueStr}' is not a valid integer"
                    });
                }
                else
                {
                    ValidateNumericRange(key, intValue, paramDef, result);
                }
                break;

            case "number":
                if (!decimal.TryParse(valueStr, out var numValue))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Field = key,
                        Message = $"Value '{valueStr}' is not a valid number"
                    });
                }
                else
                {
                    ValidateNumericRange(key, numValue, paramDef, result);
                }
                break;
        }
    }

    private void ValidateNumericRange(string key, decimal value, ParameterDefinition paramDef, ValidationResult result)
    {
        if (paramDef.Min.HasValue && value < paramDef.Min.Value)
        {
            result.Errors.Add(new ValidationError
            {
                Field = key,
                Message = $"Value {value} is below minimum {paramDef.Min.Value}"
            });
        }

        if (paramDef.Max.HasValue && value > paramDef.Max.Value)
        {
            result.Errors.Add(new ValidationError
            {
                Field = key,
                Message = $"Value {value} exceeds maximum {paramDef.Max.Value}"
            });
        }
    }

    private string ExtractStringValue(object? value)
    {
        if (value == null)
            return string.Empty;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                JsonValueKind.Number => jsonElement.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => jsonElement.ToString()
            };
        }

        return value.ToString() ?? string.Empty;
    }

    private void CheckDuplicateRules(PricingRulesConfig config, ValidationResult result)
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < config.Rules.Count; i++)
        {
            var rule = config.Rules[i];
            var conditionsKey = SerializeConditions(rule.Conditions);

            if (!seen.Add(conditionsKey))
            {
                result.Warnings.Add($"Rule {i + 1} has identical conditions to another rule");
            }
        }
    }

    private string SerializeConditions(Dictionary<string, object> conditions)
    {
        var sorted = conditions.OrderBy(c => c.Key).Select(c => $"{c.Key}={ExtractStringValue(c.Value)}");
        return string.Join("|", sorted);
    }

    private void ValidateConstraints(PricingRulesConfig config, ValidationResult result)
    {
        var c = config.Constraints;
        if (c == null)
            return;

        if (c.MinDuration.HasValue && c.MaxDuration.HasValue && c.MinDuration > c.MaxDuration)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "constraints.minDuration",
                Message = "Minimum duration cannot exceed maximum duration"
            });
        }

        if (c.MinSteps.HasValue && c.MaxSteps.HasValue && c.MinSteps > c.MaxSteps)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "constraints.minSteps",
                Message = "Minimum steps cannot exceed maximum steps"
            });
        }

        if (c.MinDuration.HasValue && c.MinDuration < 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "constraints.minDuration",
                Message = "Minimum duration cannot be negative"
            });
        }

        if (c.MinSteps.HasValue && c.MinSteps < 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "constraints.minSteps",
                Message = "Minimum steps cannot be negative"
            });
        }
    }

    private void CheckPotentialIssues(PricingRulesConfig config, ValidationResult result)
    {
        // Check for very high rates that might be errors
        foreach (var rule in config.Rules.Where(r => r.Rate > 10))
        {
            result.Warnings.Add($"Rule '{rule.Description ?? "unnamed"}' has rate ${rule.Rate} which seems high. Please verify.");
        }

        if (config.DefaultRate > 10)
        {
            result.Warnings.Add($"Default rate ${config.DefaultRate} seems high. Please verify.");
        }

        // Check for zero rates
        foreach (var rule in config.Rules.Where(r => r.Rate == 0))
        {
            result.Warnings.Add($"Rule '{rule.Description ?? "unnamed"}' has zero rate. This will result in free usage.");
        }

        if (config.DefaultRate == 0 && config.Rules.All(r => r.Rate == 0))
        {
            result.Warnings.Add("All rates are zero. This configuration will result in free usage.");
        }
    }
}
