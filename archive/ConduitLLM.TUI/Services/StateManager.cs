using System.ComponentModel;
using System.Runtime.CompilerServices;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.TUI.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.TUI.Services;

public class StateManager : INotifyPropertyChanged
{
    private readonly NavigationStateService _navigationStateService;
    private readonly ILogger<StateManager> _logger;
    
    private List<ProviderCredentialDto> _providers = new();
    private List<ModelProviderMappingDto> _modelMappings = new();
    private List<VirtualKeyDto> _virtualKeys = new();
    private Dictionary<string, List<ModelCapabilityDto>> _modelCapabilities = new();
    private NavigationStateDto? _navigationState;
    private string? _selectedVirtualKey;
    private string? _selectedModel;
    private bool _isConnected;
    private DateTime _lastNavigationRefresh = DateTime.MinValue;

    public StateManager(NavigationStateService navigationStateService, ILogger<StateManager> logger)
    {
        _navigationStateService = navigationStateService;
        _logger = logger;
    }

    public List<ProviderCredentialDto> Providers
    {
        get => _providers;
        set { _providers = value; OnPropertyChanged(); }
    }

    public List<ModelProviderMappingDto> ModelMappings
    {
        get => _modelMappings;
        set { _modelMappings = value; OnPropertyChanged(); }
    }

    public List<VirtualKeyDto> VirtualKeys
    {
        get => _virtualKeys;
        set { _virtualKeys = value; OnPropertyChanged(); }
    }

    public Dictionary<string, List<ModelCapabilityDto>> ModelCapabilities
    {
        get => _modelCapabilities;
        set { _modelCapabilities = value; OnPropertyChanged(); }
    }

    public string? SelectedVirtualKey
    {
        get => _selectedVirtualKey;
        set { _selectedVirtualKey = value; OnPropertyChanged(); }
    }

    public string? SelectedModel
    {
        get => _selectedModel;
        set { _selectedModel = value; OnPropertyChanged(); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    public NavigationStateDto? NavigationState
    {
        get => _navigationState;
        private set { _navigationState = value; OnPropertyChanged(); }
    }

    public DateTime LastNavigationRefresh
    {
        get => _lastNavigationRefresh;
        private set { _lastNavigationRefresh = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void UpdateProvider(ProviderCredentialDto provider)
    {
        var index = _providers.FindIndex(p => p.Id == provider.Id);
        if (index >= 0)
        {
            _providers[index] = provider;
        }
        else
        {
            _providers.Add(provider);
        }
        OnPropertyChanged(nameof(Providers));
    }

    public void RemoveProvider(int providerId)
    {
        _providers.RemoveAll(p => p.Id == providerId);
        OnPropertyChanged(nameof(Providers));
    }

    public void UpdateModelMapping(ModelProviderMappingDto mapping)
    {
        var index = _modelMappings.FindIndex(m => m.Id == mapping.Id);
        if (index >= 0)
        {
            _modelMappings[index] = mapping;
        }
        else
        {
            _modelMappings.Add(mapping);
        }
        OnPropertyChanged(nameof(ModelMappings));
    }

    public void RemoveModelMapping(int mappingId)
    {
        _modelMappings.RemoveAll(m => m.Id == mappingId);
        OnPropertyChanged(nameof(ModelMappings));
    }

    /// <summary>
    /// Loads the navigation state asynchronously from the navigation state service.
    /// </summary>
    /// <param name="forceRefresh">Whether to force a refresh even if cache is valid.</param>
    /// <returns>The loaded navigation state.</returns>
    public async Task<NavigationStateDto> LoadNavigationStateAsync(bool forceRefresh = false)
    {
        try
        {
            _logger.LogDebug("Loading navigation state (forceRefresh: {ForceRefresh})", forceRefresh);
            
            var navigationState = await _navigationStateService.GetNavigationStateAsync(forceRefresh);
            
            NavigationState = navigationState;
            LastNavigationRefresh = DateTime.UtcNow;
            
            _logger.LogInformation("Navigation state loaded successfully. {AvailableCount} capabilities available", 
                navigationState.Sections.AvailableCount);
            
            return navigationState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load navigation state");
            
            // Create a fallback navigation state with error information
            var fallbackState = new NavigationStateDto
            {
                ErrorMessage = $"Failed to load navigation state: {ex.Message}",
                LastRefreshed = DateTime.UtcNow,
                Sections = new UISectionAvailability(),
                ProviderDetails = new List<ProviderNavigationInfo>()
            };
            
            NavigationState = fallbackState;
            LastNavigationRefresh = DateTime.UtcNow;
            
            return fallbackState;
        }
    }

    /// <summary>
    /// Invalidates the navigation state cache, forcing a refresh on next load.
    /// </summary>
    public void InvalidateNavigationStateCache()
    {
        _logger.LogDebug("Invalidating navigation state cache");
        _navigationStateService.InvalidateCache();
    }

    /// <summary>
    /// Updates the state when navigation-related data changes and invalidates cache.
    /// </summary>
    public void OnNavigationDataChanged()
    {
        _logger.LogDebug("Navigation data changed, invalidating cache");
        InvalidateNavigationStateCache();
        OnPropertyChanged(nameof(NavigationState));
    }
}