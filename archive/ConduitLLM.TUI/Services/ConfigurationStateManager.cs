using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ConduitLLM.AdminClient.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.TUI.Services;

/// <summary>
/// Centralized state manager for all configuration data.
/// Provides reactive updates for configuration views and handles real-time synchronization.
/// </summary>
public class ConfigurationStateManager : INotifyPropertyChanged
{
    private readonly ILogger<ConfigurationStateManager> _logger;

    #region Global Settings

    private ObservableCollection<GlobalSettingDto> _globalSettings = new();
    /// <summary>
    /// Global system settings.
    /// </summary>
    public ObservableCollection<GlobalSettingDto> GlobalSettings
    {
        get => _globalSettings;
        set => SetProperty(ref _globalSettings, value);
    }

    #endregion

    #region HTTP Client Configuration

    private HttpClientConfigurationDto? _httpClientConfiguration;
    /// <summary>
    /// HTTP client configuration settings.
    /// </summary>
    public HttpClientConfigurationDto? HttpClientConfiguration
    {
        get => _httpClientConfiguration;
        set => SetProperty(ref _httpClientConfiguration, value);
    }

    #endregion

    #region Cache Configuration

    private CacheConfigurationDto? _cacheConfiguration;
    /// <summary>
    /// Cache configuration settings.
    /// </summary>
    public CacheConfigurationDto? CacheConfiguration
    {
        get => _cacheConfiguration;
        set => SetProperty(ref _cacheConfiguration, value);
    }

    #endregion

    #region Router Configuration

    private RouterConfigurationDto? _routerConfiguration;
    /// <summary>
    /// Router configuration and load balancing settings.
    /// </summary>
    public RouterConfigurationDto? RouterConfiguration
    {
        get => _routerConfiguration;
        set => SetProperty(ref _routerConfiguration, value);
    }

    #endregion

    #region IP Filtering

    private IpFilterSettingsDto? _ipFilterSettings;
    /// <summary>
    /// IP filtering global settings.
    /// </summary>
    public IpFilterSettingsDto? IpFilterSettings
    {
        get => _ipFilterSettings;
        set => SetProperty(ref _ipFilterSettings, value);
    }

    private ObservableCollection<IpFilterDto> _ipFilterRules = new();
    /// <summary>
    /// IP filter rules.
    /// </summary>
    public ObservableCollection<IpFilterDto> IpFilterRules
    {
        get => _ipFilterRules;
        set => SetProperty(ref _ipFilterRules, value);
    }

    #endregion

    #region Audio Configuration

    private ObservableCollection<AudioProviderConfigDto> _audioConfigurations = new();
    /// <summary>
    /// Audio provider configurations.
    /// </summary>
    public ObservableCollection<AudioProviderConfigDto> AudioConfigurations
    {
        get => _audioConfigurations;
        set => SetProperty(ref _audioConfigurations, value);
    }

    #endregion

    #region System Information

    private SystemInfoDto? _systemInfo;
    /// <summary>
    /// System information and health status.
    /// </summary>
    public SystemInfoDto? SystemInfo
    {
        get => _systemInfo;
        set => SetProperty(ref _systemInfo, value);
    }

    private HealthStatusDto? _systemHealth;
    /// <summary>
    /// System health status and metrics.
    /// </summary>
    public HealthStatusDto? SystemHealth
    {
        get => _systemHealth;
        set => SetProperty(ref _systemHealth, value);
    }

    #endregion

    #region Loading States

    private bool _isLoadingGlobalSettings;
    /// <summary>
    /// Whether global settings are currently being loaded.
    /// </summary>
    public bool IsLoadingGlobalSettings
    {
        get => _isLoadingGlobalSettings;
        set => SetProperty(ref _isLoadingGlobalSettings, value);
    }

    private bool _isLoadingHttpClientConfig;
    /// <summary>
    /// Whether HTTP client configuration is currently being loaded.
    /// </summary>
    public bool IsLoadingHttpClientConfig
    {
        get => _isLoadingHttpClientConfig;
        set => SetProperty(ref _isLoadingHttpClientConfig, value);
    }

    private bool _isLoadingCacheConfig;
    /// <summary>
    /// Whether cache configuration is currently being loaded.
    /// </summary>
    public bool IsLoadingCacheConfig
    {
        get => _isLoadingCacheConfig;
        set => SetProperty(ref _isLoadingCacheConfig, value);
    }

    private bool _isLoadingRouterConfig;
    /// <summary>
    /// Whether router configuration is currently being loaded.
    /// </summary>
    public bool IsLoadingRouterConfig
    {
        get => _isLoadingRouterConfig;
        set => SetProperty(ref _isLoadingRouterConfig, value);
    }

    private bool _isLoadingIpFilter;
    /// <summary>
    /// Whether IP filter configuration is currently being loaded.
    /// </summary>
    public bool IsLoadingIpFilter
    {
        get => _isLoadingIpFilter;
        set => SetProperty(ref _isLoadingIpFilter, value);
    }

    private bool _isLoadingAudioConfig;
    /// <summary>
    /// Whether audio configuration is currently being loaded.
    /// </summary>
    public bool IsLoadingAudioConfig
    {
        get => _isLoadingAudioConfig;
        set => SetProperty(ref _isLoadingAudioConfig, value);
    }

