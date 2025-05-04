using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Exceptions;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Provides centralized exception handling and standardized error processing for the application.
    /// This utility helps reduce duplication of error handling logic throughout the codebase.
    /// </summary>
    public static class ExceptionHandler
    {
        /// <summary>
        /// Executes a function with standardized exception handling, logging, and error translation.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="logger">The logger to use for error logging.</param>
        /// <param name="errorMessage">The base error message to use in thrown exceptions.</param>
        /// <param name="exceptionTransformer">Optional function to transform caught exceptions into specific types.</param>
        /// <returns>The result of the operation if successful.</returns>
        /// <exception cref="LLMCommunicationException">Thrown for general communication errors if no transformer is provided.</exception>
        public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
            Func<Task<T>> operation,
            ILogger logger,
            string errorMessage,
            Func<Exception, Exception>? exceptionTransformer = null)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (
                ex is not LLMCommunicationException &&
                ex is not ConfigurationException &&
                ex is not ValidationException &&
                ex is not OperationCanceledException)
            {
                logger.LogError(ex, errorMessage);
                
                if (exceptionTransformer != null)
                {
                    throw exceptionTransformer(ex);
                }
                
                throw new LLMCommunicationException(errorMessage, ex);
            }
        }
        
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
            catch (Exception ex) when (ex is not LLMCommunicationException)
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
            
            if (ex is ConfigurationException)
            {
                // Pass through configuration exceptions
                return ex;
            }
            
            // General error handling
            logger.LogError(ex, "Error processing request to {Provider} for model {Model}", providerName, modelName);
            return new LLMCommunicationException($"Error processing request to {providerName}: {ex.Message}", ex);
        }
        
        /// <summary>
        /// Handles configuration validation exceptions in a standardized way.
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="logger">The logger to use for error logging.</param>
        /// <param name="contextName">The context name for error messages.</param>
        /// <returns>The result of the operation if successful.</returns>
        /// <exception cref="ConfigurationException">Thrown with standardized context for configuration issues.</exception>
        public static async Task<T> HandleConfigValidationAsync<T>(
            Func<Task<T>> operation,
            ILogger logger,
            string contextName)
        {
            try
            {
                return await operation();
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Configuration validation error for {Context}: {Message}", contextName, ex.Message);
                throw new ConfigurationException($"Invalid configuration for {contextName}: {ex.Message}", ex);
            }
            catch (ConfigurationException)
            {
                // Pass through existing ConfigurationExceptions
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error validating configuration for {Context}", contextName);
                throw new ConfigurationException($"Configuration error for {contextName}: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Logs an exception appropriately based on its type and severity.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="context">Additional context information to include in the log.</param>
        public static void LogException(Exception exception, ILogger logger, string context)
        {
            if (exception is OperationCanceledException)
            {
                logger.LogInformation(exception, "Operation canceled in {Context}", context);
                return;
            }
            
            if (exception is ValidationException)
            {
                logger.LogWarning(exception, "Validation error in {Context}: {Message}", context, exception.Message);
                return;
            }
            
            if (exception is ConfigurationException)
            {
                logger.LogError(exception, "Configuration error in {Context}: {Message}", context, exception.Message);
                return;
            }
            
            if (exception is ModelUnavailableException)
            {
                logger.LogError(exception, "Model unavailable in {Context}: {Message}", context, exception.Message);
                return;
            }
            
            if (exception is LLMCommunicationException)
            {
                logger.LogError(exception, "Communication error in {Context}: {Message}", context, exception.Message);
                return;
            }
            
            // Default case for unexpected exceptions
            logger.LogError(exception, "Unexpected error in {Context}: {Message}", context, exception.Message);
        }
    }
}