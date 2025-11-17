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
}