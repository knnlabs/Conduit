using System.Net.Http.Headers;
using System.Text.Json;

using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Provider-specific HTTP utilities that extend the core HttpClientHelper functionality.
    /// Provides specialized methods for LLM provider API interactions that are not covered
    /// by the core HTTP helpers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides additional HTTP utilities specific to LLM provider needs, such as
    /// form-encoded requests for authentication endpoints and multipart content for file uploads.
    /// </para>
    /// <para>
    /// For standard JSON requests and streaming, use <see cref="Core.Utilities.HttpClientHelper"/> directly.
    /// </para>
    /// </remarks>
    public static class ProviderHttpHelper
    {
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
            // Use Core's default options if none specified
            var options = jsonOptions ?? ConduitLLM.Core.Serialization.ConduitJsonOptions.Wire;

            try
            {
                var request = new HttpRequestMessage(method, endpoint);

                // Add form content
                if (formData != null && formData.Any())
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
            if (parameters == null || !parameters.Any())
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

            return queryParts.Any() ? "?" + string.Join("&", queryParts) : string.Empty;
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
            if (parameters == null || !parameters.Any())
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

            return queryParts.Any()
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
            return ConduitLLM.Core.Utilities.MediaContentTypes.GetContentType(Path.GetExtension(fileName));
        }
    }
}
