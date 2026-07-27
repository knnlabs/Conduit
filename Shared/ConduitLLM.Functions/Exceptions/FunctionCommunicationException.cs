using System.Net;

namespace ConduitLLM.Functions.Exceptions;

/// <summary>Represents an HTTP failure returned by a function provider.</summary>
public sealed class FunctionCommunicationException : Exception
{
    public FunctionCommunicationException(
        string providerName,
        string message,
        HttpStatusCode statusCode,
        string? responseBody = null)
        : base(message)
    {
        ProviderName = providerName;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string ProviderName { get; }
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }
}
