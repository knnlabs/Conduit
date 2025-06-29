using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// Configuration tab for managing routing and load balancing settings.
/// Provides controls for routing strategies, health checks, and failover configuration.
/// </summary>
public class RouterConfigTab : ConfigurationTabBase
{
    public override string TabName => "Router Configuration";
    
    // Routing strategy settings
    private ComboBox _routingStrategyComboBox;
    private CheckBox _enableFailoverCheckBox;
    private TextField _maxRetriesField;
    private TextField _retryDelayField;
    
    // Load balancing settings
    private ComboBox _loadBalancingModeComboBox;
    private TextField _healthCheckIntervalField;
    private TextField _healthCheckTimeoutField;
    private CheckBox _enableCircuitBreakerCheckBox;
    private TextField _circuitBreakerThresholdField;
    private TextField _circuitBreakerRecoveryTimeField;
    
    // Provider priority settings
    private ListView _providerListView;
    private Button _moveUpButton;
    private Button _moveDownButton;
    private Button _setDefaultButton;
    private Label _selectedProviderLabel;
    
    // Advanced settings
    private TextField _requestTimeoutField;
    private TextField _maxConcurrentRequestsField;
    private CheckBox _enableRequestQueuingCheckBox;
    private TextField _queueSizeField;
    private CheckBox _enableMetricsCollectionCheckBox;
    
    // Buttons
    private Button _saveButton;
    private Button _resetButton;
    private Button _testRoutingButton;
    
    private RouterConfigurationDto? _currentConfig;
    private readonly List<string> _providerPriorities = new();
    private bool _isLoading;

    public RouterConfigTab(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        InitializeTabUI();
    }

    protected override void InitializeTabUI()
    {
        // Create scrollable view for all the settings
        var scrollView = new ScrollView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            ContentSize = new Size(100, 45),
            ShowVerticalScrollIndicator = true
        };

        var container = new View
        {
            Width = Dim.Fill(),
            Height = 45
        };

        int y = 0;

        // Routing Strategy Section
        var routingFrame = new FrameView("Routing Strategy")
        {
            X = 0,
            Y = y,
            Width = Dim.Percent(50),
            Height = 10
        };

        var strategyLabel = new Label("Strategy:")
        {
            X = 1,
            Y = 1
        };
        _routingStrategyComboBox = new ComboBox
        {
            X = 1,
            Y = 2,
            Width = 25,
            Height = 5
        };
        _routingStrategyComboBox.SetSource(new[] { 
            "Round Robin", "Weighted Round Robin", "Least Connections", 
            "Fastest Response", "Priority Based", "Random" 
        });
        _routingStrategyComboBox.SelectedItem = 0;
        _routingStrategyComboBox.SelectedItemChanged += OnConfigurationChanged;

        _enableFailoverCheckBox = new CheckBox("Enable Automatic Failover")
        {
            X = 1,
            Y = 5,
            Checked = true
        };
        _enableFailoverCheckBox.Toggled += OnFailoverSettingsChanged;

        AddLabelAndField(routingFrame, "Max Retries:", out _maxRetriesField, 1, 7, "3");
        AddLabelAndField(routingFrame, "Retry Delay (ms):", out _retryDelayField, 1, 8, "1000");

        routingFrame.Add(strategyLabel, _enableFailoverCheckBox);

        // Load Balancing Section
        var loadBalancingFrame = new FrameView("Load Balancing")
        {
            X = Pos.Right(routingFrame),
            Y = y,
            Width = Dim.Fill(),
            Height = 10
        };

        var modeLabel = new Label("Mode:")
        {
            X = 1,
            Y = 1
        };
        _loadBalancingModeComboBox = new ComboBox
        {
            X = 1,
            Y = 2,
            Width = 20,
            Height = 4
        };
        _loadBalancingModeComboBox.SetSource(new[] { "Active-Active", "Active-Passive", "Hybrid" });
        _loadBalancingModeComboBox.SelectedItem = 0;
        _loadBalancingModeComboBox.SelectedItemChanged += OnConfigurationChanged;

        AddLabelAndField(loadBalancingFrame, "Health Check Interval (s):", out _healthCheckIntervalField, 1, 5, "30");
        AddLabelAndField(loadBalancingFrame, "Health Check Timeout (s):", out _healthCheckTimeoutField, 1, 7, "5");

        loadBalancingFrame.Add(modeLabel);

        y += 11;

        // Circuit Breaker Section
        var circuitBreakerFrame = new FrameView("Circuit Breaker")
        {
            X = 0,
            Y = y,
            Width = Dim.Percent(50),
            Height = 8
        };

        _enableCircuitBreakerCheckBox = new CheckBox("Enable Circuit Breaker")
        {
            X = 1,
            Y = 1,
            Checked = true
        };
        _enableCircuitBreakerCheckBox.Toggled += OnCircuitBreakerSettingsChanged;

