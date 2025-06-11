using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Audio service with performance optimizations including connection pooling, caching, and CDN.
    /// </summary>
    public class PerformanceOptimizedAudioService
    {
        private readonly ILogger<PerformanceOptimizedAudioService> _logger;
        private readonly IAudioRouter _audioRouter;
        private readonly IAudioConnectionPool _connectionPool;
        private readonly IAudioStreamCache _streamCache;
        private readonly IAudioCdnService _cdnService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceOptimizedAudioService"/> class.
        /// </summary>
        public PerformanceOptimizedAudioService(
            ILogger<PerformanceOptimizedAudioService> logger,
            IAudioRouter audioRouter,
            IAudioConnectionPool connectionPool,
            IAudioStreamCache streamCache,
            IAudioCdnService cdnService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audioRouter = audioRouter ?? throw new ArgumentNullException(nameof(audioRouter));
            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
            _streamCache = streamCache ?? throw new ArgumentNullException(nameof(streamCache));
            _cdnService = cdnService ?? throw new ArgumentNullException(nameof(cdnService));
        }

        /// <summary>
        /// Transcribes audio with caching and connection pooling.
        /// </summary>
        public async Task<AudioTranscriptionResponse> TranscribeAudioAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            // Check cache first
            var cached = await _streamCache.GetCachedTranscriptionAsync(request, cancellationToken);
            if (cached != null)
            {
                _logger.LogInformation("Returning cached transcription (saved {Ms}ms)", 
                    (DateTime.UtcNow - startTime).TotalMilliseconds);
                return cached;
            }

            // Get client through router
            var client = await _audioRouter.GetTranscriptionClientAsync(request, virtualKey, cancellationToken);
            if (client == null)
            {
                throw new InvalidOperationException("No transcription provider available");
            }

            // Get pooled connection
            var connection = await _connectionPool.GetConnectionAsync(client.GetType().Name, cancellationToken);
            
            try
            {
                // Perform transcription using pooled connection
                var response = await PerformTranscriptionWithConnectionAsync(
                    client, request, connection, cancellationToken);

                // Cache the result
                await _streamCache.CacheTranscriptionAsync(request, response, cancellationToken: cancellationToken);

                _logger.LogInformation("Transcription completed in {Ms}ms (provider: {Provider})",
                    (DateTime.UtcNow - startTime).TotalMilliseconds,
                    client.GetType().Name);

                return response;
            }
            finally
            {
                // Return connection to pool
                await _connectionPool.ReturnConnectionAsync(connection);
            }
        }

        /// <summary>
        /// Generates speech with caching and CDN delivery.
        /// </summary>
        public async Task<PerformanceOptimizedTtsResponse> GenerateSpeechAsync(
            TextToSpeechRequest request,
            string virtualKey,
            bool useCdn = true,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            // Check cache first
            var cached = await _streamCache.GetCachedTtsAudioAsync(request, cancellationToken);
            if (cached != null)
            {
                _logger.LogInformation("Returning cached TTS audio (saved {Ms}ms)",
                    (DateTime.UtcNow - startTime).TotalMilliseconds);

                // Get CDN URL if requested
                string? cdnUrl = null;
                if (useCdn && cached.AudioData.Length > 1024 * 100) // > 100KB
                {
                    var cdnResult = await _cdnService.UploadAudioAsync(
                        cached.AudioData,
                        $"audio/{cached.Format ?? "mp3"}",
                        new CdnMetadata
                        {
                            AudioFormat = cached.Format,
                            DurationSeconds = cached.Duration,
                            Language = request.Language
                        },
                        cancellationToken);
                    
                    cdnUrl = cdnResult.Url;
                }

                return new PerformanceOptimizedTtsResponse
                {
                    AudioData = cached.AudioData,
                    Format = cached.Format,
                    Duration = cached.Duration,
                    CdnUrl = cdnUrl,
                    ServedFromCache = true
                };
            }

            // Get client through router
            var client = await _audioRouter.GetTextToSpeechClientAsync(request, virtualKey, cancellationToken);
            if (client == null)
            {
                throw new InvalidOperationException("No TTS provider available");
            }

            // Get pooled connection
            var connection = await _connectionPool.GetConnectionAsync(client.GetType().Name, cancellationToken);

            try
            {
                // Generate speech using pooled connection
                var response = await PerformTtsWithConnectionAsync(
                    client, request, connection, cancellationToken);

                // Cache the result
                await _streamCache.CacheTtsAudioAsync(request, response, cancellationToken: cancellationToken);

                // Upload to CDN if requested and audio is large enough
                string? cdnUrl = null;
                if (useCdn && response.AudioData.Length > 1024 * 100) // > 100KB
                {
                    var cdnResult = await _cdnService.UploadAudioAsync(
                        response.AudioData,
                        $"audio/{response.Format ?? "mp3"}",
                        new CdnMetadata
                        {
                            AudioFormat = response.Format,
                            DurationSeconds = response.Duration,
                            Language = request.Language,
                            BitRate = response.SampleRate
                        },
                        cancellationToken);

                    cdnUrl = cdnResult.Url;
                    _logger.LogInformation("Uploaded TTS audio to CDN: {Url}", cdnUrl);
                }

                _logger.LogInformation("TTS generation completed in {Ms}ms (provider: {Provider})",
                    (DateTime.UtcNow - startTime).TotalMilliseconds,
                    client.GetType().Name);

                return new PerformanceOptimizedTtsResponse
                {
                    AudioData = response.AudioData,
                    Format = response.Format,
                    Duration = response.Duration,
                    CdnUrl = cdnUrl,
                    ServedFromCache = false,
                    Provider = client.GetType().Name
                };
            }
            finally
            {
                // Return connection to pool
                await _connectionPool.ReturnConnectionAsync(connection);
            }
        }

        /// <summary>
        /// Warms up the service by pre-creating connections and caching common content.
        /// </summary>
        public async Task WarmupAsync(
            WarmupConfiguration config,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting performance optimization warmup");

            // Warm up connection pools
            var warmupTasks = new List<Task>();

            foreach (var provider in config.Providers)
            {
                warmupTasks.Add(_connectionPool.WarmupAsync(
                    provider, 
                    config.ConnectionsPerProvider, 
                    cancellationToken));
            }

            // Preload common content
            if (config.PreloadContent != null)
            {
                warmupTasks.Add(_streamCache.PreloadContentAsync(
                    config.PreloadContent, 
                    cancellationToken));
            }

            await Task.WhenAll(warmupTasks);

            _logger.LogInformation("Warmup completed successfully");
        }

        /// <summary>
        /// Gets performance statistics.
        /// </summary>
        public async Task<PerformanceStatistics> GetStatisticsAsync()
        {
            var connectionStats = await _connectionPool.GetStatisticsAsync();
            var cacheStats = await _streamCache.GetStatisticsAsync();
            var cdnStats = await _cdnService.GetUsageStatisticsAsync();

            return new PerformanceStatistics
            {
                ConnectionPool = connectionStats,
                Cache = cacheStats,
                Cdn = cdnStats
            };
        }

        private async Task<AudioTranscriptionResponse> PerformTranscriptionWithConnectionAsync(
            IAudioTranscriptionClient client,
            AudioTranscriptionRequest request,
            IAudioProviderConnection connection,
            CancellationToken cancellationToken)
        {
            // In a real implementation, the client would use the pooled connection
            // For now, just perform the transcription
            return await client.TranscribeAudioAsync(request, cancellationToken: cancellationToken);
        }

        private async Task<TextToSpeechResponse> PerformTtsWithConnectionAsync(
            ITextToSpeechClient client,
            TextToSpeechRequest request,
            IAudioProviderConnection connection,
            CancellationToken cancellationToken)
        {
            // In a real implementation, the client would use the pooled connection
            // For now, just perform the TTS
            return await client.CreateSpeechAsync(request, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Response from performance-optimized TTS.
    /// </summary>
    public class PerformanceOptimizedTtsResponse
    {
        /// <summary>
        /// Gets or sets the audio data.
        /// </summary>
        public byte[] AudioData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the audio format.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Gets or sets the duration.
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        /// Gets or sets the CDN URL if available.
        /// </summary>
        public string? CdnUrl { get; set; }

        /// <summary>
        /// Gets or sets whether this was served from cache.
        /// </summary>
        public bool ServedFromCache { get; set; }

        /// <summary>
        /// Gets or sets the provider used.
        /// </summary>
        public string? Provider { get; set; }
    }

    /// <summary>
    /// Configuration for service warmup.
    /// </summary>
    public class WarmupConfiguration
    {
        /// <summary>
        /// Gets or sets the providers to warm up.
        /// </summary>
        public List<string> Providers { get; set; } = new();

        /// <summary>
        /// Gets or sets connections per provider.
        /// </summary>
        public int ConnectionsPerProvider { get; set; } = 5;

        /// <summary>
        /// Gets or sets content to preload.
        /// </summary>
        public PreloadContent? PreloadContent { get; set; }
    }

    /// <summary>
    /// Performance statistics.
    /// </summary>
    public class PerformanceStatistics
    {
        /// <summary>
        /// Gets or sets connection pool statistics.
        /// </summary>
        public ConnectionPoolStatistics? ConnectionPool { get; set; }

        /// <summary>
        /// Gets or sets cache statistics.
        /// </summary>
        public AudioCacheStatistics? Cache { get; set; }

        /// <summary>
        /// Gets or sets CDN statistics.
        /// </summary>
        public CdnUsageStatistics? Cdn { get; set; }
    }
}