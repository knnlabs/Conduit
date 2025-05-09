using System.Net;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for matching IP addresses against IP filter rules
/// </summary>
public class IpFilterMatcher
{
    private readonly ILogger<IpFilterMatcher> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="IpFilterMatcher"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public IpFilterMatcher(ILogger<IpFilterMatcher> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Determines if an IP address is allowed based on the provided filter rules
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <param name="filters">The collection of IP filter rules</param>
    /// <param name="defaultAllow">Whether to allow access by default when no specific rule matches</param>
    /// <returns>True if the IP address is allowed, false otherwise</returns>
    public bool IsIpAllowed(string ipAddress, IEnumerable<IpFilterEntity> filters, bool defaultAllow = true)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !IpAddressValidator.IsValidIpAddress(ipAddress))
        {
            _logger.LogWarning("Invalid IP address format: {IpAddress}", ipAddress);
            return defaultAllow;
        }
        
        if (filters == null || !filters.Any())
        {
            _logger.LogDebug("No IP filters defined, using default allow setting: {DefaultAllow}", defaultAllow);
            return defaultAllow;
        }
        
        // Only consider enabled filters
        var enabledFilters = filters.Where(f => f.IsEnabled).ToList();
        if (!enabledFilters.Any())
        {
            _logger.LogDebug("No enabled IP filters found, using default allow setting: {DefaultAllow}", defaultAllow);
            return defaultAllow;
        }
        
        // Check whitelist filters first - if IP is in any whitelist, allow access
        var whitelists = enabledFilters.Where(f => f.FilterType == IpFilterConstants.WHITELIST).ToList();
        if (whitelists.Any() && IsIpInAnyFilter(ipAddress, whitelists))
        {
            _logger.LogDebug("IP {IpAddress} matched a whitelist filter, allowing access", ipAddress);
            return true;
        }
        
        // Check blacklist filters - if IP is in any blacklist, deny access
        var blacklists = enabledFilters.Where(f => f.FilterType == IpFilterConstants.BLACKLIST).ToList();
        if (blacklists.Any() && IsIpInAnyFilter(ipAddress, blacklists))
        {
            _logger.LogInformation("IP {IpAddress} matched a blacklist filter, denying access", ipAddress);
            return false;
        }
        
        // If we have whitelists but the IP didn't match any, deny access
        if (whitelists.Any())
        {
            _logger.LogInformation("IP {IpAddress} did not match any whitelist filter, denying access", ipAddress);
            return false;
        }
        
        // No matching whitelist or blacklist rules
        _logger.LogDebug("IP {IpAddress} did not match any filter rules, using default allow setting: {DefaultAllow}", 
            ipAddress, defaultAllow);
        return defaultAllow;
    }
    
    private bool IsIpInAnyFilter(string ipAddress, IEnumerable<IpFilterEntity> filters)
    {
        foreach (var filter in filters)
        {
            // Direct IP address match
            if (!filter.IpAddressOrCidr.Contains('/'))
            {
                if (string.Equals(ipAddress, filter.IpAddressOrCidr, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                
                continue;
            }
            
            // CIDR subnet match
            if (IpAddressValidator.IsIpInCidrRange(ipAddress, filter.IpAddressOrCidr))
            {
                return true;
            }
        }
        
        return false;
    }
}