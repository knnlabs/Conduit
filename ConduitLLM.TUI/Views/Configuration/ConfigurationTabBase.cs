using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// Abstract base class for all configuration tabs.
/// Provides common functionality for data loading, error handling, and state management.
/// </summary>
public abstract class ConfigurationTabBase : View
{
    protected readonly AdminApiService _adminApiService;
    protected readonly ConfigurationStateManager _stateManager;
    protected readonly SignalRService _signalRService;
    protected readonly ILogger _logger;
    
    private Label _statusLabel = null!;
    private bool _isLoading;
    private bool _isActive;

    /// <summary>
    /// Gets the display name for this configuration tab.
    /// </summary>
    public abstract string TabName { get; }

    /// <summary>
    /// Gets whether this tab has unsaved changes.
    /// </summary>
    public virtual bool HasUnsavedChanges { get; protected set; }

    /// <summary>
    /// Event raised when the tab's status changes.
    /// </summary>
    public event Action<string>? StatusChanged;

    protected ConfigurationTabBase(IServiceProvider serviceProvider)
    {
        _adminApiService = serviceProvider.GetRequiredService<AdminApiService>();
        _stateManager = serviceProvider.GetRequiredService<ConfigurationStateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _logger = serviceProvider.GetRequiredService<ILogger<ConfigurationTabBase>>();

        InitializeBaseUI();
        SubscribeToEvents();
    }

    /// <summary>
    /// Initialize the basic UI structure common to all tabs.
    /// </summary>
    private void InitializeBaseUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = Pos.Bottom(this) - 1,
            Width = Dim.Fill(),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Black)
            }
        };

        Add(_statusLabel);
    }

    /// <summary>
    /// Subscribe to SignalR and state manager events.
    /// </summary>
    private void SubscribeToEvents()
    {
        _stateManager.PropertyChanged += OnStateManagerPropertyChanged;
        // SignalR event subscriptions will be added by derived classes
    }

    /// <summary>
    /// Called when the tab becomes active (user switches to this tab).
    /// </summary>
    public virtual async Task OnTabActivatedAsync()
    {
        if (!_isActive)
        {
            _isActive = true;
            await LoadDataAsync();
        }
    }

    /// <summary>
    /// Called when the tab becomes inactive (user switches away from this tab).
    /// </summary>
    public virtual Task OnTabDeactivatedAsync()
    {
        _isActive = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Abstract method for initializing the tab-specific UI.
    /// Called during construction after base UI is set up.
    /// </summary>
    protected abstract void InitializeTabUI();

    /// <summary>
    /// Abstract method for loading tab-specific data.
    /// Called when the tab is activated.
    /// </summary>
    protected abstract Task LoadDataAsync();

    /// <summary>
    /// Abstract method for handling real-time updates via SignalR.
    /// </summary>
    protected abstract void HandleRealTimeUpdate(object eventData);

    /// <summary>
    /// Save any pending changes in this tab.
    /// </summary>
    public virtual async Task<bool> SaveChangesAsync()
    {
        // Default implementation - no changes to save
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Refresh the tab's data from the server.
    /// </summary>
    public virtual async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// Update the status label and raise StatusChanged event.
    /// </summary>
    protected void UpdateStatus(string status)
    {
        Application.MainLoop.Invoke(() =>
        {
            _statusLabel.Text = status;
            StatusChanged?.Invoke(status);
        });
    }

    /// <summary>
    /// Update loading state and status.
    /// </summary>
    protected void SetLoading(bool isLoading, string? message = null)
    {
        _isLoading = isLoading;
        
        if (isLoading)
        {
            UpdateStatus(message ?? "Loading...");
        }
        else
        {
            UpdateStatus("Ready");
        }
    }

    /// <summary>
    /// Handle errors with consistent error display and logging.
    /// </summary>
    protected void HandleError(Exception ex, string operation)
    {
        _logger.LogError(ex, "Error in {TabName} during {Operation}", TabName, operation);
        
        var errorMessage = ex.Message.Contains("Connection Failed") 
            ? "Connection failed - check network and API availability"
            : $"Error: {ex.Message}";
            
        UpdateStatus(errorMessage);
    }

    /// <summary>
    /// Execute an async operation with proper error handling and loading states.
    /// </summary>
    protected async Task ExecuteWithLoadingAsync(Func<Task> operation, string operationName)
    {
        try
        {
            SetLoading(true, $"{operationName}...");
            await operation();
            SetLoading(false);
        }
        catch (Exception ex)
        {
            SetLoading(false);
            HandleError(ex, operationName);
        }
    }

    /// <summary>
    /// Execute an async operation with return value, proper error handling and loading states.
    /// </summary>
    protected async Task<T?> ExecuteWithLoadingAsync<T>(Func<Task<T>> operation, string operationName)
    {
        try
        {
            SetLoading(true, $"{operationName}...");
            var result = await operation();
            SetLoading(false);
            return result;
        }
        catch (Exception ex)
        {
            SetLoading(false);
            HandleError(ex, operationName);
            return default;
        }
    }

    /// <summary>
    /// Handle state manager property changes.
    /// </summary>
    private void OnStateManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Application.MainLoop.Invoke(() =>
        {
            OnStateChanged(e.PropertyName);
        });
    }

    /// <summary>
    /// Called when state manager properties change.
    /// Override in derived classes to handle specific property changes.
    /// </summary>
    protected virtual void OnStateChanged(string? propertyName)
    {
        // Default implementation - derived classes can override
    }

    /// <summary>
    /// Dispose resources and unsubscribe from events.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stateManager.PropertyChanged -= OnStateManagerPropertyChanged;
        }
        base.Dispose(disposing);
    }
}