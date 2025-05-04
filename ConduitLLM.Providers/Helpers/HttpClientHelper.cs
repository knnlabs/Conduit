using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Utilities;

namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Provider-specific extension of the core HttpClientHelper with additional methods
    /// tailored for LLM API interactions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class builds on the core HttpClientHelper functionality and adds specialized methods
    /// for working with LLM provider APIs. It provides standardized approaches for handling
    /// provider-specific request formatting, authentication schemes, and response parsing.
    /// </para>
    /// <para>
    /// The helpers encapsulate common patterns used across different LLM clients to reduce
    /// code duplication and ensure consistent error handling and logging.
    /// </para>
    /// </remarks>
    public static class HttpClientHelper
    {
        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Sends a JSON request to an LLM provider API and deserializes the response.
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
        /// <remarks>
        /// This method delegates to the core HttpClientHelper.SendJsonRequestAsync method
        /// to maintain a consistent approach to HTTP requests across the application.
        /// </remarks>
        public static Task<TResponse> SendJsonRequestAsync<TRequest, TResponse>(
            HttpClient client,
            HttpMethod method,
            string endpoint,
            TRequest requestData,
            IDictionary<string, string>? headers = null,
            JsonSerializerOptions? jsonOptions = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            return Core.Utilities.HttpClientHelper.SendJsonRequestAsync<TRequest, TResponse>(
                client, method, endpoint, requestData, headers, jsonOptions, logger, cancellationToken);
        }

        /// <summary>
        /// Sends a request with form URL encoded content and deserializes the response.
        /// </summary>
        /// <typeparam name="TResponse">The type to deserialize the response into.</typeparam>
        /// <param name="client">The HttpClient to use for the request.</param>
        /// <param name="method">The HTTP method to use.</param>
        /// <param name="endpoint">The endpoint to send the request to.</param>
        /// <param name="formData">The form data to send.</param>
        /// <param name="headers">Optional additional headers to include with the request.</param>
        /// <param name="jsonOptions">Optional JSON serialization options for response deserialization.</param>
        /// <param name="logger">Optional logger for request/response logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The deserialized response object.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is an error communicating with the API.</exception>
        /// <remarks>
        /// This method is useful for APIs that require form URL encoded requests instead of JSON,
        /// such as some authentication endpoints or certain provider APIs.
        /// </remarks>
        public static async Task<TResponse> SendFormRequestAsync<TResponse>(
            HttpClient client,
            HttpMethod method,
            string endpoint,
            Dictionary<string, string> formData,
            IDictionary<string, string>? headers = null,
            JsonSerializerOptions? jsonOptions = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var options = jsonOptions ?? DefaultJsonOptions;
            
            try
            {
                var request = new HttpRequestMessage(method, endpoint);
                
                // Add form content
                if (formData != null && formData.Count > 0)
                {
                    request.Content = new FormUrlEncodedContent(formData);
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
                
                logger?.LogDebug("Sending {Method} form request to {Endpoint}", method, endpoint);
                
                using var response = await client.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await Core.Utilities.HttpClientHelper.ReadErrorContentAsync(response, cancellationToken);
                    logger?.LogError("API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"API returned an error: {(int)response.StatusCode} {response.StatusCode} - {errorContent}");
                }
                
                logger?.LogDebug("Received successful response with status code {StatusCode}", response.StatusCode);
                
                var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<TResponse>(responseStream, options, cancellationToken)
                    ?? throw new LLMCommunicationException("Failed to deserialize response");
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
        /// <remarks>
        /// This method delegates to the core HttpClientHelper.SendStreamingRequestAsync method
        /// to maintain a consistent approach to streaming requests across the application.
        /// </remarks>
        public static Task<HttpResponseMessage> SendStreamingRequestAsync<TRequest>(
            HttpClient client,
            HttpMethod method,
            string endpoint,
            TRequest requestData,
            IDictionary<string, string>? headers = null,
            JsonSerializerOptions? jsonOptions = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            return Core.Utilities.HttpClientHelper.SendStreamingRequestAsync<TRequest>(
                client, method, endpoint, requestData, headers, jsonOptions, logger, cancellationToken);
        }

        /// <summary>
        /// Formats query parameters for inclusion in a URL.
        /// </summary>
        /// <param name="parameters">Dictionary of parameter names and values.</param>
        /// <returns>A properly formatted query string.</returns>
        /// <remarks>
        /// This method handles URL encoding of parameter values and creates a properly
        /// formatted query string for appending to a base URL.
        /// </remarks>
        public static string FormatQueryParameters(Dictionary<string, string?> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return string.Empty;
            }
            
            var queryParts = new List<string>();
            
            foreach (var parameter in parameters)
            {
                if (parameter.Value != null)
                {
                    queryParts.Add($"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}");
                }
            }
            
            return queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : string.Empty;
        }

        /// <summary>
        /// Appends query parameters to a base URL.
        /// </summary>
        /// <param name="baseUrl">The base URL to append parameters to.</param>
        /// <param name="parameters">Dictionary of parameter names and values.</param>
        /// <returns>The combined URL with query parameters.</returns>
        /// <remarks>
        /// This method properly handles URL encoding of parameter values and handles
        /// the case where the base URL may already contain query parameters.
        /// </remarks>
        public static string AppendQueryParameters(string baseUrl, Dictionary<string, string?> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return baseUrl;
            }
            
            var separator = baseUrl.Contains("?") ? "&" : "?";
            var queryParts = new List<string>();
            
            foreach (var parameter in parameters)
            {
                if (parameter.Value != null)
                {
                    queryParts.Add($"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}");
                }
            }
            
            return queryParts.Count > 0 
                ? baseUrl + separator + string.Join("&", queryParts) 
                : baseUrl;
        }

        /// <summary>
        /// Creates headers specifically for multipart form data requests.
        /// </summary>
        /// <param name="boundary">The boundary string to use for the multipart request.</param>
        /// <returns>A dictionary of headers for the multipart request.</returns>
        /// <remarks>
        /// This method sets up the correct Content-Type header with the boundary parameter
        /// required for multipart form data requests.
        /// </remarks>
        public static Dictionary<string, string> CreateMultipartHeaders(string boundary)
        {
            return new Dictionary<string, string>
            {
                ["Content-Type"] = $"multipart/form-data; boundary={boundary}"
            };
        }

        /// <summary>
        /// Creates a multipart form data content for file uploads and form fields.
        /// </summary>
        /// <param name="fileContents">Dictionary mapping file parameter names to file content.</param>
        /// <param name="fileNames">Dictionary mapping file parameter names to file names.</param>
        /// <param name="formFields">Dictionary of form field names and values.</param>
        /// <returns>A MultipartFormDataContent configured with the provided files and fields.</returns>
        /// <remarks>
        /// This method is useful for APIs that require file uploads, such as vision or
        /// document processing endpoints.
        /// </remarks>
        public static MultipartFormDataContent CreateMultipartContent(
            Dictionary<string, byte[]>? fileContents = null,
            Dictionary<string, string>? fileNames = null,
            Dictionary<string, string>? formFields = null)
        {
            var content = new MultipartFormDataContent();
            
            // Add file contents if provided
            if (fileContents != null)
            {
                foreach (var file in fileContents)
                {
                    var fileContent = new ByteArrayContent(file.Value);
                    
                    // Try to determine content type from file extension if a filename is provided
                    if (fileNames != null && fileNames.TryGetValue(file.Key, out var fileName))
                    {
                        var contentType = GetContentTypeFromFileName(fileName);
                        if (contentType != null)
                        {
                            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                        }
                        
                        content.Add(fileContent, file.Key, fileName);
                    }
                    else
                    {
                        content.Add(fileContent, file.Key);
                    }
                }
            }
            
            // Add form fields if provided
            if (formFields != null)
            {
                foreach (var field in formFields)
                {
                    content.Add(new StringContent(field.Value), field.Key);
                }
            }
            
            return content;
        }

        /// <summary>
        /// Determines the content type based on a file's extension.
        /// </summary>
        /// <param name="fileName">The name of the file including extension.</param>
        /// <returns>The MIME content type if recognized, or null.</returns>
        private static string? GetContentTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".json" => "application/json",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".xml" => "application/xml",
                ".html" => "text/html",
                _ => null
            };
        }
    }
}