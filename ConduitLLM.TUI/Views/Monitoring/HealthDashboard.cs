using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.TUI.Configuration;

namespace ConduitLLM.TUI.Views.Monitoring;

public class HealthDashboard : View
{
    private readonly StateManager _stateManager;
    private readonly SignalRService _signalRService;
    private readonly AppConfiguration _config;
    private readonly ILogger<HealthDashboard> _logger;
    
    private ListView _statusList = null!;
    private Button _refreshButton = null!;
    private Label _lastUpdateLabel = null!;
    private Timer? _refreshTimer;

    public HealthDashboard(IServiceProvider serviceProvider)
    {
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _config = serviceProvider.GetRequiredService<AppConfiguration>();
        _logger = serviceProvider.GetRequiredService<ILogger<HealthDashboard>>();

        InitializeUI();
        UpdateHealthStatus();
        StartAutoRefresh();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Title
        var titleLabel = new Label("System Health Dashboard")
        {
            X = Pos.Center(),
            Y = 0,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightBlue, Color.Black)
            }
        };

        // Status list
        var statusFrame = new FrameView("Component Status")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        _statusList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        statusFrame.Add(_statusList);

        // Control panel
        var controlPanel = new View()
        {
            X = 0,
            Y = Pos.Bottom(statusFrame),
            Width = Dim.Fill(),
            Height = 3
        };

        _refreshButton = new Button("Refresh Now")
        {
            X = 0,
            Y = 0
        };
        _refreshButton.Clicked += () => UpdateHealthStatus();

        _lastUpdateLabel = new Label("Last update: Never")
        {
            X = Pos.Right(_refreshButton) + 2,
            Y = 0,
            Width = Dim.Fill()
        };

        controlPanel.Add(_refreshButton, _lastUpdateLabel);

        Add(titleLabel, statusFrame, controlPanel);
    }

    private void UpdateHealthStatus()
    {
        var statusItems = new List<string>();

        // SignalR Connection Status
        var signalRStatus = _stateManager.IsConnected ? "✓ Connected" : "✗ Disconnected";
        var signalRColor = _stateManager.IsConnected ? "Green" : "Red";
        statusItems.Add($"SignalR Connection: {signalRStatus}");

        // API Endpoints
        statusItems.Add($"Core API URL: {_config.CoreApiUrl}");
        statusItems.Add($"Admin API URL: {_config.AdminApiUrl}");

        // Virtual Key Status
        var keyStatus = !string.IsNullOrEmpty(_stateManager.SelectedVirtualKey) 
            ? $"Selected: {_stateManager.VirtualKeys.FirstOrDefault(k => k.ApiKey == _stateManager.SelectedVirtualKey)?.KeyName ?? "Unknown"}"
            : "None selected";
        statusItems.Add($"Virtual Key: {keyStatus}");

        // Provider Status
        var enabledProviders = _stateManager.Providers.Count(p => p.IsEnabled);
        statusItems.Add($"Providers: {enabledProviders} enabled / {_stateManager.Providers.Count} total");

        // Model Mappings
        var enabledMappings = _stateManager.ModelMappings.Count(m => m.IsEnabled);
        statusItems.Add($"Model Mappings: {enabledMappings} enabled / {_stateManager.ModelMappings.Count} total");

        // Model Capabilities
        var totalModels = _stateManager.ModelCapabilities.Values.Sum(list => list.Count);
        statusItems.Add($"Discovered Models: {totalModels} across {_stateManager.ModelCapabilities.Count} providers");

        // Memory Usage (approximate)
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var memoryMB = process.WorkingSet64 / (1024 * 1024);
        statusItems.Add($"Memory Usage: {memoryMB:N0} MB");

        // Update time
        var updateTime = DateTime.Now;
        statusItems.Add($"");
        statusItems.Add($"Auto-refresh: Every 30 seconds");
        
        Application.MainLoop.Invoke(() =>
        {
            _statusList.SetSource(statusItems);
            _lastUpdateLabel.Text = $"Last update: {updateTime:HH:mm:ss}";
        });
    }

    private void StartAutoRefresh()
    {
        _refreshTimer = new Timer(_ =>
        {
            UpdateHealthStatus();
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}