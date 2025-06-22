using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Model discovery provider for OpenRouter with comprehensive metadata extraction.
    /// OpenRouter provides excellent model discovery with pricing, capabilities, and provider information.
    /// </summary>
    public class OpenRouterDiscoveryProvider : BaseModelDiscoveryProvider
    {
        private const string ModelsEndpoint = "https://openrouter.ai/api/v1/models";
        private readonly string? _apiKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenRouterDiscoveryProvider"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP client for making API requests.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <param name="apiKey">Optional API key for enhanced data access.</param>
        public OpenRouterDiscoveryProvider(
            HttpClient httpClient, 
            ILogger<OpenRouterDiscoveryProvider> logger,
            string? apiKey = null) 
            : base(httpClient, logger)
        {
            _apiKey = apiKey;
        }

        /// <inheritdoc />
        public override string ProviderName => "openrouter";

        /// <inheritdoc />
        public override bool SupportsDiscovery => true;

        /// <inheritdoc />
        public override async Task<List<ModelMetadata>> DiscoverModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Discovering models from OpenRouter API");

                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                
                // Add authorization header if API key is available
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                // Add required headers for OpenRouter
                request.Headers.Add("HTTP-Referer", "https://conduit-llm.com");
                request.Headers.Add("X-Title", "Conduit LLM");

                var response = await HttpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("OpenRouter API returned {StatusCode}: {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return new List<ModelMetadata>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<OpenRouterModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null)
                {
                    Logger.LogWarning("OpenRouter API returned null or empty response");
                    return new List<ModelMetadata>();
                }

                var models = apiResponse.Data
                    .Where(model => !string.IsNullOrEmpty(model.Id))
                    .Select(ConvertToModelMetadata)
                    .ToList();

                Logger.LogInformation("Successfully discovered {Count} models from OpenRouter", models.Count);
                return models;
            }
            catch (Exception ex)
            {
                LogHttpError(ex, "model discovery");
                return new List<ModelMetadata>();
            }
        }

        /// <summary>
        /// Converts OpenRouter API model data to our ModelMetadata format.
        /// </summary>
        private ModelMetadata ConvertToModelMetadata(OpenRouterModel model)
        {
            var metadata = new ModelMetadata
            {
                ModelId = model.Id,
                DisplayName = !string.IsNullOrEmpty(model.Name) ? model.Name : model.Id,
                Provider = "openrouter",
                Source = ModelDiscoverySource.ProviderApi,
                LastUpdated = DateTime.UtcNow,
                AdditionalMetadata = new Dictionary<string, object>()
            };

            // Extract capabilities
            metadata.Capabilities = InferCapabilitiesFromOpenRouter(model);

            // Extract pricing information
            if (model.Pricing != null)
            {
                metadata.InputTokenCost = model.Pricing.Prompt;
                metadata.OutputTokenCost = model.Pricing.Completion;
                
                if (model.Pricing.Image != null)
                {
                    metadata.ImageCostPerImage = model.Pricing.Image;
                }
            }

            // Extract context window
            if (model.ContextLength.HasValue && model.ContextLength > 0)
            {
                metadata.MaxContextTokens = model.ContextLength;
            }

            // Add OpenRouter-specific metadata
            if (!string.IsNullOrEmpty(model.Description))
            {
                metadata.AdditionalMetadata["description"] = model.Description;
            }

            if (model.TopProvider != null)
            {
                metadata.AdditionalMetadata["top_provider"] = model.TopProvider;
            }

            if (model.PerRequestLimits != null)
            {
                metadata.AdditionalMetadata["per_request_limits"] = model.PerRequestLimits;
            }

            // Add architecture information if available
            if (!string.IsNullOrEmpty(model.Architecture?.Tokenizer))
            {
                metadata.AdditionalMetadata["tokenizer"] = model.Architecture.Tokenizer;
            }

            if (!string.IsNullOrEmpty(model.Architecture?.InstructType))
            {
                metadata.AdditionalMetadata["instruct_type"] = model.Architecture.InstructType;
            }

            return metadata;
        }

        /// <summary>
        /// Infers model capabilities from OpenRouter model information.
        /// </summary>
        private ModelCapabilities InferCapabilitiesFromOpenRouter(OpenRouterModel model)
        {
            var capabilities = new ModelCapabilities();

            // Most OpenRouter models support chat
            capabilities.Chat = true;
            capabilities.ChatStream = true;

            // Check for vision capabilities
            var modelIdLower = model.Id.ToLowerInvariant();
            var descriptionLower = model.Description?.ToLowerInvariant() ?? "";
            
            capabilities.Vision = modelIdLower.Contains("vision") || 
                                 modelIdLower.Contains("gpt-4o") ||
                                 modelIdLower.Contains("gpt-4-turbo") ||
                                 modelIdLower.Contains("claude-3") ||
                                 modelIdLower.Contains("gemini") ||
                                 descriptionLower.Contains("vision") ||
                                 descriptionLower.Contains("image");

            // Check for function calling capabilities
            capabilities.FunctionCalling = modelIdLower.Contains("gpt-4") ||
                                          modelIdLower.Contains("gpt-3.5") ||
                                          modelIdLower.Contains("claude") ||
                                          descriptionLower.Contains("function");

            capabilities.ToolUse = capabilities.FunctionCalling;

            // Check for JSON mode
            capabilities.JsonMode = modelIdLower.Contains("gpt-4") ||
                                   modelIdLower.Contains("gpt-3.5");

            // Set context window and output tokens
            if (model.ContextLength.HasValue)
            {
                capabilities.MaxTokens = model.ContextLength.Value;
                
                // Estimate max output tokens (usually 25-50% of context window)
                capabilities.MaxOutputTokens = Math.Min(8192, model.ContextLength.Value / 4);
            }

            return capabilities;
        }

        /// <summary>
        /// Response structure for OpenRouter models API.
        /// </summary>
        private class OpenRouterModelsResponse
        {
            public List<OpenRouterModel> Data { get; set; } = new();
        }

        /// <summary>
        /// OpenRouter model information from their API.
        /// </summary>
        private class OpenRouterModel
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public string? Description { get; set; }
            public OpenRouterPricing? Pricing { get; set; }
            public int? ContextLength { get; set; }
            public OpenRouterArchitecture? Architecture { get; set; }
            public object? TopProvider { get; set; }
            public object? PerRequestLimits { get; set; }
        }

        /// <summary>
        /// Pricing information for OpenRouter models.
        /// </summary>
        private class OpenRouterPricing
        {
            public decimal? Prompt { get; set; }
            public decimal? Completion { get; set; }
            public decimal? Image { get; set; }
            public decimal? Request { get; set; }
        }

        /// <summary>
        /// Architecture information for OpenRouter models.
        /// </summary>
        private class OpenRouterArchitecture
        {
            public string? Tokenizer { get; set; }
            public string? InstructType { get; set; }
            public string? Modality { get; set; }
        }
    }
}