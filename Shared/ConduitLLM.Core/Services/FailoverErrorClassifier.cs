using System.Net;

using ConduitLLM.Core.Exceptions;

namespace ConduitLLM.Core.Services;

/// <summary>What the failover loop should do after a failed attempt.</summary>
public enum FailoverAction
{
    /// <summary>Rethrow — retrying the identical request cannot succeed (user error,
    /// cancellation, or an unrecognizable failure).</summary>
    Abort,

    /// <summary>Key-scoped problem (bad key, exhausted balance/quota) — try the next enabled
    /// key of the same provider.</summary>
    NextKey,

    /// <summary>Provider-scoped problem (model missing, infrastructure down) — every key of
    /// this provider will fail the same way; move to the next provider (or, key-level-only
    /// mode: a sibling key with a different endpoint).</summary>
    NextProvider,
}

/// <summary>
/// Maps a failed attempt's exception to a <see cref="FailoverAction"/>. Classification mirrors
/// <c>ContextAwareLLMClient</c>'s error tracking so failover and key auto-disable always agree
/// about what an error means.
/// </summary>
public static class FailoverErrorClassifier
{
    public static FailoverAction Classify(Exception ex)
    {
        // Caller gone or budget expired — never mask a cancellation with another attempt.
        if (ex is OperationCanceledException)
        {
            return FailoverAction.Abort;
        }

        var llmEx = ExtractLLMCommunicationException(ex);
        if (llmEx == null)
        {
            // Validation errors, configuration errors, unknown exceptions: conservative abort.
            return FailoverAction.Abort;
        }

        return llmEx.StatusCode switch
        {
            HttpStatusCode.Unauthorized => FailoverAction.NextKey,        // 401 invalid key
            HttpStatusCode.PaymentRequired => FailoverAction.NextKey,     // 402 balance (per account)
            HttpStatusCode.Forbidden => FailoverAction.NextKey,           // 403 key/account scoped
            HttpStatusCode.TooManyRequests => FailoverAction.NextKey,     // 429 after the HTTP-layer
                                                                          // retry budget; ordering
                                                                          // prefers other account groups
            HttpStatusCode.NotFound => FailoverAction.NextProvider,       // 404 model — same for every key
            HttpStatusCode.BadRequest => FailoverAction.Abort,            // user error
            HttpStatusCode.RequestTimeout => FailoverAction.NextProvider,
            >= HttpStatusCode.InternalServerError => FailoverAction.NextProvider,
            // No status code: network failure, open circuit, or processing error — treat as
            // provider infrastructure.
            null => FailoverAction.NextProvider,
            _ => FailoverAction.Abort,
        };
    }

    /// <summary>
    /// True when an action is allowed for media generation under the restricted policy:
    /// only auth-class errors (401/402/403) may fail over — a timeout or 5xx may mean the
    /// provider accepted the generation job, and retrying would double-generate.
    /// </summary>
    public static bool IsAuthClassError(Exception ex)
    {
        var status = ExtractLLMCommunicationException(ex)?.StatusCode;
        return status is HttpStatusCode.Unauthorized
            or HttpStatusCode.PaymentRequired
            or HttpStatusCode.Forbidden;
    }

    /// <summary>
    /// Walks an exception (and its inner chain) for the most specific
    /// <see cref="LLMCommunicationException"/>: first one carrying a status code, else the
    /// first one found at all. Shared by failover classification and
    /// <c>ContextAwareLLMClient</c> error tracking.
    /// </summary>
    public static LLMCommunicationException? ExtractLLMCommunicationException(Exception ex)
    {
        // Check if it's already an LLMCommunicationException with a StatusCode
        if (ex is LLMCommunicationException llmEx && llmEx.StatusCode.HasValue)
            return llmEx;

        // If it's an LLMCommunicationException without StatusCode, check its inner exceptions
        if (ex is LLMCommunicationException outerLlmEx && outerLlmEx.InnerException != null)
        {
            var innerWithStatus = ExtractLLMCommunicationException(outerLlmEx.InnerException);
            if (innerWithStatus != null)
                return innerWithStatus;
        }

        // Check inner exceptions recursively for any LLMCommunicationException with StatusCode
        var current = ex.InnerException;
        while (current != null)
        {
            if (current is LLMCommunicationException innerLlmEx && innerLlmEx.StatusCode.HasValue)
                return innerLlmEx;
            current = current.InnerException;
        }

        // If we only found exceptions without StatusCode, return the first one we found
        if (ex is LLMCommunicationException firstLlmEx)
            return firstLlmEx;

        current = ex.InnerException;
        while (current != null)
        {
            if (current is LLMCommunicationException innerLlmEx)
                return innerLlmEx;
            current = current.InnerException;
        }

        return null;
    }
}
