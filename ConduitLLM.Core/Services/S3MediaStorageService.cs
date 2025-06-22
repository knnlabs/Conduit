using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides media storage using S3-compatible services (AWS S3, Cloudflare R2, MinIO, etc.).
    /// </summary>
    public class S3MediaStorageService : IMediaStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly S3StorageOptions _options;
        private readonly ILogger<S3MediaStorageService> _logger;
        private readonly string _bucketName;

        public S3MediaStorageService(
            IOptions<S3StorageOptions> options,
            ILogger<S3MediaStorageService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _bucketName = _options.BucketName;

            var config = new AmazonS3Config
            {
                ForcePathStyle = _options.ForcePathStyle,
                UseHttp = false
            };

            if (!string.IsNullOrEmpty(_options.ServiceUrl))
            {
                config.ServiceURL = _options.ServiceUrl;
            }
            else if (!string.IsNullOrEmpty(_options.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
            }

            _s3Client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);

            // Initialize bucket if needed
            Task.Run(async () => await EnsureBucketExistsAsync());
        }

        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreAsync(Stream content, MediaMetadata metadata)
        {
            try
            {
                // Generate storage key based on content hash
                var contentHash = await ComputeHashAsync(content);
                var extension = GetExtensionFromContentType(metadata.ContentType);
                var storageKey = GenerateStorageKey(contentHash, metadata.MediaType, extension);

                // Reset stream position after hashing
                content.Position = 0;

                // Upload to S3
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey,
                    InputStream = content,
                    ContentType = metadata.ContentType,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
                };

                // Add metadata
                putRequest.Metadata.Add("content-type", metadata.ContentType);
                putRequest.Metadata.Add("media-type", metadata.MediaType.ToString());
                putRequest.Metadata.Add("original-filename", metadata.FileName ?? "");
                putRequest.Metadata.Add("created-by", metadata.CreatedBy ?? "");

                // Add custom metadata
                foreach (var (key, value) in metadata.CustomMetadata ?? new Dictionary<string, string>())
                {
                    putRequest.Metadata.Add($"custom-{key}", value);
                }

                if (metadata.ExpiresAt.HasValue)
                {
                    putRequest.Metadata.Add("expires-at", metadata.ExpiresAt.Value.ToString("O"));
                }

                var response = await _s3Client.PutObjectAsync(putRequest);

                _logger.LogInformation("Stored media with key {StorageKey} to S3", storageKey);

                // Generate public URL
                var url = await GenerateUrlAsync(storageKey, _options.DefaultUrlExpiration);

                return new MediaStorageResult
                {
                    StorageKey = storageKey,
                    Url = url,
                    SizeBytes = content.Length,
                    ContentHash = contentHash,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store media to S3");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Stream?> GetStreamAsync(string storageKey)
        {
            try
            {
                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                var response = await _s3Client.GetObjectAsync(getRequest);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Media with key {StorageKey} not found", storageKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve media {StorageKey} from S3", storageKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<MediaInfo?> GetInfoAsync(string storageKey)
        {
            try
            {
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                var response = await _s3Client.GetObjectMetadataAsync(metadataRequest);

                var mediaType = MediaType.Other;
                if (response.Metadata.Keys.Contains("media-type"))
                {
                    Enum.TryParse<MediaType>(response.Metadata["media-type"], out mediaType);
                }

                var customMetadata = new Dictionary<string, string>();
                foreach (var key in response.Metadata.Keys.Where(k => k.StartsWith("custom-")))
                {
                    customMetadata[key.Substring(7)] = response.Metadata[key];
                }

                DateTime? expiresAt = null;
                if (response.Metadata.Keys.Contains("expires-at"))
                {
                    if (DateTime.TryParse(response.Metadata["expires-at"], out var expires))
                    {
                        expiresAt = expires;
                    }
                }

                return new MediaInfo
                {
                    StorageKey = storageKey,
                    ContentType = response.Headers.ContentType,
                    SizeBytes = response.ContentLength,
                    FileName = response.Metadata.Keys.Contains("original-filename") 
                        ? response.Metadata["original-filename"] 
                        : "",
                    MediaType = mediaType,
                    CreatedAt = response.LastModified ?? DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    CustomMetadata = customMetadata
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get media info for {StorageKey}", storageKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string storageKey)
        {
            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                await _s3Client.DeleteObjectAsync(deleteRequest);
                _logger.LogInformation("Deleted media with key {StorageKey}", storageKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete media {StorageKey}", storageKey);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GenerateUrlAsync(string storageKey, TimeSpan? expiration = null)
        {
            try
            {
                // If we have a public base URL configured, use it for public access
                if (!string.IsNullOrEmpty(_options.PublicBaseUrl))
                {
                    return $"{_options.PublicBaseUrl.TrimEnd('/')}/{storageKey}";
                }

                // Otherwise, generate a presigned URL
                var urlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey,
                    Verb = HttpVerb.GET,
                    Expires = DateTime.UtcNow.Add(expiration ?? _options.DefaultUrlExpiration),
                    Protocol = Protocol.HTTPS
                };

                return await _s3Client.GetPreSignedURLAsync(urlRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate URL for {StorageKey}", storageKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(string storageKey)
        {
            try
            {
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                await _s3Client.GetObjectMetadataAsync(metadataRequest);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        private async Task EnsureBucketExistsAsync()
        {
            if (!_options.AutoCreateBucket)
                return;

            try
            {
                await _s3Client.HeadBucketAsync(new HeadBucketRequest { BucketName = _bucketName });
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                try
                {
                    _logger.LogInformation("Creating S3 bucket {BucketName}", _bucketName);
                    await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName });
                }
                catch (Exception createEx)
                {
                    _logger.LogError(createEx, "Failed to create bucket {BucketName}", _bucketName);
                }
            }
        }

        private static async Task<string> ComputeHashAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToBase64String(hash).Replace("/", "-").Replace("+", "_").TrimEnd('=');
        }

        private static string GenerateStorageKey(string contentHash, MediaType mediaType, string extension)
        {
            var typeFolder = mediaType.ToString().ToLower();
            var dateFolder = DateTime.UtcNow.ToString("yyyy/MM/dd");
            return $"{typeFolder}/{dateFolder}/{contentHash}{extension}";
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
                "audio/webm" => ".weba",
                _ => ""
            };
        }
    }
}