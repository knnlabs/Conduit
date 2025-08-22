namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Provides audio processing capabilities including format conversion, compression, and enhancement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IAudioProcessingService interface defines operations for manipulating audio data,
    /// including format conversion, compression, noise reduction, and caching. This service
    /// enables the system to handle various audio formats and optimize audio quality and size.
    /// </para>
    /// <para>
    /// Implementations should be efficient and support common audio processing scenarios
    /// required by speech-to-text and text-to-speech operations.
    /// </para>
    /// </remarks>
    public interface IAudioProcessingService
    {
        /// <summary>
        /// Converts audio from one format to another.
        /// </summary>
        /// <param name="audioData">The input audio data.</param>
        /// <param name="sourceFormat">The source audio format (e.g., "mp3", "wav").</param>
        /// <param name="targetFormat">The target audio format.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The converted audio data.</returns>
        /// <exception cref="System.NotSupportedException">Thrown when the conversion is not supported.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the audio data or formats are invalid.</exception>
        /// <remarks>
        /// <para>
        /// This method handles conversion between common audio formats used in speech processing:
        /// </para>
        /// <list type="bullet">
        /// <item><description>MP3 - Compressed format, widely supported</description></item>
        /// <item><description>WAV - Uncompressed format, high quality</description></item>
        /// <item><description>FLAC - Lossless compression</description></item>
        /// <item><description>WebM - Web-optimized format</description></item>
        /// <item><description>OGG - Open-source compressed format</description></item>
        /// </list>
        /// </remarks>
        Task<byte[]> ConvertFormatAsync(
            byte[] audioData,
            string sourceFormat,
            string targetFormat,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Compresses audio data to reduce file size.
        /// </summary>
        /// <param name="audioData">The input audio data.</param>
        /// <param name="format">The audio format.</param>
        /// <param name="quality">Compression quality (0.0 = lowest, 1.0 = highest).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The compressed audio data.</returns>
        /// <remarks>
        /// <para>
        /// Applies intelligent compression based on the audio content and target use case.
        /// Higher quality values preserve more detail but result in larger files.
        /// </para>
        /// <para>
        /// The compression algorithm adapts based on:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Speech vs. music content</description></item>
        /// <item><description>Target bitrate requirements</description></item>
        /// <item><description>Perceptual quality metrics</description></item>
        /// </list>
        /// </remarks>
        Task<byte[]> CompressAudioAsync(
            byte[] audioData,
            string format,
            double quality = 0.8,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies noise reduction to improve audio quality.
        /// </summary>
        /// <param name="audioData">The input audio data.</param>
        /// <param name="format">The audio format.</param>
        /// <param name="aggressiveness">Noise reduction level (0.0 = minimal, 1.0 = maximum).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The processed audio data with reduced noise.</returns>
        /// <remarks>
        /// <para>
        /// Removes background noise while preserving speech clarity. This is particularly
        /// useful for improving STT accuracy in noisy environments.
        /// </para>
        /// <para>
        /// The noise reduction algorithm:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Identifies and suppresses constant background noise</description></item>
        /// <item><description>Preserves speech frequencies</description></item>
        /// <item><description>Adapts to changing noise conditions</description></item>
        /// </list>
        /// </remarks>
        Task<byte[]> ReduceNoiseAsync(
            byte[] audioData,
            string format,
            double aggressiveness = 0.5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Normalizes audio volume levels.
        /// </summary>
        /// <param name="audioData">The input audio data.</param>
        /// <param name="format">The audio format.</param>
        /// <param name="targetLevel">Target normalization level in dB (default: -3dB).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The normalized audio data.</returns>
        /// <remarks>
        /// <para>
        /// Adjusts audio levels to ensure consistent volume across different recordings.
        /// This improves both STT accuracy and TTS output quality.
        /// </para>
        /// </remarks>
        Task<byte[]> NormalizeAudioAsync(
            byte[] audioData,
            string format,
            double targetLevel = -3.0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Caches processed audio for faster retrieval.
        /// </summary>
        /// <param name="key">The cache key for the audio.</param>
        /// <param name="audioData">The audio data to cache.</param>
        /// <param name="metadata">Optional metadata about the audio.</param>
        /// <param name="expiration">Cache expiration time in seconds (default: 3600).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the caching operation.</returns>
        /// <remarks>
        /// <para>
        /// Stores processed audio in a distributed cache to avoid redundant processing.
        /// The cache key should be deterministic based on the audio content and processing parameters.
        /// </para>
        /// </remarks>
        Task CacheAudioAsync(
            string key,
            byte[] audioData,
            Dictionary<string, string>? metadata = null,
            int expiration = 3600,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves cached audio if available.
        /// </summary>
        /// <param name="key">The cache key for the audio.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The cached audio data and metadata, or null if not found.</returns>
        /// <remarks>
        /// Returns null if the audio is not in cache or has expired.
        /// </remarks>
        Task<CachedAudio?> GetCachedAudioAsync(
            string key,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts audio metadata and characteristics.
        /// </summary>
        /// <param name="audioData">The audio data to analyze.</param>
        /// <param name="format">The audio format.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>Metadata about the audio file.</returns>
        /// <remarks>
        /// <para>
        /// Analyzes audio to extract useful information for processing decisions:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Duration and bitrate</description></item>
        /// <item><description>Sample rate and channels</description></item>
        /// <item><description>Average volume and peak levels</description></item>
        /// <item><description>Detected language hints</description></item>
        /// <item><description>Speech vs. music classification</description></item>
        /// </list>
        /// </remarks>
        Task<AudioMetadata> GetAudioMetadataAsync(
            byte[] audioData,
            string format,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Splits audio into smaller segments for processing.
        /// </summary>
        /// <param name="audioData">The audio data to split.</param>
        /// <param name="format">The audio format.</param>
        /// <param name="segmentDuration">Target segment duration in seconds.</param>
        /// <param name="overlap">Overlap between segments in seconds (for context).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of audio segments.</returns>
        /// <remarks>
        /// <para>
        /// Useful for processing long audio files that exceed provider limits or for
        /// parallel processing of audio chunks.
        /// </para>
        /// </remarks>
        Task<List<AudioSegment>> SplitAudioAsync(
            byte[] audioData,
            string format,
            double segmentDuration = 30.0,
            double overlap = 0.5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Merges multiple audio segments into a single file.
        /// </summary>
        /// <param name="segments">The audio segments to merge.</param>
        /// <param name="format">The output audio format.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The merged audio data.</returns>
        /// <remarks>
        /// Combines audio segments with smooth transitions, useful for reassembling
        /// processed audio chunks.
        /// </remarks>
        Task<byte[]> MergeAudioAsync(
            List<AudioSegment> segments,
            string format,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a format conversion is supported.
        /// </summary>
        /// <param name="sourceFormat">The source format.</param>
        /// <param name="targetFormat">The target format.</param>
        /// <returns>True if the conversion is supported.</returns>
        bool IsConversionSupported(string sourceFormat, string targetFormat);

        /// <summary>
        /// Gets the list of supported audio formats.
        /// </summary>
        /// <returns>A list of supported format identifiers.</returns>
        List<string> GetSupportedFormats();

        /// <summary>
        /// Estimates the processing time for an audio operation.
        /// </summary>
        /// <param name="audioSizeBytes">The size of the audio in bytes.</param>
        /// <param name="operation">The type of operation (e.g., "convert", "compress", "noise-reduce").</param>
        /// <returns>Estimated processing time in milliseconds.</returns>
        /// <remarks>
        /// Helps with capacity planning and user experience by providing processing time estimates.
        /// </remarks>
        double EstimateProcessingTime(long audioSizeBytes, string operation);
    }

    /// <summary>
    /// Represents cached audio data with metadata.
    /// </summary>
    public class CachedAudio
    {
        /// <summary>
        /// Gets or sets the audio data.
        /// </summary>
        public byte[] Data { get; set; } = System.Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the audio format.
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when the audio was cached.
        /// </summary>
        public System.DateTime CachedAt { get; set; }

        /// <summary>
        /// Gets or sets when the cache expires.
        /// </summary>
        public System.DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Metadata about an audio file.
    /// </summary>
    public class AudioMetadata
    {
        /// <summary>
        /// Gets or sets the duration in seconds.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the bitrate in bits per second.
        /// </summary>
        public int Bitrate { get; set; }

        /// <summary>
        /// Gets or sets the sample rate in Hz.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Gets or sets the number of channels.
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Gets or sets the average volume level in dB.
        /// </summary>
        public double AverageVolume { get; set; }

        /// <summary>
        /// Gets or sets the peak volume level in dB.
        /// </summary>
        public double PeakVolume { get; set; }

        /// <summary>
        /// Gets or sets whether the audio contains speech.
        /// </summary>
        public bool ContainsSpeech { get; set; }

        /// <summary>
        /// Gets or sets whether the audio contains music.
        /// </summary>
        public bool ContainsMusic { get; set; }

        /// <summary>
        /// Gets or sets the estimated noise level.
        /// </summary>
        public double NoiseLevel { get; set; }

        /// <summary>
        /// Gets or sets detected language hints.
        /// </summary>
        public List<string> LanguageHints { get; set; } = new();

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }
    }

    /// <summary>
    /// Represents a segment of audio data.
    /// </summary>
    public class AudioSegment
    {
        /// <summary>
        /// Gets or sets the segment index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the audio data.
        /// </summary>
        public byte[] Data { get; set; } = System.Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the start time in seconds.
        /// </summary>
        public double StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time in seconds.
        /// </summary>
        public double EndTime { get; set; }

        /// <summary>
        /// Gets or sets the duration in seconds.
        /// </summary>
        public double Duration => EndTime - StartTime;

        /// <summary>
        /// Gets or sets whether this segment overlaps with the next.
        /// </summary>
        public bool HasOverlap { get; set; }
    }
}
