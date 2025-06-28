using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.TUI.Configuration;
using ConduitLLM.AdminClient;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Configuration;

public class ConfigurationView : View
{
    private readonly AdminApiService _adminApiService;
    private readonly AppConfiguration _appConfig;
    private readonly StateManager _stateManager;
    private readonly ILogger<ConfigurationView> _logger;
    
    private ListView _settingsList;
    private TextView _settingDetails;
    private Button _editButton;
    private Button _refreshButton;
    private Label _statusLabel;
    
    private List<SettingDto> _settings = new();
    private SettingDto? _selectedSetting;

    public ConfigurationView(IServiceProvider serviceProvider)
    {
        _adminApiService = serviceProvider.GetRequiredService<AdminApiService>();
        _appConfig = serviceProvider.GetRequiredService<AppConfiguration>();
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _logger = serviceProvider.GetRequiredService<ILogger<ConfigurationView>>();

        InitializeUI();
        LoadSettings();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Settings list
        var listFrame = new FrameView("System Settings")
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(50),
            Height = Dim.Fill(3)
        };

        _settingsList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _settingsList.SelectedItemChanged += OnSettingSelected;

        listFrame.Add(_settingsList);

        // Setting details
        var detailsFrame = new FrameView("Setting Details")
        {
            X = Pos.Right(listFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        _settingDetails = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true
        };

        detailsFrame.Add(_settingDetails);

        // Button panel
        var buttonPanel = new View()
        {
            X = 0,
            Y = Pos.Bottom(listFrame),
            Width = Dim.Fill(),
            Height = 3
        };

        _editButton = new Button("Edit Setting")
        {
            X = 0,
            Y = 0,
            Enabled = false
        };
        _editButton.Clicked += EditSetting;

        _refreshButton = new Button("Refresh")
        {
            X = Pos.Right(_editButton) + 1,
            Y = 0
        };
        _refreshButton.Clicked += () => LoadSettings();

        var exportButton = new Button("Export Settings")
        {
            X = Pos.Right(_refreshButton) + 1,
            Y = 0
        };
        exportButton.Clicked += ExportSettings;

        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill()
        };

        buttonPanel.Add(_editButton, _refreshButton, exportButton, _statusLabel);

        Add(listFrame, detailsFrame, buttonPanel);

