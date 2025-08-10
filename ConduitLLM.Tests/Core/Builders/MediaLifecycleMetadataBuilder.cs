using System;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Tests.Core.Builders
{
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
}