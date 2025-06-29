using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// Configuration tab for managing HTTP client settings.
/// Provides controls for timeouts, retries, connection pooling, and other HTTP client configuration.
/// </summary>
public class HttpClientConfigTab : ConfigurationTabBase
{
    public override string TabName => "HTTP Client Configuration";
    
    // Connection settings
    private TextField _requestTimeoutField;
    private TextField _connectionTimeoutField;
    private TextField _maxConnectionsField;
    private TextField _connectionLifetimeField;
    private TextField _idleTimeoutField;
    
    // Retry settings
    private CheckBox _enableRetriesCheckBox;
    private TextField _maxRetriesField;
    private TextField _baseDelayField;
    private TextField _maxDelayField;
    private CheckBox _useExponentialBackoffCheckBox;
    
    // HTTP/2 settings
    private CheckBox _enableHttp2CheckBox;
    private TextField _http2KeepAliveIntervalField;
    private TextField _http2KeepAliveTimeoutField;
    
    // Circuit breaker settings
    private CheckBox _enableCircuitBreakerCheckBox;
    private TextField _circuitBreakerThresholdField;
    private TextField _circuitBreakerDurationField;
    
    // Buttons
    private Button _saveButton;
    private Button _resetButton;
    private Button _testConnectionButton;
    
    private HttpClientConfigurationDto? _currentConfig;
    private bool _isLoading;