        // Add info about current configuration
        UpdateLocalConfigDisplay();
    }

    private async void LoadSettings()
    {
        try
        {
            UpdateStatus("Loading settings...");
            _settings = await _adminApiService.GetSettingsAsync();
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateSettingsList();
                UpdateStatus($"Loaded {_settings.Count} settings");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            Application.MainLoop.Invoke(() => 
            {
                UpdateStatus($"Error: {ex.Message}");
                // Show local configuration instead
                ShowLocalConfiguration();
            });
        }
    }

    private void UpdateSettingsList()
    {
        var items = _settings.Select(s => 
        {
            var value = s.Value?.Length > 30 ? s.Value.Substring(0, 30) + "..." : s.Value;
            return $"{s.Key}: {value}";
        }).ToList();
        
        _settingsList.SetSource(items);
    }

    private void OnSettingSelected(ListViewItemEventArgs e)
    {
        if (e.Item >= 0 && e.Item < _settings.Count)
        {
            _selectedSetting = _settings[e.Item];
            _editButton.Enabled = true;
            UpdateSettingDetails();
        }
        else
        {
            _selectedSetting = null;
            _editButton.Enabled = false;
            _settingDetails.Text = "";
        }
    }

    private void UpdateSettingDetails()
    {
        if (_selectedSetting == null)
            return;

        var details = $"Key: {_selectedSetting.Key}\n\n" +
                     $"Value: {_selectedSetting.Value}\n\n" +
                     $"Type: {_selectedSetting.DataType}\n\n" +
                     $"Description: {_selectedSetting.Description}\n\n" +
                     $"Category: {_selectedSetting.Category}\n\n" +
                     $"Is Sensitive: {(_selectedSetting.IsSensitive ? "Yes" : "No")}\n\n" +
                     $"Is Read-Only: {(_selectedSetting.IsReadOnly ? "Yes" : "No")}";

        _settingDetails.Text = details;
    }

    private void EditSetting()
    {
        if (_selectedSetting == null || _selectedSetting.IsReadOnly)
            return;

        var dialog = new SettingEditDialog(_selectedSetting);
        Application.Run(dialog);

        if (dialog.Result != null)
        {
            UpdateSetting(_selectedSetting.Key, dialog.Result);
        }
    }

    private async void UpdateSetting(string key, string newValue)
    {
        try
        {
            UpdateStatus($"Updating setting {key}...");
            var updateDto = new UpdateSettingDto { Value = newValue };
            var updatedSetting = await _adminApiService.UpdateSettingAsync(key, updateDto);
            
            Application.MainLoop.Invoke(() =>
            {
                // Update in local list
                var index = _settings.FindIndex(s => s.Key == key);
                if (index >= 0)
                {
                    _settings[index] = updatedSetting;
                    UpdateSettingsList();
                    UpdateSettingDetails();
                }
                UpdateStatus($"Updated setting: {key}");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update setting");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private void ShowLocalConfiguration()
    {
        var localSettings = new List<SettingDto>
        {
            new SettingDto 
            { 
                Key = "CoreApiUrl", 
                Value = _appConfig.CoreApiUrl,
                DataType = "string",
                Description = "Core API endpoint URL",
                Category = "Connection",
                IsReadOnly = true
            },
            new SettingDto 
            { 
                Key = "AdminApiUrl", 
                Value = _appConfig.AdminApiUrl,
                DataType = "string",
                Description = "Admin API endpoint URL",
                Category = "Connection",
                IsReadOnly = true
            },
            new SettingDto 
            { 
                Key = "MasterKey", 
                Value = "****" + _appConfig.MasterKey.Substring(Math.Max(0, _appConfig.MasterKey.Length - 4)),
                DataType = "string",
                Description = "Master API key (partially hidden)",
                Category = "Security",
                IsReadOnly = true,
                IsSensitive = true
            },
            new SettingDto 
            { 
                Key = "SelectedVirtualKey", 
                Value = _appConfig.SelectedVirtualKey ?? "(none)",
                DataType = "string",
                Description = "Currently selected virtual key",
                Category = "Runtime",
                IsReadOnly = true
            }
        };

        _settings = localSettings;
        UpdateSettingsList();
        _settingDetails.Text = "Note: Showing local configuration. Admin API settings not available.";
    }

    private void UpdateLocalConfigDisplay()
    {
        var configInfo = $"Local Configuration:\n" +
                        $"Core API: {_appConfig.CoreApiUrl}\n" +
                        $"Admin API: {_appConfig.AdminApiUrl}";
        
        // This could be displayed in a separate label or status area
    }

    private async void ExportSettings()
    {
        try
        {
            UpdateStatus("Exporting settings...");
            
            var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            var fileName = $"conduit-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            await File.WriteAllTextAsync(fileName, json);
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateStatus($"Settings exported to: {fileName}");
                
                var dialog = new Dialog("Export Complete", 50, 8);
                dialog.Add(
                    new Label($"Settings exported to:") { X = 1, Y = 1 },
                    new Label(fileName) { X = 1, Y = 2 }
                );
                
                var okButton = new Button("OK") { X = Pos.Center(), Y = 4 };
                okButton.Clicked += () => dialog.Running = false;
                dialog.Add(okButton);
                
                Application.Run(dialog);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export settings");
            Application.MainLoop.Invoke(() => UpdateStatus($"Export failed: {ex.Message}"));
        }
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }
}

// Setting model (if not in SDK)
public class SettingDto
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string DataType { get; set; } = "string";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsReadOnly { get; set; }
}

public class UpdateSettingDto
{
    public string Value { get; set; } = string.Empty;
}