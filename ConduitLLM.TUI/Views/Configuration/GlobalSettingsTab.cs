using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// Configuration tab for managing global system settings.
/// Provides CRUD operations for system-wide configuration values.
/// </summary>
public class GlobalSettingsTab : ConfigurationTabBase
{
    public override string TabName => "Global Settings";
    
    private ListView _settingsListView = null!;
    private TextField _keyTextField = null!;
    private TextField _valueTextField = null!;
    private TextField _categoryTextField = null!;
    private Button _addButton = null!;
    private Button _updateButton = null!;
    private Button _deleteButton = null!;
    private Button _resetButton = null!;
    private Label _selectedSettingLabel = null!;
    
    private readonly List<GlobalSettingDto> _settings = new();
    private GlobalSettingDto? _selectedSetting;

    public GlobalSettingsTab(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        InitializeTabUI();
    }

    protected override void InitializeTabUI()
    {

        // Main layout - split into list on left, details on right
        var leftPanel = new FrameView("Settings List")
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(50),
            Height = Dim.Fill(3)
        };

        var rightPanel = new FrameView("Setting Details")
        {
            X = Pos.Right(leftPanel),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        // Settings list view
        _settingsListView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false
        };
        _settingsListView.SelectedItemChanged += OnSettingSelected;
        leftPanel.Add(_settingsListView);

        // Right panel - setting details
        var keyLabel = new Label("Key:")
        {
            X = 1,
            Y = 1
        };

        _keyTextField = new TextField
        {
            X = Pos.Right(keyLabel) + 1,
            Y = 1,
            Width = Dim.Fill(1)
        };

        var valueLabel = new Label("Value:")
        {
            X = 1,
            Y = 3
        };

        _valueTextField = new TextField
        {
            X = Pos.Right(valueLabel) + 1,
            Y = 3,
            Width = Dim.Fill(1)
        };

        var categoryLabel = new Label("Category:")
        {
            X = 1,
            Y = 5
        };

        _categoryTextField = new TextField
        {
            X = Pos.Right(categoryLabel) + 1,
            Y = 5,
            Width = Dim.Fill(1)
        };

        // Buttons
        _addButton = new Button("Add New")
        {
            X = 1,
            Y = 7
        };
        _addButton.Clicked += AddNewSetting;

        _updateButton = new Button("Update")
        {
            X = Pos.Right(_addButton) + 1,
            Y = 7,
            Enabled = false
        };
        _updateButton.Clicked += UpdateSetting;

        _deleteButton = new Button("Delete")
        {
            X = Pos.Right(_updateButton) + 1,
            Y = 7,
            Enabled = false
        };
        _deleteButton.Clicked += DeleteSetting;

        _resetButton = new Button("Reset")
        {
            X = Pos.Right(_deleteButton) + 1,
            Y = 7
        };
        _resetButton.Clicked += ResetForm;

