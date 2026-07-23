using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Models;

/// <summary>
/// Stable failure codes returned by virtual-key validation.
/// </summary>
public static class VirtualKeyValidationFailureCodes
{
    public const string MissingKey = "missing_key";
    public const string KeyNotFound = "invalid_key";
    public const string KeyDisabled = "key_disabled";
    public const string KeyExpired = "key_expired";
    public const string ModelNotAllowed = "model_not_allowed";
    public const string InsufficientBalance = "insufficient_balance";
    public const string ValidationError = "validation_error";
}

/// <summary>
/// Describes the complete result of validating a virtual key without mutating HTTP state.
/// </summary>
public sealed record VirtualKeyValidationOutcome
{
    public bool IsValid { get; init; }
    public string? FailureCode { get; init; }
    public int HttpStatusCode { get; init; }
    public string? Reason { get; init; }
    public VirtualKey? Key { get; init; }

    public static VirtualKeyValidationOutcome Success(VirtualKey key) => new()
    {
        IsValid = true,
        HttpStatusCode = 200,
        Key = key
    };

    public static VirtualKeyValidationOutcome Failure(
        string failureCode,
        int httpStatusCode,
        string reason,
        VirtualKey? key = null) => new()
    {
        IsValid = false,
        FailureCode = failureCode,
        HttpStatusCode = httpStatusCode,
        Reason = reason,
        Key = key
    };
}
