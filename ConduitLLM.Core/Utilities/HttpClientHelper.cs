using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Exceptions;

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
                var request = CreateJsonRequest(method, endpoint, requestData, headers, options);
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
        /// Creates an HTTP request with JSON content and headers.
        /// </summary>
        private static HttpRequestMessage CreateJsonRequest<TRequest>(
            HttpMethod method,
            string endpoint,
            TRequest requestData,
            IDictionary<string, string>? headers,
            JsonSerializerOptions options)
        {
            var request = new HttpRequestMessage(method, endpoint);
            
            // Add content if data is provided
            if (requestData != null)
            {
                var requestJson = JsonSerializer.Serialize(requestData, options);
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
                throw new LLMCommunicationException(
                    $"API returned an error: {(int)response.StatusCode} {response.StatusCode} - {errorContent}");
            }
            
            logger?.LogDebug("Received successful response with status code {StatusCode}", response.StatusCode);
            
            var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<TResponse>(responseStream, options, cancellationToken)
                ?? throw new LLMCommunicationException("Failed to deserialize response");
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
                var request = CreateJsonRequest(method, endpoint, requestData, headers, options);
                logger?.LogDebug("Sending streaming {Method} request to {Endpoint}", method, endpoint);
                
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    logger?.LogError("API streaming error: {StatusCode} - {Content}", response.StatusCode, errorContent);
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