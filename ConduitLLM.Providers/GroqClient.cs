using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with Groq's LLM API.
/// </summary>
/// <remarks>
/// Groq uses the OpenAI-compatible API format but with much faster inference speeds.
/// </remarks>
public class GroqClient : OpenAIClient
{
    private const string DefaultGroqApiBase = "https://api.groq.com/v1/";
    private readonly ILogger<GroqClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroqClient"/> class.
    /// </summary>
    /// <param name="credentials">The credentials for the Groq API.</param>
    /// <param name="providerModelId">The model ID to use.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param> // Added factory param doc
    public GroqClient(
        ProviderCredentials credentials,
        string providerModelId,
        ILogger<GroqClient> logger,
        IHttpClientFactory httpClientFactory) // Added factory parameter, removed httpClient
        : base(
            EnsureGroqCredentials(credentials),
            providerModelId,
            logger, // Base constructor expects ILogger<OpenAIClient>, but ILogger<GroqClient> is compatible
            httpClientFactory, // Pass the factory
            providerName: "groq") // providerName is the last optional parameter
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Guard: API key must not be null or empty
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException("API key is missing for provider 'groq' and no override was provided.");
        }
    }

    private static ProviderCredentials EnsureGroqCredentials(ProviderCredentials credentials)
    {
        if (credentials == null)
        {
            throw new ArgumentNullException(nameof(credentials));
        }

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException("API key is missing for Groq provider.");
        }

        // Set the default API base for Groq if not specified
        if (string.IsNullOrWhiteSpace(credentials.ApiBase))
        {
            credentials.ApiBase = DefaultGroqApiBase;
        }

        return credentials;
    }

    /// <inheritdoc />
    public override async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Attempt to use the generic OpenAI-compatible /models endpoint
            return await base.ListModelsAsync(apiKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve models from Groq API. Returning known models.");
            
            // Fall back to a list of known Groq models
            return new List<string>
            {
                "llama3-8b-8192",
                "llama3-70b-8192",
                "llama2-70b-4096",
                "mixtral-8x7b-32768",
                "gemma-7b-it"
            };
        }
    }

    public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.CreateChatCompletionAsync(request, apiKey, cancellationToken);
        }
        catch (LLMCommunicationException ex)
        {
            // Always try to extract the error body from the message if 'Response:' is present
            var msg = ex.Message;
            var idx = msg.IndexOf("Response:");
            if (idx >= 0)
            {
                var extracted = msg.Substring(idx + "Response:".Length).Trim();
                if (!string.IsNullOrEmpty(extracted))
                    throw new LLMCommunicationException(extracted, ex);
            }
            // Fallback: try Data["Body"]
            if (ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
                throw new LLMCommunicationException(body, ex);
            // Fallback: try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                throw new LLMCommunicationException(ex.InnerException.Message, ex.InnerException);
            // Final fallback: just use the original message
            throw new LLMCommunicationException(msg, ex);
        }
        catch (Exception ex)
        {
            // Try to extract error content from HttpRequestException (if available)
            string? errorContent = ex is HttpRequestException httpEx && httpEx.Data["Body"] is string body ? body : null;
            if (!string.IsNullOrEmpty(errorContent))
                throw new LLMCommunicationException(errorContent, ex);
            if (!string.IsNullOrEmpty(ex.Message))
                throw new LLMCommunicationException(ex.Message, ex);
            string message = errorContent != null
                ? $"Groq API error: {errorContent}"
                : $"Groq API error: {ex.Message}";
            _logger.LogError(ex, message);
            throw new LLMCommunicationException(message, ex);
        }
    }
}
