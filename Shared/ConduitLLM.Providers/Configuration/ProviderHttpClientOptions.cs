using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Providers.Configuration;

/// <summary>
/// Consolidated configuration options for HTTP clients used by LLM provider clients.
/// Provides consistent timeout and retry settings across all providers.
/// </summary>
public class ProviderHttpClientOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "ConduitLLM:HttpClient";

    /// <summary>
    /// Default timeout for standard API requests (e.g., chat completions).
    /// Default: 120 seconds.
    /// </summary>
    [Range(5, 600)]
    public int DefaultTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Timeout for authentication verification requests.
    /// Should be shorter since these are simple health checks.
    /// Default: 30 seconds.
    /// </summary>
    [Range(5, 120)]
    public int AuthVerificationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout for image generation requests.
    /// Default: 180 seconds (3 minutes).
    /// </summary>
    [Range(30, 600)]
    public int ImageGenerationTimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// Timeout for video generation requests.
    /// Video generation can take a long time.
    /// Default: 600 seconds (10 minutes).
    /// </summary>
    [Range(60, 3600)]
    public int VideoGenerationTimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Timeout for large file downloads (e.g., video files).
    /// Default: 1800 seconds (30 minutes).
    /// </summary>
    [Range(60, 7200)]
    public int LargeFileDownloadTimeoutSeconds { get; set; } = 1800;

    /// <summary>
    /// Timeout for polling operations (checking async task status).
    /// Default: 30 seconds per poll request.
    /// </summary>
    [Range(5, 120)]
    public int PollingTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum duration for video generation polling loops.
    /// Default: 900 seconds (15 minutes).
    /// </summary>
    [Range(60, 3600)]
    public int VideoPollingMaxDurationSeconds { get; set; } = 900;

    /// <summary>
    /// Whether to log timeout events.
    /// Default: true.
    /// </summary>
    public bool EnableTimeoutLogging { get; set; } = true;

    /// <summary>
    /// Gets the timeout for a specific operation type.
    /// </summary>
    /// <param name="operationType">The type of operation.</param>
    /// <returns>The timeout as a TimeSpan.</returns>
    public TimeSpan GetTimeout(ProviderOperationType operationType)
    {
        return operationType switch
        {
            ProviderOperationType.AuthVerification => TimeSpan.FromSeconds(AuthVerificationTimeoutSeconds),
            ProviderOperationType.ChatCompletion => TimeSpan.FromSeconds(DefaultTimeoutSeconds),
            ProviderOperationType.ImageGeneration => TimeSpan.FromSeconds(ImageGenerationTimeoutSeconds),
            ProviderOperationType.VideoGeneration => TimeSpan.FromSeconds(VideoGenerationTimeoutSeconds),
            ProviderOperationType.LargeFileDownload => TimeSpan.FromSeconds(LargeFileDownloadTimeoutSeconds),
            ProviderOperationType.Polling => TimeSpan.FromSeconds(PollingTimeoutSeconds),
            ProviderOperationType.VideoPolling => TimeSpan.FromSeconds(VideoPollingMaxDurationSeconds),
            _ => TimeSpan.FromSeconds(DefaultTimeoutSeconds)
        };
    }
}

/// <summary>
/// Types of operations that can have different timeout configurations.
/// </summary>
public enum ProviderOperationType
{
    /// <summary>Standard API request (default).</summary>
    Default,

    /// <summary>Authentication verification request.</summary>
    AuthVerification,

    /// <summary>Chat completion request.</summary>
    ChatCompletion,

    /// <summary>Image generation request.</summary>
    ImageGeneration,

    /// <summary>Video generation request.</summary>
    VideoGeneration,

    /// <summary>Large file download (e.g., video files).</summary>
    LargeFileDownload,

    /// <summary>Polling for async task status.</summary>
    Polling,

    /// <summary>Video generation polling loop.</summary>
    VideoPolling
}
