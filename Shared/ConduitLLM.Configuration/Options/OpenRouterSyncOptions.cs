namespace ConduitLLM.Configuration.Options
{
    /// <summary>
    /// Options for the scheduled OpenRouter metadata sync (drift detection). Bound from the
    /// <c>OpenRouterSync</c> configuration section (env: <c>OpenRouterSync__*</c>).
    /// </summary>
    public class OpenRouterSyncOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "OpenRouterSync";

        /// <summary>
        /// Whether the scheduled sync is enabled. Defaults to false — drift detection is opt-in and can
        /// also be triggered on demand via the Admin API regardless of this flag.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>How often the scheduled sync runs, in hours.</summary>
        public int ScheduleIntervalHours { get; set; } = 24;

        /// <summary>Delay before the first scheduled run after startup, in seconds.</summary>
        public int InitialDelaySeconds { get; set; } = 60;

        /// <summary>The OpenRouter models endpoint to fetch (public, unauthenticated).</summary>
        public string ModelsEndpoint { get; set; } = "https://openrouter.ai/api/v1/models";

        /// <summary>HTTP timeout for the catalog fetch, in seconds.</summary>
        public int HttpTimeoutSeconds { get; set; } = 60;
    }
}
