using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with Mistral AI's API.
/// </summary>
/// <remarks>
/// Mistral AI uses the OpenAI-compatible API format with some specific models and capabilities.
/// </remarks>
public class MistralClient : OpenAIClient
{
    private const string DefaultMistralApiBase = "https://api.mistral.ai/v1/";
    private readonly ILogger<MistralClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MistralClient"/> class.
    /// </summary>
    /// <param name="credentials">The credentials for the Mistral AI API.</param>
    /// <param name="providerModelId">The model ID to use.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param> // Added factory param doc
    public MistralClient(
        ProviderCredentials credentials,
        string providerModelId,
        ILogger<MistralClient> logger,
        IHttpClientFactory httpClientFactory) // Added factory parameter, removed httpClient
        : base(
            EnsureMistralCredentials(credentials),
            providerModelId,
            logger, // Base constructor expects ILogger<OpenAIClient>, but ILogger<MistralClient> is compatible
            httpClientFactory, // Pass the factory
            providerName: "mistral") // providerName is the last optional parameter
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static ProviderCredentials EnsureMistralCredentials(ProviderCredentials credentials)
    {
        if (credentials == null)
        {
            throw new ArgumentNullException(nameof(credentials));
        }

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException("API key is missing for Mistral AI provider.");
        }

        // Set the default API base for Mistral if not specified
        if (string.IsNullOrWhiteSpace(credentials.ApiBase))
        {
            credentials.ApiBase = DefaultMistralApiBase;
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
            _logger.LogWarning(ex, "Failed to retrieve models from Mistral AI API. Returning known models.");
            
            // Fall back to a list of known Mistral models
            return new List<string>
            {
                "mistral-tiny",
                "mistral-small",
                "mistral-medium",
                "mistral-large-latest",
                "mistral-embed"
            };
        }
    }
}
