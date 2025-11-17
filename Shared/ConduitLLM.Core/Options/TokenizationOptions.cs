namespace ConduitLLM.Core.Options
{
    /// <summary>
    /// Configuration options for tokenization services.
    /// </summary>
    public class TokenizationOptions
    {
        /// <summary>
        /// Configuration section name.
        /// </summary>
        public const string SectionName = "ConduitLLM:Tokenization";

        /// <summary>
        /// Whether to automatically download tokenizer model files from HuggingFace.
        /// Default: true (enabled for best user experience).
        /// Set to false in air-gapped environments or when you want manual control.
        /// </summary>
        public bool AutoDownloadTokenizers { get; set; } = true;

        /// <summary>
        /// Directory where tokenizer model files are cached.
        /// Default: null (uses system app data directory: ~/.conduit/tokenizers or %APPDATA%/Conduit/tokenizers).
        /// </summary>
        public string? CacheDirectory { get; set; }

        /// <summary>
        /// Timeout in milliseconds for downloading tokenizer files.
        /// Default: 30000 (30 seconds).
        /// </summary>
        public int DownloadTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Number of retry attempts for failed downloads.
        /// Default: 3.
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Whether to use fallback estimation if tokenizer download fails.
        /// Default: true (graceful degradation).
        /// Set to false to throw exceptions on download failures.
        /// </summary>
        public bool FallbackOnDownloadFailure { get; set; } = true;

        /// <summary>
        /// Whether to verify tokenizer file integrity using checksums.
        /// Default: false (for simplicity).
        /// Set to true for enhanced security in production.
        /// </summary>
        public bool VerifyChecksums { get; set; } = false;

        /// <summary>
        /// Gets the effective cache directory, using the default if not specified.
        /// </summary>
        public string GetEffectiveCacheDirectory()
        {
            if (!string.IsNullOrEmpty(CacheDirectory))
            {
                return CacheDirectory;
            }

            // Use platform-appropriate application data directory
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Conduit", "tokenizers");
        }
    }
}
