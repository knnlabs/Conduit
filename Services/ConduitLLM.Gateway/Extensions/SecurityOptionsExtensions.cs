// Re-export the shared security options extension methods for Gateway API
// This file is a facade that delegates to the shared ConduitLLM.Security library
using ConduitLLM.Security.Options;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Extension methods for configuring Gateway security options.
    /// Delegates to the shared ConduitLLM.Security.Options.SecurityOptionsExtensions.
    /// </summary>
    public static class GatewaySecurityOptionsExtensions
    {
        /// <summary>
        /// Configures Gateway security options from environment variables.
        /// This is a facade method that delegates to the shared implementation.
        /// </summary>
        public static IServiceCollection ConfigureCoreApiSecurityOptions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Delegate to the shared implementation
            return SecurityOptionsExtensions.ConfigureGatewaySecurityOptions(services, configuration);
        }
    }
}