// Re-export the shared security options extension methods for Admin API
// This file is a facade that delegates to the shared ConduitLLM.Security library
using ConduitLLM.Security.Options;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for configuring Admin security options.
    /// Delegates to the shared ConduitLLM.Security.Options.SecurityOptionsExtensions.
    /// </summary>
    public static class AdminSecurityOptionsExtensions
    {
        /// <summary>
        /// Configures Admin security options from environment variables.
        /// This is a facade method that delegates to the shared implementation.
        /// </summary>
        public static IServiceCollection ConfigureAdminSecurityOptions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Delegate to the shared implementation
            return SecurityOptionsExtensions.ConfigureAdminSecurityOptions(services, configuration);
        }
    }
}