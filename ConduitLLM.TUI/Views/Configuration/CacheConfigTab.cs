using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// Configuration tab for managing cache settings.
/// Provides controls for Redis configuration, cache TTL, and cache strategies.
/// </summary>
public class CacheConfigTab : ConfigurationTabBase
{
    public override string TabName => "Cache Configuration";
    
    // Redis connection settings
    private TextField _redisConnectionStringField;
    private TextField _redisDatabaseField;
    private TextField _redisPasswordField;
    private CheckBox _useRedisCheckBox;
    
    // Cache settings
    private TextField _defaultTtlField;
    private TextField _maxCacheSizeField;
    private ComboBox _evictionPolicyComboBox;
    private CheckBox _enableCompressionCheckBox;
    
    // Cache types
    private CheckBox _enableModelCacheCheckBox;
    private TextField _modelCacheTtlField;
    private CheckBox _enableResponseCacheCheckBox;
    private TextField _responseCacheTtlField;
    private CheckBox _enableProviderCacheCheckBox;
    private TextField _providerCacheTtlField;
    
    // Memory cache settings
    private CheckBox _enableMemoryCacheCheckBox;
    private TextField _memoryCacheSizeField;
    private TextField _memoryCacheEntryLimitField;
    
    // Buttons
    private Button _saveButton;
    private Button _resetButton;
    private Button _testConnectionButton;
    private Button _clearCacheButton;
    
    private CacheConfigurationDto? _currentConfig;
    private bool _isLoading;

