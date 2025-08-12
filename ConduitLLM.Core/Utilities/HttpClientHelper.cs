using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Helper class for common HTTP client operations used across the application.
    /// Provides standardized methods for request/response handling and error processing.
    /// </summary>
    public static class HttpClientHelper
    {
        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Sends a request with JSON content and deserializes the response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request object to serialize.</typeparam>
        /// <typeparam name="TResponse">The type to deserialize the response into.</typeparam>
        /// <param name="client">The HttpClient to use for the request.</param>
        /// <param name="method">The HTTP method to use.</param>
        /// <param name="endpoint">The endpoint to send the request to.</param>
        /// <param name="requestData">The data to serialize and send.</param>
        /// <param name="headers">Optional additional headers to include with the request.</param>
        /// <param name="jsonOptions">Optional JSON serialization options.</param>
        /// <param name="logger">Optional logger for request/response logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The deserialized response object.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the API.</exception>
        public static async Task<TResponse> SendJsonRequestAsync<TRequest, TResponse>(
            HttpClient client,
            HttpMethod method,
            string endpoint,
            TRequest requestData,
            IDictionary<string, string>? headers = null,
            JsonSerializerOptions? jsonOptions = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var options = jsonOptions ?? DefaultJsonOptions;

            try
            {
                var request = CreateJsonRequest(method, endpoint, requestData, headers, options, logger);
                logger?.LogDebug("Sending {Method} request to {Endpoint}", method, endpoint);

                using var response = await client.SendAsync(request, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, options, logger, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger?.LogError(ex, "HTTP request error communicating with API at {Endpoint}", endpoint);
                throw new LLMCommunicationException($"HTTP request error: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning("Request to {Endpoint} was cancelled", endpoint);
                throw new LLMCommunicationException("Request was cancelled", ex);
            }
            catch (TaskCanceledException ex)
            {
                logger?.LogError(ex, "Request to {Endpoint} timed out", endpoint);
                throw new LLMCommunicationException("Request timed out", ex);
            }
            catch (JsonException ex)
            {
                logger?.LogError(ex, "JSON error processing response from {Endpoint}", endpoint);
                throw new LLMCommunicationException("Error processing response", ex);
            }
            catch (Exception ex) when (ex is not LLMCommunicationException)
            {
                logger?.LogError(ex, "Unexpected error during API communication with {Endpoint}", endpoint);
                throw new LLMCommunicationException($"Unexpected error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends a GET request and deserializes the JSON response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response object.</typeparam>
        /// <param name="client">The HTTP client to use for the request.</param>
        /// <param name="endpoint">The endpoint to send the request to.</param>
        /// <param name="headers">Optional headers to include in the request.</param>
        /// <param name="jsonOptions">Optional JSON serialization options.</param>
        /// <param name="logger">Optional logger for diagnostic information.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The deserialized response object.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the API.</exception>
        public static async Task<TResponse> GetJsonAsync<TResponse>(
            HttpClient client,
            string endpoint,
            IDictionary<string, string>? headers = null,
            JsonSerializerOptions? jsonOptions = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var options = jsonOptions ?? DefaultJsonOptions;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                
                // Add custom headers if provided
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                
                logger?.LogDebug("Sending GET request to {Endpoint}", endpoint);

                using var response = await client.SendAsync(request, cancellationToken);
                return await ProcessResponseAsync<TResponse>(response, options, logger, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger?.LogError(ex, "HTTP request error communicating with API at {Endpoint}", endpoint);
                throw new LLMCommunicationException($"HTTP request error: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning("Request to {Endpoint} was cancelled", endpoint);
                throw new LLMCommunicationException("Request was cancelled", ex);
            }
            catch (TaskCanceledException ex)
            {
                logger?.LogError(ex, "Request to {Endpoint} timed out", endpoint);
                throw new LLMCommunicationException("Request timed out", ex);
            }
            catch (JsonException ex)
            {
                logger?.LogError(ex, "Failed to deserialize JSON response from {Endpoint}", endpoint);
                throw new LLMCommunicationException($"Failed to deserialize response: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not LLMCommunicationException)
            {
                logger?.LogError(ex, "Unexpected error during API communication with {Endpoint}", endpoint);
                throw new LLMCommunicationException($"Unexpected error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates an HTTP request with JSON content and headers.
        /// </summary>
        private static HttpRequestMessage CreateJsonRequest<TRequest>(
            HttpMethod method,
            string endpoint,
            TRequest requestData,
            IDictionary<string, string>? headers,
            JsonSerializerOptions options,
            ILogger? logger = null)
        {
            var request = new HttpRequestMessage(method, endpoint);

            // Add content if data is provided
            if (requestData != null)
            {
                var requestJson = JsonSerializer.Serialize(requestData, options);
                logger?.LogInformation("Sending JSON request: {Json}", requestJson);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            }

            // Add headers
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            
            // Log all headers for debugging (especially for authentication issues)
            if (logger != null && logger.IsEnabled(LogLevel.Debug))
            {
                var allHeaders = new List<string>();
                foreach (var header in request.Headers)
                {
                    allHeaders.Add($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                if (request.Content?.Headers != null)
                {
                    foreach (var header in request.Content.Headers)
                    {
                        allHeaders.Add($"{header.Key}: {string.Join(", ", header.Value)}");
                    }
                }
                logger.LogDebug("Request headers: {Headers}", string.Join("; ", allHeaders));
            }

            return request;
        }

        /// <summary>
        /// Processes an HTTP response, handling errors and deserializing content.
        /// </summary>
        private static async Task<TResponse> ProcessResponseAsync<TResponse>(
            HttpResponseMessage response,
            JsonSerializerOptions options,
            ILogger? logger,
            CancellationToken cancellationToken)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await ReadErrorContentAsync(response, cancellationToken);
                logger?.LogError("API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                
                // Check for OpenAI quota error pattern (null message with image_generation_user_error)
                if (errorContent.Contains("\"message\": null") && errorContent.Contains("image_generation_user_error"))
                {
                    logger?.LogWarning("Detected possible OpenAI quota/billing issue - image generation errors with null messages often indicate insufficient quota");
                    throw new LLMCommunicationException(
                        $"API returned an error: {(int)response.StatusCode} {response.StatusCode} - Possible quota/billing issue. Please check your OpenAI account status. Raw error: {errorContent}");
                }
                
                // Check for Anthropic authentication errors
                if (errorContent.Contains("invalid bearer token", StringComparison.OrdinalIgnoreCase) ||
                    errorContent.Contains("bearer", StringComparison.OrdinalIgnoreCase) && errorContent.Contains("anthropic", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogWarning("Detected Anthropic authentication error - Bearer token used instead of x-api-key");
                    throw new LLMCommunicationException(
                        "Anthropic authentication error: Invalid API key or authentication method. Anthropic requires 'x-api-key' header, not Bearer tokens. Please verify your API key is valid and starts with 'sk-ant-'.");
                }
                
                throw new LLMCommunicationException(
                    $"API returned an error: {(int)response.StatusCode} {response.StatusCode} - {errorContent}");
            }

            logger?.LogDebug("Received successful response with status code {StatusCode}", response.StatusCode);

            // Read the response as string first for debugging
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Log the first 500 chars of the response for debugging
            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                var preview = responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent;
                logger.LogDebug("Response content preview: {Content}", preview);
            }
            
            // Deserialize from the string
            try
            {
                return JsonSerializer.Deserialize<TResponse>(responseContent, options)
                    ?? throw new LLMCommunicationException("Failed to deserialize response - result was null");
            }
            catch (JsonException ex)
            {
                // Log the full response on error for debugging
                logger?.LogError(ex, "Failed to deserialize response. Full content: {Content}", responseContent);
                throw;
            }
        }

        /// <summary>
        /// Reads error content from an HTTP response.
        /// </summary>
        public static async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                if (response.Content == null)
                {
                    return "No content";
                }

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception)
            {
                return "Could not read error content";
            }
        }

        /// <summary>
        /// Sends a streaming request and returns the response for processing.
        /// </summary>
        /// <param name="client">The HttpClient to use for the request.</param>
        /// <param name="method">The HTTP method to use.</param>
        /// <param name="endpoint">The endpoint to send the request to.</param>
        /// <param name="requestData">The data to serialize and send.</param>
        /// <param name="headers">Optional additional headers to include with the request.</param>
        /// <param name="jsonOptions">Optional JSON serialization options.</param>
        /// <param name="logger">Optional logger for request/response logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The HttpResponseMessage for further processing.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the API.</exception>
        public static async Task<HttpResponseMessage> SendStreamingRequestAsync<TRequest>(
            HttpClient client,
            HttpMethod method,
            string endpoint,
            TRequest requestData,
            IDictionary<string, string>? headers = null,
            JsonSerializerOptions? jsonOptions = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var options = jsonOptions ?? DefaultJsonOptions;

            try
            {
                var request = CreateJsonRequest(method, endpoint, requestData, headers, options, logger);
                
                // Add Accept header for SSE if not already present
                if (!request.Headers.Accept.Any(h => h.MediaType == "text/event-stream"))
                {
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
                }
                
                logger?.LogDebug("Sending streaming {Method} request to {Endpoint}", method, endpoint);
                logger?.LogDebug("Request headers: {Headers}", request.Headers.ToString());

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    logger?.LogError("API streaming error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    
                    // Check for Anthropic authentication errors
                    if (errorContent.Contains("invalid bearer token", StringComparison.OrdinalIgnoreCase) ||
                        errorContent.Contains("bearer", StringComparison.OrdinalIgnoreCase) && errorContent.Contains("anthropic", StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogWarning("Detected Anthropic authentication error - Bearer token used instead of x-api-key");
                        throw new LLMCommunicationException(
                            "Anthropic authentication error: Invalid API key or authentication method. Anthropic requires 'x-api-key' header, not Bearer tokens. Please verify your API key is valid and starts with 'sk-ant-'.");
                    }
                    
                    throw new LLMCommunicationException(
                        $"API returned an error: {(int)response.StatusCode} {response.StatusCode} - {errorContent}");
                }

                logger?.LogDebug("Received successful streaming response with status code {StatusCode}", response.StatusCode);
                return response;
            }
            catch (HttpRequestException ex)
            {
                logger?.LogError(ex, "HTTP request error communicating with streaming API at {Endpoint}", endpoint);
                throw new LLMCommunicationException($"HTTP request error during streaming: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger?.LogWarning("Streaming request to {Endpoint} was cancelled", endpoint);
                throw new LLMCommunicationException("Streaming request was cancelled", ex);
            }
            catch (TaskCanceledException ex)
            {
                logger?.LogError(ex, "Streaming request to {Endpoint} timed out", endpoint);
                throw new LLMCommunicationException("Streaming request timed out", ex);
            }
            catch (Exception ex) when (ex is not LLMCommunicationException)
            {
                logger?.LogError(ex, "Unexpected error during streaming API communication with {Endpoint}", endpoint);
                throw new LLMCommunicationException($"Unexpected streaming error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Adds standard authentication headers to a request based on the authentication type.
        /// </summary>
        /// <param name="headers">The headers dictionary to add authentication to.</param>
        /// <param name="authType">The type of authentication (Bearer, ApiKey, etc.).</param>
        /// <param name="authValue">The authentication value (token, key, etc.).</param>
        public static void AddAuthenticationHeader(
            IDictionary<string, string> headers,
            string authType,
            string authValue)
        {
            if (string.IsNullOrEmpty(authValue))
            {
                throw new ArgumentException("Authentication value cannot be empty", nameof(authValue));
            }

            switch (authType.ToLowerInvariant())
            {
                case "bearer":
                    headers["Authorization"] = $"Bearer {authValue}";
                    break;

                case "apikey":
                case "api-key":
                    headers["x-api-key"] = authValue;
                    break;

                case "basic":
                    var encodedAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authValue));
                    headers["Authorization"] = $"Basic {encodedAuth}";
                    break;

                default:
                    // For custom header auth
                    headers[authType] = authValue;
                    break;
            }
        }
    }
}
