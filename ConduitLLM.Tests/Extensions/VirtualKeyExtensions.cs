using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Extensions
{
    /// <summary>
    /// Extension methods for VirtualKey entity to provide backward compatibility properties
    /// </summary>
    public static class VirtualKeyExtensions
    {
        /// <summary>
        /// Gets the name of the virtual key (compatibility property for KeyName)
        /// </summary>
        public static string Name(this VirtualKey virtualKey)
        {
            return virtualKey.KeyName;
        }

        /// <summary>
        /// Gets whether the virtual key is active (compatibility property for IsEnabled)
        /// </summary>
        public static bool IsActive(this VirtualKey virtualKey)
        {
            return virtualKey.IsEnabled;
        }

        /// <summary>
        /// Gets the usage limit of the virtual key (compatibility property for MaxBudget)
        /// </summary>
        public static decimal? UsageLimit(this VirtualKey virtualKey)
        {
            return virtualKey.MaxBudget;
        }

        /// <summary>
        /// Gets the rate limit of the virtual key (compatibility property for RateLimitRpm)
        /// </summary>
        public static int? RateLimit(this VirtualKey virtualKey)
        {
            return virtualKey.RateLimitRpm;
        }
    }
}
