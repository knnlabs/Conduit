using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Builders
{
    /// <summary>
    /// Builder for creating MediaMetadata test objects.
    /// </summary>
    public class MediaMetadataBuilder
    {
        private string _contentType = "image/jpeg";
        private string _fileName = "test.jpg";
        private MediaType _mediaType = MediaType.Image;
        private string _createdBy = "test-user";
        private DateTime? _expiresAt = null;
        private Dictionary<string, string> _customMetadata = new();

        public MediaMetadataBuilder WithContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        public MediaMetadataBuilder WithFileName(string fileName)
        {
            _fileName = fileName;
            return this;
        }

        public MediaMetadataBuilder WithMediaType(MediaType mediaType)
        {
            _mediaType = mediaType;
            return this;
        }

        public MediaMetadataBuilder WithCreatedBy(string createdBy)
        {
            _createdBy = createdBy;
            return this;
        }

        public MediaMetadataBuilder WithExpiresAt(DateTime? expiresAt)
        {
            _expiresAt = expiresAt;
            return this;
        }

        public MediaMetadataBuilder WithCustomMetadata(string key, string value)
        {
            _customMetadata[key] = value;
            return this;
        }

        public MediaMetadataBuilder WithCustomMetadata(Dictionary<string, string> metadata)
        {
            _customMetadata = metadata ?? new Dictionary<string, string>();
            return this;
        }

        public MediaMetadata Build()
        {
            return new MediaMetadata
            {
                ContentType = _contentType,
                FileName = _fileName,
                MediaType = _mediaType,
                CreatedBy = _createdBy,
                ExpiresAt = _expiresAt,
                CustomMetadata = _customMetadata
            };
        }
    }

    /// <summary>
    /// Builder for creating VideoMediaMetadata test objects.
    /// </summary>
    public class VideoMediaMetadataBuilder
    {
        private string _contentType = "video/mp4";
        private string _fileName = "test.mp4";
        private TimeSpan _duration = TimeSpan.FromSeconds(30);
        private string _resolution = "1920x1080";
        private int _width = 1920;
        private int _height = 1080;
        private double _frameRate = 30.0;
        private string _codec = "h264";
        private long? _bitrate = 5000000;
        private string _generatedByModel = "test-model";
        private string _generationPrompt = "test prompt";
        private long _fileSizeBytes = 1024000;
        private string _createdBy = "test-user";
        private DateTime? _expiresAt = null;
        private Dictionary<string, string> _customMetadata = new();

        public VideoMediaMetadataBuilder WithContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        public VideoMediaMetadataBuilder WithFileName(string fileName)
        {
            _fileName = fileName;
            return this;
        }

        public VideoMediaMetadataBuilder WithDuration(TimeSpan duration)
        {
            _duration = duration;
            return this;
        }

        public VideoMediaMetadataBuilder WithResolution(string resolution)
        {
            _resolution = resolution;
            return this;
        }

        public VideoMediaMetadataBuilder WithDimensions(int width, int height)
        {
            _width = width;
            _height = height;
            _resolution = $"{width}x{height}";
            return this;
        }

        public VideoMediaMetadataBuilder WithFrameRate(double frameRate)
        {
            _frameRate = frameRate;
            return this;
        }

        public VideoMediaMetadataBuilder WithCodec(string codec)
        {
            _codec = codec;
            return this;
        }

        public VideoMediaMetadataBuilder WithBitrate(long? bitrate)
        {
            _bitrate = bitrate;
            return this;
        }

        public VideoMediaMetadataBuilder WithGeneratedByModel(string model)
        {
            _generatedByModel = model;
            return this;
        }

        public VideoMediaMetadataBuilder WithGenerationPrompt(string prompt)
        {
            _generationPrompt = prompt;
            return this;
        }

        public VideoMediaMetadataBuilder WithFileSizeBytes(long fileSizeBytes)
        {
            _fileSizeBytes = fileSizeBytes;
            return this;
        }

        public VideoMediaMetadataBuilder WithCreatedBy(string createdBy)
        {
            _createdBy = createdBy;
            return this;
        }

        public VideoMediaMetadataBuilder WithExpiresAt(DateTime? expiresAt)
        {
            _expiresAt = expiresAt;
            return this;
        }

        public VideoMediaMetadataBuilder WithCustomMetadata(string key, string value)
        {
            _customMetadata[key] = value;
            return this;
        }

        public VideoMediaMetadata Build()
        {
            return new VideoMediaMetadata
            {
                ContentType = _contentType,
                FileName = _fileName,
                Duration = _duration.TotalSeconds,
                Resolution = _resolution,
                Width = _width,
                Height = _height,
                FrameRate = _frameRate,
                Codec = _codec,
                Bitrate = _bitrate,
                GeneratedByModel = _generatedByModel,
                GenerationPrompt = _generationPrompt,
                FileSizeBytes = _fileSizeBytes,
                CreatedBy = _createdBy,
                ExpiresAt = _expiresAt,
                CustomMetadata = _customMetadata
            };
        }
    }

    /// <summary>
    /// Builder for creating MediaStorageResult test objects.
    /// </summary>
    public class MediaStorageResultBuilder
    {
        private string _storageKey = "image/2023/01/01/test-hash.jpg";
        private string _url = "https://storage.example.com/image/test-hash.jpg";
        private long _sizeBytes = 1024;
        private string _contentHash = "test-hash";
        private DateTime _createdAt = DateTime.UtcNow;

        public MediaStorageResultBuilder WithStorageKey(string storageKey)
        {
            _storageKey = storageKey;
            return this;
        }

        public MediaStorageResultBuilder WithUrl(string url)
        {
            _url = url;
            return this;
        }

        public MediaStorageResultBuilder WithSizeBytes(long sizeBytes)
        {
            _sizeBytes = sizeBytes;
            return this;
        }

        public MediaStorageResultBuilder WithContentHash(string contentHash)
        {
            _contentHash = contentHash;
            return this;
        }

        public MediaStorageResultBuilder WithCreatedAt(DateTime createdAt)
        {
            _createdAt = createdAt;
            return this;
        }

        public MediaStorageResult Build()
        {
            return new MediaStorageResult
            {
                StorageKey = _storageKey,
                Url = _url,
                SizeBytes = _sizeBytes,
                ContentHash = _contentHash,
                CreatedAt = _createdAt
            };
        }
    }

    /// <summary>
    /// Builder for creating MediaInfo test objects.
    /// </summary>
    public class MediaInfoBuilder
    {
        private string _storageKey = "image/2023/01/01/test-hash.jpg";
        private string _contentType = "image/jpeg";
        private long _sizeBytes = 1024;
        private string _fileName = "test.jpg";
        private MediaType _mediaType = MediaType.Image;
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime? _expiresAt = null;
        private Dictionary<string, string> _customMetadata = new();

        public MediaInfoBuilder WithStorageKey(string storageKey)
        {
            _storageKey = storageKey;
            return this;
        }

        public MediaInfoBuilder WithContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        public MediaInfoBuilder WithSizeBytes(long sizeBytes)
        {
            _sizeBytes = sizeBytes;
            return this;
        }

        public MediaInfoBuilder WithFileName(string fileName)
        {
            _fileName = fileName;
            return this;
        }

        public MediaInfoBuilder WithMediaType(MediaType mediaType)
        {
            _mediaType = mediaType;
            return this;
        }

        public MediaInfoBuilder WithCreatedAt(DateTime createdAt)
        {
            _createdAt = createdAt;
            return this;
        }

        public MediaInfoBuilder WithExpiresAt(DateTime? expiresAt)
        {
            _expiresAt = expiresAt;
            return this;
        }

        public MediaInfoBuilder WithCustomMetadata(string key, string value)
        {
            _customMetadata[key] = value;
            return this;
        }

        public MediaInfoBuilder WithCustomMetadata(Dictionary<string, string> metadata)
        {
            _customMetadata = metadata ?? new Dictionary<string, string>();
            return this;
        }

        public MediaInfo Build()
        {
            return new MediaInfo
            {
                StorageKey = _storageKey,
                ContentType = _contentType,
                SizeBytes = _sizeBytes,
                FileName = _fileName,
                MediaType = _mediaType,
                CreatedAt = _createdAt,
                ExpiresAt = _expiresAt,
                CustomMetadata = _customMetadata
            };
        }
    }

    /// <summary>
    /// Builder for creating MediaRecord test objects.
    /// </summary>
    public class MediaRecordBuilder
    {
        private Guid _id = Guid.NewGuid();
        private string _storageKey = "image/2023/01/01/test-hash.jpg";
        private int _virtualKeyId = 1;
        private string _mediaType = "image";
        private string _contentType = "image/jpeg";
        private long? _sizeBytes = 1024;
        private string _contentHash = "test-hash";
        private string _provider = "openai";
        private string _model = "dall-e-3";
        private string _prompt = "A beautiful landscape";
        private string _storageUrl = "https://storage.example.com/image.jpg";
        private string _publicUrl = "https://cdn.example.com/image.jpg";
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime? _expiresAt = null;
        private DateTime? _lastAccessedAt = null;
        private int _accessCount = 0;

        public MediaRecordBuilder WithId(Guid id)
        {
            _id = id;
            return this;
        }

        public MediaRecordBuilder WithStorageKey(string storageKey)
        {
            _storageKey = storageKey;
            return this;
        }

        public MediaRecordBuilder WithVirtualKeyId(int virtualKeyId)
        {
            _virtualKeyId = virtualKeyId;
            return this;
        }

        public MediaRecordBuilder WithMediaType(string mediaType)
        {
            _mediaType = mediaType;
            return this;
        }

        public MediaRecordBuilder WithContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        public MediaRecordBuilder WithSizeBytes(long? sizeBytes)
        {
            _sizeBytes = sizeBytes;
            return this;
        }

        public MediaRecordBuilder WithContentHash(string contentHash)
        {
            _contentHash = contentHash;
            return this;
        }

        public MediaRecordBuilder WithProvider(string provider)
        {
            _provider = provider;
            return this;
        }

        public MediaRecordBuilder WithModel(string model)
        {
            _model = model;
            return this;
        }

        public MediaRecordBuilder WithPrompt(string prompt)
        {
            _prompt = prompt;
            return this;
        }

        public MediaRecordBuilder WithStorageUrl(string storageUrl)
        {
            _storageUrl = storageUrl;
            return this;
        }

        public MediaRecordBuilder WithPublicUrl(string publicUrl)
        {
            _publicUrl = publicUrl;
            return this;
        }

        public MediaRecordBuilder WithCreatedAt(DateTime createdAt)
        {
            _createdAt = createdAt;
            return this;
        }

        public MediaRecordBuilder WithExpiresAt(DateTime? expiresAt)
        {
            _expiresAt = expiresAt;
            return this;
        }

        public MediaRecordBuilder WithLastAccessedAt(DateTime? lastAccessedAt)
        {
            _lastAccessedAt = lastAccessedAt;
            return this;
        }

        public MediaRecordBuilder WithAccessCount(int accessCount)
        {
            _accessCount = accessCount;
            return this;
        }

        public MediaRecord Build()
        {
            return new MediaRecord
            {
                Id = _id,
                StorageKey = _storageKey,
                VirtualKeyId = _virtualKeyId,
                MediaType = _mediaType,
                ContentType = _contentType,
                SizeBytes = _sizeBytes,
                ContentHash = _contentHash,
                Provider = _provider,
                Model = _model,
                Prompt = _prompt,
                StorageUrl = _storageUrl,
                PublicUrl = _publicUrl,
                CreatedAt = _createdAt,
                ExpiresAt = _expiresAt,
                LastAccessedAt = _lastAccessedAt,
                AccessCount = _accessCount
            };
        }
    }

    /// <summary>
    /// Builder for creating MediaLifecycleMetadata test objects.
    /// </summary>
    public class MediaLifecycleMetadataBuilder
    {
        private string _contentType = "image/jpeg";
        private long? _sizeBytes = 1024;
        private string _contentHash = "test-hash";
        private string _provider = "openai";
        private string _model = "dall-e-3";
        private string _prompt = "A beautiful landscape";
        private string _storageUrl = "https://storage.example.com/image.jpg";
        private string _publicUrl = "https://cdn.example.com/image.jpg";
        private DateTime? _expiresAt = null;

        public MediaLifecycleMetadataBuilder WithContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithSizeBytes(long? sizeBytes)
        {
            _sizeBytes = sizeBytes;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithContentHash(string contentHash)
        {
            _contentHash = contentHash;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithProvider(string provider)
        {
            _provider = provider;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithModel(string model)
        {
            _model = model;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithPrompt(string prompt)
        {
            _prompt = prompt;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithStorageUrl(string storageUrl)
        {
            _storageUrl = storageUrl;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithPublicUrl(string publicUrl)
        {
            _publicUrl = publicUrl;
            return this;
        }

        public MediaLifecycleMetadataBuilder WithExpiresAt(DateTime? expiresAt)
        {
            _expiresAt = expiresAt;
            return this;
        }

        public MediaLifecycleMetadata Build()
        {
            return new MediaLifecycleMetadata
            {
                ContentType = _contentType,
                SizeBytes = _sizeBytes,
                ContentHash = _contentHash,
                Provider = _provider,
                Model = _model,
                Prompt = _prompt,
                StorageUrl = _storageUrl,
                PublicUrl = _publicUrl,
                ExpiresAt = _expiresAt
            };
        }
    }

    /// <summary>
    /// Factory for creating common test streams.
    /// </summary>
    public static class TestStreamFactory
    {
        /// <summary>
        /// Creates a memory stream with test image data.
        /// </summary>
        public static MemoryStream CreateImageStream(string content = "fake image data")
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a memory stream with test video data.
        /// </summary>
        public static MemoryStream CreateVideoStream(string content = "fake video data")
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a memory stream with test audio data.
        /// </summary>
        public static MemoryStream CreateAudioStream(string content = "fake audio data")
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a memory stream with specified size.
        /// </summary>
        public static MemoryStream CreateStreamWithSize(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            for (int i = 0; i < sizeBytes; i++)
            {
                data[i] = (byte)(i % 256);
            }
            return new MemoryStream(data);
        }

        /// <summary>
        /// Creates a memory stream with base64 encoded data.
        /// </summary>
        public static MemoryStream CreateBase64Stream(string base64Data)
        {
            var bytes = Convert.FromBase64String(base64Data);
            return new MemoryStream(bytes);
        }
    }

    /// <summary>
    /// Factory for creating common test values.
    /// </summary>
    public static class TestValueFactory
    {
        /// <summary>
        /// Creates a test storage key for the given media type.
        /// </summary>
        public static string CreateStorageKey(MediaType mediaType, string hash = "test-hash")
        {
            var typeFolder = mediaType.ToString().ToLower();
            var dateFolder = DateTime.UtcNow.ToString("yyyy/MM/dd");
            var extension = mediaType switch
            {
                MediaType.Image => ".jpg",
                MediaType.Video => ".mp4",
                MediaType.Audio => ".mp3",
                _ => ".bin"
            };
            return $"{typeFolder}/{dateFolder}/{hash}{extension}";
        }

        /// <summary>
        /// Creates a test URL for the given storage key.
        /// </summary>
        public static string CreateUrl(string storageKey, string baseUrl = "https://storage.example.com")
        {
            return $"{baseUrl.TrimEnd('/')}/{storageKey}";
        }

        /// <summary>
        /// Creates a test content hash.
        /// </summary>
        public static string CreateContentHash(string suffix = "")
        {
            return $"sha256-{Guid.NewGuid().ToString("N")[..16]}{suffix}";
        }

        /// <summary>
        /// Creates a test base64 string.
        /// </summary>
        public static string CreateBase64Data(string content = "test data")
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Creates a test file name for the given media type.
        /// </summary>
        public static string CreateFileName(MediaType mediaType, string prefix = "test")
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var extension = mediaType switch
            {
                MediaType.Image => ".jpg",
                MediaType.Video => ".mp4",
                MediaType.Audio => ".mp3",
                _ => ".bin"
            };
            return $"{prefix}_{timestamp}{extension}";
        }
    }
}