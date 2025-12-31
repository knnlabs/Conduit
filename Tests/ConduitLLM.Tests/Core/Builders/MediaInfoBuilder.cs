using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Builders
{
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
}