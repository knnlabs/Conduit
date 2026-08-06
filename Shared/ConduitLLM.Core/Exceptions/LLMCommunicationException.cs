using System.Net;

namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Represents errors that occur during communication with an LLM provider's API.
/// </summary>
public class LLMCommunicationException : ConduitException
{
    /// <summary>
    /// The HTTP status code received from the provider, if available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// The response body received from the provider, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// The provider that produced this error, when known. Settable because provider
    /// clients throw without this context; <c>ContextAwareLLMClient</c> stamps it on
    /// the way out. Customer-facing only in Internal mode.
    /// </summary>
    public string? ProviderName { get; set; }

    public LLMCommunicationException() { }
    public LLMCommunicationException(string message) : base(message) { }
    public LLMCommunicationException(string message, Exception? innerException = null) : base(message, innerException ?? new Exception(message)) { }
    public LLMCommunicationException(string message, HttpStatusCode? statusCode, string? responseBody, Exception? innerException = null)
        : base(message, innerException ?? new Exception(message))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Walks the exception chain and returns the most informative
    /// <see cref="LLMCommunicationException"/>: the outermost one that carries a
    /// <see cref="StatusCode"/>, falling back to the outermost one without.
    /// Provider clients routinely wrap a status-bearing instance inside a status-less
    /// one, so callers that stop at the first match misclassify the error.
    /// </summary>
    public static LLMCommunicationException? FindWithStatus(Exception exception)
    {
        LLMCommunicationException? withoutStatus = null;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is LLMCommunicationException communicationException)
            {
                if (communicationException.StatusCode.HasValue)
                {
                    return communicationException;
                }

                withoutStatus ??= communicationException;
            }
        }

        return withoutStatus;
    }
}