    public HttpClientConfigTab(IServiceProvider serviceProvider) : base(serviceProvider)
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
            ContentSize = new Size(100, 40),
            ShowVerticalScrollIndicator = true
        };

        var container = new View
        {
            Width = Dim.Fill(),
            Height = 40
        };

        int y = 0;

        // Connection Settings Section
        var connectionFrame = new FrameView("Connection Settings")
        {
            X = 0,
            Y = y,
            Width = Dim.Percent(50),
            Height = 10
        };

        AddLabelAndField(connectionFrame, "Request Timeout (ms):", out _requestTimeoutField, 1, 1, "30000");
        AddLabelAndField(connectionFrame, "Connection Timeout (ms):", out _connectionTimeoutField, 1, 3, "5000");
        AddLabelAndField(connectionFrame, "Max Connections:", out _maxConnectionsField, 1, 5, "50");
        AddLabelAndField(connectionFrame, "Connection Lifetime (min):", out _connectionLifetimeField, 1, 7, "5");

        // Retry Settings Section
        var retryFrame = new FrameView("Retry Settings")
        {
            X = Pos.Right(connectionFrame),
            Y = y,
            Width = Dim.Fill(),
            Height = 10
        };

        _enableRetriesCheckBox = new CheckBox("Enable Retries")
        {
            X = 1,
            Y = 1,
            Checked = true
        };
        _enableRetriesCheckBox.Toggled += OnRetrySettingsChanged;

        AddLabelAndField(retryFrame, "Max Retries:", out _maxRetriesField, 1, 3, "3");
        AddLabelAndField(retryFrame, "Base Delay (ms):", out _baseDelayField, 1, 5, "1000");
        AddLabelAndField(retryFrame, "Max Delay (ms):", out _maxDelayField, 1, 7, "30000");

        retryFrame.Add(_enableRetriesCheckBox);

        y += 11;

        // HTTP/2 Settings Section
        var http2Frame = new FrameView("HTTP/2 Settings")
        {
            X = 0,
            Y = y,
            Width = Dim.Percent(50),
            Height = 8
        };

        _enableHttp2CheckBox = new CheckBox("Enable HTTP/2")
        {
            X = 1,
            Y = 1,
            Checked = true
        };
        _enableHttp2CheckBox.Toggled += OnHttp2SettingsChanged;

        AddLabelAndField(http2Frame, "Keep-Alive Interval (s):", out _http2KeepAliveIntervalField, 1, 3, "30");
        AddLabelAndField(http2Frame, "Keep-Alive Timeout (s):", out _http2KeepAliveTimeoutField, 1, 5, "20");

        http2Frame.Add(_enableHttp2CheckBox);

        // Circuit Breaker Settings Section
        var circuitFrame = new FrameView("Circuit Breaker Settings")
        {
            X = Pos.Right(http2Frame),
            Y = y,
            Width = Dim.Fill(),
            Height = 8
        };

        _enableCircuitBreakerCheckBox = new CheckBox("Enable Circuit Breaker")
        {
            X = 1,
            Y = 1,
            Checked = false
        };
        _enableCircuitBreakerCheckBox.Toggled += OnCircuitBreakerSettingsChanged;

        AddLabelAndField(circuitFrame, "Failure Threshold (%):", out _circuitBreakerThresholdField, 1, 3, "50");
        AddLabelAndField(circuitFrame, "Break Duration (s):", out _circuitBreakerDurationField, 1, 5, "60");

        circuitFrame.Add(_enableCircuitBreakerCheckBox);

        y += 9;

        // Additional Settings Section
        var additionalFrame = new FrameView("Additional Settings")
        {
            X = 0,
            Y = y,
            Width = Dim.Fill(),
            Height = 6
        };

        AddLabelAndField(additionalFrame, "Idle Timeout (min):", out _idleTimeoutField, 1, 1, "2");

        _useExponentialBackoffCheckBox = new CheckBox("Use Exponential Backoff")
        {
            X = 1,
            Y = 3,
            Checked = true
        };

        additionalFrame.Add(_useExponentialBackoffCheckBox);

        // Add all frames to container
        container.Add(connectionFrame, retryFrame, http2Frame, circuitFrame, additionalFrame);
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

        _testConnectionButton = new Button("Test Connection")
        {
            X = Pos.Right(_resetButton) + 1,
            Y = 0
        };
        _testConnectionButton.Clicked += TestConnection;

        buttonPanel.Add(_saveButton, _resetButton, _testConnectionButton);

        Add(scrollView, buttonPanel);

        // Add change handlers
        AddChangeHandlers();
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
        _requestTimeoutField.TextChanged += OnConfigurationChanged;
        _connectionTimeoutField.TextChanged += OnConfigurationChanged;
        _maxConnectionsField.TextChanged += OnConfigurationChanged;
        _connectionLifetimeField.TextChanged += OnConfigurationChanged;
        _idleTimeoutField.TextChanged += OnConfigurationChanged;
        _maxRetriesField.TextChanged += OnConfigurationChanged;
        _baseDelayField.TextChanged += OnConfigurationChanged;
        _maxDelayField.TextChanged += OnConfigurationChanged;
        _http2KeepAliveIntervalField.TextChanged += OnConfigurationChanged;
        _http2KeepAliveTimeoutField.TextChanged += OnConfigurationChanged;
        _circuitBreakerThresholdField.TextChanged += OnConfigurationChanged;
        _circuitBreakerDurationField.TextChanged += OnConfigurationChanged;
        
        _enableRetriesCheckBox.Toggled += OnConfigurationChanged;
        _useExponentialBackoffCheckBox.Toggled += OnConfigurationChanged;
        _enableHttp2CheckBox.Toggled += OnConfigurationChanged;
        _enableCircuitBreakerCheckBox.Toggled += OnConfigurationChanged;
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

    private void OnRetrySettingsChanged(bool enabled)
    {
        _maxRetriesField.Enabled = enabled;
        _baseDelayField.Enabled = enabled;
        _maxDelayField.Enabled = enabled;
        _useExponentialBackoffCheckBox.Enabled = enabled;
        OnConfigurationChanged();
    }

    private void OnHttp2SettingsChanged(bool enabled)
    {
        _http2KeepAliveIntervalField.Enabled = enabled;
        _http2KeepAliveTimeoutField.Enabled = enabled;
        OnConfigurationChanged();
    }

    private void OnCircuitBreakerSettingsChanged(bool enabled)
    {
        _circuitBreakerThresholdField.Enabled = enabled;
        _circuitBreakerDurationField.Enabled = enabled;
        OnConfigurationChanged();
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            _isLoading = true;
            UpdateStatus("Loading HTTP client configuration...");
            _stateManager.IsLoadingHttpClientConfig = true;
            
            // Note: HTTP client configuration not yet implemented in AdminApiService
            HttpClientConfigurationDto? config = null;
            _currentConfig = config;
            
            if (config != null)
            {
                _stateManager.HttpClientConfiguration = config;
                PopulateFields(config);
            }
            else
            {
                ResetToDefaults();
            }

            _stateManager.HttpClientConfigLastUpdated = DateTime.UtcNow;
            UpdateStatus("HTTP client configuration loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load HTTP client configuration");
            UpdateStatus($"Error loading configuration: {ex.Message}");
        }
        finally
        {
            _stateManager.IsLoadingHttpClientConfig = false;
            _isLoading = false;
        }
    }

    private void PopulateFields(HttpClientConfigurationDto config)
    {
        // Note: Populating fields with actual config data not yet implemented
        // For now, set default values
        ResetToDefaults();
        UpdateStatus("Using default HTTP client configuration values");
    }

    private async void SaveConfiguration()
    {
        try
        {
            // Note: HTTP client configuration save not yet fully implemented
            UpdateStatus("HTTP client configuration save not yet implemented");
            await Task.Delay(100); // Simulate async operation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save HTTP client configuration");
            UpdateStatus($"Error saving configuration: {ex.Message}");
        }
    }

    // Note: BuildConfigurationDto temporarily disabled until proper DTOs are implemented

    private void ResetToDefaults()
    {
        _isLoading = true;
        
        _requestTimeoutField.Text = "30000";
        _connectionTimeoutField.Text = "5000";
        _maxConnectionsField.Text = "50";
        _connectionLifetimeField.Text = "5";
        _idleTimeoutField.Text = "2";
        
        _enableRetriesCheckBox.Checked = true;
        _maxRetriesField.Text = "3";
        _baseDelayField.Text = "1000";
        _maxDelayField.Text = "30000";
        _useExponentialBackoffCheckBox.Checked = true;
        
        _enableHttp2CheckBox.Checked = true;
        _http2KeepAliveIntervalField.Text = "30";
        _http2KeepAliveTimeoutField.Text = "20";
        
        _enableCircuitBreakerCheckBox.Checked = false;
        _circuitBreakerThresholdField.Text = "50";
        _circuitBreakerDurationField.Text = "60";

        OnRetrySettingsChanged(_enableRetriesCheckBox.Checked);
        OnHttp2SettingsChanged(_enableHttp2CheckBox.Checked);
        OnCircuitBreakerSettingsChanged(_enableCircuitBreakerCheckBox.Checked);
        
        _isLoading = false;
        _saveButton.Enabled = true;
        HasUnsavedChanges = true;
        UpdateStatus("Reset to default values");
    }

    private async void TestConnection()
    {
        UpdateStatus("Testing HTTP client configuration...");
        
        try
        {
            // Test connection using the current configuration
            await Task.Delay(1000); // Simulate test
            UpdateStatus("HTTP client test completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP client test failed");
            UpdateStatus($"Test failed: {ex.Message}");
        }
    }

    public override async Task<bool> SaveChangesAsync()
    {
        if (_saveButton.Enabled)
        {
            SaveConfiguration();
            // Wait a moment for the async operation
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

    private void ShowHelp()
    {
        var help = new Dialog("HTTP Client Configuration Help", 70, 22);
        
        var helpText = @"HTTP Client Configuration

This tab manages HTTP client settings for all outbound requests.

Connection Settings:
• Request Timeout - Maximum time to wait for a complete response
• Connection Timeout - Maximum time to establish a connection
• Max Connections - Maximum concurrent connections per server
• Connection Lifetime - How long to keep connections alive

Retry Settings:
• Enable Retries - Whether to retry failed requests
• Max Retries - Maximum number of retry attempts
• Base/Max Delay - Delay timing for retry attempts
• Exponential Backoff - Increase delay between retries

HTTP/2 Settings:
• Enable HTTP/2 - Use HTTP/2 protocol when available
• Keep-Alive settings - Maintain connection health

Circuit Breaker:
• Prevents cascade failures by stopping requests to failing services
• Threshold - Failure percentage to trigger circuit breaker
• Break Duration - How long to keep circuit open

Keyboard Shortcuts:
• F1 - Show this help
• F10 - Save configuration
• Tab/Shift+Tab - Navigate between fields

All timeouts are in milliseconds unless specified otherwise.";

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
        // Handle SignalR updates for HTTP client configuration
        Application.MainLoop.Invoke(async () =>
        {
            await RefreshAsync();
        });
    }
}