using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// In-memory implementation of media storage for development and testing.
    /// </summary>
    public class InMemoryMediaStorageService : IMediaStorageService
    {
        private readonly ConcurrentDictionary<string, StoredMedia> _storage = new();
        private readonly ILogger<InMemoryMediaStorageService> _logger;
        private readonly string _baseUrl;

        private class StoredMedia
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public MediaInfo Info { get; set; } = new();
        }

        public InMemoryMediaStorageService(
            ILogger<InMemoryMediaStorageService> logger,
            string baseUrl = "http://localhost:5000")
        {
            _logger = logger;
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreAsync(Stream content, MediaMetadata metadata)
        {
            try
            {
                // Read content into memory
                using var memoryStream = new MemoryStream();
                await content.CopyToAsync(memoryStream);
                var data = memoryStream.ToArray();

                // Generate storage key
                var contentHash = ComputeHash(data);
                var extension = GetExtensionFromContentType(metadata.ContentType);
                var storageKey = GenerateStorageKey(contentHash, metadata.MediaType, extension);

                // Store in memory
                var mediaInfo = new MediaInfo
                {
                    StorageKey = storageKey,
                    ContentType = metadata.ContentType,
                    SizeBytes = data.Length,
                    FileName = metadata.FileName,
                    MediaType = metadata.MediaType,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = metadata.ExpiresAt,
                    CustomMetadata = new Dictionary<string, string>(metadata.CustomMetadata ?? new())
                };

                _storage[storageKey] = new StoredMedia
                {
                    Data = data,
                    Info = mediaInfo
                };

                _logger.LogInformation("Stored media in memory with key {StorageKey}", storageKey);

                var url = await GenerateUrlAsync(storageKey);
                
                return new MediaStorageResult
                {
                    StorageKey = storageKey,
                    Url = url,
                    SizeBytes = data.Length,
                    ContentHash = contentHash,
                    CreatedAt = mediaInfo.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store media in memory");
                throw;
            }
        }

        /// <inheritdoc/>
        public Task<Stream?> GetStreamAsync(string storageKey)
        {
            if (_storage.TryGetValue(storageKey, out var stored))
            {
                return Task.FromResult<Stream?>(new MemoryStream(stored.Data));
            }

            _logger.LogWarning("Media with key {StorageKey} not found in memory", storageKey);
            return Task.FromResult<Stream?>(null);
        }

        /// <inheritdoc/>
        public Task<MediaInfo?> GetInfoAsync(string storageKey)
        {
            if (_storage.TryGetValue(storageKey, out var stored))
            {
                // Return a copy to prevent external modification
                return Task.FromResult<MediaInfo?>(new MediaInfo
                {
                    StorageKey = stored.Info.StorageKey,
                    ContentType = stored.Info.ContentType,
                    SizeBytes = stored.Info.SizeBytes,
                    FileName = stored.Info.FileName,
                    MediaType = stored.Info.MediaType,
                    CreatedAt = stored.Info.CreatedAt,
                    ExpiresAt = stored.Info.ExpiresAt,
                    CustomMetadata = new Dictionary<string, string>(stored.Info.CustomMetadata)
                });
            }

            return Task.FromResult<MediaInfo?>(null);
        }

        /// <inheritdoc/>
        public Task<bool> DeleteAsync(string storageKey)
        {
            var removed = _storage.TryRemove(storageKey, out _);
            if (removed)
            {
                _logger.LogInformation("Deleted media with key {StorageKey} from memory", storageKey);
            }
            return Task.FromResult(removed);
        }

        /// <inheritdoc/>
        public Task<string> GenerateUrlAsync(string storageKey, TimeSpan? expiration = null)
        {
            // For in-memory storage, we'll need the HTTP endpoint to serve these
            var url = $"{_baseUrl}/v1/media/{storageKey}";
            return Task.FromResult(url);
        }

        /// <inheritdoc/>
        public Task<bool> ExistsAsync(string storageKey)
        {
            return Task.FromResult(_storage.ContainsKey(storageKey));
        }

        private static string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToBase64String(hash).Replace("/", "-").Replace("+", "_").TrimEnd('=');
        }

        private static string GenerateStorageKey(string contentHash, MediaType mediaType, string extension)
        {
            var typeFolder = mediaType.ToString().ToLower();
            return $"{typeFolder}/{contentHash}{extension}";
        }

        private static string GetExtensionFromContentType(string contentType)
        {
            return contentType?.ToLower() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "audio/mpeg" => ".mp3",
                "audio/wav" => ".wav",
                _ => ""
            };
        }

        /// <summary>
        /// Gets the current storage size in bytes.
        /// </summary>
        public long GetTotalSizeBytes()
        {
            long total = 0;
            foreach (var item in _storage.Values)
            {
                total += item.Data.Length;
            }
            return total;
        }

        /// <summary>
        /// Gets the number of items in storage.
        /// </summary>
        public int GetItemCount()
        {
            return _storage.Count;
        }
    }
}