namespace ConduitLLM.AdminClient.Exceptions;

/// <summary>
/// Base exception class for Conduit Admin API errors.
/// </summary>
public class ConduitAdminException : Exception
{
    /// <summary>
    /// Gets the HTTP status code associated with the error.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets additional error details.
    /// </summary>
    public object? Details { get; }

    /// <summary>
    /// Gets the API endpoint that caused the error.
    /// </summary>
    public string? Endpoint { get; }

    /// <summary>
    /// Gets the HTTP method used in the request.
    /// </summary>
    public string? Method { get; }

    /// <summary>
    /// Initializes a new instance of the ConduitAdminException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public ConduitAdminException(
        string message,
        int? statusCode = null,
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Details = details;
        Endpoint = endpoint;
        Method = method;
    }

    /// <summary>
    /// Returns a string representation that includes error details.
    /// </summary>
    /// <returns>A formatted string representation of the exception.</returns>
    public override string ToString()
    {
        var baseString = base.ToString();
        
        if (StatusCode.HasValue || !string.IsNullOrEmpty(Endpoint))
        {
            var details = new List<string>();
            
            if (StatusCode.HasValue)
                details.Add($"Status: {StatusCode}");
                
            if (!string.IsNullOrEmpty(Method) && !string.IsNullOrEmpty(Endpoint))
                details.Add($"Request: {Method} {Endpoint}");
            else if (!string.IsNullOrEmpty(Endpoint))
                details.Add($"Endpoint: {Endpoint}");
                
            baseString += $" [{string.Join(", ", details)}]";
        }
        
        return baseString;
    }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the AuthenticationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public AuthenticationException(
        string message = "Authentication failed",
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, 401, details, endpoint, method, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when access is forbidden.
/// </summary>
public class AuthorizationException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the AuthorizationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public AuthorizationException(
        string message = "Access forbidden",
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, 403, details, endpoint, method, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the ValidationException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public ValidationException(
        string message = "Validation failed",
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, 400, details, endpoint, method, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a resource is not found.
/// </summary>
public class NotFoundException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the NotFoundException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public NotFoundException(
        string message = "Resource not found",
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, 404, details, endpoint, method, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when there is a resource conflict.
/// </summary>
public class ConflictException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the ConflictException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public ConflictException(
        string message = "Resource conflict",
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, 409, details, endpoint, method, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// </summary>
public class RateLimitException : ConduitAdminException
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
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public RateLimitException(
        string message = "Rate limit exceeded",
        int? retryAfter = null,
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, 429, details, endpoint, method, innerException)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Exception thrown when there is a server error.
/// </summary>
public class ServerException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the ServerException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="innerException">The inner exception.</param>
    public ServerException(
        string message = "Internal server error",
        object? details = null,
        string? endpoint = null,
        string? method = null,
        Exception? innerException = null)
        : base(message, 500, details, endpoint, method, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when there is a network error.
/// </summary>
public class NetworkException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the NetworkException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="innerException">The inner exception.</param>
    public NetworkException(
        string message = "Network error",
        object? details = null,
        Exception? innerException = null)
        : base(message, null, details, null, null, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a request times out.
/// </summary>
public class TimeoutException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the TimeoutException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="innerException">The inner exception.</param>
    public TimeoutException(
        string message = "Request timeout",
        object? details = null,
        Exception? innerException = null)
        : base(message, 408, details, null, null, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a feature is not implemented.
/// </summary>
public class NotImplementedException : ConduitAdminException
{
    /// <summary>
    /// Initializes a new instance of the NotImplementedException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Additional error details.</param>
    /// <param name="innerException">The inner exception.</param>
    public NotImplementedException(
        string message,
        object? details = null,
        Exception? innerException = null)
        : base(message, 501, details, null, null, innerException)
    {
    }
}