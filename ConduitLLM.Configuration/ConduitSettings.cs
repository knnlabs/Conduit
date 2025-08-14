namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Main configuration settings for Conduit.
    /// This class holds non-provider configuration that may still come from appsettings.json.
    /// Provider configuration is now entirely database-driven.
    /// </summary>
    public class ConduitSettings
    {
        /// <summary>
        /// Performance tracking settings.
        /// </summary>
        public PerformanceTrackingSettings? PerformanceTracking { get; set; }
        
        /// <summary>
        /// Other non-provider settings can be added here as needed.
        /// </summary>
    }
}