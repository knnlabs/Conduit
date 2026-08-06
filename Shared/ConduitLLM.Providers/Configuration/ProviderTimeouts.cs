namespace ConduitLLM.Providers.Configuration;

/// <summary>
/// Timeouts read directly from the environment because they apply inside provider
/// clients that deliberately bypass the pooled HttpClient policies (video generation
/// polling outlives every configured HTTP timeout).
/// </summary>
public static class ProviderTimeouts
{
    private const int DefaultVideoPollingSeconds = 600;

    /// <summary>
    /// How long a provider client keeps polling an async video job before giving up.
    /// </summary>
    public static int VideoPollingSeconds()
    {
        var env = Environment.GetEnvironmentVariable("CONDUITLLM__TIMEOUTS__VIDEO_POLLING__SECONDS");
        return !string.IsNullOrEmpty(env) && int.TryParse(env, out var parsed) && parsed > 0
            ? parsed
            : DefaultVideoPollingSeconds;
    }
}
