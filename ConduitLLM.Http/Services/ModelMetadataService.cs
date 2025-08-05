using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for retrieving model metadata.
    /// </summary>
    public interface IModelMetadataService
    {
        /// <summary>
        /// Gets metadata for a specific model.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <returns>Model metadata or null if not found.</returns>
        Task<object?> GetModelMetadataAsync(string modelId);
    }

    /// <summary>
    /// Implementation of model metadata service that reads from static JSON.
    /// </summary>
    public class ModelMetadataService : IModelMetadataService
    {
        private readonly ILogger<ModelMetadataService> _logger;
        private readonly Dictionary<string, object> _metadataCache;
        private readonly object _cacheLock = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

        public ModelMetadataService(ILogger<ModelMetadataService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metadataCache = new Dictionary<string, object>();
        }

        public async Task<object?> GetModelMetadataAsync(string modelId)
        {
            try
            {
                await LoadMetadataIfNeededAsync();

                lock (_cacheLock)
                {
                    if (_metadataCache.TryGetValue(modelId, out var metadata))
                    {
                        return metadata;
                    }
                }

                _logger.LogDebug("No metadata found for model {ModelId}", modelId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metadata for model {ModelId}", modelId);
                throw; // Re-throw to ensure errors are properly reported
            }
        }

        private async Task LoadMetadataIfNeededAsync()
        {
            lock (_cacheLock)
            {
                if (DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry && _metadataCache.Count > 0)
                {
                    return;
                }
            }

            try
            {
                var assemblyLocation = GetAssemblyDirectory();
                var metadataPath = Path.Combine(assemblyLocation, "StaticModels", "model-metadata.json");
                
                _logger.LogDebug("Assembly location: {AssemblyLocation}", assemblyLocation);
                _logger.LogDebug("Looking for metadata at: {MetadataPath}", metadataPath);

                if (!File.Exists(metadataPath))
                {
                    // Try alternative paths
                    var alternativePaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StaticModels", "model-metadata.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "StaticModels", "model-metadata.json"),
                        Path.Combine("/app", "StaticModels", "model-metadata.json")
                    };

                    foreach (var altPath in alternativePaths)
                    {
                        _logger.LogDebug("Trying alternative path: {Path}", altPath);
                        if (File.Exists(altPath))
                        {
                            metadataPath = altPath;
                            _logger.LogInformation("Found metadata file at alternative path: {Path}", altPath);
                            break;
                        }
                    }

                    if (!File.Exists(metadataPath))
                    {
                        _logger.LogWarning("Model metadata file not found at any location. Tried: {OriginalPath} and alternatives", metadataPath);
                        return;
                    }
                }

                var json = await File.ReadAllTextAsync(metadataPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var allMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);

                if (allMetadata != null)
                {
                    lock (_cacheLock)
                    {
                        _metadataCache.Clear();
                        foreach (var kvp in allMetadata)
                        {
                            _metadataCache[kvp.Key] = kvp.Value;
                        }
                        _lastCacheUpdate = DateTime.UtcNow;
                    }

                    _logger.LogInformation("Loaded metadata for {Count} models from {Path}", _metadataCache.Count, metadataPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading model metadata");
                throw; // Re-throw to ensure the error is visible in the endpoint response
            }
        }

        /// <summary>
        /// Gets the assembly directory. Virtual to allow testing.
        /// </summary>
        protected virtual string GetAssemblyDirectory()
        {
            return Path.GetDirectoryName(typeof(ModelMetadataService).Assembly.Location)!;
        }
    }
}