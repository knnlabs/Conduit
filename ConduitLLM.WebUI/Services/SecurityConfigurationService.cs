using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.IpFilter;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for managing security configuration from environment variables
/// </summary>
public interface ISecurityConfigurationService
{
    /// <summary>
    /// Gets the IP filter settings from environment variables
    /// </summary>
    /// <returns>IP filter settings</returns>
    IpFilterSettingsDto GetIpFilterSettings();

    /// <summary>
    /// Gets whether to automatically allow private/intranet IPs
    /// </summary>
    bool AllowPrivateIps { get; }

    /// <summary>
    /// Gets the maximum failed login attempts before banning
    /// </summary>
    int MaxFailedLoginAttempts { get; }

    /// <summary>
    /// Gets the IP ban duration in minutes
    /// </summary>
    int IpBanDurationMinutes { get; }
}

/// <summary>
/// Implementation of security configuration service
/// </summary>
public class SecurityConfigurationService : ISecurityConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecurityConfigurationService> _logger;

    // Environment variable keys
    private const string IP_FILTERING_ENABLED = "CONDUIT_IP_FILTERING_ENABLED";
    private const string IP_FILTER_MODE = "CONDUIT_IP_FILTER_MODE";
    private const string IP_FILTER_DEFAULT_ALLOW = "CONDUIT_IP_FILTER_DEFAULT_ALLOW";
    private const string IP_FILTER_BYPASS_ADMIN_UI = "CONDUIT_IP_FILTER_BYPASS_ADMIN_UI";
    private const string IP_FILTER_ALLOW_PRIVATE = "CONDUIT_IP_FILTER_ALLOW_PRIVATE";
    private const string IP_FILTER_WHITELIST = "CONDUIT_IP_FILTER_WHITELIST";
    private const string IP_FILTER_BLACKLIST = "CONDUIT_IP_FILTER_BLACKLIST";
    private const string MAX_FAILED_ATTEMPTS = "CONDUIT_MAX_FAILED_ATTEMPTS";
    private const string IP_BAN_DURATION = "CONDUIT_IP_BAN_DURATION_MINUTES";

    // Default values
    private const bool DEFAULT_FILTERING_ENABLED = false;
    private const string DEFAULT_FILTER_MODE = "permissive";
    private const bool DEFAULT_ALLOW = true;
    private const bool DEFAULT_BYPASS_ADMIN_UI = true;
    private const bool DEFAULT_ALLOW_PRIVATE = true;
    private const int DEFAULT_MAX_FAILED_ATTEMPTS = 5;
    private const int DEFAULT_BAN_DURATION_MINUTES = 30;

    public SecurityConfigurationService(
        IConfiguration configuration,
        ILogger<SecurityConfigurationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool AllowPrivateIps => GetBoolValue(IP_FILTER_ALLOW_PRIVATE, DEFAULT_ALLOW_PRIVATE);

    /// <inheritdoc />
    public int MaxFailedLoginAttempts => GetIntValue(MAX_FAILED_ATTEMPTS, DEFAULT_MAX_FAILED_ATTEMPTS);

    /// <inheritdoc />
    public int IpBanDurationMinutes => GetIntValue(IP_BAN_DURATION, DEFAULT_BAN_DURATION_MINUTES);

    /// <inheritdoc />
    public IpFilterSettingsDto GetIpFilterSettings()
    {
        var settings = new IpFilterSettingsDto
        {
            IsEnabled = GetBoolValue(IP_FILTERING_ENABLED, DEFAULT_FILTERING_ENABLED),
            FilterMode = GetStringValue(IP_FILTER_MODE, DEFAULT_FILTER_MODE),
            DefaultAllow = GetBoolValue(IP_FILTER_DEFAULT_ALLOW, DEFAULT_ALLOW),
            BypassForAdminUi = GetBoolValue(IP_FILTER_BYPASS_ADMIN_UI, DEFAULT_BYPASS_ADMIN_UI),
            ExcludedEndpoints = GetDefaultExcludedEndpoints(),
            WhitelistFilters = ParseIpFilters(IP_FILTER_WHITELIST, "whitelist"),
            BlacklistFilters = ParseIpFilters(IP_FILTER_BLACKLIST, "blacklist")
        };

        LogSettings(settings);
        return settings;
    }

    private bool GetBoolValue(string key, bool defaultValue)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (bool.TryParse(value, out var result))
            return result;

        _logger.LogWarning("Invalid boolean value '{Value}' for {Key}, using default: {Default}", 
            value, key, defaultValue);
        return defaultValue;
    }

    private int GetIntValue(string key, int defaultValue)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (int.TryParse(value, out var result) && result > 0)
            return result;

        _logger.LogWarning("Invalid integer value '{Value}' for {Key}, using default: {Default}", 
            value, key, defaultValue);
        return defaultValue;
    }

    private string GetStringValue(string key, string defaultValue)
    {
        var value = _configuration[key];
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private List<string> GetDefaultExcludedEndpoints()
    {
        return new List<string>
        {
            "/login",
            "/logout", 
            "/health",
            "/health/ready",
            "/health/live",
            "/_blazor",
            "/css",
            "/js",
            "/images",
            "/favicon.ico"
        };
    }

    private List<IpFilterDto> ParseIpFilters(string envKey, string filterType)
    {
        var filters = new List<IpFilterDto>();
        var value = _configuration[envKey];
        
        if (string.IsNullOrWhiteSpace(value))
            return filters;

        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Validate the IP or CIDR
            if (!IpAddressValidator.IsValidIpAddress(trimmed) && !IpAddressValidator.IsValidCidr(trimmed))
            {
                _logger.LogWarning("Invalid IP/CIDR '{Value}' in {Key}, skipping", trimmed, envKey);
                continue;
            }

            filters.Add(new IpFilterDto
            {
                Id = index++,
                FilterType = filterType,
                IpAddressOrCidr = trimmed,
                Name = $"Environment {filterType} #{index}",
                Description = $"Loaded from {envKey}",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (filters.Count > 0)
        {
            _logger.LogInformation("Loaded {Count} {FilterType} filters from {Key}", 
                filters.Count, filterType, envKey);
        }

        return filters;
    }

    private void LogSettings(IpFilterSettingsDto settings)
    {
        _logger.LogInformation("Security Configuration loaded:");
        _logger.LogInformation("  - IP Filtering: {Enabled}", settings.IsEnabled ? "Enabled" : "Disabled");
        
        if (settings.IsEnabled)
        {
            _logger.LogInformation("  - Filter Mode: {Mode}", settings.FilterMode);
            _logger.LogInformation("  - Default Action: {Action}", settings.DefaultAllow ? "Allow" : "Deny");
            _logger.LogInformation("  - Bypass Admin UI: {Bypass}", settings.BypassForAdminUi);
            _logger.LogInformation("  - Allow Private IPs: {AllowPrivate}", AllowPrivateIps);
            _logger.LogInformation("  - Whitelist Entries: {Count}", settings.WhitelistFilters.Count);
            _logger.LogInformation("  - Blacklist Entries: {Count}", settings.BlacklistFilters.Count);
        }

        _logger.LogInformation("  - Max Failed Login Attempts: {Max}", MaxFailedLoginAttempts);
        _logger.LogInformation("  - IP Ban Duration: {Minutes} minutes", IpBanDurationMinutes);
    }
}