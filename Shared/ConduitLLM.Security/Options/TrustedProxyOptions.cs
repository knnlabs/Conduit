using Microsoft.Extensions.Configuration;

namespace ConduitLLM.Security.Options
{
    /// <summary>
    /// Configuration for trusted reverse proxies, used to derive the real client IP address
    /// securely via ASP.NET Core's <c>ForwardedHeadersMiddleware</c>.
    ///
    /// <para>
    /// Security model: forwarded headers (<c>X-Forwarded-For</c>, <c>CF-Connecting-IP</c>) are
    /// client-controlled and MUST NOT be trusted unless the request demonstrably arrived through a
    /// known proxy. The middleware only rewrites <c>HttpContext.Connection.RemoteIpAddress</c> from a
    /// forwarded header when the immediate connecting peer is in <see cref="KnownProxies"/> or
    /// <see cref="KnownNetworks"/> — so a direct attacker who forges the header is ignored.
    /// </para>
    ///
    /// <para>
    /// Default is <see cref="Enabled"/> = <c>false</c>: no forwarded-header trust at all, and the raw
    /// socket peer is used. Operators opt in per deployment. When the edge is Cloudflare, set
    /// <see cref="Enabled"/> and <see cref="TrustCloudflare"/> = <c>true</c>.
    /// </para>
    ///
    /// <para>
    /// IMPORTANT deployment prerequisite: when trusting a CDN/proxy you MUST also restrict origin
    /// ingress to that proxy's IPs (firewall / security group) or require a shared-secret header
    /// (e.g. Cloudflare Authenticated Origin Pulls). Otherwise an attacker can bypass the proxy by
    /// connecting to the origin directly and the trust model provides no protection.
    /// </para>
    /// </summary>
    public class TrustedProxyOptions
    {
        /// <summary>
        /// Whether to enable trusted-proxy forwarded-header processing. When <c>false</c> (default),
        /// forwarded headers are ignored entirely and the direct socket peer is the client IP.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Whether to trust Cloudflare's published IP ranges as known proxies. Adds
        /// <see cref="CloudflareIpRanges.All"/> to the trusted networks.
        /// </summary>
        public bool TrustCloudflare { get; set; }

        /// <summary>
        /// When trusting Cloudflare, use the <c>CF-Connecting-IP</c> header (Cloudflare's authoritative
        /// single client IP) as the forwarded-for header instead of <c>X-Forwarded-For</c>. Default <c>true</c>.
        /// </summary>
        public bool UseCloudflareConnectingIp { get; set; } = true;

        /// <summary>
        /// Individual trusted proxy IP addresses (in addition to any networks).
        /// </summary>
        public List<string> KnownProxies { get; set; } = new();

        /// <summary>
        /// Trusted proxy networks in CIDR notation (e.g. <c>10.0.0.0/8</c>).
        /// </summary>
        public List<string> KnownNetworks { get; set; } = new();

        /// <summary>
        /// Maximum number of forwarded entries to walk (proxy hops). Default 1 (a single trusted edge).
        /// Increase if there are multiple trusted hops (e.g. CDN → internal load balancer → app).
        /// </summary>
        public int ForwardLimit { get; set; } = 1;

        /// <summary>
        /// Builds a <see cref="TrustedProxyOptions"/> from configuration using the shared
        /// <c>CONDUIT_TRUSTED_PROXY_*</c> environment variables (topology is the same for both APIs).
        /// </summary>
        public static TrustedProxyOptions FromConfiguration(IConfiguration configuration)
        {
            var options = new TrustedProxyOptions();

            var enabled = configuration["CONDUIT_TRUSTED_PROXY_ENABLED"];
            if (!string.IsNullOrWhiteSpace(enabled))
            {
                options.Enabled = bool.Parse(enabled);
            }

            var trustCloudflare = configuration["CONDUIT_TRUSTED_PROXY_TRUST_CLOUDFLARE"];
            if (!string.IsNullOrWhiteSpace(trustCloudflare))
            {
                options.TrustCloudflare = bool.Parse(trustCloudflare);
            }

            var useCfConnectingIp = configuration["CONDUIT_TRUSTED_PROXY_USE_CF_CONNECTING_IP"];
            if (!string.IsNullOrWhiteSpace(useCfConnectingIp))
            {
                options.UseCloudflareConnectingIp = bool.Parse(useCfConnectingIp);
            }

            var knownProxies = configuration["CONDUIT_TRUSTED_PROXY_KNOWN_PROXIES"];
            if (!string.IsNullOrWhiteSpace(knownProxies))
            {
                options.KnownProxies = ParseCsv(knownProxies);
            }

            var knownNetworks = configuration["CONDUIT_TRUSTED_PROXY_KNOWN_NETWORKS"];
            if (!string.IsNullOrWhiteSpace(knownNetworks))
            {
                options.KnownNetworks = ParseCsv(knownNetworks);
            }

            var forwardLimit = configuration["CONDUIT_TRUSTED_PROXY_FORWARD_LIMIT"];
            if (!string.IsNullOrWhiteSpace(forwardLimit))
            {
                options.ForwardLimit = int.Parse(forwardLimit);
            }

            return options;
        }

        private static List<string> ParseCsv(string value)
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }
    }

    /// <summary>
    /// Cloudflare's published edge IP ranges. Source: https://www.cloudflare.com/ips/
    /// (IPv4: https://www.cloudflare.com/ips-v4, IPv6: https://www.cloudflare.com/ips-v6).
    /// These change rarely; refresh from the source if Cloudflare updates them.
    /// </summary>
    public static class CloudflareIpRanges
    {
        /// <summary>Cloudflare IPv4 CIDR ranges.</summary>
        public static readonly IReadOnlyList<string> IPv4 = new[]
        {
            "173.245.48.0/20",
            "103.21.244.0/22",
            "103.22.200.0/22",
            "103.31.4.0/22",
            "141.101.64.0/18",
            "108.162.192.0/18",
            "190.93.240.0/20",
            "188.114.96.0/20",
            "197.234.240.0/22",
            "198.41.128.0/17",
            "162.158.0.0/15",
            "104.16.0.0/13",
            "104.24.0.0/14",
            "172.64.0.0/13",
            "131.0.72.0/22",
        };

        /// <summary>Cloudflare IPv6 CIDR ranges.</summary>
        public static readonly IReadOnlyList<string> IPv6 = new[]
        {
            "2400:cb00::/32",
            "2606:4700::/32",
            "2803:f800::/32",
            "2405:b500::/32",
            "2405:8100::/32",
            "2a06:98c0::/29",
            "2c0f:f248::/32",
        };

        /// <summary>All Cloudflare IPv4 and IPv6 CIDR ranges.</summary>
        public static readonly IReadOnlyList<string> All = IPv4.Concat(IPv6).ToList();
    }
}
