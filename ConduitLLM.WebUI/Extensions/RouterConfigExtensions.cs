using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for RouterConfig to provide backward compatibility
    /// </summary>
    public static class RouterConfigExtensions
    {
        // Backward compatibility property
        public static bool Enabled(this RouterConfig config) => true;

        // Backward compatibility property - maps to DefaultRoutingStrategy
        public static string DefaultModelSelectionStrategy(this RouterConfig config)
        {
            return string.IsNullOrEmpty(config.DefaultRoutingStrategy) ? "Simple" : config.DefaultRoutingStrategy;
        }

        // Backward compatibility property - maps to FallbacksEnabled
        public static bool FallbackEnabled(this RouterConfig config)
        {
            return config.FallbacksEnabled;
        }
    }
}
