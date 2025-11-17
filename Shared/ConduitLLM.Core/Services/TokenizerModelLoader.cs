using ConduitLLM.Core.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for loading tokenizer model files with automatic downloading and caching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service provides automatic downloading of tokenizer model files from HuggingFace
    /// with intelligent caching to avoid repeated downloads. Files are small (2-4 MB) and
    /// downloaded only once per tokenizer version.
    /// </para>
    /// <para>
    /// Features:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Automatic downloading from HuggingFace</description></item>
    ///   <item><description>Local caching for offline usage</description></item>
    ///   <item><description>Retry logic with exponential backoff</description></item>
    ///   <item><description>Graceful fallback if downloads fail</description></item>
    ///   <item><description>Configurable via TokenizationOptions</description></item>
    /// </list>
    /// </remarks>
    public class TokenizerModelLoader
    {
        private readonly ILogger<TokenizerModelLoader> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TokenizationOptions _options;
        private readonly AsyncRetryPolicy _retryPolicy;

        // HuggingFace URLs for tokenizer model files
        private static readonly Dictionary<string, TokenizerInfo> TokenizerUrls = new()
        {
            ["llama2"] = new TokenizerInfo(
                Url: "https://huggingface.co/meta-llama/Llama-2-7b-hf/resolve/main/tokenizer.model",
                SizeBytes: 500_000, // ~500 KB
                Description: "LLaMA 2 SentencePiece tokenizer"
            ),
            ["llama3"] = new TokenizerInfo(
                Url: "https://huggingface.co/meta-llama/Meta-Llama-3-8B/resolve/main/original/tokenizer.model",
                SizeBytes: 2_180_000, // ~2.18 MB
                Description: "LLaMA 3 Tiktoken tokenizer"
            ),
            ["llama3.1"] = new TokenizerInfo(
                Url: "https://huggingface.co/meta-llama/Meta-Llama-3.1-8B/resolve/main/original/tokenizer.model",
                SizeBytes: 2_180_000, // ~2.18 MB
                Description: "LLaMA 3.1 Tiktoken tokenizer"
            )
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenizerModelLoader"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
        /// <param name="options">Tokenization configuration options.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public TokenizerModelLoader(
            ILogger<TokenizerModelLoader> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<TokenizationOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            // Configure retry policy with exponential backoff
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: _options.RetryAttempts,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Tokenizer download attempt {RetryCount} failed. Retrying in {Seconds}s...",
                            retryCount, timeSpan.TotalSeconds);
                    });
        }

        /// <summary>
        /// Gets a stream for the specified tokenizer model file.
        /// Downloads and caches if necessary.
        /// </summary>
        /// <param name="tokenizerVersion">The tokenizer version (e.g., "llama3", "llama3.1").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A stream containing the tokenizer model data.</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the tokenizer file is not found and auto-download is disabled.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when download fails and FallbackOnDownloadFailure is false.
        /// </exception>
        public async Task<Stream?> GetTokenizerStreamAsync(
            string tokenizerVersion,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tokenizerVersion))
            {
                throw new ArgumentException("Tokenizer version cannot be null or empty", nameof(tokenizerVersion));
            }

            var cacheDir = _options.GetEffectiveCacheDirectory();
            var localPath = Path.Combine(cacheDir, $"{tokenizerVersion}.model");

            // Check if already cached
            if (File.Exists(localPath))
            {
                _logger.LogDebug("Using cached tokenizer from {Path}", localPath);
                return File.OpenRead(localPath);
            }

            // Not cached - check if auto-download is enabled
            if (!_options.AutoDownloadTokenizers)
            {
                var message = $"Tokenizer '{tokenizerVersion}' not found at {localPath} and auto-download is disabled. " +
                             $"Either enable AutoDownloadTokenizers in configuration or download manually from HuggingFace.";
                _logger.LogWarning(message);

                if (_options.FallbackOnDownloadFailure)
                {
                    return null; // Will trigger fallback to character-based estimation
                }

                throw new FileNotFoundException(message, localPath);
            }

            // Auto-download enabled - download and cache
            try
            {
                await DownloadAndCacheTokenizerAsync(tokenizerVersion, localPath, cancellationToken);
                return File.OpenRead(localPath);
            }
            catch (Exception ex) when (_options.FallbackOnDownloadFailure)
            {
                _logger.LogError(ex,
                    "Failed to download tokenizer '{TokenizerVersion}'. Falling back to estimation.",
                    tokenizerVersion);
                return null; // Will trigger fallback
            }
        }

        /// <summary>
        /// Downloads and caches a tokenizer model file.
        /// </summary>
        private async Task DownloadAndCacheTokenizerAsync(
            string tokenizerVersion,
            string localPath,
            CancellationToken cancellationToken)
        {
            if (!TokenizerUrls.TryGetValue(tokenizerVersion, out var info))
            {
                throw new ArgumentException(
                    $"Unknown tokenizer version: {tokenizerVersion}. " +
                    $"Supported versions: {string.Join(", ", TokenizerUrls.Keys)}",
                    nameof(tokenizerVersion));
            }

            _logger.LogInformation(
                "Downloading {Description} ({Size:N2} MB) from HuggingFace...",
                info.Description,
                info.SizeBytes / 1_000_000.0);

            // Ensure cache directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            // Download with retry policy
            await _retryPolicy.ExecuteAsync(async () =>
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMilliseconds(_options.DownloadTimeoutMs);

                using var response = await httpClient.GetAsync(info.Url, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Write to temporary file first, then move (atomic operation)
                var tempPath = localPath + ".tmp";
                try
                {
                    await using var fileStream = File.Create(tempPath);
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);
                    fileStream.Close();

                    // Move temp file to final location
                    File.Move(tempPath, localPath, overwrite: true);

                    _logger.LogInformation(
                        "Successfully cached tokenizer at {Path} ({Size:N0} bytes)",
                        localPath,
                        new FileInfo(localPath).Length);
                }
                finally
                {
                    // Clean up temp file if it exists
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            });
        }

        /// <summary>
        /// Checks if a tokenizer is cached locally.
        /// </summary>
        /// <param name="tokenizerVersion">The tokenizer version to check.</param>
        /// <returns>True if the tokenizer is cached, false otherwise.</returns>
        public bool IsTokenizerCached(string tokenizerVersion)
        {
            var cacheDir = _options.GetEffectiveCacheDirectory();
            var localPath = Path.Combine(cacheDir, $"{tokenizerVersion}.model");
            return File.Exists(localPath);
        }

        /// <summary>
        /// Gets information about available tokenizers.
        /// </summary>
        /// <returns>A dictionary of tokenizer version to info.</returns>
        public static IReadOnlyDictionary<string, TokenizerInfo> GetAvailableTokenizers()
        {
            return TokenizerUrls;
        }

        /// <summary>
        /// Clears the tokenizer cache.
        /// </summary>
        /// <param name="tokenizerVersion">
        /// Specific tokenizer version to clear, or null to clear all.
        /// </param>
        public void ClearCache(string? tokenizerVersion = null)
        {
            var cacheDir = _options.GetEffectiveCacheDirectory();

            if (!Directory.Exists(cacheDir))
            {
                return;
            }

            if (tokenizerVersion != null)
            {
                var filePath = Path.Combine(cacheDir, $"{tokenizerVersion}.model");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Cleared cache for tokenizer '{TokenizerVersion}'", tokenizerVersion);
                }
            }
            else
            {
                // Clear all cached tokenizers
                foreach (var file in Directory.GetFiles(cacheDir, "*.model"))
                {
                    File.Delete(file);
                }
                _logger.LogInformation("Cleared all tokenizer caches");
            }
        }

        /// <summary>
        /// Information about a tokenizer model file.
        /// </summary>
        public record TokenizerInfo(
            string Url,
            long SizeBytes,
            string Description
        );
    }
}
