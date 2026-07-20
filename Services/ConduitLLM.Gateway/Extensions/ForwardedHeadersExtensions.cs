using System.Net;

using ConduitLLM.Security.Options;

using Microsoft.AspNetCore.HttpOverrides;

namespace ConduitLLM.Gateway.Extensions
{
    /// <summary>
    /// Configures ASP.NET Core's <see cref="ForwardedHeadersMiddleware"/> as the single, spoof-resistant
    /// source of the client IP address. Forwarded headers are only honored when the connecting peer is an
    /// explicitly-trusted proxy (see <see cref="TrustedProxyOptions"/>); otherwise the raw socket peer is used.
    /// </summary>
    public static class ForwardedHeadersExtensions
    {
        /// <summary>
        /// Registers <see cref="ForwardedHeadersOptions"/> from <c>CONDUIT_TRUSTED_PROXY_*</c> config.
        /// No-op when trusted-proxy support is disabled (the safe default), leaving forwarded headers untrusted.
        /// </summary>
        public static IServiceCollection AddTrustedProxyForwardedHeaders(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var trusted = TrustedProxyOptions.FromConfiguration(configuration);
            if (!trusted.Enabled)
            {
                return services;
            }

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // Clear the framework's default loopback entries so ONLY explicitly-trusted proxies are honored.
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                var networks = new List<string>(trusted.KnownNetworks);
                if (trusted.TrustCloudflare)
                {
                    networks.AddRange(CloudflareIpRanges.All);
                    if (trusted.UseCloudflareConnectingIp)
                    {
                        // Use Cloudflare's authoritative single client IP instead of X-Forwarded-For.
                        options.ForwardedForHeaderName = "CF-Connecting-IP";
                    }
                }

                foreach (var cidr in networks)
                {
                    var slash = cidr.Split('/');
                    if (slash.Length == 2
                        && IPAddress.TryParse(slash[0], out var prefix)
                        && int.TryParse(slash[1], out var prefixLength))
                    {
                        options.KnownNetworks.Add(
                            new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
                    }
                }

                foreach (var proxy in trusted.KnownProxies)
                {
                    if (IPAddress.TryParse(proxy, out var ip))
                    {
                        options.KnownProxies.Add(ip);
                    }
                }

                options.ForwardLimit = trusted.ForwardLimit;
            });

            return services;
        }

        /// <summary>
        /// Inserts <see cref="ForwardedHeadersMiddleware"/> into the pipeline when trusted-proxy support is
        /// enabled. Must run before any middleware that reads the client IP (auth, correlation, security).
        /// </summary>
        public static IApplicationBuilder UseTrustedProxyForwardedHeaders(this WebApplication app)
        {
            var trusted = TrustedProxyOptions.FromConfiguration(app.Configuration);
            if (trusted.Enabled)
            {
                app.UseForwardedHeaders();
            }

            return app;
        }
    }
}
