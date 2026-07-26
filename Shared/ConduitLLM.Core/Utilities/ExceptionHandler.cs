using System.Net;

using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Provides centralized exception handling and standardized error processing for the application.
    /// This utility helps reduce duplication of error handling logic throughout the codebase.
    /// </summary>
    public static class ExceptionHandler
    {
        /// <summary>
        /// Executes an operation with specific handling for HTTP-related exceptions.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="logger">The logger to use for error logging.</param>
        /// <param name="serviceName">The name of the service being called, for error messages.</param>
        /// <returns>The result of the operation if successful.</returns>
        /// <exception cref="LLMCommunicationException">Thrown for HTTP communication errors with appropriate context.</exception>
        public static async Task<T> HandleHttpRequestAsync<T>(
            Func<Task<T>> operation,
            ILogger logger,
            string serviceName)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "HTTP request to {ServiceName} failed: {Message}", serviceName, ex.Message);

                var statusCode = ex.StatusCode ?? HttpStatusCode.ServiceUnavailable;
                var statusMessage = statusCode != HttpStatusCode.ServiceUnavailable
                    ? $"HTTP {(int)statusCode} {statusCode}"
                    : "Service Unavailable";

                throw new LLMCommunicationException(
                    $"Failed to communicate with {serviceName}: {statusMessage} - {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested == false)
            {
                logger.LogError(ex, "Request to {ServiceName} timed out", serviceName);
                throw new LLMCommunicationException($"Request to {serviceName} timed out", ex);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Request to {ServiceName} was canceled by user", serviceName);
                throw; // Pass cancellation exceptions through unchanged
            }
            catch (Exception ex) when (
                ex is not LLMCommunicationException &&
                ex is not ConfigurationException &&
                ex is not ModelUnavailableException &&
                ex is not ValidationException)
            {
                logger.LogError(ex, "Unexpected error during {ServiceName} communication", serviceName);
                throw new LLMCommunicationException($"Unexpected error during {serviceName} communication: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Handles exceptions that occur during LLM model communication.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        /// <param name="logger">The logger to use for error logging.</param>
        /// <param name="providerName">The LLM provider name.</param>
        /// <param name="modelName">The model being used.</param>
        /// <returns>An appropriate exception with context about the LLM operation.</returns>
        public static Exception HandleLlmException(
            Exception ex,
            ILogger logger,
            string providerName,
            string modelName)
        {
            if (ex is LLMCommunicationException or
                ConfigurationException or
                ModelUnavailableException or
                ValidationException)
            {
                // Provider-specific translations are already safe for the caller.
                return ex;
            }

            if (ex is HttpRequestException httpEx)
            {
                var statusCode = httpEx.StatusCode ?? HttpStatusCode.ServiceUnavailable;

                if (statusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.LogWarning(httpEx, "Rate limit exceeded for {Provider} model {Model}", providerName, modelName);
                    return new LLMCommunicationException($"Rate limit exceeded for {providerName}", httpEx);
                }

                if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
                {
                    logger.LogError(httpEx, "Authentication failed for {Provider}", providerName);
                    return new ConfigurationException($"Authentication failed for {providerName}. Please check your API key.", httpEx);
                }

                if (statusCode == HttpStatusCode.NotFound)
                {
                    logger.LogError(httpEx, "Model {Model} not found for {Provider}", modelName, providerName);
                    return new ModelUnavailableException($"Model '{modelName}' not found for provider {providerName}", httpEx);
                }

                logger.LogError(httpEx, "HTTP error from {Provider} using model {Model}: {StatusCode}",
                    providerName, modelName, statusCode);
                return new LLMCommunicationException($"Error communicating with {providerName}: HTTP {(int)statusCode}", httpEx);
            }

            if (ex is TaskCanceledException or TimeoutException)
            {
                logger.LogWarning(ex, "Request to {Provider} timed out for model {Model}", providerName, modelName);
                return new LLMCommunicationException($"Request to {providerName} timed out", ex);
            }

            // General error handling
            logger.LogError(ex, "Error processing request to {Provider} for model {Model}", providerName, modelName);
            return new LLMCommunicationException($"Error processing request to {providerName}: {ex.Message}", ex);
        }

    }
}
