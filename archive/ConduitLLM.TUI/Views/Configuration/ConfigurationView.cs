using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// Main configuration view with tabbed interface for managing all system configuration.
/// Provides comprehensive configuration management with real-time updates via SignalR.
/// </summary>
public class ConfigurationView : View
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConfigurationStateManager _stateManager;
    private readonly SignalRService _signalRService;
    private readonly ILogger<ConfigurationView> _logger;
    
    private TabbedView _tabbedView = null!;
    private Label _statusLabel = null!;
    private Button _saveButton = null!;
    private Button _refreshButton = null!;
    private Button _helpButton = null!;
    
    private readonly Dictionary<string, ConfigurationTabBase> _tabs = new();
    private bool _hasUnsavedChanges;

    /// <summary>
    /// Event raised when the configuration view status changes.
    /// </summary>
    public event Action<string>? StatusChanged;

    public ConfigurationView(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _stateManager = serviceProvider.GetRequiredService<ConfigurationStateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _logger = serviceProvider.GetRequiredService<ILogger<ConfigurationView>>();

        InitializeUI();
        CreateTabs();
        SubscribeToEvents();
        LoadInitialData();
    }

    /// <summary>
    /// Initialize the main UI layout.
    /// </summary>
    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Main tabbed view (takes most of the space)
        _tabbedView = new TabbedView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3) // Leave space for buttons and status
        };

        // Button panel
        var buttonPanel = new View
        {
            X = 0,
            Y = Pos.Bottom(_tabbedView),
            Width = Dim.Fill(),
            Height = 2
        };

        _saveButton = new Button("Save All (F10)")
        {
            X = 0,
            Y = 0,
            Enabled = false
        };
        _saveButton.Clicked += SaveAllChanges;

        _refreshButton = new Button("Refresh (F5)")
        {
            X = Pos.Right(_saveButton) + 1,
            Y = 0
        };
        _refreshButton.Clicked += RefreshAllTabs;

        _helpButton = new Button("Help (F1)")
        {
            X = Pos.Right(_refreshButton) + 1,
            Y = 0
        };
        _helpButton.Clicked += ShowHelp;

        // Connection status indicator
        var connectionLabel = new Label("● Connected")
        {
            X = Pos.Right(_helpButton) + 3,
            Y = 0,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
            }
        };

        buttonPanel.Add(_saveButton, _refreshButton, _helpButton, connectionLabel);

        // Status bar
        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = Pos.Bottom(buttonPanel),
            Width = Dim.Fill()
        };

        Add(_tabbedView, buttonPanel, _statusLabel);
    }

    /// <summary>
    /// Create all configuration tabs.
    /// </summary>
    private void CreateTabs()
    {
        // Create actual configuration tab implementations
        CreateConfigurationTab<GlobalSettingsTab>("Global", "F2", "Global system settings");
        CreateConfigurationTab<HttpClientConfigTab>("HTTP Client", "F3", "HTTP client configuration");
        CreateConfigurationTab<CacheConfigTab>("Cache", "F4", "Cache configuration settings");
        CreateConfigurationTab<RouterConfigTab>("Router", "F5", "Routing and load balancing");
        CreatePlaceholderTab("Security", "F6", "IP filtering and access control");
        CreatePlaceholderTab("Audio", "F7", "Audio provider configuration");
        CreatePlaceholderTab("System", "F8", "System information and health");
        CreatePlaceholderTab("Import/Export", "F9", "Configuration backup and restore");
    }

    /// <summary>
    /// Create an actual configuration tab using dependency injection.
    /// </summary>
    private void CreateConfigurationTab<T>(string title, string shortcut, string description) where T : ConfigurationTabBase
    {
        try
        {
            var tab = ActivatorUtilities.CreateInstance<T>(_serviceProvider);
            _tabs[title] = tab;
            _tabbedView.AddTab(title, tab, shortcut);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create configuration tab: {TabType}", typeof(T).Name);
            // Fall back to placeholder if tab creation fails
            CreatePlaceholderTab(title, shortcut, $"Error loading {description}");
        }
    }

    /// <summary>
    /// Create a placeholder tab for testing the infrastructure.
    /// This will be replaced with actual tab implementations in later phases.
    /// </summary>
    private void CreatePlaceholderTab(string title, string shortcut, string description)
    {
        var placeholderView = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var titleLabel = new Label($"{title} Configuration")
        {
            X = Pos.Center(),
            Y = 2,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Black)
            }
        };

        var descriptionLabel = new Label(description)
        {
            X = Pos.Center(),
            Y = 4
        };

        var comingSoonLabel = new Label("Coming soon in Phase 2...")
        {
            X = Pos.Center(),
            Y = 6,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.DarkGray, Color.Black)
            }
        };

        var instructionsLabel = new Label($"Press {shortcut} or click the tab to access this section")
        {
            X = Pos.Center(),
            Y = 8,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Cyan, Color.Black)
            }
        };

        placeholderView.Add(titleLabel, descriptionLabel, comingSoonLabel, instructionsLabel);
        _tabbedView.AddTab(title, placeholderView, shortcut);
    }

    /// <summary>
    /// Subscribe to events from state manager and SignalR.
    /// </summary>
    private void SubscribeToEvents()
    {
        // Subscribe to tab change events
        _tabbedView.TabChanged += OnTabChanged;

        // Subscribe to state manager changes
        _stateManager.PropertyChanged += OnStateManagerPropertyChanged;

        // Subscribe to SignalR connection events
        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;

        // TODO: Subscribe to specific configuration change events in Phase 4
    }

    /// <summary>
    /// Load initial data for all tabs.
    /// </summary>
    private async void LoadInitialData()
    {
        try
        {
            UpdateStatus("Loading configuration data...");
            
            // Load initial data asynchronously
            // TODO: Implement actual data loading in Phase 2
            await Task.Delay(500); // Simulate loading time
            
            UpdateStatus("Configuration loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial configuration data");
            UpdateStatus($"Error loading configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle tab change events.
    /// </summary>
    private async void OnTabChanged(int tabIndex, TabInfo tabInfo)
    {
        try
        {
            UpdateStatus($"Switched to {tabInfo.Title} configuration");
            
            // If this is a ConfigurationTabBase, notify it of activation
            if (_tabs.TryGetValue(tabInfo.Title, out var tab))
            {
                await tab.OnTabActivatedAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching to tab {TabTitle}", tabInfo.Title);
            UpdateStatus($"Error switching to {tabInfo.Title} tab");
        }
    }

    /// <summary>
    /// Handle state manager property changes.
    /// </summary>
    private void OnStateManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Application.MainLoop.Invoke(() =>
        {
            // Update unsaved changes status
            CheckForUnsavedChanges();
        });
    }

    /// <summary>
    /// Handle SignalR connection state changes.
    /// </summary>
    private void OnConnectionStateChanged(bool isConnected)
    {
        Application.MainLoop.Invoke(() =>
        {
            var connectionView = Subviews.FirstOrDefault(v => v is View buttonPanel)?
                .Subviews.FirstOrDefault(v => v is Label label && label.Text.Contains("●"));
            
            if (connectionView is Label connectionLabel)
            {
                connectionLabel.Text = isConnected ? "● Connected" : "● Disconnected";
                connectionLabel.ColorScheme = new ColorScheme
                {
                    Normal = Application.Driver.MakeAttribute(
                        isConnected ? Color.BrightGreen : Color.BrightRed, 
                        Color.Black)
                };
            }
        });
    }

    /// <summary>
    /// Check if any tabs have unsaved changes.
    /// </summary>
    private void CheckForUnsavedChanges()
    {
        var hasChanges = _tabs.Values.Any(tab => tab.HasUnsavedChanges);
        
        if (hasChanges != _hasUnsavedChanges)
        {
            _hasUnsavedChanges = hasChanges;
            _saveButton.Enabled = hasChanges;
            
            if (hasChanges)
            {
                UpdateStatus("You have unsaved changes - press F10 to save");
            }
        }
    }

    /// <summary>
    /// Save all pending changes across all tabs.
    /// </summary>
    private async void SaveAllChanges()
    {
        try
        {
            UpdateStatus("Saving all changes...");
            var savedTabs = new List<string>();
            
            foreach (var (tabName, tab) in _tabs)
            {
                if (tab.HasUnsavedChanges)
                {
                    var success = await tab.SaveChangesAsync();
                    if (success)
                    {
                        savedTabs.Add(tabName);
                    }
                }
            }
            
            if (savedTabs.Count > 0)
            {
                UpdateStatus($"Saved changes in: {string.Join(", ", savedTabs)}");
            }
            else
            {
                UpdateStatus("No changes to save");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration changes");
            UpdateStatus($"Error saving changes: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresh all tabs to get latest data from server.
    /// </summary>
    private async void RefreshAllTabs()
    {
        try
        {
            UpdateStatus("Refreshing all configuration data...");
            
            foreach (var tab in _tabs.Values)
            {
                await tab.RefreshAsync();
            }
            
            UpdateStatus("All configuration data refreshed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing configuration data");
            UpdateStatus($"Error refreshing data: {ex.Message}");
        }
    }

    /// <summary>
    /// Show help dialog for the configuration view.
    /// </summary>
    private void ShowHelp()
    {
        var help = new Dialog("Configuration Help", 70, 20);
        
        var helpText = @"Conduit Configuration Management

This interface allows you to configure all aspects of the Conduit LLM system.

Keyboard Shortcuts:
• F1 - Show this help
• F2-F9 - Switch between configuration tabs
• F10 - Save all changes
• F5 - Refresh all data
• Tab/Shift+Tab - Navigate between tabs
• Esc - Return to main menu

Available Configuration Sections:
• Global - System-wide settings and general configuration
• HTTP Client - Request timeouts, retries, and connection pooling
• Cache - Caching configuration and Redis settings
• Router - Load balancing and routing strategies
• Security - IP filtering and access control rules
• Audio - Audio provider configurations and voice settings
• System - System health monitoring and information
• Import/Export - Configuration backup and restore

Features:
• Real-time updates via SignalR
• Automatic validation of configuration values
• Comprehensive error handling and logging
• Undo/redo support for configuration changes
• Export/import for configuration backup

For more help on specific sections, navigate to the relevant tab and press F1.";

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

    /// <summary>
    /// Handle keyboard shortcuts for the configuration view.
    /// </summary>
    public override bool ProcessKey(KeyEvent keyEvent)
    {
        switch (keyEvent.Key)
        {
            case Key.F1:
                ShowHelp();
                return true;
                
            case Key.F5:
                RefreshAllTabs();
                return true;
                
            case Key.F10:
                if (_hasUnsavedChanges)
                    SaveAllChanges();
                return true;
                
            case Key.F2:
                _tabbedView.SetActiveTab("Global");
                return true;
                
            case Key.F3:
                _tabbedView.SetActiveTab("HTTP Client");
                return true;
                
            case Key.F4:
                _tabbedView.SetActiveTab("Cache");
                return true;
                
            case Key.F6:
                _tabbedView.SetActiveTab("Security");
                return true;
                
            case Key.F7:
                _tabbedView.SetActiveTab("Audio");
                return true;
                
            case Key.F8:
                _tabbedView.SetActiveTab("System");
                return true;
                
            case Key.F9:
                _tabbedView.SetActiveTab("Import/Export");
                return true;
        }

        return base.ProcessKey(keyEvent);
    }

    /// <summary>
    /// Update the status label and raise StatusChanged event.
    /// </summary>
    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
        StatusChanged?.Invoke(status);
    }

    /// <summary>
    /// Dispose resources and unsubscribe from events.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stateManager.PropertyChanged -= OnStateManagerPropertyChanged;
            _signalRService.ConnectionStateChanged -= OnConnectionStateChanged;
            _tabbedView.TabChanged -= OnTabChanged;
        }
        base.Dispose(disposing);
    }
}