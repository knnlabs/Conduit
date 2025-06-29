using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Providers;

public class ProviderListView : View
{
    private readonly AdminApiService _adminApiService;
    private readonly StateManager _stateManager;
    private readonly ILogger<ProviderListView> _logger;
    
    private ListView _providerList = null!;
    private Label _statusLabel = null!;
    private Button _addButton = null!;
    private Button _editButton = null!;
    private Button _deleteButton = null!;
    private Button _discoverButton = null!;
    private Button _refreshButton = null!;
    
    private List<ProviderCredentialDto> _providers = new();

    public ProviderListView(IServiceProvider serviceProvider)
    {
        _adminApiService = serviceProvider.GetRequiredService<AdminApiService>();
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _logger = serviceProvider.GetRequiredService<ILogger<ProviderListView>>();

        InitializeUI();
        LoadProviders();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Provider list
        var listFrame = new FrameView("Provider Credentials")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        _providerList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _providerList.SelectedItemChanged += OnProviderSelected;

        listFrame.Add(_providerList);

        // Button panel
        var buttonPanel = new View()
        {
            X = 0,
            Y = Pos.Bottom(listFrame),
            Width = Dim.Fill(),
            Height = 3
        };

        _addButton = new Button("Add")
        {
            X = 0,
            Y = 0
        };
        _addButton.Clicked += AddProvider;

        _editButton = new Button("Edit")
        {
            X = Pos.Right(_addButton) + 1,
            Y = 0,
            Enabled = false
        };
        _editButton.Clicked += EditProvider;

        _deleteButton = new Button("Delete")
        {
            X = Pos.Right(_editButton) + 1,
            Y = 0,
            Enabled = false
        };
        _deleteButton.Clicked += DeleteProvider;

        _discoverButton = new Button("Discover Models")
        {
            X = Pos.Right(_deleteButton) + 1,
            Y = 0
        };
        _discoverButton.Clicked += DiscoverModels;

        _refreshButton = new Button("Refresh")
        {
            X = Pos.Right(_discoverButton) + 1,
            Y = 0
        };
        _refreshButton.Clicked += () => LoadProviders();

        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        buttonPanel.Add(_addButton, _editButton, _deleteButton, _discoverButton, _refreshButton, _statusLabel);

        Add(listFrame, buttonPanel);
    }

    private async void LoadProviders()
    {
        try
        {
            UpdateStatus("Loading providers...");
            _providers = await _adminApiService.GetProvidersAsync();
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateProviderList();
                UpdateStatus($"Loaded {_providers.Count} providers");
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Connection Failed"))
        {
            _logger.LogError(ex, "Connection failed while loading providers");
            Application.MainLoop.Invoke(() => UpdateStatus("Connection failed - Admin API unavailable"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load providers");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private void UpdateProviderList()
    {
        var items = _providers.Select(p => 
            $"{p.ProviderName} - {(p.IsEnabled ? "Enabled" : "Disabled")}"
        ).ToList();
        
        _providerList.SetSource(items);
    }

    private void OnProviderSelected(ListViewItemEventArgs e)
    {
        var hasSelection = e.Item >= 0 && e.Item < _providers.Count;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddProvider()
    {
        var dialog = new ProviderEditDialog(null);
        Application.Run(dialog);

        if (dialog.CreateResult != null)
        {
            CreateProvider(dialog.CreateResult);
        }
    }

    private void EditProvider()
    {
        if (_providerList.SelectedItem < 0 || _providerList.SelectedItem >= _providers.Count)
            return;

        var provider = _providers[_providerList.SelectedItem];
        var dialog = new ProviderEditDialog(provider);
        Application.Run(dialog);

        if (dialog.UpdateResult != null)
        {
            UpdateProvider(provider.Id, dialog.UpdateResult);
        }
    }

    private async void CreateProvider(CreateProviderCredentialDto createDto)
    {
        try
        {
            UpdateStatus("Creating provider...");
            var provider = await _adminApiService.CreateProviderAsync(createDto);
            
            Application.MainLoop.Invoke(() =>
            {
                _providers.Add(provider);
                UpdateProviderList();
                UpdateStatus($"Created provider: {provider.ProviderName}");
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Connection Failed"))
        {
            _logger.LogError(ex, "Connection failed while creating provider");
            Application.MainLoop.Invoke(() => UpdateStatus("Failed to create provider - connection error"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create provider");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private async void UpdateProvider(int id, UpdateProviderCredentialDto updateDto)
    {
        try
        {
            UpdateStatus("Updating provider...");
            var provider = await _adminApiService.UpdateProviderAsync(id, updateDto);
            
            Application.MainLoop.Invoke(() =>
            {
                var index = _providers.FindIndex(p => p.Id == id);
                if (index >= 0)
                {
                    _providers[index] = provider;
                    UpdateProviderList();
                }
                UpdateStatus($"Updated provider: {provider.ProviderName}");
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Connection Failed"))
        {
            _logger.LogError(ex, "Connection failed while updating provider");
            Application.MainLoop.Invoke(() => UpdateStatus("Failed to update provider - connection error"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update provider");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private async void DeleteProvider()
    {
        if (_providerList.SelectedItem < 0 || _providerList.SelectedItem >= _providers.Count)
            return;

        var provider = _providers[_providerList.SelectedItem];
        
        var confirm = MessageBox.Query("Delete Provider", 
            $"Are you sure you want to delete provider '{provider.ProviderName}'?", 
            "Yes", "No");
        
        if (confirm == 0)
        {
            try
            {
                UpdateStatus("Deleting provider...");
                await _adminApiService.DeleteProviderAsync(provider.Id);
                
                Application.MainLoop.Invoke(() =>
                {
                    _providers.RemoveAt(_providerList.SelectedItem);
                    UpdateProviderList();
                    UpdateStatus($"Deleted provider: {provider.ProviderName}");
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Connection Failed"))
            {
                _logger.LogError(ex, "Connection failed while deleting provider");
                Application.MainLoop.Invoke(() => UpdateStatus("Failed to delete provider - connection error"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete provider");
                Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
            }
        }
    }

    private async void DiscoverModels()
    {
        try
        {
            UpdateStatus("Discovering models...");
            var capabilities = await _adminApiService.DiscoverModelsAsync();
            
            Application.MainLoop.Invoke(() =>
            {
                var totalModels = capabilities.Values.Sum(list => list.Count);
                UpdateStatus($"Discovered {totalModels} models across {capabilities.Count} providers");
                LoadProviders(); // Refresh to show updated model counts
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Connection Failed"))
        {
            _logger.LogError(ex, "Connection failed while discovering models");
            Application.MainLoop.Invoke(() => UpdateStatus("Model discovery failed - connection error"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover models");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }
}