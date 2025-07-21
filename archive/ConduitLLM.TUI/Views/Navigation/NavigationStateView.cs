using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.TUI.Models;
using ConduitLLM.TUI.Constants;

namespace ConduitLLM.TUI.Views.Navigation;

public class NavigationStateView : View
{
    private readonly StateManager _stateManager;
    private readonly SignalRService _signalRService;
    private readonly ILogger<NavigationStateView> _logger;
    
    private ListView _navigationList = null!;
    private Label _statusLabel = null!;
    private Label _realTimeLabel = null!;
    private Label _sectionsLabel = null!;
    private Button _refreshButton = null!;
    private TextField _searchField = null!;
    
    private NavigationStateDto? _currentState;
    private string _searchFilter = string.Empty;
    private bool _isLoading = false;

    public NavigationStateView(IServiceProvider serviceProvider)
    {
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _logger = serviceProvider.GetRequiredService<ILogger<NavigationStateView>>();

        InitializeUI();
        SetupEventHandlers();
        LoadNavigationState();
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

        // Section availability summary
        _sectionsLabel = new Label("Available sections: None")
        {
            X = Pos.Right(_realTimeLabel) + 2,
            Y = 0,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Blue, Color.Black)
            }
        };

        // Search field
        var searchLabel = new Label("Search:")
        {
            X = 0,
            Y = 2
        };

        _searchField = new TextField("")
        {
            X = Pos.Right(searchLabel) + 1,
            Y = 2,
            Width = 20
        };
        _searchField.TextChanged += (text) => OnSearchChanged();

        // Navigation list frame
        var listFrame = new FrameView("Navigation State - Providers, Models & Capabilities")
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        _navigationList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        listFrame.Add(_navigationList);

        // Control panel
        var controlPanel = new View()
        {
            X = 0,
            Y = Pos.Bottom(listFrame),
            Width = Dim.Fill(),
            Height = 3
        };

        _refreshButton = new Button("Refresh")
        {
            X = 0,
            Y = 0
        };
        _refreshButton.Clicked += () => LoadNavigationState(forceRefresh: true);

        _statusLabel = new Label("Ready")
        {
            X = Pos.Right(_refreshButton) + 2,
            Y = 0,
            Width = Dim.Fill()
        };

        controlPanel.Add(_refreshButton, _statusLabel);

        Add(_realTimeLabel, _sectionsLabel, searchLabel, _searchField, listFrame, controlPanel);
    }

    private void SetupEventHandlers()
    {
        // Subscribe to SignalR navigation state updates
        _signalRService.NavigationStateUpdated += OnNavigationStateUpdated;
        
        // Subscribe to state manager changes
        _stateManager.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(StateManager.NavigationState))
            {
                Application.MainLoop.Invoke(() => UpdateNavigationTree());
            }
            else if (e.PropertyName == nameof(StateManager.IsConnected))
            {
                Application.MainLoop.Invoke(() => UpdateRealTimeStatus());
            }
        };
    }

    private async void LoadNavigationState(bool forceRefresh = false)
    {
        if (_isLoading) return;

        try
        {
            _isLoading = true;
            UpdateStatus("Loading navigation state...");
            
            _currentState = await _stateManager.LoadNavigationStateAsync(forceRefresh);
            
            Application.MainLoop.Invoke(() =>
            {
                UpdateNavigationTree();
                UpdateSectionsDisplay();
                UpdateStatus($"Loaded at {_currentState.LastRefreshed:HH:mm:ss}");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load navigation state");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void UpdateNavigationTree()
    {
        if (_currentState == null)
        {
            _navigationList.SetSource(new List<string>());
            return;
        }

        var items = new List<string>();

        if (!string.IsNullOrEmpty(_currentState.ErrorMessage))
        {
            items.Add($"❌ Error: {_currentState.ErrorMessage}");
        }
        else
        {
            // Group providers by health status
            var healthyProviders = _currentState.ProviderDetails.Where(p => p.HealthStatus == ProviderHealthStatus.Healthy && MatchesSearchFilter(p)).ToList();
            var degradedProviders = _currentState.ProviderDetails.Where(p => p.HealthStatus == ProviderHealthStatus.Degraded && MatchesSearchFilter(p)).ToList();
            var unhealthyProviders = _currentState.ProviderDetails.Where(p => p.HealthStatus == ProviderHealthStatus.Unhealthy && MatchesSearchFilter(p)).ToList();
            var unknownProviders = _currentState.ProviderDetails.Where(p => p.HealthStatus == ProviderHealthStatus.Unknown && MatchesSearchFilter(p)).ToList();

            // Add provider groups
            if (healthyProviders.Any())
            {
                items.Add("🟢 Healthy Providers");
                foreach (var provider in healthyProviders)
                {
                    AddProviderItems(provider, items, "  ");
                }
                items.Add(""); // Separator
            }

            if (degradedProviders.Any())
            {
                items.Add("🟡 Degraded Providers");
                foreach (var provider in degradedProviders)
                {
                    AddProviderItems(provider, items, "  ");
                }
                items.Add(""); // Separator
            }

            if (unhealthyProviders.Any())
            {
                items.Add("🔴 Unhealthy Providers");
                foreach (var provider in unhealthyProviders)
                {
                    AddProviderItems(provider, items, "  ");
                }
                items.Add(""); // Separator
            }

            if (unknownProviders.Any())
            {
                items.Add("⚫ Unknown Status Providers");
                foreach (var provider in unknownProviders)
                {
                    AddProviderItems(provider, items, "  ");
                }
            }
        }

        // Remove trailing empty line
        if (items.Any() && string.IsNullOrEmpty(items.Last()))
        {
            items.RemoveAt(items.Count - 1);
        }

        _navigationList.SetSource(items);
    }

    private void AddProviderItems(ProviderNavigationInfo provider, List<string> items, string indent)
    {
        var statusIcon = provider.IsEnabled ? "✅" : "❌";
        var providerText = $"{indent}{statusIcon} {provider.Name} ({provider.HealthText})";
        
        if (provider.ResponseTimeMs.HasValue)
        {
            providerText += $" - {provider.ResponseTimeMs:F0}ms";
        }

        items.Add(providerText);

        // Add model nodes
        var modelsToShow = provider.Models.Where(m => MatchesSearchFilter(m)).ToList();
        foreach (var model in modelsToShow)
        {
            var modelIcon = model.IsEnabled ? "📄" : "📋";
            var modelText = $"{indent}  {modelIcon} {model.DisplayName}";
            
            if (!model.IsAvailable)
            {
                modelText += " (unavailable)";
            }

            items.Add(modelText);

            // Add capability info
            if (model.Capabilities.Any())
            {
                items.Add($"{indent}    🔧 Capabilities: {model.CapabilitiesText}");
            }
            else
            {
                items.Add($"{indent}    🔧 No capabilities discovered");
            }
        }

        if (!provider.Models.Any())
        {
            items.Add($"{indent}  📋 No models configured");
        }
    }

    private bool MatchesSearchFilter(ProviderNavigationInfo provider)
    {
        if (string.IsNullOrEmpty(_searchFilter)) return true;
        
        return provider.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
               provider.Models.Any(m => MatchesSearchFilter(m));
    }

    private bool MatchesSearchFilter(ModelNavigationInfo model)
    {
        if (string.IsNullOrEmpty(_searchFilter)) return true;
        
        return model.ModelId.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
               (model.Alias?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
               model.Capabilities.Any(c => c.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateSectionsDisplay()
    {
        if (_currentState?.Sections == null)
        {
            _sectionsLabel.Text = "Available sections: None";
            return;
        }

        var sections = new List<string>();
        if (_currentState.Sections.Chat) sections.Add("Chat");
        if (_currentState.Sections.Images) sections.Add("Images");
        if (_currentState.Sections.Video) sections.Add("Video");
        if (_currentState.Sections.Audio) sections.Add("Audio");
        if (_currentState.Sections.Embeddings) sections.Add("Embeddings");

        var sectionsText = sections.Any() ? string.Join(", ", sections) : "None";
        _sectionsLabel.Text = $"Available sections: {sectionsText} ({_currentState.Sections.AvailableCount})";
    }

    private void UpdateRealTimeStatus()
    {
        var isConnected = _stateManager.IsConnected;
        _realTimeLabel.Text = isConnected ? "⚡ Real-time updates enabled" : "❌ Real-time updates disabled";
        _realTimeLabel.ColorScheme = new ColorScheme
        {
            Normal = Application.Driver.MakeAttribute(isConnected ? Color.Green : Color.Red, Color.Black)
        };
    }

    private void UpdateStatus(string message)
    {
        _statusLabel.Text = message;
        _logger.LogDebug("Navigation state status: {Message}", message);
    }

    private void OnSearchChanged()
    {
        _searchFilter = _searchField.Text?.ToString() ?? string.Empty;
        UpdateNavigationTree();
    }

    private void OnNavigationStateUpdated(object? sender, ConduitLLM.TUI.Services.NavigationStateUpdateDto e)
    {
        Application.MainLoop.Invoke(() =>
        {
            UpdateStatus("Navigation state updated via SignalR");
            LoadNavigationState(forceRefresh: true);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _signalRService.NavigationStateUpdated -= OnNavigationStateUpdated;
        }
        base.Dispose(disposing);
    }
}