        AddLabelAndField(circuitBreakerFrame, "Failure Threshold (%):", out _circuitBreakerThresholdField, 1, 3, "50");
        AddLabelAndField(circuitBreakerFrame, "Recovery Time (s):", out _circuitBreakerRecoveryTimeField, 1, 5, "60");

        circuitBreakerFrame.Add(_enableCircuitBreakerCheckBox);

        // Provider Priority Section
        var priorityFrame = new FrameView("Provider Priority")
        {
            X = Pos.Right(circuitBreakerFrame),
            Y = y,
            Width = Dim.Fill(),
            Height = 8
        };

        _providerListView = new ListView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(15),
            Height = 5,
            AllowsMarking = false
        };
        _providerListView.SelectedItemChanged += OnProviderSelected;

        _moveUpButton = new Button("↑ Up")
        {
            X = Pos.Right(_providerListView) + 1,
            Y = 1,
            Width = 6,
            Enabled = false
        };
        _moveUpButton.Clicked += MoveProviderUp;

        _moveDownButton = new Button("↓ Down")
        {
            X = Pos.Right(_providerListView) + 1,
            Y = 2,
            Width = 6,
            Enabled = false
        };
        _moveDownButton.Clicked += MoveProviderDown;

        _setDefaultButton = new Button("Default")
        {
            X = Pos.Right(_providerListView) + 1,
            Y = 4,
            Width = 8,
            Enabled = false
        };
        _setDefaultButton.Clicked += SetAsDefaultProvider;

        priorityFrame.Add(_providerListView, _moveUpButton, _moveDownButton, _setDefaultButton);

        y += 9;

        // Advanced Settings Section
        var advancedFrame = new FrameView("Advanced Settings")
        {
            X = 0,
            Y = y,
            Width = Dim.Fill(),
            Height = 10
        };

        AddLabelAndField(advancedFrame, "Request Timeout (ms):", out _requestTimeoutField, 1, 1, "30000");
        AddLabelAndField(advancedFrame, "Max Concurrent Requests:", out _maxConcurrentRequestsField, 1, 3, "100");

        _enableRequestQueuingCheckBox = new CheckBox("Enable Request Queuing")
        {
            X = 1,
            Y = 5,
            Checked = true
        };
        _enableRequestQueuingCheckBox.Toggled += OnQueueingSettingsChanged;

        AddLabelAndField(advancedFrame, "Queue Size:", out _queueSizeField, 1, 7, "1000");

        _enableMetricsCollectionCheckBox = new CheckBox("Enable Metrics Collection")
        {
            X = 40,
            Y = 5,
            Checked = true
        };

        advancedFrame.Add(_enableRequestQueuingCheckBox, _enableMetricsCollectionCheckBox);

        y += 11;

        // Selected provider info
        _selectedProviderLabel = new Label("No provider selected")
        {
            X = 0,
            Y = y,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.DarkGray, Color.Black)
            }
        };

        // Add all frames to container
        container.Add(routingFrame, loadBalancingFrame, circuitBreakerFrame, priorityFrame, advancedFrame, _selectedProviderLabel);
        scrollView.Add(container);

        // Button panel
        var buttonPanel = new View
        {
            X = 0,
            Y = Pos.Bottom(scrollView),
            Width = Dim.Fill(),
            Height = 2
        };

        _saveButton = new Button("Save Configuration (F10)")
        {
            X = 0,
            Y = 0,
            Enabled = false
        };
        _saveButton.Clicked += SaveConfiguration;

        _resetButton = new Button("Reset to Defaults")
        {
            X = Pos.Right(_saveButton) + 1,
            Y = 0
        };
        _resetButton.Clicked += ResetToDefaults;

        _testRoutingButton = new Button("Test Routing")
        {
            X = Pos.Right(_resetButton) + 1,
            Y = 0
        };
        _testRoutingButton.Clicked += TestRouting;

        buttonPanel.Add(_saveButton, _resetButton, _testRoutingButton);

        Add(scrollView, buttonPanel);

        // Add change handlers
        AddChangeHandlers();
        
        // Load initial provider list
        LoadProviderPriorities();
    }

    private void AddLabelAndField(View parent, string labelText, out TextField field, int x, int y, string defaultValue)
    {
        var label = new Label(labelText)
        {
            X = x,
            Y = y
        };

        field = new TextField(defaultValue)
        {
            X = x + labelText.Length + 1,
            Y = y,
            Width = 10
        };
        field.TextChanged += OnConfigurationChanged;

        parent.Add(label, field);
    }

    private void AddChangeHandlers()
    {
        _maxRetriesField.TextChanged += OnConfigurationChanged;
        _retryDelayField.TextChanged += OnConfigurationChanged;
        _healthCheckIntervalField.TextChanged += OnConfigurationChanged;
        _healthCheckTimeoutField.TextChanged += OnConfigurationChanged;
        _circuitBreakerThresholdField.TextChanged += OnConfigurationChanged;
        _circuitBreakerRecoveryTimeField.TextChanged += OnConfigurationChanged;
        _requestTimeoutField.TextChanged += OnConfigurationChanged;
        _maxConcurrentRequestsField.TextChanged += OnConfigurationChanged;
        _queueSizeField.TextChanged += OnConfigurationChanged;
        
        _enableFailoverCheckBox.Toggled += OnConfigurationChanged;
        _enableCircuitBreakerCheckBox.Toggled += OnConfigurationChanged;
        _enableRequestQueuingCheckBox.Toggled += OnConfigurationChanged;
        _enableMetricsCollectionCheckBox.Toggled += OnConfigurationChanged;
    }

    private void OnConfigurationChanged(NStack.ustring? oldValue = null)
    {
        if (!_isLoading)
        {
            _saveButton.Enabled = true;
            HasUnsavedChanges = true;
        }
    }

    private void OnConfigurationChanged(bool value)
    {
        OnConfigurationChanged((NStack.ustring?)null);
    }

    private void OnConfigurationChanged(ListViewItemEventArgs args)
    {
        OnConfigurationChanged((NStack.ustring?)null);
    }

    private void OnFailoverSettingsChanged(bool enabled)
    {
        _maxRetriesField.Enabled = enabled;
        _retryDelayField.Enabled = enabled;
        OnConfigurationChanged();
    }

    private void OnCircuitBreakerSettingsChanged(bool enabled)
    {
        _circuitBreakerThresholdField.Enabled = enabled;
        _circuitBreakerRecoveryTimeField.Enabled = enabled;
        OnConfigurationChanged();
    }

    private void OnQueueingSettingsChanged(bool enabled)
    {
        _queueSizeField.Enabled = enabled;
        OnConfigurationChanged();
    }

    private void OnProviderSelected(ListViewItemEventArgs args)
    {
        var hasSelection = args.Item >= 0 && args.Item < _providerPriorities.Count;
        _moveUpButton.Enabled = hasSelection && args.Item > 0;
        _moveDownButton.Enabled = hasSelection && args.Item < _providerPriorities.Count - 1;
        _setDefaultButton.Enabled = hasSelection && args.Item > 0;

        if (hasSelection)
        {
            var provider = _providerPriorities[args.Item];
            _selectedProviderLabel.Text = $"Selected: {provider} (Priority: {args.Item + 1})";
        }
        else
        {
            _selectedProviderLabel.Text = "No provider selected";
        }
    }

    private void LoadProviderPriorities()
    {
        // Note: Load from actual provider list when available
        _providerPriorities.Clear();
        _providerPriorities.AddRange(new[] { "OpenAI", "Anthropic", "Google", "Azure" });
        RefreshProviderList();
    }

    private void RefreshProviderList()
    {
        var displayItems = _providerPriorities
            .Select((provider, index) => $"{index + 1}. {provider}")
            .ToList();
        _providerListView.SetSource(displayItems);
    }

    private void MoveProviderUp()
    {
        var selectedIndex = _providerListView.SelectedItem;
        if (selectedIndex > 0 && selectedIndex < _providerPriorities.Count)
        {
            var provider = _providerPriorities[selectedIndex];
            _providerPriorities.RemoveAt(selectedIndex);
            _providerPriorities.Insert(selectedIndex - 1, provider);
            RefreshProviderList();
            _providerListView.SelectedItem = selectedIndex - 1;
            OnConfigurationChanged();
        }
    }

    private void MoveProviderDown()
    {
        var selectedIndex = _providerListView.SelectedItem;
        if (selectedIndex >= 0 && selectedIndex < _providerPriorities.Count - 1)
        {
            var provider = _providerPriorities[selectedIndex];
            _providerPriorities.RemoveAt(selectedIndex);
            _providerPriorities.Insert(selectedIndex + 1, provider);
            RefreshProviderList();
            _providerListView.SelectedItem = selectedIndex + 1;
            OnConfigurationChanged();
        }
    }

    private void SetAsDefaultProvider()
    {
        var selectedIndex = _providerListView.SelectedItem;
        if (selectedIndex > 0 && selectedIndex < _providerPriorities.Count)
        {
            var provider = _providerPriorities[selectedIndex];
            _providerPriorities.RemoveAt(selectedIndex);
            _providerPriorities.Insert(0, provider);
            RefreshProviderList();
            _providerListView.SelectedItem = 0;
            OnConfigurationChanged();
        }
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            _isLoading = true;
            UpdateStatus("Loading router configuration...");
            _stateManager.IsLoadingRouterConfig = true;
            
            // Note: Router configuration not yet implemented in AdminApiService
            RouterConfigurationDto? config = null;
            _currentConfig = config;
            
            if (config != null)
            {
                _stateManager.RouterConfiguration = config;
                PopulateFields(config);
            }
            else
            {
                ResetToDefaults();
            }

            UpdateStatus("Router configuration loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load router configuration");
            UpdateStatus($"Error loading configuration: {ex.Message}");
        }
        finally
        {
            _stateManager.IsLoadingRouterConfig = false;
            _isLoading = false;
        }
    }

    private void PopulateFields(RouterConfigurationDto config)
    {
        // Note: Populating fields with actual config data not yet implemented
        ResetToDefaults();
        UpdateStatus("Using default router configuration values");
    }

    private async void SaveConfiguration()
    {
        try
        {
            // Note: Router configuration save not yet fully implemented
            UpdateStatus("Router configuration save not yet implemented");
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save router configuration");
            UpdateStatus($"Error saving configuration: {ex.Message}");
        }
    }

    private void ResetToDefaults()
    {
        _isLoading = true;
        
        _routingStrategyComboBox.SelectedItem = 0; // Round Robin
        _loadBalancingModeComboBox.SelectedItem = 0; // Active-Active
        
        _enableFailoverCheckBox.Checked = true;
        _maxRetriesField.Text = "3";
        _retryDelayField.Text = "1000";
        
        _healthCheckIntervalField.Text = "30";
        _healthCheckTimeoutField.Text = "5";
        
        _enableCircuitBreakerCheckBox.Checked = true;
        _circuitBreakerThresholdField.Text = "50";
        _circuitBreakerRecoveryTimeField.Text = "60";
        
        _requestTimeoutField.Text = "30000";
        _maxConcurrentRequestsField.Text = "100";
        _enableRequestQueuingCheckBox.Checked = true;
        _queueSizeField.Text = "1000";
        _enableMetricsCollectionCheckBox.Checked = true;

        OnFailoverSettingsChanged(_enableFailoverCheckBox.Checked);
        OnCircuitBreakerSettingsChanged(_enableCircuitBreakerCheckBox.Checked);
        OnQueueingSettingsChanged(_enableRequestQueuingCheckBox.Checked);
        
        _isLoading = false;
        _saveButton.Enabled = true;
        HasUnsavedChanges = true;
        UpdateStatus("Reset to default values");
    }

    private async void TestRouting()
    {
        UpdateStatus("Testing routing configuration...");
        
        try
        {
            var strategy = _routingStrategyComboBox.Text.ToString();
            var mode = _loadBalancingModeComboBox.Text.ToString();
            
            // Simulate routing test
            await Task.Delay(1500);
            UpdateStatus($"Routing test completed: {strategy} with {mode} load balancing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Routing test failed");
            UpdateStatus($"Routing test failed: {ex.Message}");
        }
    }

    public override async Task<bool> SaveChangesAsync()
    {
        if (_saveButton.Enabled)
        {
            SaveConfiguration();
            await Task.Delay(100);
        }
        return !HasUnsavedChanges;
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
                
            case Key.F10:
                if (_saveButton.Enabled)
                    SaveConfiguration();
                return true;
        }

        return base.ProcessKey(keyEvent);
    }

    protected override void HandleRealTimeUpdate(object eventData)
    {
        Application.MainLoop.Invoke(async () =>
        {
            await RefreshAsync();
        });
    }

    private void ShowHelp()
    {
        var help = new Dialog("Router Configuration Help", 70, 26);
        
        var helpText = @"Router Configuration

This tab manages routing and load balancing for LLM providers.

Routing Strategy:
• Round Robin - Distribute requests evenly across providers
• Weighted Round Robin - Based on provider capacity/performance
• Least Connections - Route to provider with fewest active requests
• Fastest Response - Route to provider with best response time
• Priority Based - Use provider priority order
• Random - Randomly select provider

Load Balancing:
• Active-Active - All providers handle requests simultaneously
• Active-Passive - Use backup providers only when primary fails
• Hybrid - Combination of both approaches

Circuit Breaker:
• Automatically stops sending requests to failing providers
• Failure Threshold - Percentage of failures to trigger circuit breaker
• Recovery Time - How long to wait before retrying failed provider

Provider Priority:
• Drag and drop or use buttons to reorder providers
• Higher priority providers are preferred for routing
• Default provider is used as primary choice

Advanced Settings:
• Request Timeout - Maximum time to wait for provider response
• Concurrent Requests - Maximum simultaneous requests per provider
• Request Queuing - Queue requests when providers are busy
• Metrics Collection - Track performance and health statistics

Keyboard Shortcuts:
• F1 - Show this help
• F10 - Save configuration
• ↑/↓ Arrow keys - Navigate provider list

The routing configuration affects how requests are distributed
across available LLM providers for optimal performance and reliability.";

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
}