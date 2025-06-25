using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implementation of audio stream caching.
    /// </summary>
    public class AudioStreamCache : IAudioStreamCache
    {
        private readonly ILogger<AudioStreamCache> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly ICacheService _distributedCache;
        private readonly AudioCacheOptions _options;
        private readonly AudioCacheMetrics _metrics = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioStreamCache"/> class.
        /// </summary>
        public AudioStreamCache(
            ILogger<AudioStreamCache> logger,
            IMemoryCache memoryCache,
            ICacheService distributedCache,
            IOptions<AudioCacheOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public Task CacheTranscriptionAsync(
            AudioTranscriptionRequest request,
            AudioTranscriptionResponse response,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = GenerateTranscriptionCacheKey(request);
            var effectiveTtl = ttl ?? _options.DefaultTranscriptionTtl;

            // Cache in memory for fast access
            _memoryCache.Set(cacheKey, response, effectiveTtl);

            // Cache in distributed cache for sharing across instances
            _distributedCache.Set(
                cacheKey,
                response,
                effectiveTtl);

            _metrics.IncrementCachedItems();
            _logger.LogDebug("Cached transcription with key {Key} for {Duration}", cacheKey, effectiveTtl);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<AudioTranscriptionResponse?> GetCachedTranscriptionAsync(
            AudioTranscriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = GenerateTranscriptionCacheKey(request);

            // Try memory cache first
            if (_memoryCache.TryGetValue<AudioTranscriptionResponse>(cacheKey, out var cached))
            {
                _metrics.IncrementTranscriptionHit();
                _logger.LogDebug("Transcription cache hit (memory) for key {Key}", cacheKey);
                return Task.FromResult<AudioTranscriptionResponse?>(cached);
            }

            // Try distributed cache
            var distributedResult = _distributedCache.Get<AudioTranscriptionResponse>(cacheKey);

            if (distributedResult != null)
            {
                // Populate memory cache for next time
                _memoryCache.Set(cacheKey, distributedResult, _options.MemoryCacheTtl);
                _metrics.IncrementTranscriptionHit();
                _logger.LogDebug("Transcription cache hit (distributed) for key {Key}", cacheKey);
                return Task.FromResult<AudioTranscriptionResponse?>(distributedResult);
            }

            _metrics.IncrementTranscriptionMiss();
            _logger.LogDebug("Transcription cache miss for key {Key}", cacheKey);
            return Task.FromResult<AudioTranscriptionResponse?>(null);
        }

        /// <inheritdoc />
        public Task CacheTtsAudioAsync(
            TextToSpeechRequest request,
            TextToSpeechResponse response,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = GenerateTtsCacheKey(request);
            var effectiveTtl = ttl ?? _options.DefaultTtsTtl;

            // For large audio, only cache metadata in memory
            var cacheEntry = new TtsCacheEntry
            {
                Response = response,
                CachedAt = DateTime.UtcNow,
                SizeBytes = response.AudioData.Length
            };

            if (response.AudioData.Length <= _options.MaxMemoryCacheSizeBytes)
            {
                _memoryCache.Set(cacheKey, cacheEntry, effectiveTtl);
            }

            // Always cache in distributed cache
            _distributedCache.Set(
                cacheKey,
                cacheEntry,
                effectiveTtl);

            _metrics.IncrementCachedItems();
            _metrics.AddCachedBytes(response.AudioData.Length);
            _logger.LogDebug("Cached TTS audio with key {Key} ({Size} bytes) for {Duration}",
                cacheKey, response.AudioData.Length, effectiveTtl);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<TextToSpeechResponse?> GetCachedTtsAudioAsync(
            TextToSpeechRequest request,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = GenerateTtsCacheKey(request);

            // Try memory cache first
            if (_memoryCache.TryGetValue<TtsCacheEntry>(cacheKey, out var cached))
            {
                _metrics.IncrementTtsHit();
                _logger.LogDebug("TTS cache hit (memory) for key {Key}", cacheKey);
                return Task.FromResult<TextToSpeechResponse?>(cached?.Response);
            }

            // Try distributed cache
            var distributedResult = _distributedCache.Get<TtsCacheEntry>(cacheKey);

            if (distributedResult != null)
            {
                // Populate memory cache if small enough
                if (distributedResult.SizeBytes <= _options.MaxMemoryCacheSizeBytes)
                {
                    _memoryCache.Set(cacheKey, distributedResult, _options.MemoryCacheTtl);
                }

                _metrics.IncrementTtsHit();
                _logger.LogDebug("TTS cache hit (distributed) for key {Key}", cacheKey);
                return Task.FromResult<TextToSpeechResponse?>(distributedResult.Response);
            }

            _metrics.IncrementTtsMiss();
            _logger.LogDebug("TTS cache miss for key {Key}", cacheKey);
            return Task.FromResult<TextToSpeechResponse?>(null);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AudioChunk> StreamCachedAudioAsync(
            string cacheKey,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Get the cached audio
            var cached = _distributedCache.Get<TtsCacheEntry>(cacheKey);
            if (cached?.Response.AudioData == null)
            {
                yield break;
            }

            var audioData = cached.Response.AudioData;
            var chunkSize = _options.StreamingChunkSizeBytes;
            var totalChunks = (int)Math.Ceiling((double)audioData.Length / chunkSize);

            for (int i = 0; i < totalChunks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var offset = i * chunkSize;
                var length = Math.Min(chunkSize, audioData.Length - offset);
                var chunkData = new byte[length];
                Array.Copy(audioData, offset, chunkData, 0, length);

                yield return new AudioChunk
                {
                    Data = chunkData,
                    ChunkIndex = i,
                    IsFinal = i == totalChunks - 1,
                    Timestamp = new ChunkTimestamp
                    {
                        Start = (double)offset / audioData.Length * (cached.Response.Duration ?? 0),
                        End = (double)(offset + length) / audioData.Length * (cached.Response.Duration ?? 0)
                    }
                };

                // Small delay to simulate streaming
                await Task.Delay(10, cancellationToken);
            }
        }

        /// <inheritdoc />
        public Task<AudioCacheStatistics> GetStatisticsAsync()
        {
            var stats = new AudioCacheStatistics
            {
                TotalEntries = _metrics.TotalEntries,
                TotalSizeBytes = _metrics.TotalSizeBytes,
                TranscriptionHits = _metrics.TranscriptionHits,
                TranscriptionMisses = _metrics.TranscriptionMisses,
                TtsHits = _metrics.TtsHits,
                TtsMisses = _metrics.TtsMisses,
                AverageEntrySizeBytes = _metrics.TotalEntries > 0
                    ? _metrics.TotalSizeBytes / _metrics.TotalEntries
                    : 0,
                OldestEntryAge = _metrics.OldestEntryTime.HasValue
                    ? DateTime.UtcNow - _metrics.OldestEntryTime.Value
                    : TimeSpan.Zero
            };

            return Task.FromResult(stats);
        }

        /// <inheritdoc />
        public async Task<int> ClearExpiredAsync()
        {
            // Memory cache handles expiration automatically
            // For distributed cache, we'd need to track keys separately

            _logger.LogInformation("Clearing expired cache entries");

            // Reset metrics for expired entries
            var cleared = await Task.FromResult(0);

            return cleared;
        }

        /// <inheritdoc />
        public async Task PreloadContentAsync(
            PreloadContent content,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Preloading {TtsCount} TTS items and {TranscriptionCount} transcriptions",
                content.CommonPhrases.Count, content.CommonAudioFiles.Count);

            // Note: Actual implementation would need access to the audio services
            // This is a placeholder showing the caching logic

            foreach (var phrase in content.CommonPhrases)
            {
                var request = new TextToSpeechRequest
                {
                    Input = phrase.Text,
                    Voice = phrase.Voice,
                    Language = phrase.Language
                };

                // Check if already cached
                var existing = await GetCachedTtsAudioAsync(request, cancellationToken);
                if (existing != null)
                {
                    continue;
                }

                _logger.LogDebug("Preloading TTS for: {Text}", phrase.Text);
                // In real implementation, would call TTS service and cache result
            }

            await Task.CompletedTask;
        }

        private string GenerateTranscriptionCacheKey(AudioTranscriptionRequest request)
        {
            using var sha256 = SHA256.Create();
            var dataHash = Convert.ToBase64String(sha256.ComputeHash(request.AudioData ?? Array.Empty<byte>()));

            var keyParts = new[]
            {
                "transcription",
                request.Model ?? "default",
                request.Language ?? "auto",
                request.ResponseFormat?.ToString() ?? "json",
                dataHash
            };

            return string.Join(":", keyParts);
        }

        private string GenerateTtsCacheKey(TextToSpeechRequest request)
        {
            using var sha256 = SHA256.Create();
            var textHash = Convert.ToBase64String(
                sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Input)));

            var keyParts = new[]
            {
                "tts",
                request.Model ?? "default",
                request.Voice,
                request.Language ?? "auto",
                request.Speed?.ToString() ?? "1.0",
                request.ResponseFormat?.ToString() ?? "mp3",
                textHash
            };

            return string.Join(":", keyParts);
        }
    }

    /// <summary>
    /// Cache entry for TTS responses.
    /// </summary>
    internal class TtsCacheEntry
    {
        public TextToSpeechResponse Response { get; set; } = new();
        public DateTime CachedAt { get; set; }
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Internal metrics tracking.
    /// </summary>
    internal class AudioCacheMetrics
    {
        private long _totalEntries;
        private long _totalSizeBytes;
        private long _transcriptionHits;
        private long _transcriptionMisses;
        private long _ttsHits;
        private long _ttsMisses;

        public long TotalEntries => _totalEntries;
        public long TotalSizeBytes => _totalSizeBytes;
        public long TranscriptionHits => _transcriptionHits;
        public long TranscriptionMisses => _transcriptionMisses;
        public long TtsHits => _ttsHits;
        public long TtsMisses => _ttsMisses;
        public DateTime? OldestEntryTime { get; set; }

        public void IncrementCachedItems() => Interlocked.Increment(ref _totalEntries);
        public void AddCachedBytes(long bytes) => Interlocked.Add(ref _totalSizeBytes, bytes);
        public void IncrementTranscriptionHit() => Interlocked.Increment(ref _transcriptionHits);
        public void IncrementTranscriptionMiss() => Interlocked.Increment(ref _transcriptionMisses);
        public void IncrementTtsHit() => Interlocked.Increment(ref _ttsHits);
        public void IncrementTtsMiss() => Interlocked.Increment(ref _ttsMisses);
    }

    /// <summary>
    /// Options for audio caching.
    /// </summary>
    public class AudioCacheOptions
    {
        /// <summary>
        /// Gets or sets the default TTL for transcriptions.
        /// </summary>
        public TimeSpan DefaultTranscriptionTtl { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets the default TTL for TTS audio.
        /// </summary>
        public TimeSpan DefaultTtsTtl { get; set; } = TimeSpan.FromHours(48);

        /// <summary>
        /// Gets or sets the memory cache TTL.
        /// </summary>
        public TimeSpan MemoryCacheTtl { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets the maximum size for memory caching.
        /// </summary>
        public long MaxMemoryCacheSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Gets or sets the streaming chunk size.
        /// </summary>
        public int StreamingChunkSizeBytes { get; set; } = 64 * 1024; // 64KB
    }
}
