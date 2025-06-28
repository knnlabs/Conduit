using System.ComponentModel;
using System.Runtime.CompilerServices;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.TUI.Models;

namespace ConduitLLM.TUI.Services;

public class StateManager : INotifyPropertyChanged
{
    private List<ProviderCredentialDto> _providers = new();
    private List<ModelProviderMappingDto> _modelMappings = new();
    private List<VirtualKeyDto> _virtualKeys = new();
    private Dictionary<string, List<ModelCapabilityDto>> _modelCapabilities = new();
    private string? _selectedVirtualKey;
    private string? _selectedModel;
    private bool _isConnected;

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
}