using System.Text.Json.Serialization;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Represents the result of a Conduit validation operation without colliding with
/// <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/>.
/// </summary>
public class ConduitValidationResult
{
    /// <summary>
    /// Whether validation completed without errors.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Validation errors that must be resolved.
    /// </summary>
    [JsonInclude]
    public IReadOnlyList<ValidationError> Errors { get; private set; } = [];

    /// <summary>
    /// Non-blocking validation warnings.
    /// </summary>
    [JsonInclude]
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    public static ConduitValidationResult Success() => new();

    public static ConduitValidationResult Failure(
        string message,
        string field = "") =>
        new ConduitValidationResult().AddError(new ValidationError(field, message));

    public ConduitValidationResult AddError(ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Errors = [.. Errors, error];
        return this;
    }

    public ConduitValidationResult AddWarning(string warning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(warning);
        Warnings = [.. Warnings, warning];
        return this;
    }
}

/// <summary>
/// A structured validation failure.
/// </summary>
public sealed record ValidationError
{
    public string Field { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int? RuleIndex { get; init; }

    public ValidationError()
    {
    }

    public ValidationError(string field, string message, int? ruleIndex = null)
    {
        Field = field;
        Message = message;
        RuleIndex = ruleIndex;
    }
}
