using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Caching functionality for the audio processing service.
    /// </summary>
    public partial class AudioProcessingService
    {
        /// <inheritdoc />
        public Task CacheAudioAsync(
            string key,
            byte[] audioData,
            Dictionary<string, string>? metadata = null,
            int expiration = 3600,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            try
            {
                var cacheData = new CachedAudio
                {
                    Data = audioData,
                    Format = metadata?.GetValueOrDefault("format", "unknown") ?? "unknown",
                    Metadata = metadata ?? new Dictionary<string, string>(),
                    CachedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expiration)
                };

                var serialized = System.Text.Json.JsonSerializer.Serialize(cacheData);
                _cacheService.Set($"audio:{key}", serialized, TimeSpan.FromSeconds(expiration));

                _logger.LogDebug("Cached audio with key {Key} for {Expiration} seconds", key.Replace(Environment.NewLine, ""), expiration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache audio, continuing without cache");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<CachedAudio?> GetCachedAudioAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cached = _cacheService.Get<string>($"audio:{key}");
                if (!string.IsNullOrEmpty(cached))
                {
                    var cacheData = System.Text.Json.JsonSerializer.Deserialize<CachedAudio>(cached);
                    if (cacheData != null && cacheData.ExpiresAt > DateTime.UtcNow)
                    {
                        _logger.LogDebug("Retrieved cached audio with key {Key}", key.Replace(Environment.NewLine, ""));
                        return Task.FromResult<CachedAudio?>(cacheData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve cached audio");
            }

            return Task.FromResult<CachedAudio?>(null);
        }

        private string GenerateCacheKey(byte[] audioData, string operation)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(audioData);
            var hashString = Convert.ToBase64String(hash);
            return $"{operation}:{hashString}";
        }
    }
}