using System.Net;
using System.Net.Sockets;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Helper class for IP address operations including CIDR matching,
    /// client IP extraction, and validation.
    /// </summary>
    public static class IpAddressHelper
    {
        #region CIDR Matching

        /// <summary>
        /// Checks if an IP address matches a rule (exact IP or CIDR range).
        /// </summary>
        /// <param name="ipAddress">The IP address to check.</param>
        /// <param name="rule">The IP address or CIDR range to match against.</param>
        /// <returns>True if the IP matches the rule, false otherwise.</returns>
        public static bool IsIpInRange(string ipAddress, string rule)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(rule))
                return false;

            // Exact IP match
            if (ipAddress == rule)
                return true;

            if (!rule.Contains('/') &&
                IPAddress.TryParse(ipAddress, out var parsedIp) &&
                IPAddress.TryParse(rule, out var parsedRule))
            {
                return NormalizeAddress(parsedIp).Equals(NormalizeAddress(parsedRule));
            }

            // CIDR range check
            if (rule.Contains('/'))
            {
                return IsIpInCidrRange(ipAddress, rule);
            }

            return false;
        }

        /// <summary>
        /// Checks if an IP address falls within a CIDR range.
        /// Supports both IPv4 and IPv6 addresses.
        /// </summary>
        /// <param name="ipAddress">The IP address to check.</param>
        /// <param name="cidrRange">The CIDR range (e.g., "192.168.1.0/24" or "2001:db8::/32").</param>
        /// <returns>True if the IP is within the CIDR range, false otherwise.</returns>
        public static bool IsIpInCidrRange(string ipAddress, string cidrRange)
        {
            try
            {
                var parts = cidrRange.Split('/');
                if (parts.Length != 2)
                    return false;

                if (!IPAddress.TryParse(ipAddress, out var ip))
                    return false;

                if (!IPAddress.TryParse(parts[0], out var baseAddress))
                    return false;

                if (!int.TryParse(parts[1], out var prefixLength))
                    return false;

                var baseWasIpv4Mapped = baseAddress.IsIPv4MappedToIPv6;
                ip = NormalizeAddress(ip);
                baseAddress = NormalizeAddress(baseAddress);
                if (baseWasIpv4Mapped)
                {
                    if (prefixLength < 96)
                        return false;

                    prefixLength -= 96;
                }

                // Ensure both addresses are the same family
                if (ip.AddressFamily != baseAddress.AddressFamily)
                    return false;

                // Handle IPv4
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return IsIpv4InCidrRange(ip, baseAddress, prefixLength);
                }

                // Handle IPv6
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    return IsIpv6InCidrRange(ip, baseAddress, prefixLength);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if an IPv4 address falls within a CIDR range.
        /// </summary>
        private static bool IsIpv4InCidrRange(IPAddress ip, IPAddress baseAddress, int prefixLength)
        {
            if (prefixLength < 0 || prefixLength > 32)
                return false;

            var ipBytes = ip.GetAddressBytes();
            var baseBytes = baseAddress.GetAddressBytes();

            // Calculate the mask
            var maskBytes = new byte[4];
            var remainingBits = prefixLength;

            for (int i = 0; i < 4; i++)
            {
                if (remainingBits >= 8)
                {
                    maskBytes[i] = 0xFF;
                    remainingBits -= 8;
                }
                else if (remainingBits > 0)
                {
                    maskBytes[i] = (byte)(0xFF << (8 - remainingBits));
                    remainingBits = 0;
                }
                else
                {
                    maskBytes[i] = 0x00;
                }
            }

            // Check if the IP is in the range
            for (int i = 0; i < 4; i++)
            {
                if ((ipBytes[i] & maskBytes[i]) != (baseBytes[i] & maskBytes[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if an IPv6 address falls within a CIDR range.
        /// </summary>
        private static bool IsIpv6InCidrRange(IPAddress ip, IPAddress baseAddress, int prefixLength)
        {
            if (prefixLength < 0 || prefixLength > 128)
                return false;

            var ipBytes = ip.GetAddressBytes();
            var baseBytes = baseAddress.GetAddressBytes();

            // Calculate the mask (IPv6 has 16 bytes)
            var maskBytes = new byte[16];
            var remainingBits = prefixLength;

            for (int i = 0; i < 16; i++)
            {
                if (remainingBits >= 8)
                {
                    maskBytes[i] = 0xFF;
                    remainingBits -= 8;
                }
                else if (remainingBits > 0)
                {
                    maskBytes[i] = (byte)(0xFF << (8 - remainingBits));
                    remainingBits = 0;
                }
                else
                {
                    maskBytes[i] = 0x00;
                }
            }

            // Check if the IP is in the range
            for (int i = 0; i < 16; i++)
            {
                if ((ipBytes[i] & maskBytes[i]) != (baseBytes[i] & maskBytes[i]))
                    return false;
            }

            return true;
        }

        private static IPAddress NormalizeAddress(IPAddress address) =>
            address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        #endregion

        #region IP Validation

        /// <summary>
        /// Validates if a string is a valid IP address or CIDR notation.
        /// </summary>
        /// <param name="ipAddressOrCidr">The string to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public static bool IsValidIpAddressOrCidr(string ipAddressOrCidr)
        {
            if (string.IsNullOrWhiteSpace(ipAddressOrCidr))
                return false;

            try
            {
                // Check if it's a CIDR notation
                if (ipAddressOrCidr.Contains('/'))
                {
                    var parts = ipAddressOrCidr.Split('/');
                    if (parts.Length != 2)
                        return false;

                    // Validate IP part
                    if (!IPAddress.TryParse(parts[0], out var ip))
                        return false;

                    // Validate prefix length
                    if (!int.TryParse(parts[1], out int prefixLength))
                        return false;

                    // Validate prefix length based on address family
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return prefixLength >= 0 && prefixLength <= 32;
                    }
                    else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        return prefixLength >= 0 && prefixLength <= 128;
                    }

                    return false;
                }
                else
                {
                    // It's a simple IP address
                    return IPAddress.TryParse(ipAddressOrCidr, out _);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if an IP address is a private/internal IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address to check.</param>
        /// <returns>True if the IP is private, false otherwise.</returns>
        public static bool IsPrivateIp(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            return IsPrivateIp(ip);
        }

        /// <summary>
        /// Checks if an IP address is a private/internal IP address.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>True if the IP is private, false otherwise.</returns>
        public static bool IsPrivateIp(IPAddress ip)
        {
            // Check loopback
            if (IPAddress.IsLoopback(ip))
                return true;

            // IPv4 private ranges
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var ipBytes = ip.GetAddressBytes();

                // 10.0.0.0/8 (Class A private)
                if (ipBytes[0] == 10)
                    return true;

                // 172.16.0.0/12 (Class B private)
                if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31)
                    return true;

                // 192.168.0.0/16 (Class C private)
                if (ipBytes[0] == 192 && ipBytes[1] == 168)
                    return true;

                // 169.254.0.0/16 (Link-local)
                if (ipBytes[0] == 169 && ipBytes[1] == 254)
                    return true;

                // 127.0.0.0/8 (Loopback - already covered above but being explicit)
                if (ipBytes[0] == 127)
                    return true;
            }

            // IPv6 private/special ranges
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var ipBytes = ip.GetAddressBytes();

                // ::1 (Loopback - already covered above)
                // fe80::/10 (Link-local)
                if (ipBytes[0] == 0xFE && (ipBytes[1] & 0xC0) == 0x80)
                    return true;

                // fc00::/7 (Unique local addresses)
                if ((ipBytes[0] & 0xFE) == 0xFC)
                    return true;

                // ::ffff:0:0/96 (IPv4-mapped addresses - check the IPv4 part)
                if (ipBytes[0] == 0 && ipBytes[1] == 0 && ipBytes[2] == 0 && ipBytes[3] == 0 &&
                    ipBytes[4] == 0 && ipBytes[5] == 0 && ipBytes[6] == 0 && ipBytes[7] == 0 &&
                    ipBytes[8] == 0 && ipBytes[9] == 0 && ipBytes[10] == 0xFF && ipBytes[11] == 0xFF)
                {
                    // Extract IPv4 portion and check
                    var ipv4Bytes = new byte[] { ipBytes[12], ipBytes[13], ipBytes[14], ipBytes[15] };
                    var ipv4 = new IPAddress(ipv4Bytes);
                    return IsPrivateIp(ipv4);
                }
            }

            return false;
        }

        #endregion

        #region Client IP Extraction

        /// <summary>
        /// Extracts the client IP address from HTTP headers.
        /// </summary>
        /// <remarks>
        /// DEPRECATED and no longer trusts forwarded headers. Raw <c>X-Forwarded-For</c>/<c>X-Real-IP</c>
        /// headers are client-controlled and spoofable; trusting them lets an attacker bypass IP
        /// filters, evade bans, and poison another IP's rate-limit/ban counters. Trusted-proxy
        /// resolution is now handled once, up front, by ASP.NET Core's <c>ForwardedHeadersMiddleware</c>
        /// (configured with an explicit known-proxy allowlist), which rewrites
        /// <c>Connection.RemoteIpAddress</c> only when the peer is a trusted proxy. This overload
        /// therefore ignores <paramref name="headers"/> and returns <paramref name="remoteIpAddress"/>.
        /// </remarks>
        /// <param name="headers">Ignored. Present only for backwards compatibility.</param>
        /// <param name="remoteIpAddress">The (already vetted) connection remote IP address.</param>
        /// <returns>The client IP address as a string, or "unknown" if it cannot be determined.</returns>
        [Obsolete("Forwarded headers are no longer trusted here. Use GetClientIpAddress(HttpContext), which returns the ForwardedHeadersMiddleware-vetted RemoteIpAddress.")]
        public static string GetClientIpAddress(
            IDictionary<string, Microsoft.Extensions.Primitives.StringValues> headers,
            IPAddress? remoteIpAddress)
        {
            // Do NOT read forwarded headers here — they are spoofable. The connection's
            // RemoteIpAddress has already been vetted by ForwardedHeadersMiddleware.
            return remoteIpAddress == null ? "unknown" : NormalizeAddress(remoteIpAddress).ToString();
        }

        /// <summary>
        /// Extracts the client IP address from an HttpContext.
        /// </summary>
        /// <remarks>
        /// Returns <c>Connection.RemoteIpAddress</c>. This is the real client IP when the app sits
        /// behind a trusted proxy configured via <c>ForwardedHeadersMiddleware</c> (see
        /// <c>TrustedProxyOptions</c>); otherwise it is the direct socket peer. Forwarded headers are
        /// intentionally NOT read here, so an untrusted client cannot spoof its source IP.
        /// </remarks>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The client IP address as a string, or "unknown" if it cannot be determined.</returns>
        public static string GetClientIpAddress(Microsoft.AspNetCore.Http.HttpContext context)
        {
            var remoteIpAddress = context.Connection.RemoteIpAddress;
            return remoteIpAddress == null ? "unknown" : NormalizeAddress(remoteIpAddress).ToString();
        }

        #endregion

        #region Private Network Request Detection

        /// <summary>
        /// Checks if an HTTP request originates from a private/internal network.
        /// Useful for allowing internal monitoring tools (e.g., Prometheus) to access
        /// endpoints without authentication while still requiring auth for external requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>True if the request comes from a private network, false otherwise.</returns>
        public static bool IsPrivateNetworkRequest(Microsoft.AspNetCore.Http.HttpContext context)
        {
            // For internal Docker network requests, we check the direct connection IP
            // rather than forwarded headers (which could be spoofed from external sources)
            var remoteIp = context.Connection.RemoteIpAddress;

            if (remoteIp == null)
                return false;

            return IsPrivateIp(remoteIp);
        }

        #endregion

        #region Address Family Detection

        /// <summary>
        /// Determines if an IP address string is IPv4.
        /// </summary>
        /// <param name="ipAddress">The IP address string.</param>
        /// <returns>True if IPv4, false otherwise.</returns>
        public static bool IsIpv4(string ipAddress)
        {
            return IPAddress.TryParse(ipAddress, out var ip) &&
                   ip.AddressFamily == AddressFamily.InterNetwork;
        }

        /// <summary>
        /// Determines if an IP address string is IPv6.
        /// </summary>
        /// <param name="ipAddress">The IP address string.</param>
        /// <returns>True if IPv6, false otherwise.</returns>
        public static bool IsIpv6(string ipAddress)
        {
            return IPAddress.TryParse(ipAddress, out var ip) &&
                   ip.AddressFamily == AddressFamily.InterNetworkV6;
        }

        #endregion
    }
}
