using System;
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

    public LLMCommunicationException() { }
    public LLMCommunicationException(string message) : base(message) { }
    public LLMCommunicationException(string message, Exception? innerException = null) : base(message, innerException ?? new Exception(message)) { }
    public LLMCommunicationException(string message, HttpStatusCode? statusCode, string? responseBody, Exception? innerException = null)
        : base(message, innerException ?? new Exception(message))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
