using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Builders
{
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
}