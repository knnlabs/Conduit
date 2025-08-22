using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Core functionality for audio processing service.
    /// </summary>
    public partial class AudioProcessingService : IAudioProcessingService
    {
        private readonly ILogger<AudioProcessingService> _logger;
        private readonly ICacheService _cacheService;

        // Supported formats matrix
        private readonly Dictionary<string, HashSet<string>> _conversionMatrix = new()
        {
            ["mp3"] = new HashSet<string> { "wav", "flac", "ogg", "webm", "m4a" },
            ["wav"] = new HashSet<string> { "mp3", "flac", "ogg", "webm", "m4a" },
            ["flac"] = new HashSet<string> { "mp3", "wav", "ogg", "webm", "m4a" },
            ["ogg"] = new HashSet<string> { "mp3", "wav", "flac", "webm", "m4a" },
            ["webm"] = new HashSet<string> { "mp3", "wav", "flac", "ogg", "m4a" },
            ["m4a"] = new HashSet<string> { "mp3", "wav", "flac", "ogg", "webm" }
        };

        private readonly List<string> _supportedFormats = new()
        {
            "mp3", "wav", "flac", "ogg", "webm", "m4a", "opus", "aac"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioProcessingService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="cacheService">The cache service for audio caching.</param>
        public AudioProcessingService(
            ILogger<AudioProcessingService> logger,
            ICacheService cacheService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        /// <inheritdoc />
        public bool IsConversionSupported(string sourceFormat, string targetFormat)
        {
            sourceFormat = sourceFormat?.ToLowerInvariant() ?? string.Empty;
            targetFormat = targetFormat?.ToLowerInvariant() ?? string.Empty;

            return sourceFormat == targetFormat ||
                   (_conversionMatrix.ContainsKey(sourceFormat) &&
                    _conversionMatrix[sourceFormat].Contains(targetFormat));
        }

        /// <inheritdoc />
        public List<string> GetSupportedFormats()
        {
            return new List<string>(_supportedFormats);
        }

        /// <inheritdoc />
        public double EstimateProcessingTime(long audioSizeBytes, string operation)
        {
            // Simple estimation based on file size and operation type
            var baseFactor = audioSizeBytes / 1024.0 / 1024.0; // MB

            return operation?.ToLowerInvariant() switch
            {
                "convert" => baseFactor * 100, // 100ms per MB
                "compress" => baseFactor * 150, // 150ms per MB
                "noise-reduce" => baseFactor * 200, // 200ms per MB
                "normalize" => baseFactor * 50, // 50ms per MB
                "split" => baseFactor * 20, // 20ms per MB
                "merge" => baseFactor * 30, // 30ms per MB
                _ => baseFactor * 100 // Default
            };
        }
    }
}