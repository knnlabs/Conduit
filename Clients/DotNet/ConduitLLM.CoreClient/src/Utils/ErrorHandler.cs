using System.Net;
using System.Text.Json;
using ConduitLLM.CoreClient.Exceptions;

namespace ConduitLLM.CoreClient.Utils;

/// <summary>
/// Utility class for handling HTTP errors and converting them to appropriate exceptions.
/// </summary>
public static class ErrorHandler
{
    /// <summary>
    /// Handles HTTP response errors and throws appropriate exceptions.
    /// </summary>
    /// <param name="response">The HTTP response message.</param>
    /// <exception cref="ConduitCoreException">Thrown when an API error occurs.</exception>
    public static async Task HandleErrorResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync();
        var statusCode = (int)response.StatusCode;
        
        // Try to parse error response
        ErrorResponse? errorResponse = null;
        string errorMessage = $"HTTP {statusCode}";
        
        try
        {
            if (!string.IsNullOrEmpty(content))
            {
                errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                });
                
                if (errorResponse?.Error != null && !string.IsNullOrEmpty(errorResponse.Error.Message))
                {
                    errorMessage = errorResponse.Error.Message;
                }
            }
        }
        catch (JsonException)
        {
            // If we can't parse JSON, use the raw content as error message
            errorMessage = !string.IsNullOrEmpty(content) ? content : errorMessage;
        }

        // Throw specific exception based on status code or error response
        if (errorResponse?.Error != null)
        {
            throw ConduitCoreException.FromErrorResponse(errorResponse, statusCode);
        }

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new AuthenticationException(errorMessage),
            HttpStatusCode.TooManyRequests => CreateRateLimitException(response, errorMessage),
            HttpStatusCode.BadRequest => new ValidationException(errorMessage),
            _ => new ConduitCoreException(errorMessage, statusCode)
        };
    }

    /// <summary>
    /// Handles general exceptions and converts them to appropriate Conduit exceptions.
    /// </summary>
    /// <param name="exception">The original exception.</param>
    /// <exception cref="ConduitCoreException">Thrown when an error occurs.</exception>
    public static void HandleException(Exception exception)
    {
        // Don't wrap if it's already a Conduit exception
        if (exception is ConduitCoreException)
            throw exception;

        switch (exception)
        {
            case HttpRequestException httpEx:
                throw new NetworkException($"Network error: {httpEx.Message}", httpEx);
                
            case TaskCanceledException taskEx when taskEx.InnerException is TimeoutException:
                throw new NetworkException("Request timeout", taskEx);
                
            case TaskCanceledException taskEx:
                throw new NetworkException("Request was cancelled", taskEx);
                
            case JsonException jsonEx:
                throw new ConduitCoreException($"JSON parsing error: {jsonEx.Message}", null, null, null, null, jsonEx);
                
            default:
                throw new ConduitCoreException($"Unexpected error: {exception.Message}", null, null, null, null, exception);
        }
    }

    private static RateLimitException CreateRateLimitException(HttpResponseMessage response, string message)
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

        return new RateLimitException(message, retryAfter);
    }
}