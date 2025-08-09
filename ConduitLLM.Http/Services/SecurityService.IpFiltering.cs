using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Security.Interfaces;

namespace ConduitLLM.Http.Services
{
    public partial class SecurityService
    {
        private async Task<SecurityCheckResult> CheckIpFilterAsync(string ipAddress)
        {
            // Check if it's a private IP and we allow private IPs
            if (_options.IpFiltering.AllowPrivateIps)
            {
                if (IsPrivateIp(ipAddress))
                {
                    _logger.LogDebug("Private/Intranet IP {IpAddress} is automatically allowed", ipAddress);
                    return new SecurityCheckResult { IsAllowed = true };
                }
            }

            // Check environment variable based filters
            var isInWhitelist = _options.IpFiltering.Whitelist.Any(rule => IsIpInRange(ipAddress, rule));
            var isInBlacklist = _options.IpFiltering.Blacklist.Any(rule => IsIpInRange(ipAddress, rule));

            var isAllowed = _options.IpFiltering.Mode.ToLower() == "restrictive" 
                ? isInWhitelist && !isInBlacklist
                : !isInBlacklist;

            if (!isAllowed)
            {
                _logger.LogWarning("IP {IpAddress} blocked by IP filter rules", ipAddress);
                return new SecurityCheckResult
                {
                    IsAllowed = false,
                    Reason = "IP address not allowed",
                    StatusCode = 403
                };
            }

            // Also check database-based IP filters
            using (var scope = _serviceProvider.CreateScope())
            {
                var ipFilterService = scope.ServiceProvider.GetRequiredService<IIpFilterService>();
                var isAllowedByDb = await ipFilterService.IsIpAllowedAsync(ipAddress);
                if (!isAllowedByDb)
                {
                    _logger.LogWarning("IP {IpAddress} blocked by database IP filter", ipAddress);
                    return new SecurityCheckResult
                    {
                        IsAllowed = false,
                        Reason = "IP address not allowed",
                        StatusCode = 403
                    };
                }
            }

            return new SecurityCheckResult { IsAllowed = true };
        }

        private bool IsPrivateIp(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            // Check loopback
            if (IPAddress.IsLoopback(ip))
                return true;

            // Check private ranges
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var ipBytes = ip.GetAddressBytes();
                
                // Check private ranges
                if (ipBytes[0] == 10 || // 10.0.0.0/8
                    (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) || // 172.16.0.0/12
                    (ipBytes[0] == 192 && ipBytes[1] == 168) || // 192.168.0.0/16
                    (ipBytes[0] == 169 && ipBytes[1] == 254)) // 169.254.0.0/16 (link-local)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsIpInRange(string ipAddress, string rule)
        {
            // Simple IP match
            if (ipAddress == rule)
                return true;

            // CIDR range check
            if (rule.Contains('/'))
            {
                return IsIpInCidrRange(ipAddress, rule);
            }

            return false;
        }

        private bool IsIpInCidrRange(string ipAddress, string cidrRange)
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

                // Only support IPv4 for now
                if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                    baseAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    return false;

                var ipBytes = ip.GetAddressBytes();
                var baseBytes = baseAddress.GetAddressBytes();

                // Calculate the mask
                var maskBytes = new byte[4];
                for (int i = 0; i < 4; i++)
                {
                    if (prefixLength >= 8)
                    {
                        maskBytes[i] = 0xFF;
                        prefixLength -= 8;
                    }
                    else if (prefixLength > 0)
                    {
                        maskBytes[i] = (byte)(0xFF << (8 - prefixLength));
                        prefixLength = 0;
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
            catch
            {
                return false;
            }
        }

        private bool IsPathExcluded(string path, List<string> excludedPaths)
        {
            return excludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Check X-Forwarded-For header first (for reverse proxies)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first IP in the chain
                var ip = forwardedFor.Split(',').First().Trim();
                if (IPAddress.TryParse(ip, out _))
                {
                    return ip;
                }
            }

            // Check X-Real-IP header
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
            {
                return realIp;
            }

            // Fall back to direct connection IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}