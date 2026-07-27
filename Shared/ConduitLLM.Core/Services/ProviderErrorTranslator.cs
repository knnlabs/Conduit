using System.Net;

using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Translates provider errors for customer consumption per CONDUIT_CUSTOMER_MODE.
/// External mode returns classified generic messages keyed by <see cref="ProviderErrorType"/>;
/// Internal mode returns the provider name, upstream status, and redacted raw message.
/// HTTP status mapping is unchanged by mode — only message content and detail differ.
/// </summary>
public sealed class ProviderErrorTranslator : IProviderErrorTranslator
{
    // Raw provider bodies can be arbitrarily large (e.g. HTML error pages); cap what
    // reaches customer-facing detail. Full bodies remain available in logs.
    private const int MaxRawMessageLength = 2000;

    private readonly CustomerErrorOptions _options;

    public ProviderErrorTranslator(CustomerErrorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public CustomerErrorMode Mode => _options.Mode;

    public CustomerFacingProviderError Translate(
        HttpStatusCode? upstreamStatus, string? rawMessage, string? providerName)
    {
        var errorType = ProviderErrorClassifier.Classify(upstreamStatus, rawMessage);
        return Build(errorType, upstreamStatus, rawMessage, providerName);
    }

    public CustomerFacingProviderError Translate(Exception exception, string? providerNameFallback = null)
    {
        var commEx = LLMCommunicationException.FindWithStatus(exception);
        if (commEx is not null)
        {
            return Translate(
                commEx.StatusCode,
                commEx.ResponseBody ?? commEx.Message,
                commEx.ProviderName ?? providerNameFallback);
        }

        var errorType = ProviderErrorClassifier.ClassifyException(exception);
        return Build(errorType, upstreamStatus: null, exception.Message, providerNameFallback);
    }

    public ExceptionToResponseMapper.ExceptionMappingResult MapProviderError(
        LLMCommunicationException exception)
    {
        var translated = Translate(exception);
        return ExceptionToResponseMapper.MapProviderCommunicationStatus(exception.StatusCode, translated.Message)
            with
        { ProviderDetail = translated.Detail };
    }

    private CustomerFacingProviderError Build(
        ProviderErrorType errorType, HttpStatusCode? upstreamStatus, string? rawMessage, string? providerName)
    {
        if (_options.Mode == CustomerErrorMode.External)
        {
            return new CustomerFacingProviderError(ExternalMessageFor(errorType), errorType, Detail: null);
        }

        var redactedRaw = RedactAndTruncate(rawMessage);
        var detail = new ProviderErrorDetail(
            Provider: providerName,
            ErrorType: ProviderErrorCodes.For(errorType),
            UpstreamStatus: upstreamStatus.HasValue ? (int)upstreamStatus.Value : null,
            RawMessage: redactedRaw);

        return new CustomerFacingProviderError(
            BuildInternalMessage(providerName, upstreamStatus, redactedRaw), errorType, detail);
    }

    private static string ExternalMessageFor(ProviderErrorType errorType) => errorType switch
    {
        ProviderErrorType.RateLimitExceeded =>
            "The model provider rate-limited this request. Please retry later.",
        ProviderErrorType.ServiceUnavailable =>
            "The model provider is temporarily unavailable. Please retry later.",
        ProviderErrorType.Timeout or ProviderErrorType.NetworkError =>
            "The request to the model provider timed out. Please retry.",
        ProviderErrorType.InvalidApiKey or ProviderErrorType.InsufficientBalance or ProviderErrorType.AccessForbidden =>
            "The model provider rejected the request due to an upstream configuration issue.",
        ProviderErrorType.ModelNotFound =>
            "The requested model was not accepted by the model provider.",
        _ =>
            "An upstream provider error occurred. Please retry later."
    };

    private static string BuildInternalMessage(
        string? providerName, HttpStatusCode? upstreamStatus, string? redactedRaw)
    {
        var subject = string.IsNullOrWhiteSpace(providerName) ? "The model provider" : providerName;
        var verb = upstreamStatus.HasValue
            ? $"returned HTTP {(int)upstreamStatus.Value} {upstreamStatus.Value}"
            : "request failed";

        return string.IsNullOrWhiteSpace(redactedRaw)
            ? $"{subject} {verb}."
            : $"{subject} {verb}: {redactedRaw}";
    }

    private static string? RedactAndTruncate(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return null;
        }

        var redacted = SensitiveDataRedactor.Redact(rawMessage);
        return redacted.Length <= MaxRawMessageLength
            ? redacted
            : redacted[..MaxRawMessageLength] + "…";
    }

}