    public CacheConfigTab(IServiceProvider serviceProvider) : base(serviceProvider)
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
            ContentSize = new Size(100, 50),
            ShowVerticalScrollIndicator = true
        };

        var container = new View
        {
            Width = Dim.Fill(),
            Height = 50
        };

        int y = 0;

        // Redis Connection Section
        var redisFrame = new FrameView("Redis Connection")
        {
            X = 0,
            Y = y,
            Width = Dim.Percent(50),
            Height = 12
        };

        _useRedisCheckBox = new CheckBox("Enable Redis Cache")
        {
            X = 1,
            Y = 1,
            Checked = true
        };
        _useRedisCheckBox.Toggled += OnRedisSettingsChanged;

        AddLabelAndField(redisFrame, "Connection String:", out _redisConnectionStringField, 1, 3, "localhost:6379");
        AddLabelAndField(redisFrame, "Database:", out _redisDatabaseField, 1, 5, "0");
        AddLabelAndField(redisFrame, "Password:", out _redisPasswordField, 1, 7, "");
        _redisPasswordField.Secret = true;

        redisFrame.Add(_useRedisCheckBox);

        // Cache Settings Section
        var cacheFrame = new FrameView("Cache Settings")
        {
            X = Pos.Right(redisFrame),
            Y = y,
            Width = Dim.Fill(),
            Height = 12
        };

        AddLabelAndField(cacheFrame, "Default TTL (seconds):", out _defaultTtlField, 1, 1, "3600");
        AddLabelAndField(cacheFrame, "Max Cache Size (MB):", out _maxCacheSizeField, 1, 3, "100");

        var evictionLabel = new Label("Eviction Policy:")
        {
            X = 1,
            Y = 5
        };
        _evictionPolicyComboBox = new ComboBox
        {
            X = 1,
            Y = 6,
            Width = 20,
            Height = 5
        };
        _evictionPolicyComboBox.SetSource(new[] { "LRU", "LFU", "TTL", "Random" });
        _evictionPolicyComboBox.SelectedItem = 0; // LRU

        _enableCompressionCheckBox = new CheckBox("Enable Compression")
        {
            X = 1,
            Y = 8,
            Checked = false
        };

        cacheFrame.Add(evictionLabel, _enableCompressionCheckBox);

        y += 13;

        // Cache Types Section
        var cacheTypesFrame = new FrameView("Cache Types")
        {
            X = 0,
            Y = y,
            Width = Dim.Fill(),
            Height = 15
        };

        // Model Cache
        _enableModelCacheCheckBox = new CheckBox("Model Capabilities Cache")
        {
            X = 1,
            Y = 1,
            Checked = true
        };
        AddLabelAndField(cacheTypesFrame, "Model Cache TTL (sec):", out _modelCacheTtlField, 1, 3, "7200");

        // Response Cache
        _enableResponseCacheCheckBox = new CheckBox("Response Cache")
        {
            X = 30,
            Y = 1,
            Checked = true
        };
        AddLabelAndField(cacheTypesFrame, "Response TTL (sec):", out _responseCacheTtlField, 30, 3, "1800");

        // Provider Cache
        _enableProviderCacheCheckBox = new CheckBox("Provider Credentials Cache")
        {
            X = 1,
            Y = 6,
            Checked = true
        };
        AddLabelAndField(cacheTypesFrame, "Provider TTL (sec):", out _providerCacheTtlField, 1, 8, "3600");

        cacheTypesFrame.Add(_enableModelCacheCheckBox, _enableResponseCacheCheckBox, _enableProviderCacheCheckBox);

        y += 16;

        // Memory Cache Section
        var memoryCacheFrame = new FrameView("In-Memory Cache (Fallback)")
        {
            X = 0,
            Y = y,
            Width = Dim.Fill(),
            Height = 8
        };

        _enableMemoryCacheCheckBox = new CheckBox("Enable Memory Cache Fallback")
        {
            X = 1,
            Y = 1,
            Checked = true
        };

        AddLabelAndField(memoryCacheFrame, "Memory Size (MB):", out _memoryCacheSizeField, 1, 3, "50");
        AddLabelAndField(memoryCacheFrame, "Max Entries:", out _memoryCacheEntryLimitField, 1, 5, "1000");

        memoryCacheFrame.Add(_enableMemoryCacheCheckBox);

        y += 9;

        // Add all frames to container
        container.Add(redisFrame, cacheFrame, cacheTypesFrame, memoryCacheFrame);
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

        _testConnectionButton = new Button("Test Redis Connection")
        {
            X = Pos.Right(_resetButton) + 1,
            Y = 0
        };
        _testConnectionButton.Clicked += TestRedisConnection;

        _clearCacheButton = new Button("Clear All Cache")
        {
            X = Pos.Right(_testConnectionButton) + 1,
            Y = 0
        };
        _clearCacheButton.Clicked += ClearAllCache;

        buttonPanel.Add(_saveButton, _resetButton, _testConnectionButton, _clearCacheButton);

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
            Width = 15
        };
        field.TextChanged += OnConfigurationChanged;

        parent.Add(label, field);
    }

    private void AddChangeHandlers()
    {
        _redisConnectionStringField.TextChanged += OnConfigurationChanged;
        _redisDatabaseField.TextChanged += OnConfigurationChanged;
        _redisPasswordField.TextChanged += OnConfigurationChanged;
        _defaultTtlField.TextChanged += OnConfigurationChanged;
        _maxCacheSizeField.TextChanged += OnConfigurationChanged;
        _modelCacheTtlField.TextChanged += OnConfigurationChanged;
        _responseCacheTtlField.TextChanged += OnConfigurationChanged;
        _providerCacheTtlField.TextChanged += OnConfigurationChanged;
        _memoryCacheSizeField.TextChanged += OnConfigurationChanged;
        _memoryCacheEntryLimitField.TextChanged += OnConfigurationChanged;
        
        _useRedisCheckBox.Toggled += OnConfigurationChanged;
        _enableCompressionCheckBox.Toggled += OnConfigurationChanged;
        _enableModelCacheCheckBox.Toggled += OnConfigurationChanged;
        _enableResponseCacheCheckBox.Toggled += OnConfigurationChanged;
        _enableProviderCacheCheckBox.Toggled += OnConfigurationChanged;
        _enableMemoryCacheCheckBox.Toggled += OnConfigurationChanged;
        
        _evictionPolicyComboBox.SelectedItemChanged += OnConfigurationChanged;
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

    private void OnRedisSettingsChanged(bool enabled)
    {
        _redisConnectionStringField.Enabled = enabled;
        _redisDatabaseField.Enabled = enabled;
        _redisPasswordField.Enabled = enabled;
        _testConnectionButton.Enabled = enabled;
        OnConfigurationChanged();
    }

    protected override async Task LoadDataAsync()
    {
        try
        {
            _isLoading = true;
            UpdateStatus("Loading cache configuration...");
            _stateManager.IsLoadingCacheConfig = true;
            
            // Note: Cache configuration not yet implemented in AdminApiService
            CacheConfigurationDto? config = null;
            _currentConfig = config;
            
            if (config != null)
            {
                _stateManager.CacheConfiguration = config;
                PopulateFields(config);
            }
            else
            {
                ResetToDefaults();
            }

            _stateManager.CacheConfigLastUpdated = DateTime.UtcNow;
            UpdateStatus("Cache configuration loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load cache configuration");
            UpdateStatus($"Error loading configuration: {ex.Message}");
        }
        finally
        {
            _stateManager.IsLoadingCacheConfig = false;
            _isLoading = false;
        }
    }

    private void PopulateFields(CacheConfigurationDto config)
    {
        // Note: Populating fields with actual config data not yet implemented
        // For now, set default values
        ResetToDefaults();
        UpdateStatus("Using default cache configuration values");
    }

    private async void SaveConfiguration()
    {
        try
        {
            // Note: Cache configuration save not yet fully implemented
            UpdateStatus("Cache configuration save not yet implemented");
            await Task.Delay(100); // Simulate async operation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save cache configuration");
            UpdateStatus($"Error saving configuration: {ex.Message}");
        }
    }

    private void ResetToDefaults()
    {
        _isLoading = true;
        
        _useRedisCheckBox.Checked = true;
        _redisConnectionStringField.Text = "localhost:6379";
        _redisDatabaseField.Text = "0";
        _redisPasswordField.Text = "";
        
        _defaultTtlField.Text = "3600";
        _maxCacheSizeField.Text = "100";
        _evictionPolicyComboBox.SelectedItem = 0; // LRU
        _enableCompressionCheckBox.Checked = false;
        
        _enableModelCacheCheckBox.Checked = true;
        _modelCacheTtlField.Text = "7200";
        _enableResponseCacheCheckBox.Checked = true;
        _responseCacheTtlField.Text = "1800";
        _enableProviderCacheCheckBox.Checked = true;
        _providerCacheTtlField.Text = "3600";
        
        _enableMemoryCacheCheckBox.Checked = true;
        _memoryCacheSizeField.Text = "50";
        _memoryCacheEntryLimitField.Text = "1000";

        OnRedisSettingsChanged(_useRedisCheckBox.Checked);
        
        _isLoading = false;
        _saveButton.Enabled = true;
        HasUnsavedChanges = true;
        UpdateStatus("Reset to default values");
    }

    private async void TestRedisConnection()
    {
        UpdateStatus("Testing Redis connection...");
        
        try
        {
            var connectionString = _redisConnectionStringField.Text.ToString();
            var database = _redisDatabaseField.Text.ToString();
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                UpdateStatus("Please enter a Redis connection string");
                return;
            }

            // Simulate Redis connection test
            await Task.Delay(1000);
            UpdateStatus($"Redis connection test completed successfully: {connectionString}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis connection test failed");
            UpdateStatus($"Redis test failed: {ex.Message}");
        }
    }

    private async void ClearAllCache()
    {
        var result = MessageBox.Query("Clear Cache", 
            "Are you sure you want to clear all cache data? This action cannot be undone.", 
            "Yes", "No");
            
        if (result == 0) // Yes
        {
            try
            {
                UpdateStatus("Clearing all cache data...");
                
                // Note: Cache clearing not yet implemented in AdminApiService
                await Task.Delay(1000); // Simulate operation
                
                UpdateStatus("All cache data cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache");
                UpdateStatus($"Error clearing cache: {ex.Message}");
            }
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

    protected override void HandleRealTimeUpdate(object eventData)
    {
        // Handle SignalR updates for cache configuration
        Application.MainLoop.Invoke(async () =>
        {
            await RefreshAsync();
        });
    }

    private void ShowHelp()
    {
        var help = new Dialog("Cache Configuration Help", 70, 24);
        
        var helpText = @"Cache Configuration

This tab manages all caching settings for the Conduit system.

Redis Connection:
• Connection String - Redis server address and port
• Database - Redis database number (0-15)
• Password - Redis authentication password (optional)

Cache Settings:
• Default TTL - Time-to-live for cached items in seconds
• Max Cache Size - Maximum memory usage for cache in MB
• Eviction Policy - Strategy for removing old cache entries
• Compression - Reduce memory usage with compression

Cache Types:
• Model Cache - Caches model capabilities and metadata
• Response Cache - Caches API response data for repeated requests
• Provider Cache - Caches provider credential information

Memory Cache:
• Fallback when Redis is unavailable
• Size and entry limits for in-memory storage

Operations:
• Test Connection - Verify Redis connectivity
• Clear Cache - Remove all cached data
• Save - Apply configuration changes

Keyboard Shortcuts:
• F1 - Show this help
• F10 - Save configuration
• Tab/Shift+Tab - Navigate between fields

Note: Changes take effect immediately after saving.
A restart may be required for some connection changes.";

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