using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Database-backed implementation of the model capability service.
    /// Retrieves model capabilities from the ModelProviderMapping table.
    /// </summary>
    public class DatabaseModelCapabilityService : IModelCapabilityService
    {
        private readonly ILogger<DatabaseModelCapabilityService> _logger;
        private readonly IModelProviderMappingRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private const string CacheKeyPrefix = "ModelCapability:";

        public DatabaseModelCapabilityService(
            ILogger<DatabaseModelCapabilityService> logger,
            IModelProviderMappingRepository repository,
            IMemoryCache cache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsVisionAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Vision:{model}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.SupportsVision ?? false;
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking vision capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsAudioTranscriptionAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}AudioTranscription:{model}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.SupportsAudioTranscription ?? false;
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking audio transcription capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsTextToSpeechAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}TTS:{model}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.SupportsTextToSpeech ?? false;
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking TTS capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsRealtimeAudioAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}RealtimeAudio:{model}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.SupportsRealtimeAudio ?? false;
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking realtime audio capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SupportsVideoGenerationAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}VideoGeneration:{model}";
            if (_cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.SupportsVideoGeneration ?? false;
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking video generation capability for model {Model}", model);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetTokenizerTypeAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Tokenizer:{model}";
            if (_cache.TryGetValue<string?>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = mapping?.TokenizerType;

                // Default to cl100k_base if not specified
                if (string.IsNullOrEmpty(result))
                {
                    result = "cl100k_base";
                }

                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tokenizer type for model {Model}", model);
                return "cl100k_base"; // Default fallback
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetSupportedVoicesAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Voices:{model}";
            if (_cache.TryGetValue<List<string>>(cacheKey, out var cached))
            {
                return cached ?? new List<string>();
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = new List<string>();

                if (!string.IsNullOrEmpty(mapping?.SupportedVoices))
                {
                    try
                    {
                        result = JsonSerializer.Deserialize<List<string>>(mapping.SupportedVoices) ?? new List<string>();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in SupportedVoices for model {Model}", model);
                    }
                }

                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported voices for model {Model}", model);
                return new List<string>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetSupportedLanguagesAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Languages:{model}";
            if (_cache.TryGetValue<List<string>>(cacheKey, out var cached))
            {
                return cached ?? new List<string>();
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = new List<string>();

                if (!string.IsNullOrEmpty(mapping?.SupportedLanguages))
                {
                    try
                    {
                        result = JsonSerializer.Deserialize<List<string>>(mapping.SupportedLanguages) ?? new List<string>();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in SupportedLanguages for model {Model}", model);
                    }
                }

                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported languages for model {Model}", model);
                return new List<string>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetSupportedFormatsAsync(string model)
        {
            var cacheKey = $"{CacheKeyPrefix}Formats:{model}";
            if (_cache.TryGetValue<List<string>>(cacheKey, out var cached))
            {
                return cached ?? new List<string>();
            }

            try
            {
                var mapping = await GetMappingByModelNameAsync(model);
                var result = new List<string>();

                if (!string.IsNullOrEmpty(mapping?.SupportedFormats))
                {
                    try
                    {
                        result = JsonSerializer.Deserialize<List<string>>(mapping.SupportedFormats) ?? new List<string>();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in SupportedFormats for model {Model}", model);
                    }
                }

                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported formats for model {Model}", model);
                return new List<string>();
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetDefaultModelAsync(string provider, string capabilityType)
        {
            var cacheKey = $"{CacheKeyPrefix}Default:{provider}:{capabilityType}";
            if (_cache.TryGetValue<string?>(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                var allMappings = await _repository.GetAllAsync(default);
                var defaultMapping = allMappings.FirstOrDefault(m =>
                    m.IsDefault &&
                    m.ProviderCredential?.ProviderType.ToString().Equals(provider, StringComparison.OrdinalIgnoreCase) == true &&
                    m.DefaultCapabilityType?.Equals(capabilityType, StringComparison.OrdinalIgnoreCase) == true);

                var result = defaultMapping?.ModelAlias;
                if (result != null)
                {
                    _cache.Set(cacheKey, result, _cacheExpiration);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default model for provider {Provider} and capability {Capability}",
                    provider, capabilityType);
                return null;
            }
        }

        /// <inheritdoc/>
        public Task RefreshCacheAsync()
        {
            // Clear all cached entries with our prefix
            // Note: IMemoryCache doesn't provide a way to clear by prefix,
            // so we'll rely on the expiration timeout for now
            _logger.LogInformation("Model capability cache refresh requested");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Helper method to get a mapping by model name, checking both alias and provider model name.
        /// </summary>
        private async Task<ModelProviderMapping?> GetMappingByModelNameAsync(string model, CancellationToken cancellationToken = default)
        {
            var mapping = await _repository.GetByModelNameAsync(model, cancellationToken);
            if (mapping == null)
            {
                // Try to find by provider model name
                var allMappings = await _repository.GetAllAsync(cancellationToken);
                mapping = allMappings.FirstOrDefault(m =>
                    m.ProviderModelName.Equals(model, StringComparison.OrdinalIgnoreCase));
            }
            return mapping;
        }
    }
}
