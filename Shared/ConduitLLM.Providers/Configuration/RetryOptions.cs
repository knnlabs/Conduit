namespace ConduitLLM.Providers.Configuration;

/// <summary>
/// Configuration options for HTTP retry policies used by LLM provider clients.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Section name in the configuration file.
    /// </summary>
    public const string SectionName = "Conduit:HttpRetry";

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in seconds before first retry.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Maximum delay cap in seconds for any retry.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Whether retry logging is enabled.
    /// </summary>
    public bool EnableRetryLogging { get; set; } = true;
}
