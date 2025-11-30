using ConduitLLM.Core.Utilities;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    public partial class SecurityService
    {
        private async Task<SecurityCheckResult> CheckIpFilterAsync(string ipAddress)
        {
            // Check if it's a private IP and we allow private IPs
            if (_options.IpFiltering.AllowPrivateIps)
            {
                if (IpAddressHelper.IsPrivateIp(ipAddress))
                {
                    _logger.LogDebug("Private/Intranet IP {IpAddress} is automatically allowed", ipAddress);
                    return new SecurityCheckResult { IsAllowed = true };
                }
            }

            // Check environment variable based filters
            var isInWhitelist = _options.IpFiltering.Whitelist.Any(rule => IpAddressHelper.IsIpInRange(ipAddress, rule));
            var isInBlacklist = _options.IpFiltering.Blacklist.Any(rule => IpAddressHelper.IsIpInRange(ipAddress, rule));

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

        private bool IsPathExcluded(string path, List<string> excludedPaths)
        {
            return excludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
        }

        private string GetClientIpAddress(HttpContext context)
        {
            return IpAddressHelper.GetClientIpAddress(context);
        }
    }
}