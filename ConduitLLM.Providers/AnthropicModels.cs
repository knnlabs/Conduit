using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Provides model discovery capabilities for Anthropic.
    /// </summary>
    public static class AnthropicModels
    {
        private const string ModelsEndpoint = "https://api.anthropic.com/v1/models";
        private const string AnthropicVersion = "2023-06-01";

        /// <summary>
        /// Discovers available models from the Anthropic API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">Anthropic API key. If null, returns empty list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models with their capabilities.</returns>
        public static async Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient, 
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            // Anthropic doesn't have a public models endpoint, so we return a hardcoded list
            // when an API key is provided
            if (string.IsNullOrEmpty(apiKey))
            {
                return new List<DiscoveredModel>();
            }

            // Return known Anthropic models
            return await Task.FromResult(GetKnownModels());
        }

        private static List<DiscoveredModel> GetKnownModels()
        {
            var models = new List<AnthropicModel>
            {
                // Claude 3.5 models
                new AnthropicModel { Id = "claude-3-5-haiku-20241022", DisplayName = "Claude 3.5 Haiku", IsLatest = true },
                new AnthropicModel { Id = "claude-3-5-sonnet-20241022", DisplayName = "Claude 3.5 Sonnet", IsLatest = true },
                
                // Claude 3 Opus
                new AnthropicModel { Id = "claude-3-opus-20240229", DisplayName = "Claude 3 Opus", IsLatest = true },
                
                // Legacy Claude 3 models
                new AnthropicModel { Id = "claude-3-sonnet-20240229", DisplayName = "Claude 3 Sonnet (Legacy)" },
                new AnthropicModel { Id = "claude-3-haiku-20240307", DisplayName = "Claude 3 Haiku (Legacy)" },
                
                // Claude 2 models (deprecated but still available)
                new AnthropicModel { Id = "claude-2.1", DisplayName = "Claude 2.1" },
                new AnthropicModel { Id = "claude-2.0", DisplayName = "Claude 2.0" },
                
                // Claude Instant (deprecated but still available)
                new AnthropicModel { Id = "claude-instant-1.2", DisplayName = "Claude Instant 1.2" }
            };

            return models.Select(ConvertToDiscoveredModel).ToList();
        }

        private static DiscoveredModel ConvertToDiscoveredModel(AnthropicModel model)
        {
            var capabilities = InferCapabilities(model);
            
            return new DiscoveredModel
            {
                ModelId = model.Id,
                DisplayName = !string.IsNullOrEmpty(model.DisplayName) ? model.DisplayName : FormatDisplayName(model.Id),
                Provider = "anthropic",
                Capabilities = capabilities
            };
        }

        private static ModelCapabilities InferCapabilities(AnthropicModel model)
        {
            var capabilities = new ModelCapabilities();
            var modelIdLower = model.Id.ToLowerInvariant();

            // All Claude models support chat
            capabilities.Chat = true;
            capabilities.ChatStream = true;

            // Claude 3 and 3.5 models support vision
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

        private static string FormatDisplayName(string modelId)
        {
            // Convert model IDs to more readable display names
            var displayName = modelId
                .Replace("-", " ")
                .Replace("claude", "Claude");

            // Capitalize words and handle version numbers
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !char.IsDigit(words[i][0]) && !char.IsUpper(words[i][0]))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }

            return string.Join(" ", words);
        }

        private class AnthropicModelsResponse
        {
            public List<AnthropicModel> Data { get; set; } = new();
        }

        private class AnthropicModel
        {
            public string Id { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
            public string? Type { get; set; }
            public int? MaxTokens { get; set; }
            public int? MaxOutputTokens { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string? OwnedBy { get; set; }
            public bool IsLatest { get; set; }
        }
    }
}