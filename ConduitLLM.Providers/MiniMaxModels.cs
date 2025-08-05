using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Provides model discovery capabilities for MiniMax.
    /// </summary>
    public static class MiniMaxModels
    {
        /// <summary>
        /// Discovers available models from the MiniMax API.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for API calls.</param>
        /// <param name="apiKey">MiniMax API key. If null, returns empty list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered models with their capabilities.</returns>
        public static async Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient, 
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return new List<DiscoveredModel>();
            }

            // Load models from static JSON file
            return await LoadStaticModelsAsync();
        }

        private static async Task<List<DiscoveredModel>> LoadStaticModelsAsync()
        {
            try
            {
                // Get the path to the JSON file relative to the assembly location
                var assembly = typeof(MiniMaxModels).Assembly;
                var assemblyLocation = Path.GetDirectoryName(assembly.Location);
                var jsonPath = Path.Combine(assemblyLocation!, "StaticModels", "minimax-models.json");
                
                if (!File.Exists(jsonPath))
                {
                    // Fallback to empty list if JSON file not found
                    throw new NotSupportedException(
                        "MiniMax does not provide a models listing endpoint. " +
                        "Model availability must be confirmed through MiniMax's documentation. " +
                        "Configure specific model IDs directly in your application settings.");
                }

                var json = await File.ReadAllTextAsync(jsonPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var modelsData = JsonSerializer.Deserialize<StaticModelsData>(json, options);
                
                if (modelsData?.Models == null || modelsData.Models.Count == 0)
                {
                    throw new NotSupportedException(
                        "MiniMax does not provide a models listing endpoint. " +
                        "Model availability must be confirmed through MiniMax's documentation. " +
                        "Configure specific model IDs directly in your application settings.");
                }

                return modelsData.Models.Select(model => new DiscoveredModel
                {
                    ModelId = model.Id,
                    DisplayName = model.Name ?? FormatDisplayName(model.Id),
                    Provider = "minimax",
                    Capabilities = ConvertCapabilities(model),
                    Metadata = new Dictionary<string, object>
                    {
                        ["created"] = model.Created ?? 0,
                        ["owned_by"] = model.OwnedBy ?? "minimax",
                        ["object"] = model.Object ?? "model"
                    }
                }).ToList();
            }
            catch (NotSupportedException)
            {
                // Rethrow NotSupportedException so it can be handled properly
                throw;
            }
            catch (Exception)
            {
                // If any error occurs loading from JSON, throw NotSupportedException
                throw new NotSupportedException(
                    "MiniMax does not provide a models listing endpoint. " +
                    "Model availability must be confirmed through MiniMax's documentation. " +
                    "Configure specific model IDs directly in your application settings.");
            }
        }

        private static ModelCapabilities ConvertCapabilities(StaticModelData model)
        {
            var capabilities = new ModelCapabilities();
            
            if (model.Capabilities != null)
            {
                capabilities.Chat = model.Capabilities.Chat ?? false;
                capabilities.ChatStream = model.Capabilities.Chat ?? false; // If chat is supported, streaming usually is too
                capabilities.Embeddings = model.Capabilities.Embeddings ?? false;
                capabilities.ImageGeneration = model.Capabilities.ImageGeneration ?? false;
                capabilities.Vision = model.Capabilities.Vision ?? false;
                capabilities.FunctionCalling = model.Capabilities.FunctionCalling ?? false;
                capabilities.ToolUse = model.Capabilities.FunctionCalling ?? false;
                capabilities.JsonMode = model.Capabilities.JsonMode ?? false;
                capabilities.VideoGeneration = model.Capabilities.VideoGeneration ?? false;
                capabilities.VideoUnderstanding = model.Capabilities.VideoUnderstanding ?? false;
            }
            
            capabilities.MaxTokens = model.ContextLength;
            capabilities.MaxOutputTokens = model.MaxOutputTokens;
            capabilities.SupportedImageSizes = model.SupportedImageSizes;
            capabilities.SupportedVideoResolutions = model.SupportedVideoResolutions;
            capabilities.MaxVideoDurationSeconds = model.MaxVideoDurationSeconds;
            
            return capabilities;
        }



        private static string FormatDisplayName(string modelId)
        {
            // Format model IDs to more readable display names
            var displayName = modelId
                .Replace("-", " ")
                .Replace("abab", "ABAB")
                .Replace("embo", "Embo")
                .Replace("image", "Image")
                .Replace("video", "Video")
                .Replace("speech", "Speech");

            // Capitalize words
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0 && !words[i].Any(char.IsDigit) && 
                    !words[i].Equals("ABAB", StringComparison.OrdinalIgnoreCase))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        private class StaticModelsData
        {
            public List<StaticModelData> Models { get; set; } = new();
        }

        private class StaticModelData
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public long? Created { get; set; }
            public string? OwnedBy { get; set; }
            public string? Object { get; set; }
            public int? ContextLength { get; set; }
            public int? MaxOutputTokens { get; set; }
            public int? EmbeddingDimensions { get; set; }
            public StaticModelCapabilities? Capabilities { get; set; }
            public List<string>? SupportedImageSizes { get; set; }
            public List<string>? SupportedVideoResolutions { get; set; }
            public int? MaxVideoDurationSeconds { get; set; }
            public List<string>? SupportedVoices { get; set; }
            public List<string>? SupportedAudioFormats { get; set; }
        }

        private class StaticModelCapabilities
        {
            public bool? Chat { get; set; }
            public bool? Vision { get; set; }
            public bool? FunctionCalling { get; set; }
            public bool? JsonMode { get; set; }
            public bool? SystemMessage { get; set; }
            public bool? Embeddings { get; set; }
            public bool? ImageGeneration { get; set; }
            public bool? VideoGeneration { get; set; }
            public bool? VideoUnderstanding { get; set; }
            public bool? AudioSynthesis { get; set; }
            public bool? AudioGeneration { get; set; }
            public bool? AnimationGeneration { get; set; }
        }
    }
}