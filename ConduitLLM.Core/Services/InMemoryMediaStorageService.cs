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
        private readonly ConcurrentDictionary<string, MultipartUploadSession> _multipartSessions = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte[]>> _multipartParts = new();
        private readonly ILogger<InMemoryMediaStorageService> _logger;
        private readonly string _baseUrl;

        private class StoredMedia
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public MediaInfo Info { get; set; } = new();
            public VideoMediaMetadata? VideoMetadata { get; set; }
        }

        public InMemoryMediaStorageService(
            ILogger<InMemoryMediaStorageService> logger,
            string baseUrl = "http://localhost:5000")
        {
            _logger = logger;
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreAsync(Stream content, MediaMetadata metadata, IProgress<long>? progress = null)
        {
            try
            {
                // Read content into memory
                using var memoryStream = new MemoryStream();
                var buffer = new byte[81920]; // 80KB buffer
                int bytesRead;
                long totalBytesRead = 0;
                
                while ((bytesRead = await content.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    progress?.Report(totalBytesRead);
                }
                
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
            if (string.IsNullOrEmpty(storageKey))
            {
                return Task.FromResult<Stream?>(null);
            }

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
            if (string.IsNullOrEmpty(storageKey))
            {
                return Task.FromResult(false);
            }

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

        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreVideoAsync(
            Stream content, 
            VideoMediaMetadata metadata,
            Action<long>? progressCallback = null)
        {
            try
            {
                // Read content with progress reporting
                using var memoryStream = new MemoryStream();
                var buffer = new byte[81920]; // 80KB buffer
                int bytesRead;
                long totalBytesRead = 0;
                
                while ((bytesRead = await content.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    progressCallback?.Invoke(totalBytesRead);
                }
                
                var data = memoryStream.ToArray();

                // Generate storage key
                var contentHash = ComputeHash(data);
                var extension = GetExtensionFromContentType(metadata.ContentType);
                var storageKey = GenerateStorageKey(contentHash, MediaType.Video, extension);

                // Store in memory
                var mediaInfo = new MediaInfo
                {
                    StorageKey = storageKey,
                    ContentType = metadata.ContentType,
                    SizeBytes = data.Length,
                    FileName = metadata.FileName,
                    MediaType = MediaType.Video,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = metadata.ExpiresAt,
                    CustomMetadata = new Dictionary<string, string>(metadata.CustomMetadata ?? new())
                    {
                        ["duration"] = metadata.Duration.ToString(),
                        ["resolution"] = metadata.Resolution,
                        ["width"] = metadata.Width.ToString(),
                        ["height"] = metadata.Height.ToString(),
                        ["framerate"] = metadata.FrameRate.ToString()
                    }
                };

                _storage[storageKey] = new StoredMedia
                {
                    Data = data,
                    Info = mediaInfo,
                    VideoMetadata = metadata
                };

                _logger.LogInformation("Stored video in memory with key {StorageKey}", storageKey);

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
                _logger.LogError(ex, "Failed to store video in memory");
                throw;
            }
        }

        /// <inheritdoc/>
        public Task<MultipartUploadSession> InitiateMultipartUploadAsync(VideoMediaMetadata metadata)
        {
            var sessionId = Guid.NewGuid().ToString();
            var storageKey = GenerateStorageKey(
                sessionId, 
                MediaType.Video, 
                GetExtensionFromContentType(metadata.ContentType));

            var session = new MultipartUploadSession
            {
                SessionId = sessionId,
                StorageKey = storageKey,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // 1 hour for in-memory
                MinimumPartSize = 1024 * 1024, // 1MB for in-memory
                MaxParts = 1000
            };

            _multipartSessions[sessionId] = session;
            _multipartParts[sessionId] = new ConcurrentDictionary<int, byte[]>();

            _logger.LogInformation("Initiated in-memory multipart upload session {SessionId}", sessionId);

            return Task.FromResult(session);
        }

        /// <inheritdoc/>
        public async Task<PartUploadResult> UploadPartAsync(string sessionId, int partNumber, Stream content)
        {
            if (!_multipartSessions.ContainsKey(sessionId))
            {
                throw new InvalidOperationException($"Upload session {sessionId} not found");
            }

            if (!_multipartParts.TryGetValue(sessionId, out var parts))
            {
                throw new InvalidOperationException($"Parts storage for session {sessionId} not found");
            }

            using var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();

            parts[partNumber] = data;

            _logger.LogDebug("Uploaded part {PartNumber} for in-memory session {SessionId}", partNumber, sessionId);

            return new PartUploadResult
            {
                PartNumber = partNumber,
                ETag = ComputeHash(data),
                SizeBytes = data.Length
            };
        }

        /// <inheritdoc/>
        public Task<MediaStorageResult> CompleteMultipartUploadAsync(string sessionId, List<PartUploadResult> parts)
        {
            if (!_multipartSessions.TryRemove(sessionId, out var session))
            {
                throw new InvalidOperationException($"Upload session {sessionId} not found");
            }

            if (!_multipartParts.TryRemove(sessionId, out var partData))
            {
                throw new InvalidOperationException($"Parts data for session {sessionId} not found");
            }

            // Combine all parts
            var sortedParts = parts.OrderBy(p => p.PartNumber).ToList();
            using var finalStream = new MemoryStream();
            
            foreach (var part in sortedParts)
            {
                if (partData.TryGetValue(part.PartNumber, out var data))
                {
                    finalStream.Write(data, 0, data.Length);
                }
            }

            var finalData = finalStream.ToArray();
            var contentHash = ComputeHash(finalData);

            // Store the complete video
            var mediaInfo = new MediaInfo
            {
                StorageKey = session.StorageKey,
                ContentType = "video/mp4", // Default for now
                SizeBytes = finalData.Length,
                MediaType = MediaType.Video,
                CreatedAt = DateTime.UtcNow
            };

            _storage[session.StorageKey] = new StoredMedia
            {
                Data = finalData,
                Info = mediaInfo
            };

            _logger.LogInformation("Completed in-memory multipart upload for key {StorageKey}", session.StorageKey);

            var url = GenerateUrlAsync(session.StorageKey).Result;

            return Task.FromResult(new MediaStorageResult
            {
                StorageKey = session.StorageKey,
                Url = url,
                SizeBytes = finalData.Length,
                ContentHash = contentHash,
                CreatedAt = DateTime.UtcNow
            });
        }

        /// <inheritdoc/>
        public Task AbortMultipartUploadAsync(string sessionId)
        {
            _multipartSessions.TryRemove(sessionId, out _);
            _multipartParts.TryRemove(sessionId, out _);
            
            _logger.LogInformation("Aborted in-memory multipart upload session {SessionId}", sessionId);
            
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<RangedStream?> GetVideoStreamAsync(string storageKey, long? rangeStart = null, long? rangeEnd = null)
        {
            if (!_storage.TryGetValue(storageKey, out var stored))
            {
                _logger.LogWarning("Video with key {StorageKey} not found in memory", storageKey);
                return Task.FromResult<RangedStream?>(null);
            }

            var totalSize = stored.Data.Length;
            var start = rangeStart ?? 0;
            var end = rangeEnd ?? totalSize - 1;

            // Ensure range is valid
            start = Math.Max(0, Math.Min(start, totalSize - 1));
            end = Math.Max(start, Math.Min(end, totalSize - 1));

            var length = end - start + 1;
            var rangedData = new byte[length];
            Array.Copy(stored.Data, start, rangedData, 0, length);

            var rangedStream = new RangedStream
            {
                Stream = new MemoryStream(rangedData),
                RangeStart = start,
                RangeEnd = end,
                TotalSize = totalSize,
                ContentType = stored.Info.ContentType
            };

            return Task.FromResult<RangedStream?>(rangedStream);
        }

        /// <inheritdoc/>
        public Task<PresignedUploadUrl> GeneratePresignedUploadUrlAsync(VideoMediaMetadata metadata, TimeSpan expiration)
        {
            // For in-memory storage, we'll generate a temporary upload token
            var uploadToken = Guid.NewGuid().ToString();
            var storageKey = GenerateStorageKey(
                uploadToken, 
                MediaType.Video, 
                GetExtensionFromContentType(metadata.ContentType));

            // In a real implementation, you might store this token temporarily
            // For now, we'll just return a URL that would be handled by a separate endpoint

            var presignedUrl = new PresignedUploadUrl
            {
                Url = $"{_baseUrl}/v1/media/upload/{uploadToken}",
                HttpMethod = "PUT",
                RequiredHeaders = new Dictionary<string, string>
                {
                    ["Content-Type"] = metadata.ContentType
                },
                ExpiresAt = DateTime.UtcNow.Add(expiration),
                StorageKey = storageKey,
                MaxFileSizeBytes = 100 * 1024 * 1024 // 100MB max for in-memory
            };

            _logger.LogInformation("Generated presigned upload URL for in-memory storage with token {Token}", uploadToken);

            return Task.FromResult(presignedUrl);
        }
    }
}