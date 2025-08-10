using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.SambaNova
{
    /// <summary>
    /// SambaNovaClient partial class containing model listing functionality.
    /// </summary>
    public partial class SambaNovaClient
    {
        /// <summary>
        /// Gets available models for SambaNova.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available SambaNova models.</returns>
        /// <remarks>
        /// This implementation loads models from a static JSON file since SambaNova
        /// may not provide a public models endpoint. The models are curated and maintained
        /// in the sambanova-models.json file.
        /// </remarks>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogDebug("Loading SambaNova models from static configuration");

                // Load models from the static JSON file
                var models = await LoadStaticModelsAsync(cancellationToken);
                
                if (models.Count > 0)
                {
                    Logger.LogInformation("Loaded {Count} SambaNova models from static configuration", models.Count);
                    return models;
                }

                // If static loading fails, return the fallback models
                Logger.LogWarning("Failed to load models from static file, using fallback models");
                return SambaNovaModels;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading SambaNova models, returning fallback list");
                return SambaNovaModels;
            }
        }

        /// <summary>
        /// Loads models from the static JSON file.
        /// </summary>
        private async Task<List<ExtendedModelInfo>> LoadStaticModelsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get the path to the JSON file
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyLocation = assembly.Location;
                var directory = Path.GetDirectoryName(assemblyLocation);
                var jsonPath = Path.Combine(directory!, "StaticModels", "sambanova-models.json");

                if (!File.Exists(jsonPath))
                {
                    Logger.LogWarning("SambaNova models JSON file not found at {Path}", jsonPath);
                    return new List<ExtendedModelInfo>();
                }

                // Read and parse the JSON file
                var jsonContent = await File.ReadAllTextAsync(jsonPath, cancellationToken);
                var modelsData = JsonSerializer.Deserialize<SambaNovaModelsJson>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (modelsData?.Models == null)
                {
                    Logger.LogWarning("Invalid or empty SambaNova models JSON file");
                    return new List<ExtendedModelInfo>();
                }

                // Convert to ExtendedModelInfo
                return modelsData.Models
                    .Select(m => ExtendedModelInfo.Create(
                        m.Id,
                        "sambanova",
                        m.Id)
                    .WithName(m.Name ?? m.Id))
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load SambaNova models from static JSON file");
                return new List<ExtendedModelInfo>();
            }
        }

        /// <summary>
        /// Represents the structure of the sambanova-models.json file.
        /// </summary>
        private class SambaNovaModelsJson
        {
            public List<SambaNovaModelJson> Models { get; set; } = new();
        }

        /// <summary>
        /// Represents a single model in the JSON file.
        /// </summary>
        private class SambaNovaModelJson
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public long Created { get; set; }
            public string OwnedBy { get; set; } = "sambanova";
            public string Object { get; set; } = "model";
            public int ContextLength { get; set; }
            public int MaxOutputTokens { get; set; }
            public ModelCapabilitiesJson? Capabilities { get; set; }
        }

        /// <summary>
        /// Represents model capabilities in the JSON file.
        /// </summary>
        private class ModelCapabilitiesJson
        {
            public bool Chat { get; set; }
            public bool Vision { get; set; }
            public bool FunctionCalling { get; set; }
            public bool JsonMode { get; set; }
            public bool SystemMessage { get; set; }
        }

        /// <summary>
        /// Lists the models available from SambaNova.
        /// </summary>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of available model IDs.</returns>
        /// <remarks>
        /// This implementation returns model IDs from our static configuration since
        /// SambaNova may not provide a public models endpoint.
        /// </remarks>
        public override async Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogDebug("Listing SambaNova models from static configuration");
                
                // Get the full model info
                var models = await GetModelsAsync(apiKey, cancellationToken);
                
                // Return just the model IDs
                var modelIds = models.Select(m => m.Id).ToList();
                
                Logger.LogInformation("Returning {Count} SambaNova model IDs", modelIds.Count);
                return modelIds;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error listing SambaNova models, returning fallback list");
                // Return fallback model IDs
                return SambaNovaModels.Select(m => m.Id).ToList();
            }
        }
    }
}