    private bool _isLoadingSystemInfo;
    /// <summary>
    /// Whether system information is currently being loaded.
    /// </summary>
    public bool IsLoadingSystemInfo
    {
        get => _isLoadingSystemInfo;
        set => SetProperty(ref _isLoadingSystemInfo, value);
    }

    #endregion

    #region Last Updated Timestamps

    private DateTime? _globalSettingsLastUpdated;
    /// <summary>
    /// When global settings were last updated.
    /// </summary>
    public DateTime? GlobalSettingsLastUpdated
    {
        get => _globalSettingsLastUpdated;
        set => SetProperty(ref _globalSettingsLastUpdated, value);
    }

    private DateTime? _httpClientConfigLastUpdated;
    /// <summary>
    /// When HTTP client configuration was last updated.
    /// </summary>
    public DateTime? HttpClientConfigLastUpdated
    {
        get => _httpClientConfigLastUpdated;
        set => SetProperty(ref _httpClientConfigLastUpdated, value);
    }

    private DateTime? _cacheConfigLastUpdated;
    /// <summary>
    /// When cache configuration was last updated.
    /// </summary>
    public DateTime? CacheConfigLastUpdated
    {
        get => _cacheConfigLastUpdated;
        set => SetProperty(ref _cacheConfigLastUpdated, value);
    }

    private DateTime? _systemInfoLastUpdated;
    /// <summary>
    /// When system information was last updated.
    /// </summary>
    public DateTime? SystemInfoLastUpdated
    {
        get => _systemInfoLastUpdated;
        set => SetProperty(ref _systemInfoLastUpdated, value);
    }

    #endregion

    public ConfigurationStateManager(ILogger<ConfigurationStateManager> logger)
    {
        _logger = logger;
    }

    #region Helper Methods

    /// <summary>
    /// Update a global setting value and trigger property change notification.
    /// </summary>
    public void UpdateGlobalSetting(string key, string value)
    {
        var setting = GlobalSettings.FirstOrDefault(s => s.Key == key);
        if (setting != null)
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            OnPropertyChanged(nameof(GlobalSettings));
            GlobalSettingsLastUpdated = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Add or update a global setting.
    /// </summary>
    public void AddOrUpdateGlobalSetting(GlobalSettingDto setting)
    {
        var existingIndex = GlobalSettings.ToList().FindIndex(s => s.Key == setting.Key);
        if (existingIndex >= 0)
        {
            GlobalSettings[existingIndex] = setting;
        }
        else
        {
            GlobalSettings.Add(setting);
        }
        GlobalSettingsLastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Remove a global setting.
    /// </summary>
    public void RemoveGlobalSetting(string key)
    {
        var setting = GlobalSettings.FirstOrDefault(s => s.Key == key);
        if (setting != null)
        {
            GlobalSettings.Remove(setting);
            GlobalSettingsLastUpdated = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Add or update an IP filter rule.
    /// </summary>
    public void AddOrUpdateIpFilterRule(IpFilterDto rule)
    {
        var existingIndex = IpFilterRules.ToList().FindIndex(r => r.Id == rule.Id);
        if (existingIndex >= 0)
        {
            IpFilterRules[existingIndex] = rule;
        }
        else
        {
            IpFilterRules.Add(rule);
        }
    }

    /// <summary>
    /// Remove an IP filter rule.
    /// </summary>
    public void RemoveIpFilterRule(int ruleId)
    {
        var rule = IpFilterRules.FirstOrDefault(r => r.Id == ruleId);
        if (rule != null)
        {
            IpFilterRules.Remove(rule);
        }
    }

    /// <summary>
    /// Add or update an audio configuration.
    /// </summary>
    public void AddOrUpdateAudioConfiguration(AudioProviderConfigDto config)
    {
        var existingIndex = AudioConfigurations.ToList().FindIndex(c => c.Name == config.Name);
        if (existingIndex >= 0)
        {
            AudioConfigurations[existingIndex] = config;
        }
        else
        {
            AudioConfigurations.Add(config);
        }
    }

    /// <summary>
    /// Remove an audio configuration.
    /// </summary>
    public void RemoveAudioConfiguration(string provider)
    {
        var config = AudioConfigurations.FirstOrDefault(c => c.Name == provider);
        if (config != null)
        {
            AudioConfigurations.Remove(config);
        }
    }

    /// <summary>
    /// Clear all configuration data.
    /// </summary>
    public void ClearAll()
    {
        GlobalSettings.Clear();
        HttpClientConfiguration = null;
        CacheConfiguration = null;
        RouterConfiguration = null;
        IpFilterSettings = null;
        IpFilterRules.Clear();
        AudioConfigurations.Clear();
        SystemInfo = null;
        SystemHealth = null;
        
        // Reset loading states
        IsLoadingGlobalSettings = false;
        IsLoadingHttpClientConfig = false;
        IsLoadingCacheConfig = false;
        IsLoadingRouterConfig = false;
        IsLoadingIpFilter = false;
        IsLoadingAudioConfig = false;
        IsLoadingSystemInfo = false;
        
        // Reset timestamps
        GlobalSettingsLastUpdated = null;
        HttpClientConfigLastUpdated = null;
        CacheConfigLastUpdated = null;
        SystemInfoLastUpdated = null;
    }

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}