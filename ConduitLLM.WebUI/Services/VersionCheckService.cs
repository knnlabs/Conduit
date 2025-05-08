using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for checking for new versions of Conduit
/// </summary>
public class VersionCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VersionCheckService> _logger;
    private readonly NotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private string _currentVersion = "0.0.0";
    private DateTime _lastCheck = DateTime.MinValue;
    private TimeSpan _checkInterval;

    public VersionCheckService(
        IHttpClientFactory httpClientFactory,
        ILogger<VersionCheckService> logger,
        NotificationService notificationService,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _notificationService = notificationService;
        _configuration = configuration;
        
        // Default check interval is 24 hours - can be overridden in configuration
        // Try from environment variables first (Docker-friendly)
        var intervalHours = Environment.GetEnvironmentVariable("CONDUIT_VERSION_CHECK_INTERVAL_HOURS");
        if (!string.IsNullOrEmpty(intervalHours) && double.TryParse(intervalHours, out var hours))
        {
            _checkInterval = TimeSpan.FromHours(hours);
        }
        else
        {
            // Fall back to configuration
            _checkInterval = TimeSpan.FromHours(
                configuration.GetValue<double>("VersionCheck:IntervalHours", 24));
        }
    }

    /// <summary>
    /// Initializes the service and loads the current version
    /// </summary>
    public void Initialize()
    {
        // Load the current version from assembly
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        
        if (version != null)
        {
            _currentVersion = version.ToString();
        }
        else
        {
            // Try to get informational version
            var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoVersionAttr != null)
            {
                _currentVersion = infoVersionAttr.InformationalVersion;
            }
        }
        
        _logger.LogInformation("Current version: {Version}", _currentVersion);
    }

    /// <summary>
    /// Gets the current version
    /// </summary>
    public string GetCurrentVersion()
    {
        return _currentVersion;
    }

    /// <summary>
    /// Checks for a new version if enough time has elapsed since the last check
    /// </summary>
    public async Task CheckForNewVersionAsync(bool forceCheck = false)
    {
        // Skip if checks are disabled
        // Check environment variable first (Docker-friendly)
        var envEnabled = Environment.GetEnvironmentVariable("CONDUIT_VERSION_CHECK_ENABLED");
        bool isEnabled = true; // Enabled by default
        
        if (!string.IsNullOrEmpty(envEnabled))
        {
            isEnabled = envEnabled.ToLowerInvariant() == "true";
        }
        else 
        {
            // Fall back to configuration
            isEnabled = _configuration.GetValue<bool>("VersionCheck:Enabled", true);
        }
        
        if (!isEnabled)
        {
            return;
        }
    
        // Check if we need to perform a version check
        if (!forceCheck && DateTime.UtcNow - _lastCheck < _checkInterval)
        {
            return;
        }
        
        _lastCheck = DateTime.UtcNow;
        
        try
        {
            var client = _httpClientFactory.CreateClient("GithubApi");
            
            // Use GitHub releases API to get the latest release
            var latestRelease = await client.GetFromJsonAsync<GitHubRelease>(
                "https://api.github.com/repos/knnlabs/Conduit/releases/latest");
                
            if (latestRelease == null)
            {
                _logger.LogWarning("Failed to get latest release information");
                return;
            }
            
            var latestVersion = latestRelease.TagName;
            if (latestVersion.StartsWith('v'))
            {
                latestVersion = latestVersion[1..]; // Remove 'v' prefix if present
            }
            
            _logger.LogInformation("Latest version: {LatestVersion}, Current version: {CurrentVersion}", 
                latestVersion, _currentVersion);
                
            // Compare versions
            if (IsNewerVersion(latestVersion, _currentVersion))
            {
                _notificationService.AddNotification(
                    NotificationType.System,
                    $"New version available: {latestVersion}",
                    "Version Update",
                    $"You are currently running version {_currentVersion}. " +
                    $"A new version ({latestVersion}) is available. " +
                    $"Release notes: {latestRelease.HtmlUrl}");
                    
                _logger.LogInformation("New version notification added");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for new version");
        }
    }

    /// <summary>
    /// Determines if the latest version is newer than the current version
    /// </summary>
    private bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        try
        {
            // Parse versions 
            if (!Version.TryParse(latestVersion, out var latest))
            {
                return false;
            }
            
            if (!Version.TryParse(currentVersion, out var current))
            {
                return false;
            }
            
            return latest > current;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing versions");
            return false;
        }
    }
    
    /// <summary>
    /// Model for GitHub release API response
    /// </summary>
    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
        
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }
        
        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }
}