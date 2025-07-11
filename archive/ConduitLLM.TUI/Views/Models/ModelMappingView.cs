using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Models;

public class ModelMappingView : View
{
    private readonly AdminApiService _adminApiService;
    private readonly StateManager _stateManager;
    private readonly SignalRService _signalRService;
    private readonly ILogger<ModelMappingView> _logger;
    
    private ListView _mappingList = null!;
    private Label _statusLabel = null!;
    private Label _realTimeLabel = null!;
    private Button _addButton = null!;
    private Button _editButton = null!;
    private Button _deleteButton = null!;
    private Button _refreshButton = null!;
    
    private List<ModelProviderMappingDto> _mappings = new();

    public ModelMappingView(IServiceProvider serviceProvider)
    {
        _adminApiService = serviceProvider.GetRequiredService<AdminApiService>();
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _logger = serviceProvider.GetRequiredService<ILogger<ModelMappingView>>();

        InitializeUI();
        SetupEventHandlers();
        LoadMappings();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Real-time status indicator
        _realTimeLabel = new Label("⚡ Real-time updates enabled")
        {
            X = 0,
            Y = 0,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Green, Color.Black)
            }
        };

        // Mapping list
        var listFrame = new FrameView("Model Mappings")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(4)
        };

        _mappingList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _mappingList.SelectedItemChanged += OnMappingSelected;

        listFrame.Add(_mappingList);

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
        _addButton.Clicked += AddMapping;

        _editButton = new Button("Edit")
        {
            X = Pos.Right(_addButton) + 1,
            Y = 0,
            Enabled = false
        };
        _editButton.Clicked += EditMapping;

        _deleteButton = new Button("Delete")
        {
            X = Pos.Right(_editButton) + 1,
            Y = 0,
            Enabled = false
        };
        _deleteButton.Clicked += DeleteMapping;

        _refreshButton = new Button("Refresh")
        {
            X = Pos.Right(_deleteButton) + 1,
            Y = 0
        };
        _refreshButton.Clicked += () => LoadMappings();

        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        buttonPanel.Add(_addButton, _editButton, _deleteButton, _refreshButton, _statusLabel);

        Add(_realTimeLabel, listFrame, buttonPanel);
    }

    private void SetupEventHandlers()
    {
        // Subscribe to SignalR navigation state updates
        _signalRService.NavigationStateUpdated += OnNavigationStateUpdated;
        
        // Subscribe to state manager changes
        _stateManager.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(StateManager.ModelMappings))
            {
                Application.MainLoop.Invoke(() => UpdateMappingList());
            }
            else if (e.PropertyName == nameof(StateManager.IsConnected))
            {
                Application.MainLoop.Invoke(() => UpdateRealTimeStatus());
            }
        };
    }

    private async void LoadMappings()
    {
        try
        {
            UpdateStatus("Loading model mappings...");
            _mappings = await _adminApiService.GetModelMappingsAsync();
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateMappingList();
                UpdateStatus($"Loaded {_mappings.Count} mappings");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model mappings");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private void UpdateMappingList()
    {
        _mappings = _stateManager.ModelMappings;
        
        var items = _mappings.Select(m => 
            $"{m.ModelId} → {m.ProviderModelId} ({m.ProviderId}) - {(m.IsEnabled ? "Enabled" : "Disabled")}"
        ).ToList();
        
        _mappingList.SetSource(items);
    }

    private void OnMappingSelected(ListViewItemEventArgs e)
    {
        var hasSelection = e.Item >= 0 && e.Item < _mappings.Count;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void AddMapping()
    {
        var dialog = new ModelMappingEditDialog(_stateManager, null);
        Application.Run(dialog);

        if (dialog.Result != null)
        {
            CreateMapping(dialog.Result);
        }
    }

    private void EditMapping()
    {
        if (_mappingList.SelectedItem < 0 || _mappingList.SelectedItem >= _mappings.Count)
            return;

        var mapping = _mappings[_mappingList.SelectedItem];
        var dialog = new ModelMappingEditDialog(_stateManager, mapping);
        Application.Run(dialog);

        if (dialog.Result != null)
        {
            // Convert CreateModelProviderMappingDto to UpdateModelProviderMappingDto
            var updateRequest = new ConduitLLM.AdminClient.Models.UpdateModelProviderMappingDto
            {
                Priority = 50, // Default priority
                IsEnabled = dialog.Result.IsEnabled,
                MaxContextLength = dialog.Result.MaxContextLength
            };
            UpdateMapping(mapping.Id, updateRequest);
        }
    }

    private async void CreateMapping(CreateModelProviderMappingDto createDto)
    {
        try
        {
            UpdateStatus("Creating model mapping...");
            var mapping = await _adminApiService.CreateModelMappingAsync(createDto);
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateStatus($"Created mapping: {mapping.ModelId} → {mapping.ProviderModelId}");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create model mapping");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private async void UpdateMapping(int id, UpdateModelProviderMappingDto updateDto)
    {
        try
        {
            UpdateStatus("Updating model mapping...");
            var mapping = await _adminApiService.UpdateModelMappingAsync(id, updateDto);
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateStatus($"Updated mapping: {mapping.ModelId} → {mapping.ProviderModelId}");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update model mapping");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private async void DeleteMapping()
    {
        if (_mappingList.SelectedItem < 0 || _mappingList.SelectedItem >= _mappings.Count)
            return;

        var mapping = _mappings[_mappingList.SelectedItem];
        
        var confirm = MessageBox.Query("Delete Mapping", 
            $"Are you sure you want to delete mapping '{mapping.ModelId} → {mapping.ProviderModelId}'?", 
            "Yes", "No");
        
        if (confirm == 0)
        {
            try
            {
                UpdateStatus("Deleting model mapping...");
                await _adminApiService.DeleteModelMappingAsync(mapping.Id);
                
                Application.MainLoop.Invoke(() =>
                {
                    UpdateStatus($"Deleted mapping: {mapping.ModelId} → {mapping.ProviderModelId}");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete model mapping");
                Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
            }
        }
    }

    private void OnNavigationStateUpdated(object? sender, NavigationStateUpdateDto e)
    {
        Application.MainLoop.Invoke(() =>
        {
            _mappings = e.ModelMappings;
            UpdateMappingList();
            UpdateStatus("Real-time update received");
        });
    }

    private void UpdateRealTimeStatus()
    {
        if (_stateManager.IsConnected)
        {
            _realTimeLabel.Text = "⚡ Real-time updates enabled";
            _realTimeLabel.ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Green, Color.Black)
            };
        }
        else
        {
            _realTimeLabel.Text = "⚠ Real-time updates disconnected";
            _realTimeLabel.ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Black)
            };
        }
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }
}