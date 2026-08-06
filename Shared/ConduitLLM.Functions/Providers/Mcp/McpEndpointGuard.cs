using System.Net;
using System.Net.Sockets;

namespace ConduitLLM.Functions.Providers.Mcp;

/// <summary>
/// Lightweight SSRF egress guard for MCP server URLs.
/// </summary>
/// <remarks>
/// MCP servers are added only by master-key admins, so this is defense-in-depth rather than the
/// sole control. It requires an absolute http(s) URL and, unless the configuration opts in via
/// <c>allowPrivateNetwork</c>, rejects loopback / private / link-local hosts. It matches IP
/// literals and well-known loopback hostnames; it does not resolve DNS, so it does not defend
/// against DNS-rebinding — that hardening is deferred (see plan).
/// </remarks>
public static class McpEndpointGuard
{
    /// <summary>
    /// Validates the server URL, throwing <see cref="InvalidOperationException"/> when it is not an
    /// acceptable egress target.
    /// </summary>
    public static void EnsureAllowed(string? serverUrl, bool allowPrivateNetwork)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) ||
            !Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"MCP server URL is not a valid absolute URL: '{serverUrl}'");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"MCP server URL must use http or https, but was '{uri.Scheme}'.");
        }

        if (allowPrivateNetwork)
        {
            return;
        }

        var host = uri.DnsSafeHost;

        if (IsLoopbackHostName(host))
        {
            throw new InvalidOperationException(
                $"MCP server host '{host}' resolves to a loopback address; set allowPrivateNetwork to override.");
        }

        if (IPAddress.TryParse(host, out var ip) && IsPrivateOrReserved(ip))
        {
            throw new InvalidOperationException(
                $"MCP server host '{host}' is a private/reserved address; set allowPrivateNetwork to override.");
        }
    }

    private static bool IsLoopbackHostName(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("localhost.localdomain", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (b[0] == 10) return true;
            // 172.16.0.0/12
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            // 192.168.0.0/16
            if (b[0] == 192 && b[1] == 168) return true;
            // 169.254.0.0/16 link-local
            if (b[0] == 169 && b[1] == 254) return true;
            // 0.0.0.0/8 "this host"
            if (b[0] == 0) return true;
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
            {
                return true;
            }

            // Unique-local addresses fc00::/7 (first 7 bits == 1111110).
            var b = ip.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}
