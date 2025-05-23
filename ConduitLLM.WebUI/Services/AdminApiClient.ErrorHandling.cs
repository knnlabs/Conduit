using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Partial class containing enhanced error handling functionality for AdminApiClient.
    /// </summary>
    public partial class AdminApiClient
    {
        /// <summary>
        /// Executes an HTTP operation with comprehensive error handling.
        /// </summary>
        /// <typeparam name="T">The return type.</typeparam>
        /// <param name="operation">The operation name for logging.</param>
        /// <param name="httpCall">The HTTP call to execute.</param>
        /// <param name="defaultValue">Default value to return on failure.</param>
        /// <returns>The result or default value.</returns>
        protected async Task<T?> ExecuteWithErrorHandlingAsync<T>(
            string operation,
            Func<Task<T>> httpCall,
            T? defaultValue = default)
        {
            try
            {
                return await httpCall();
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, 
                    "Circuit breaker is open for {Operation}. Admin API is temporarily unavailable.",
                    operation);
                return defaultValue;
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogError(ex,
                    "Request timeout for {Operation}. The operation took too long to complete.",
                    operation);
                return defaultValue;
            }
            catch (HttpRequestException ex) when (ex.InnerException is TaskCanceledException)
            {
                _logger.LogError(ex,
                    "Request cancelled for {Operation}. This might indicate a timeout.",
                    operation);
                return defaultValue;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "HTTP error during {Operation}. Status: {StatusCode}, Message: {Message}",
                    operation, ex.StatusCode, ex.Message);
                return defaultValue;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex,
                    "Request cancelled for {Operation}. The operation was aborted.",
                    operation);
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error during {Operation}: {Message}",
                    operation, ex.Message);
                return defaultValue;
            }
        }

        /// <summary>
        /// Executes an HTTP operation that returns a boolean result.
        /// </summary>
        protected async Task<bool> ExecuteWithErrorHandlingAsync(
            string operation,
            Func<Task<bool>> httpCall)
        {
            return await ExecuteWithErrorHandlingAsync(operation, httpCall, false);
        }

        /// <summary>
        /// Handles specific HTTP status codes with appropriate actions.
        /// </summary>
        protected async Task<T?> HandleHttpResponseAsync<T>(
            HttpResponseMessage response,
            string operation) where T : class
        {
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync();

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    _logger.LogInformation(
                        "{Operation} returned NotFound (404). This might be expected.",
                        operation);
                    return null;

                case HttpStatusCode.Unauthorized:
                    _logger.LogError(
                        "{Operation} returned Unauthorized (401). Check master key configuration. Response: {Response}",
                        operation, content);
                    throw new UnauthorizedAccessException($"Authentication failed for {operation}");

                case HttpStatusCode.Forbidden:
                    _logger.LogError(
                        "{Operation} returned Forbidden (403). Insufficient permissions. Response: {Response}",
                        operation, content);
                    throw new UnauthorizedAccessException($"Access denied for {operation}");

                case HttpStatusCode.BadRequest:
                    _logger.LogError(
                        "{Operation} returned BadRequest (400). Invalid request. Response: {Response}",
                        operation, content);
                    return null;

                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.GatewayTimeout:
                    _logger.LogError(
                        "{Operation} returned {StatusCode}. Server error. Response: {Response}",
                        operation, response.StatusCode, content);
                    throw new HttpRequestException($"Server error during {operation}: {response.StatusCode}");

                default:
                    _logger.LogError(
                        "{Operation} returned unexpected status {StatusCode}. Response: {Response}",
                        operation, response.StatusCode, content);
                    return null;
            }
        }

        /// <summary>
        /// Creates a detailed error message for logging.
        /// </summary>
        protected string CreateDetailedErrorMessage(
            string operation,
            HttpResponseMessage? response = null,
            Exception? exception = null)
        {
            var details = $"Operation: {operation}";
            
            if (response != null)
            {
                details += $", Status: {response.StatusCode}";
                details += $", Reason: {response.ReasonPhrase}";
                
                if (response.RequestMessage != null)
                {
                    details += $", URL: {response.RequestMessage.RequestUri}";
                    details += $", Method: {response.RequestMessage.Method}";
                }
            }

            if (exception != null)
            {
                details += $", Exception: {exception.GetType().Name}";
                details += $", Message: {exception.Message}";
                
                if (exception.InnerException != null)
                {
                    details += $", Inner: {exception.InnerException.Message}";
                }
            }

            return details;
        }

        /// <summary>
        /// Determines if an exception is transient and should be retried.
        /// </summary>
        protected bool IsTransientException(Exception ex)
        {
            return ex switch
            {
                HttpRequestException httpEx => IsTransientHttpException(httpEx),
                TaskCanceledException => true,
                TimeoutRejectedException => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if an HTTP exception is transient.
        /// </summary>
        private bool IsTransientHttpException(HttpRequestException ex)
        {
            if (ex.StatusCode.HasValue)
            {
                var statusCode = (int)ex.StatusCode.Value;
                return statusCode >= 500 || statusCode == 408 || statusCode == 429;
            }

            // If no status code, check the message
            return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
        }
    }
}