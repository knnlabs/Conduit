using System.Net;
using System.Text.Json.Serialization;

using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Structured provider-error detail exposed to customers in Internal mode only.
/// Serialized under <c>error.metadata.provider_error</c> in the OpenAI envelope and
/// as <c>provider_error</c> in SSE error events.
/// </summary>
public sealed record ProviderErrorDetail(
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("error_type")] string ErrorType,
    [property: JsonPropertyName("upstream_status")] int? UpstreamStatus,
    [property: JsonPropertyName("raw_message")] string? RawMessage);

/// <summary>
/// A provider error translated for customer consumption per the deployment's
/// <see cref="CustomerErrorMode"/>.
/// </summary>
/// <param name="Message">The customer-visible message: a classified generic message in
/// External mode, or a detailed provider message in Internal mode.</param>
/// <param name="ErrorType">The classified error type (always populated; safe for
/// programmatic use on events and metrics in either mode).</param>
/// <param name="Detail">Structured detail; non-null only in Internal mode.</param>
/// <param name="ErrorCodeOverride">Overrides <see cref="ErrorCode"/> for failures that are not
/// provider errors (e.g. Conduit-side validation), where the classified type carries no signal.</param>
public sealed record CustomerFacingProviderError(
    string Message,
    ProviderErrorType ErrorType,
    ProviderErrorDetail? Detail,
    string? ErrorCodeOverride = null)
{
    /// <summary>Error code for events and payloads (e.g. "rate_limit_exceeded").</summary>
    public string ErrorCode => ErrorCodeOverride ?? ProviderErrorCodes.For(ErrorType);
}

/// <summary>Snake_case wire codes for <see cref="ProviderErrorType"/> values.</summary>
public static class ProviderErrorCodes
{
    public static string For(ProviderErrorType errorType)
    {
        var name = errorType.ToString();
        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                builder.Append(name[i]);
            }
        }

        return builder.ToString();
    }
}

/// <summary>
/// The single translation point between provider errors and customer-facing text.
/// Every surface that emits a provider failure to a customer (HTTP responses, SSE
/// streams, async task status, webhooks, SignalR events) must go through this service
/// so that CONDUIT_CUSTOMER_MODE is honored uniformly.
/// </summary>
public interface IProviderErrorTranslator
{
    CustomerErrorMode Mode { get; }

    /// <summary>
    /// Translates a provider error described by its upstream status and raw message.
    /// </summary>
    CustomerFacingProviderError Translate(HttpStatusCode? upstreamStatus, string? rawMessage, string? providerName);

    /// <summary>
    /// Translates an exception from a provider call, walking the inner chain for the
    /// most informative <see cref="LLMCommunicationException"/> when present.
    /// </summary>
    CustomerFacingProviderError Translate(Exception exception, string? providerNameFallback = null);

    /// <summary>
    /// Maps a provider communication exception to an HTTP response mapping whose status,
    /// error code, and OpenAI error type are identical to
    /// <see cref="ExceptionToResponseMapper.MapProviderCommunicationStatus"/>, with the
    /// message translated per mode and <see cref="ProviderErrorDetail"/> attached in
    /// Internal mode.
    /// </summary>
    ExceptionToResponseMapper.ExceptionMappingResult MapProviderError(LLMCommunicationException exception);
}
