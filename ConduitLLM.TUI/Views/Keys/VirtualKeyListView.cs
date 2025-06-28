using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Keys;

public class VirtualKeyListView : View
{
    private readonly AdminApiService _adminApiService;
    private readonly StateManager _stateManager;
    private readonly ILogger<VirtualKeyListView> _logger;
    
    private ListView _keyList;
    private Label _statusLabel;
    private Label _selectedKeyLabel;
    private Button _addButton;
    private Button _editButton;
    private Button _deleteButton;
    private Button _selectButton;
    private Button _refreshButton;
    
    private List<VirtualKeyDto> _keys = new();

    public VirtualKeyListView(IServiceProvider serviceProvider)
    {
        _adminApiService = serviceProvider.GetRequiredService<AdminApiService>();
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _logger = serviceProvider.GetRequiredService<ILogger<VirtualKeyListView>>();

        InitializeUI();
        LoadKeys();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Selected key display
        _selectedKeyLabel = new Label($"Selected Key: {_stateManager.SelectedVirtualKey ?? "None"}")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Cyan, Color.Black)
            }
        };

        // Key list
        var listFrame = new FrameView("Virtual Keys")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(4)
        };

        _keyList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _keyList.SelectedItemChanged += OnKeySelected;

        listFrame.Add(_keyList);

        // Button panel
        var buttonPanel = new View()
        {
            X = 0,
            Y = Pos.Bottom(listFrame),
            Width = Dim.Fill(),
            Height = 3
        };

        _selectButton = new Button("Select for Use")
        {
            X = 0,
            Y = 0,
            Enabled = false
        };
        _selectButton.Clicked += SelectKey;

        _addButton = new Button("Add")
        {
            X = Pos.Right(_selectButton) + 1,
            Y = 0
        };
        _addButton.Clicked += AddKey;

        _editButton = new Button("Edit")
        {
            X = Pos.Right(_addButton) + 1,
            Y = 0,
            Enabled = false
        };
        _editButton.Clicked += EditKey;

        _deleteButton = new Button("Delete")
        {
            X = Pos.Right(_editButton) + 1,
            Y = 0,
            Enabled = false
        };
        _deleteButton.Clicked += DeleteKey;

        _refreshButton = new Button("Refresh")
        {
            X = Pos.Right(_deleteButton) + 1,
            Y = 0
        };
        _refreshButton.Clicked += () => LoadKeys();

        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        buttonPanel.Add(_selectButton, _addButton, _editButton, _deleteButton, _refreshButton, _statusLabel);

        Add(_selectedKeyLabel, listFrame, buttonPanel);
    }

    private async void LoadKeys()
    {
        try
        {
            UpdateStatus("Loading virtual keys...");
            _keys = await _adminApiService.GetVirtualKeysAsync();
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateKeyList();
                UpdateStatus($"Loaded {_keys.Count} keys");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load virtual keys");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private void UpdateKeyList()
    {
        var items = _keys.Select(k => 
        {
            var status = k.IsEnabled ? "Enabled" : "Disabled";
            var budget = k.MaxBudget > 0 ? $"${k.MaxBudget:F2}" : "Unlimited";
            var spent = $"${k.CurrentSpend:F2}";
            var modelCount = string.IsNullOrEmpty(k.AllowedModels) ? 0 : k.AllowedModels.Split(',').Length;
            var models = modelCount > 0 ? $"{modelCount} models" : "All models";
            
            return $"{k.KeyName} - {status} - Budget: {budget} - Spent: {spent} - {models}";
        }).ToList();
        
        _keyList.SetSource(items);
    }

    private void OnKeySelected(ListViewItemEventArgs e)
    {
        var hasSelection = e.Item >= 0 && e.Item < _keys.Count;
        _selectButton.Enabled = hasSelection;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    private void SelectKey()
    {
        if (_keyList.SelectedItem < 0 || _keyList.SelectedItem >= _keys.Count)
            return;

        var key = _keys[_keyList.SelectedItem];
        _stateManager.SelectedVirtualKey = key.ApiKey ?? key.KeyPrefix ?? key.KeyName;
        _selectedKeyLabel.Text = $"Selected Key: {key.KeyName}";
        UpdateStatus($"Selected key: {key.KeyName}");
    }

    private void AddKey()
    {
        var dialog = new KeyEditDialog(null);
        Application.Run(dialog);

        if (dialog.Result != null)
        {
            CreateKey(dialog.Result);
        }
    }

    private void EditKey()
    {
        if (_keyList.SelectedItem < 0 || _keyList.SelectedItem >= _keys.Count)
            return;

        var key = _keys[_keyList.SelectedItem];
        var dialog = new KeyEditDialog(key);
        Application.Run(dialog);

        if (dialog.Result != null)
        {
            // Convert CreateVirtualKeyRequest to UpdateVirtualKeyRequest
            var updateRequest = new ConduitLLM.AdminClient.Models.UpdateVirtualKeyRequest
            {
                KeyName = dialog.Result.KeyName,
                AllowedModels = dialog.Result.AllowedModels,
                MaxBudget = dialog.Result.MaxBudget
            };
            UpdateKey(key.Id, updateRequest);
        }
    }

    private async void CreateKey(CreateVirtualKeyRequest createDto)
    {
        try
        {
            UpdateStatus("Creating virtual key...");
            var response = await _adminApiService.CreateVirtualKeyAsync(createDto);
            
            Application.MainLoop.Invoke(() =>
            {
                // Reload keys to get the new key with all details
                LoadKeys();
                UpdateStatus($"Created key: {response.KeyInfo.KeyName}");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create virtual key");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private async void UpdateKey(int id, UpdateVirtualKeyRequest updateDto)
    {
        try
        {
            UpdateStatus("Updating virtual key...");
            var key = await _adminApiService.UpdateVirtualKeyAsync(id, updateDto);
            
            Application.MainLoop.Invoke(() =>
            {
                var index = _keys.FindIndex(k => k.Id == id);
                if (index >= 0)
                {
                    _keys[index] = key;
                    UpdateKeyList();
                }
                UpdateStatus($"Updated key: {key.KeyName}");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update virtual key");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private async void DeleteKey()
    {
        if (_keyList.SelectedItem < 0 || _keyList.SelectedItem >= _keys.Count)
            return;

        var key = _keys[_keyList.SelectedItem];
        
        var confirm = MessageBox.Query("Delete Key", 
            $"Are you sure you want to delete key '{key.KeyName}'?", 
            "Yes", "No");
        
        if (confirm == 0)
        {
            try
            {
                UpdateStatus("Deleting virtual key...");
                await _adminApiService.DeleteVirtualKeyAsync(key.Id);
                
                Application.MainLoop.Invoke(() =>
                {
                    _keys.RemoveAt(_keyList.SelectedItem);
                    UpdateKeyList();
                    
                    // Clear selection if deleted key was selected
                    if (_stateManager.SelectedVirtualKey == key.ApiKey)
                    {
                        _stateManager.SelectedVirtualKey = null;
                        _selectedKeyLabel.Text = "Selected Key: None";
                    }
                    
                    UpdateStatus($"Deleted key: {key.KeyName}");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete virtual key");
                Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
            }
        }
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }
}