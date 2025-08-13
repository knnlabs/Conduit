using System;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Core.Builders
{
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
        private int _accessCount;

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
}