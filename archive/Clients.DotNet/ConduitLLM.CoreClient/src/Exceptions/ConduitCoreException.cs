namespace ConduitLLM.CoreClient.Exceptions;

/// <summary>
/// Base exception class for Conduit Core API errors.
/// </summary>
public class ConduitCoreException : Exception
{
    /// <summary>
    /// Gets the HTTP status code associated with the error.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets the error code from the API response.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Gets the error type from the API response.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the parameter that caused the error.
    /// </summary>
    public string? Param { get; }

    /// <summary>
    /// Initializes a new instance of the ConduitCoreException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The error code.</param>
    /// <param name="type">The error type.</param>
    /// <param name="param">The parameter that caused the error.</param>
    /// <param name="innerException">The inner exception.</param>
    public ConduitCoreException(
        string message,
        int? statusCode = null,
        string? code = null,
        string? type = null,
        string? param = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Type = type;
        Param = param;
    }

    /// <summary>
    /// Creates a ConduitCoreException from an error response.
    /// </summary>
    /// <param name="errorResponse">The error response from the API.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>A new ConduitCoreException instance.</returns>
    public static ConduitCoreException FromErrorResponse(ErrorResponse errorResponse, int? statusCode = null)
    {
        return new ConduitCoreException(
            errorResponse.Error.Message,
            statusCode,
            errorResponse.Error.Code,
            errorResponse.Error.Type,
            errorResponse.Error.Param);
    }

    /// <summary>
    /// Returns a string representation that includes error details.
    /// </summary>
    /// <returns>A formatted string representation of the exception.</returns>
    public override string ToString()
    {
        var baseString = base.ToString();
        
        var details = new List<string>();
        
        if (StatusCode.HasValue)
            details.Add($"Status: {StatusCode}");
            
        if (!string.IsNullOrEmpty(Code))
            details.Add($"Code: {Code}");
            
        if (!string.IsNullOrEmpty(Type))
            details.Add($"Type: {Type}");
            
        if (!string.IsNullOrEmpty(Param))
            details.Add($"Param: {Param}");
            
        if (details.Count > 0)
            baseString += $" [{string.Join(", ", details)}]";
        
        return baseString;
    }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationException : ConduitCoreException
{
    /// <summary>
    /// Initializes a new instance of the AuthenticationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AuthenticationException(
        string message = "Authentication failed",
        Exception? innerException = null)
        : base(message, 401, "authentication_error", "invalid_request_error", null, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// </summary>
public class RateLimitException : ConduitCoreException
{
    /// <summary>
    /// Gets the number of seconds to wait before retrying.
    /// </summary>
    public int? RetryAfter { get; }

    /// <summary>
    /// Initializes a new instance of the RateLimitException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="retryAfter">Number of seconds to wait before retrying.</param>
    /// <param name="innerException">The inner exception.</param>
    public RateLimitException(
        string message = "Rate limit exceeded",
        int? retryAfter = null,
        Exception? innerException = null)
        : base(message, 429, "rate_limit_error", "rate_limit_error", null, innerException)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : ConduitCoreException
{
    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="param">The parameter that failed validation.</param>
    /// <param name="innerException">The inner exception.</param>
    public ValidationException(
        string message,
        string? param = null,
        Exception? innerException = null)
        : base(message, 400, "validation_error", "invalid_request_error", param, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when there is a network error.
/// </summary>
public class NetworkException : ConduitCoreException
{
    /// <summary>
    /// Initializes a new instance of the NetworkException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NetworkException(
        string message = "Network request failed",
        Exception? innerException = null)
        : base(message, null, null, null, null, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when stream processing fails.
/// </summary>
public class StreamException : ConduitCoreException
{
    /// <summary>
    /// Initializes a new instance of the StreamException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StreamException(
        string message = "Stream processing failed",
        Exception? innerException = null)
        : base(message, null, null, null, null, innerException)
    {
    }
}

/// <summary>
/// Represents an error response from the Conduit Core API.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Gets or sets the error details.
    /// </summary>
    public ErrorDetails Error { get; set; } = new();
}

/// <summary>
/// Represents the error details from an API response.
/// </summary>
public class ErrorDetails
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the error type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the parameter that caused the error.
    /// </summary>
    public string? Param { get; set; }
}