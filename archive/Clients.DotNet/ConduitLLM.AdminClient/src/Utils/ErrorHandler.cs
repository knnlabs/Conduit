using System.Net;
using System.Text.Json;
using ConduitLLM.AdminClient.Exceptions;

namespace ConduitLLM.AdminClient.Utils;

/// <summary>
/// Utility class for handling HTTP errors and converting them to appropriate exceptions.
/// </summary>
public static class ErrorHandler
{
    /// <summary>
    /// Handles HTTP response errors and throws appropriate exceptions.
    /// </summary>
    /// <param name="response">The HTTP response message.</param>
    /// <param name="endpoint">The API endpoint that was called.</param>
    /// <param name="method">The HTTP method used.</param>
    /// <exception cref="ConduitAdminException">Thrown when an API error occurs.</exception>
    public static async Task HandleErrorResponseAsync(
        HttpResponseMessage response,
        string? endpoint = null,
        string? method = null)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync();
        var statusCode = (int)response.StatusCode;
        
        // Try to parse error details from response
        object? errorDetails = null;
        string errorMessage = $"HTTP {statusCode}";
        
        try
        {
            if (!string.IsNullOrEmpty(content))
            {
                var jsonDoc = JsonDocument.Parse(content);
                errorDetails = JsonSerializer.Deserialize<object>(content);
                
                // Try to extract error message from common response formats
                if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    errorMessage = errorElement.GetString() ?? errorMessage;
                }
                else if (jsonDoc.RootElement.TryGetProperty("message", out var messageElement))
                {
                    errorMessage = messageElement.GetString() ?? errorMessage;
                }
                else if (jsonDoc.RootElement.TryGetProperty("detail", out var detailElement))
                {
                    errorMessage = detailElement.GetString() ?? errorMessage;
                }
            }
        }
        catch (JsonException)
        {
            // If we can't parse JSON, use the raw content as error message
            errorMessage = !string.IsNullOrEmpty(content) ? content : errorMessage;
        }

        // Add endpoint information to error message
        var endpointInfo = !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(method) 
            ? $" ({method.ToUpper()} {endpoint})" 
            : "";
        var enhancedMessage = $"{errorMessage}{endpointInfo}";

        // Throw specific exception based on status code
        throw response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new ValidationException(enhancedMessage, errorDetails, endpoint, method),
            HttpStatusCode.Unauthorized => new AuthenticationException(enhancedMessage, errorDetails, endpoint, method),
            HttpStatusCode.Forbidden => new AuthorizationException(enhancedMessage, errorDetails, endpoint, method),
            HttpStatusCode.NotFound => new NotFoundException(enhancedMessage, errorDetails, endpoint, method),
            HttpStatusCode.Conflict => new ConflictException(enhancedMessage, errorDetails, endpoint, method),
            HttpStatusCode.TooManyRequests => CreateRateLimitException(response, enhancedMessage, errorDetails, endpoint, method),
            HttpStatusCode.InternalServerError or 
            HttpStatusCode.BadGateway or 
            HttpStatusCode.ServiceUnavailable or 
            HttpStatusCode.GatewayTimeout => new ServerException(enhancedMessage, errorDetails, endpoint, method),
            _ => new ConduitAdminException(enhancedMessage, statusCode, errorDetails, endpoint, method)
        };
    }

    /// <summary>
    /// Handles general exceptions and converts them to appropriate Conduit exceptions.
    /// </summary>
    /// <param name="exception">The original exception.</param>
    /// <param name="endpoint">The API endpoint that was called.</param>
    /// <param name="method">The HTTP method used.</param>
    /// <exception cref="ConduitAdminException">Thrown when an error occurs.</exception>
    public static void HandleException(Exception exception, string? endpoint = null, string? method = null)
    {
        // Don't wrap if it's already a Conduit exception
        if (exception is ConduitAdminException)
            throw exception;

        var endpointInfo = !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(method) 
            ? $" ({method.ToUpper()} {endpoint})" 
            : "";

        switch (exception)
        {
            case HttpRequestException httpEx:
                throw new NetworkException($"Network error: {httpEx.Message}{endpointInfo}", null, httpEx);
                
            case TaskCanceledException taskEx when taskEx.InnerException is TimeoutException:
                throw new TimeoutException($"Request timeout{endpointInfo}", null, taskEx);
                
            case TaskCanceledException taskEx:
                throw new TimeoutException($"Request was cancelled{endpointInfo}", null, taskEx);
                
            case JsonException jsonEx:
                throw new ConduitAdminException($"JSON parsing error: {jsonEx.Message}{endpointInfo}", null, 
                    new { originalError = jsonEx.Message }, endpoint, method, jsonEx);
                
            default:
                throw new ConduitAdminException($"Unexpected error: {exception.Message}{endpointInfo}", null, 
                    new { originalError = exception.Message }, endpoint, method, exception);
        }
    }

    private static RateLimitException CreateRateLimitException(
        HttpResponseMessage response,
        string message,
        object? details,
        string? endpoint,
        string? method)
    {
        int? retryAfter = null;
        
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            retryAfter = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
        }
        else if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
        {
            var retryAfterValue = retryAfterValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(retryAfterValue) && int.TryParse(retryAfterValue, out var seconds))
            {
                retryAfter = seconds;
            }
        }

        return new RateLimitException(message, retryAfter, details, endpoint, method);
    }
}