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

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Discovery methods for SambaNova models.
    /// </summary>
    public static class SambaNovaModels
    {
        /// <summary>
        /// Discovers available SambaNova models.
        /// </summary>
        /// <param name="httpClient">HTTP client for making requests.</param>
        /// <param name="apiKey">Optional API key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered SambaNova models.</returns>
        public static async Task<List<DiscoveredModel>> DiscoverAsync(
            HttpClient httpClient,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Load models from static JSON file
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyLocation = assembly.Location;
                var directory = Path.GetDirectoryName(assemblyLocation);
                var jsonPath = Path.Combine(directory!, "StaticModels", "sambanova-models.json");

                if (!File.Exists(jsonPath))
                {
                    // Return fallback models if JSON file not found
                    return GetFallbackModels();
                }

                var jsonContent = await File.ReadAllTextAsync(jsonPath, cancellationToken);
                var modelsData = JsonSerializer.Deserialize<ModelsJson>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (modelsData?.Models == null || modelsData.Models.Count() == 0)
                {
                    return GetFallbackModels();
                }

                return modelsData.Models.Select(m => new DiscoveredModel
                {
                    ModelId = m.Id,
                    Provider = "sambanova",
                    DisplayName = m.Name ?? m.Id,
                    Capabilities = new ModelCapabilities
                    {
                        Chat = m.Capabilities?.Chat ?? true,
                        ChatStream = m.Capabilities?.Chat ?? true,
                        Vision = m.Capabilities?.Vision ?? false,
                        FunctionCalling = m.Capabilities?.FunctionCalling ?? true,
                        JsonMode = m.Capabilities?.JsonMode ?? true,
                        MaxTokens = m.ContextLength,
                        MaxOutputTokens = m.MaxOutputTokens
                    },
                    LastVerified = DateTime.UtcNow
                }).ToList();
            }
            catch
            {
                // Return fallback models on any error
                return GetFallbackModels();
            }
        }

        private static List<DiscoveredModel> GetFallbackModels()
        {
            var models = new[]
            {
                ("DeepSeek-R1", "DeepSeek R1", 32768, 8192, false),
                ("DeepSeek-V3-0324", "DeepSeek V3 0324", 32768, 8192, false),
                ("DeepSeek-R1-Distill-Llama-70B", "DeepSeek R1 Distill Llama 70B", 131072, 16384, false),
                ("Meta-Llama-3.3-70B-Instruct", "Meta Llama 3.3 70B Instruct", 131072, 16384, false),
                ("Meta-Llama-3.1-8B-Instruct", "Meta Llama 3.1 8B Instruct", 16384, 4096, false),
                ("Llama-3.3-Swallow-70B-Instruct-v0.4", "Llama 3.3 Swallow 70B Instruct", 16384, 4096, false),
                ("Qwen3-32B", "Qwen3 32B", 8192, 4096, false),
                ("E5-Mistral-7B-Instruct", "E5 Mistral 7B Instruct", 4096, 2048, false),
                ("Llama-4-Maverick-17B-128E-Instruct", "Llama 4 Maverick 17B (Multimodal)", 131072, 16384, true)
            };

            return models.Select(m => new DiscoveredModel
            {
                ModelId = m.Item1,
                Provider = "sambanova",
                DisplayName = m.Item2,
                Capabilities = new ModelCapabilities
                {
                    Chat = true,
                    ChatStream = true,
                    Vision = m.Item5,
                    FunctionCalling = true,
                    JsonMode = true,
                    MaxTokens = m.Item3,
                    MaxOutputTokens = m.Item4
                },
                LastVerified = DateTime.UtcNow
            }).ToList();
        }

        // Helper classes for JSON deserialization
        private class ModelsJson
        {
            public List<ModelJson> Models { get; set; } = new();
        }

        private class ModelJson
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public int ContextLength { get; set; }
            public int MaxOutputTokens { get; set; }
            public CapabilitiesJson? Capabilities { get; set; }
        }

        private class CapabilitiesJson
        {
            public bool Chat { get; set; }
            public bool Vision { get; set; }
            public bool FunctionCalling { get; set; }
            public bool JsonMode { get; set; }
        }
    }
}