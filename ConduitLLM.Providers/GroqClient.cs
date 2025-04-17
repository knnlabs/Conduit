using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

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
    /// <param name="httpClient">Optional HTTP client for testing.</param>
    public GroqClient(
        ProviderCredentials credentials,
        string providerModelId,
        ILogger<GroqClient> logger,
        HttpClient? httpClient = null)
        : base(
            EnsureGroqCredentials(credentials),
            providerModelId,
            logger,
            providerName: "groq",
            httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
}
