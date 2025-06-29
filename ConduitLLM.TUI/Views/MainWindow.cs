using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.TUI.Views.Chat;
using ConduitLLM.TUI.Views.Providers;
using ConduitLLM.TUI.Views.Models;
using ConduitLLM.TUI.Views.Media;
using ConduitLLM.TUI.Views.Keys;
using ConduitLLM.TUI.Views.Monitoring;
using ConduitLLM.TUI.Utils;
using ConduitLLM.TUI.Views.Configuration;

namespace ConduitLLM.TUI.Views;

public class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly StateManager _stateManager;
    private readonly SignalRService _signalRService;
    private readonly ILogger<MainWindow> _logger;
    private readonly LogBuffer _logBuffer;
    
    private MenuBar _menuBar = null!;
    private StatusBar _statusBar = null!;
    private FrameView _contentFrame = null!;
    private FrameView _logFrame = null!;
    private LogView _logView = null!;
    private View? _currentView;
    private bool _logPanelVisible = true;

    public MainWindow(IServiceProvider serviceProvider) : base("Conduit TUI")
    {
        _serviceProvider = serviceProvider;
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindow>>();
        _logBuffer = serviceProvider.GetRequiredService<LogBuffer>();

        InitializeUI();
        SetupEventHandlers();
    }

    private void InitializeUI()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Create menu bar
        _menuBar = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Quit", "", () => Application.RequestStop(), null, null, Key.Q | Key.CtrlMask)
            }),
            new MenuBarItem("_Views", new MenuItem[]
            {
                new MenuItem("_Chat", "", () => ShowChatView(), null, null, Key.F2),
                new MenuItem("_Models", "", () => ShowModelsView(), null, null, Key.F3),
                new MenuItem("_Providers", "", () => ShowProvidersView(), null, null, Key.F4),
                new MenuItem("_Images", "", () => ShowImageGenerationView(), null, null, Key.F5),
                new MenuItem("_Videos", "", () => ShowVideoGenerationView(), null, null, Key.F6),
                new MenuItem("Virtual _Keys", "", () => ShowVirtualKeysView(), null, null, Key.F7),
                new MenuItem("_Health", "", () => ShowHealthDashboard(), null, null, Key.F8),
                new MenuItem("_Configuration", "", () => ShowConfigurationView(), null, null, Key.F9),
                null!, // Separator
                new MenuItem("Toggle _Log Panel", "", () => ToggleLogPanel(), null, null, Key.L | Key.CtrlMask)
            }),
            new MenuBarItem("_Help", new MenuItem[]
            {
                new MenuItem("_About", "", ShowAbout),
                new MenuItem("_Keyboard Shortcuts", "", ShowKeyboardShortcuts)
            })
        });

        // Create status bar
        var statusItems = new StatusItem[]
        {
            new StatusItem(Key.F1, "~F1~ Help", ShowKeyboardShortcuts),
            new StatusItem(Key.F2, "~F2~ Chat", () => ShowChatView()),
            new StatusItem(Key.F3, "~F3~ Models", () => ShowModelsView()),
            new StatusItem(Key.F4, "~F4~ Providers", () => ShowProvidersView()),
            new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop())
        };
        _statusBar = new StatusBar(statusItems);

        // Calculate heights based on log panel visibility
        var contentHeight = _logPanelVisible ? Dim.Percent(70) : Dim.Fill(1);
        
        // Create content frame
        _contentFrame = new FrameView("Welcome")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = contentHeight
        };

        // Create log frame
        _logFrame = new FrameView("Logs (Ctrl+L to toggle)")
        {
            X = 0,
            Y = Pos.Bottom(_contentFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = _logPanelVisible
        };

        // Create log view
        _logView = new LogView(_logBuffer)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _logFrame.Add(_logView);

        // Add components
        Add(_menuBar);
        Add(_contentFrame);
        Add(_logFrame);
        Add(_statusBar);

        // Show initial view
        ShowChatView();
    }

    private void SetupEventHandlers()
    {
        // SignalR events
        _signalRService.NavigationStateUpdated += OnNavigationStateUpdated;
        _signalRService.VideoGenerationStatusUpdated += OnVideoGenerationStatusUpdated;
        _signalRService.ImageGenerationStatusUpdated += OnImageGenerationStatusUpdated;

        // State manager events
        _stateManager.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(StateManager.IsConnected))
            {
                Application.MainLoop.Invoke(() => UpdateConnectionStatus());
            }
        };
    }

    private void ShowChatView()
    {
        SetCurrentView(new ChatView(_serviceProvider), "Chat");
    }

    private void ShowModelsView()
    {
        SetCurrentView(new ModelMappingView(_serviceProvider), "Model Mappings");
    }

    private void ShowProvidersView()
    {
        SetCurrentView(new ProviderListView(_serviceProvider), "Provider Credentials");
    }

    private void ShowImageGenerationView()
    {
        SetCurrentView(new ImageGenerationView(_serviceProvider), "Image Generation");
    }

    private void ShowVideoGenerationView()
    {
        SetCurrentView(new VideoGenerationView(_serviceProvider), "Video Generation");
    }

    private void ShowVirtualKeysView()
    {
        SetCurrentView(new VirtualKeyListView(_serviceProvider), "Virtual Keys");
    }

    private void ShowHealthDashboard()
    {
        SetCurrentView(new HealthDashboard(_serviceProvider), "System Health");
    }

    private void ShowConfigurationView()
    {
        SetCurrentView(new Configuration.ConfigurationView(_serviceProvider), "Configuration");
    }

    private void SetCurrentView(View view, string title)
    {
        if (_currentView != null)
        {
            _contentFrame.Remove(_currentView);
            _currentView.Dispose();
        }

        _currentView = view;
        _contentFrame.Title = title;
        _contentFrame.Add(_currentView);
        SetNeedsDisplay();
    }

    private void UpdateConnectionStatus()
    {
        var status = _stateManager.IsConnected ? "Connected" : "Disconnected";
        var color = _stateManager.IsConnected ? ConsoleColor.Green : ConsoleColor.Red;
        
        // Update status bar with connection info
        Application.MainLoop.Invoke(() =>
        {
            SetNeedsDisplay();
        });
    }

    private void OnNavigationStateUpdated(object? sender, NavigationStateUpdateDto e)
    {
        Application.MainLoop.Invoke(() =>
        {
            _stateManager.Providers = e.Providers;
            _stateManager.ModelMappings = e.ModelMappings;
            _stateManager.ModelCapabilities = e.ModelCapabilities;
            SetNeedsDisplay();
        });
    }

    private void OnVideoGenerationStatusUpdated(object? sender, VideoGenerationStatusDto e)
    {
        Application.MainLoop.Invoke(() =>
        {
            // Update video generation view if it's active
            if (_currentView is VideoGenerationView videoView)
            {
                videoView.UpdateTaskStatus(e);
            }
        });
    }

    private void OnImageGenerationStatusUpdated(object? sender, ImageGenerationStatusDto e)
    {
        Application.MainLoop.Invoke(() =>
        {
            // Update image generation view if it's active
            if (_currentView is ImageGenerationView imageView)
            {
                imageView.UpdateTaskStatus(e);
            }
        });
    }

    private void ShowAbout()
    {
        var about = new Dialog("About Conduit TUI", 60, 10);
        about.Add(
            new Label("Conduit TUI v1.0.0") { X = Pos.Center(), Y = 1 },
            new Label("Terminal User Interface for Conduit LLM") { X = Pos.Center(), Y = 3 },
            new Label("© 2025 KNN Labs, Inc.") { X = Pos.Center(), Y = 5 }
        );
        
        var okButton = new Button("OK") { X = Pos.Center(), Y = 7 };
        okButton.Clicked += () => about.Running = false;
        about.Add(okButton);
        
        Application.Run(about);
    }

    private void ToggleLogPanel()
    {
        _logPanelVisible = !_logPanelVisible;
        _logFrame.Visible = _logPanelVisible;
        
        // Update content frame height
        _contentFrame.Height = _logPanelVisible ? Dim.Percent(70) : Dim.Fill(1);
        
        // Force layout update
        SetNeedsDisplay();
        LayoutSubviews();
        
        _logger.LogInformation("Log panel toggled: {Visible}", _logPanelVisible ? "visible" : "hidden");
    }

    private void ShowKeyboardShortcuts()
    {
        var dialog = new Dialog("Keyboard Shortcuts", 60, 22);
        
        var shortcuts = @"F1          - Help / Keyboard Shortcuts
F2          - Chat View
F3          - Model Mappings
F4          - Provider Credentials
F5          - Image Generation
F6          - Video Generation
F7          - Virtual Keys
F8          - System Health
F9          - Configuration

Ctrl+L      - Toggle Log Panel
Ctrl+Q      - Quit Application
Tab         - Next Field
Shift+Tab   - Previous Field
Enter       - Select/Confirm
Esc         - Cancel/Back

Log Panel:
Enter       - Copy selected line to clipboard
Double-click- Copy clicked line to clipboard
Ctrl+A      - Toggle Auto-scroll
Ctrl+C      - Clear Logs";

        dialog.Add(new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            Text = shortcuts,
            ReadOnly = true
        });
        
        var okButton = new Button("OK") { X = Pos.Center(), Y = Pos.Bottom(dialog) - 3 };
        okButton.Clicked += () => dialog.Running = false;
        dialog.Add(okButton);
        
        Application.Run(dialog);
    }
}