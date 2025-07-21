using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Model discovery provider for Anthropic Claude models.
    /// Uses Anthropic's model discovery API with custom authentication headers.
    /// </summary>
    public class AnthropicDiscoveryProvider : BaseModelDiscoveryProvider
    {
        private const string ModelsEndpoint = "https://api.anthropic.com/v1/models";
        private const string AnthropicVersion = "2023-06-01";
        private readonly string? _apiKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnthropicDiscoveryProvider"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP client for making API requests.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <param name="apiKey">API key for Anthropic authentication.</param>
        public AnthropicDiscoveryProvider(
            HttpClient httpClient, 
            ILogger<AnthropicDiscoveryProvider> logger,
            string? apiKey = null) 
            : base(httpClient, logger)
        {
            _apiKey = apiKey;
        }

        /// <inheritdoc />
        public override string ProviderName => "anthropic";

        /// <inheritdoc />
        public override bool SupportsDiscovery => !string.IsNullOrEmpty(_apiKey);
        
        /// <inheritdoc />
        public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            // If no API key, return false for availability
            if (string.IsNullOrEmpty(_apiKey))
            {
                return false;
            }
            
            // Use base implementation for actual API discovery
            return await base.IsAvailableAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<List<ModelMetadata>> DiscoverModelsAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Logger.LogWarning("No API key available for Anthropic discovery, using fallback patterns");
                return GetFallbackModels();
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                Logger.LogInformation("Discovering models from Anthropic API");

                var request = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
                
                // Anthropic uses custom headers instead of Authorization
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", AnthropicVersion);
                request.Headers.Add("Accept", "application/json");

                var response = await HttpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Anthropic API returned {StatusCode}: {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    return GetFallbackModels();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<AnthropicModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                {
                    Logger.LogWarning("Anthropic API returned null or empty response");
                    return GetFallbackModels();
                }

                var models = apiResponse.Data
                    .Where(model => !string.IsNullOrEmpty(model.Id))
                    .Select(ConvertToModelMetadata)
                    .ToList();

                if (models.Count == 0)
                {
                    Logger.LogWarning("No valid models found in Anthropic API response");
                    return GetFallbackModels();
                }

                Logger.LogInformation("Successfully discovered {Count} models from Anthropic", models.Count);
                return models;
            }
            catch (Exception ex)
            {
                LogHttpError(ex, "model discovery");
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Gets fallback models when API discovery fails.
        /// </summary>
        private List<ModelMetadata> GetFallbackModels()
        {
            Logger.LogInformation("Using hardcoded Anthropic model list as fallback");

            var fallbackModels = new[]
            {
                "claude-3-5-sonnet-20241022",
                "claude-3-5-sonnet-20240620", 
                "claude-3-5-haiku-20241022",
                "claude-3-opus-20240229",
                "claude-3-sonnet-20240229",
                "claude-3-haiku-20240307",
                "claude-2.1",
                "claude-2.0",
                "claude-instant-1.2"
            };

            return fallbackModels.Select(modelId => CreateFallbackMetadata(modelId, "API discovery unavailable"))
                                .ToList();
        }

        /// <summary>
        /// Converts Anthropic API model data to our ModelMetadata format.
        /// </summary>
        private ModelMetadata ConvertToModelMetadata(AnthropicModel model)
        {
            var metadata = new ModelMetadata
            {
                ModelId = model.Id,
                DisplayName = !string.IsNullOrEmpty(model.DisplayName) ? model.DisplayName : model.Id,
                Provider = "anthropic",
                Source = ModelDiscoverySource.ProviderApi,
                LastUpdated = DateTime.UtcNow,
                AdditionalMetadata = new Dictionary<string, object>()
            };

            // Extract capabilities based on model information
            metadata.Capabilities = InferCapabilitiesFromAnthropic(model);

            // Extract context window and limits
            if (model.MaxTokens.HasValue && model.MaxTokens > 0)
            {
                metadata.MaxContextTokens = model.MaxTokens;
            }

            if (model.MaxOutputTokens.HasValue && model.MaxOutputTokens > 0)
            {
                metadata.MaxOutputTokens = model.MaxOutputTokens;
            }

            // Add Anthropic-specific metadata
            if (model.Type != null)
            {
                metadata.AdditionalMetadata["type"] = model.Type;
            }

            if (model.CreatedAt.HasValue)
            {
                metadata.AdditionalMetadata["created_at"] = model.CreatedAt.Value;
            }

            if (model.OwnedBy != null)
            {
                metadata.AdditionalMetadata["owned_by"] = model.OwnedBy;
            }

            return metadata;
        }

        /// <summary>
        /// Infers model capabilities from Anthropic model information.
        /// </summary>
        private ModelCapabilities InferCapabilitiesFromAnthropic(AnthropicModel model)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = model.Id.ToLowerInvariant();

            // All Claude models support chat
            capabilities.Chat = true;
            capabilities.ChatStream = true;

            // Claude 3 models support vision
            capabilities.Vision = modelIdLower.Contains("claude-3");

            // All Claude models support tool use
            capabilities.ToolUse = true;

            // Claude models don't support JSON mode in the same way as OpenAI
            capabilities.JsonMode = false;

            // Function calling is available but uses tool use instead
            capabilities.FunctionCalling = false;

            // Set context window and output limits based on model
            if (model.MaxTokens.HasValue)
            {
                capabilities.MaxTokens = model.MaxTokens.Value;
            }
            else
            {
                // Default context windows for known models
                capabilities.MaxTokens = modelIdLower switch
                {
                    var id when id.Contains("claude-3") => 200000,
                    var id when id.Contains("claude-2") => 200000,
                    var id when id.Contains("claude-instant") => 100000,
                    _ => 100000
                };
            }

            if (model.MaxOutputTokens.HasValue)
            {
                capabilities.MaxOutputTokens = model.MaxOutputTokens.Value;
            }
            else
            {
                // Default output limits
                capabilities.MaxOutputTokens = 4096;
            }

            return capabilities;
        }

        /// <summary>
        /// Response structure for Anthropic models API.
        /// </summary>
        private class AnthropicModelsResponse
        {
            public List<AnthropicModel> Data { get; set; } = new();
        }

        /// <summary>
        /// Anthropic model information from their API.
        /// </summary>
        private class AnthropicModel
        {
            public string Id { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
            public string? Type { get; set; }
            public int? MaxTokens { get; set; }
            public int? MaxOutputTokens { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string? OwnedBy { get; set; }
        }

        /// <summary>
        /// Gets fallback capabilities for Anthropic models when API is not available.
        /// </summary>
        protected override ModelCapabilities GetAnthropicFallbackCapabilities(string modelId)
        {
            return new ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = modelId.Contains("claude-3"),
                ToolUse = true,
                JsonMode = false,
                FunctionCalling = false,
                MaxTokens = modelId.Contains("claude-3") || modelId.Contains("claude-2") ? 200000 : 100000,
                MaxOutputTokens = 4096
            };
        }
    }
}