        // Selected setting info
        _selectedSettingLabel = new Label("No setting selected")
        {
            X = 1,
            Y = 9,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.DarkGray, Color.Black)
            }
        };

        rightPanel.Add(keyLabel, _keyTextField, valueLabel, _valueTextField, 
                      categoryLabel, _categoryTextField, _addButton, _updateButton, 
                      _deleteButton, _resetButton, _selectedSettingLabel);

        Add(leftPanel, rightPanel);

        // Add text change handlers for validation
        _keyTextField.TextChanged += OnFormDataChanged;
        _valueTextField.TextChanged += OnFormDataChanged;
        _categoryTextField.TextChanged += OnFormDataChanged;
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            UpdateStatus("Loading global settings...");
            _stateManager.IsLoadingGlobalSettings = true;
            
            var settings = await _adminApiService.GetSettingsAsync();
            _settings.Clear();
            
            if (settings != null)
            {
                _settings.AddRange(settings);
                _stateManager.GlobalSettings.Clear();
                foreach (var setting in settings)
                {
                    _stateManager.GlobalSettings.Add(setting);
                }
            }

            RefreshListView();
            _stateManager.GlobalSettingsLastUpdated = DateTime.UtcNow;
            UpdateStatus($"Loaded {_settings.Count} global settings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load global settings");
            UpdateStatus($"Error loading settings: {ex.Message}");
        }
        finally
        {
            _stateManager.IsLoadingGlobalSettings = false;
        }
    }

    private void RefreshListView()
    {
        var displayItems = _settings
            .OrderBy(s => s.Category ?? "")
            .ThenBy(s => s.Key)
            .Select(s => $"{s.Key} = {s.Value}" + 
                        (string.IsNullOrEmpty(s.Category) ? "" : $" [{s.Category}]"))
            .ToList();

        _settingsListView.SetSource(displayItems);
        
        // Update selected setting if it's still in the list
        if (_selectedSetting != null)
        {
            var index = _settings.FindIndex(s => s.Key == _selectedSetting.Key);
            if (index >= 0)
            {
                _settingsListView.SelectedItem = index;
            }
        }
    }

    private void OnSettingSelected(ListViewItemEventArgs args)
    {
        if (args.Item >= 0 && args.Item < _settings.Count)
        {
            _selectedSetting = _settings[args.Item];
            PopulateForm(_selectedSetting);
            UpdateButtonStates();
        }
    }

    private void PopulateForm(GlobalSettingDto setting)
    {
        _keyTextField.Text = setting.Key ?? "";
        _valueTextField.Text = setting.Value ?? "";
        _categoryTextField.Text = setting.Category ?? "";
        
        _selectedSettingLabel.Text = $"Selected: {setting.Key} (Updated: {setting.UpdatedAt:yyyy-MM-dd HH:mm})";
        UpdateButtonStates();
    }

    private void OnFormDataChanged(NStack.ustring oldValue)
    {
        UpdateButtonStates();
        HasUnsavedChanges = true;
    }

    private void UpdateButtonStates()
    {
        var hasKey = !string.IsNullOrWhiteSpace(_keyTextField.Text.ToString());
        var hasValue = !string.IsNullOrWhiteSpace(_valueTextField.Text.ToString());
        var hasSelection = _selectedSetting != null;

        _addButton.Enabled = hasKey && hasValue && !HasSettingWithKey(_keyTextField.Text?.ToString() ?? string.Empty);
        _updateButton.Enabled = hasSelection && hasKey && hasValue && HasFormChanges();
        _deleteButton.Enabled = hasSelection;
    }

    private bool HasSettingWithKey(string key)
    {
        return _settings.Any(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasFormChanges()
    {
        if (_selectedSetting == null) return false;

        return _selectedSetting.Key != _keyTextField.Text.ToString() ||
               _selectedSetting.Value != _valueTextField.Text.ToString() ||
               (_selectedSetting.Category ?? "") != _categoryTextField.Text.ToString();
    }

    private async void AddNewSetting()
    {
        try
        {
            // Note: Setting creation not yet implemented in AdminApiService
            UpdateStatus("Setting creation not yet implemented");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add new setting");
            UpdateStatus($"Error adding setting: {ex.Message}");
        }
    }

    private async void UpdateSetting()
    {
        if (_selectedSetting == null) return;

        try
        {
            // Note: Setting updates not yet implemented in AdminApiService
            UpdateStatus("Setting updates not yet implemented");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update setting");
            UpdateStatus($"Error updating setting: {ex.Message}");
        }
    }

    private async void DeleteSetting()
    {
        if (_selectedSetting == null) return;

        var result = MessageBox.Query("Delete Setting", 
            $"Are you sure you want to delete the setting '{_selectedSetting.Key}'?", 
            "Yes", "No");
            
        if (result == 0) // Yes
        {
            try
            {
                // Note: Setting deletion not yet implemented in AdminApiService
                UpdateStatus("Setting deletion not yet implemented");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete setting");
                UpdateStatus($"Error deleting setting: {ex.Message}");
            }
        }
    }

    private void ResetForm()
    {
        _keyTextField.Text = "";
        _valueTextField.Text = "";
        _categoryTextField.Text = "";
        _selectedSetting = null;
        _selectedSettingLabel.Text = "No setting selected";
        UpdateButtonStates();
    }

    public override async Task<bool> SaveChangesAsync()
    {
        // For global settings, changes are saved immediately via API calls
        // This method is called when Save All is pressed
        HasUnsavedChanges = false;
        await Task.CompletedTask;
        return true;
    }

    public override async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        switch (keyEvent.Key)
        {
            case Key.F1:
                ShowHelp();
                return true;
                
            case Key.F2: // Add new
                if (_addButton.Enabled)
                    AddNewSetting();
                return true;
                
            case Key.F3: // Update
                if (_updateButton.Enabled)
                    UpdateSetting();
                return true;
                
            case Key.Delete: // Delete
                if (_deleteButton.Enabled)
                    DeleteSetting();
                return true;
                
            case Key.Esc: // Reset form
                ResetForm();
                return true;
        }

        return base.ProcessKey(keyEvent);
    }

    private void ShowHelp()
    {
        var help = new Dialog("Global Settings Help", 60, 18);
        
        var helpText = @"Global Settings Management

This tab allows you to manage system-wide configuration settings.

Operations:
• Select a setting from the list to view/edit details
• Add new settings with unique keys
• Update existing setting values and categories
• Delete settings that are no longer needed

Keyboard Shortcuts:
• F1 - Show this help
• F2 - Add new setting (when form is valid)
• F3 - Update selected setting (when form is modified)
• Delete - Delete selected setting
• Esc - Reset form

Tips:
• Setting keys must be unique (case-insensitive)
• Categories are optional but help organize settings
• Changes are saved immediately to the database
• Settings are used throughout the Conduit system";

        var textView = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            Text = helpText,
            ReadOnly = true,
            WordWrap = true
        };

        var okButton = new Button("OK")
        {
            X = Pos.Center(),
            Y = Pos.Bottom(help) - 3
        };
        okButton.Clicked += () => help.Running = false;

        help.Add(textView, okButton);
        Application.Run(help);
    }

    protected override void HandleRealTimeUpdate(object eventData)
    {
        // Handle SignalR updates for global settings
        Application.MainLoop.Invoke(async () =>
        {
            await RefreshAsync();
        });
    }
}