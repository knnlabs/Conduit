using ConduitLLM.Core.Models;

namespace ConduitLLM.Tests.Core.Builders
{
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
}