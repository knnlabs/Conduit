using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for caching audio streams and transcriptions.
    /// </summary>
    public interface IAudioStreamCache
    {
        /// <summary>
        /// Caches transcription results.
        /// </summary>
        /// <param name="request">The original request.</param>
        /// <param name="response">The transcription response.</param>
        /// <param name="ttl">Time to live for the cache entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CacheTranscriptionAsync(
            AudioTranscriptionRequest request,
            AudioTranscriptionResponse response,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cached transcription if available.
        /// </summary>
        /// <param name="request">The transcription request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cached response or null.</returns>
        Task<AudioTranscriptionResponse?> GetCachedTranscriptionAsync(
            AudioTranscriptionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Caches TTS audio data.
        /// </summary>
        /// <param name="request">The original request.</param>
        /// <param name="response">The TTS response.</param>
        /// <param name="ttl">Time to live for the cache entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task CacheTtsAudioAsync(
            TextToSpeechRequest request,
            TextToSpeechResponse response,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cached TTS audio if available.
        /// </summary>
        /// <param name="request">The TTS request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cached response or null.</returns>
        Task<TextToSpeechResponse?> GetCachedTtsAudioAsync(
            TextToSpeechRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams cached audio chunks for real-time playback.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Stream of audio chunks.</returns>
        IAsyncEnumerable<AudioChunk> StreamCachedAudioAsync(
            string cacheKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        Task<AudioCacheStatistics> GetStatisticsAsync();

        /// <summary>
        /// Clears expired cache entries.
        /// </summary>
        /// <returns>Number of entries cleared.</returns>
        Task<int> ClearExpiredAsync();

        /// <summary>
        /// Preloads frequently used content into cache.
        /// </summary>
        /// <param name="content">Content to preload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PreloadContentAsync(
            PreloadContent content,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Statistics about the audio cache.
    /// </summary>
    public class AudioCacheStatistics
    {
        /// <summary>
        /// Gets or sets the total cache entries.
        /// </summary>
        public long TotalEntries { get; set; }

        /// <summary>
        /// Gets or sets the total cache size in bytes.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the transcription cache hits.
        /// </summary>
        public long TranscriptionHits { get; set; }

        /// <summary>
        /// Gets or sets the transcription cache misses.
        /// </summary>
        public long TranscriptionMisses { get; set; }

        /// <summary>
        /// Gets or sets the TTS cache hits.
        /// </summary>
        public long TtsHits { get; set; }

        /// <summary>
        /// Gets or sets the TTS cache misses.
        /// </summary>
        public long TtsMisses { get; set; }

        /// <summary>
        /// Gets or sets the average entry size.
        /// </summary>
        public long AverageEntrySizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the oldest entry age.
        /// </summary>
        public TimeSpan OldestEntryAge { get; set; }

        /// <summary>
        /// Gets the transcription hit rate.
        /// </summary>
        public double TranscriptionHitRate =>
            TranscriptionHits + TranscriptionMisses == 0 ? 0 :
            (double)TranscriptionHits / (TranscriptionHits + TranscriptionMisses);

        /// <summary>
        /// Gets the TTS hit rate.
        /// </summary>
        public double TtsHitRate =>
            TtsHits + TtsMisses == 0 ? 0 :
            (double)TtsHits / (TtsHits + TtsMisses);
    }

    /// <summary>
    /// Content to preload into cache.
    /// </summary>
    public class PreloadContent
    {
        /// <summary>
        /// Gets or sets common phrases to cache TTS for.
        /// </summary>
        public List<PreloadTtsItem> CommonPhrases { get; set; } = new();

        /// <summary>
        /// Gets or sets common audio files to cache transcriptions for.
        /// </summary>
        public List<PreloadTranscriptionItem> CommonAudioFiles { get; set; } = new();
    }

    /// <summary>
    /// TTS content to preload.
    /// </summary>
    public class PreloadTtsItem
    {
        /// <summary>
        /// Gets or sets the text to synthesize.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the voice to use.
        /// </summary>
        public string Voice { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the cache TTL.
        /// </summary>
        public TimeSpan? Ttl { get; set; }
    }

    /// <summary>
    /// Transcription content to preload.
    /// </summary>
    public class PreloadTranscriptionItem
    {
        /// <summary>
        /// Gets or sets the audio file URL or path.
        /// </summary>
        public string AudioSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expected language.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets the cache TTL.
        /// </summary>
        public TimeSpan? Ttl { get; set; }
    }
}
