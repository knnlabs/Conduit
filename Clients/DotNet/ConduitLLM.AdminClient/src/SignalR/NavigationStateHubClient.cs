using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.SignalR;

/// <summary>
/// SignalR hub client for navigation state updates including model discovery
/// and provider health changes.
/// </summary>
public class NavigationStateHubClient : BaseSignalRConnection
{
    /// <summary>
    /// Event raised when navigation state is updated.
    /// </summary>
    public event Action<NavigationStateUpdateEvent>? OnNavigationStateUpdate;
    
    /// <summary>
    /// Event raised when a new model is discovered.
    /// </summary>
    public event Action<ModelDiscoveredEvent>? OnModelDiscovered;
    
    /// <summary>
    /// Event raised when provider health changes.
    /// </summary>
    public event Action<ProviderHealthChangeEvent>? OnProviderHealthChange;

    /// <summary>
    /// Initializes a new instance of the NavigationStateHubClient class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Conduit Admin API.</param>
    /// <param name="masterKey">Master key for authentication.</param>
    /// <param name="logger">Optional logger instance.</param>
    public NavigationStateHubClient(string baseUrl, string masterKey, ILogger<NavigationStateHubClient>? logger = null)
        : base(baseUrl, masterKey, logger)
    {
    }

    /// <summary>
    /// Gets the hub path for navigation state.
    /// </summary>
    protected override string HubPath => SignalREndpoints.NavigationStateHub;

    /// <summary>
    /// Configures hub-specific event handlers.
    /// </summary>
    /// <param name="connection">The hub connection to configure.</param>
    protected override void ConfigureHubHandlers(HubConnection connection)
    {
        // Navigation state updates
        connection.On<NavigationStateUpdateEvent>("NavigationStateUpdate", (update) =>
        {
            _logger?.LogDebug("Received navigation state update with {ChangedEntities}", 
                string.Join(", ", GetChangedEntities(update.ChangedEntities)));
            OnNavigationStateUpdate?.Invoke(update);
        });

        // Model discovery events
        connection.On<ModelDiscoveredEvent>("ModelDiscovered", (eventData) =>
        {
            _logger?.LogInformation("Model discovered: {ModelId} from provider {ProviderName}", 
                eventData.Model.Id, eventData.ProviderName);
            OnModelDiscovered?.Invoke(eventData);
        });

        // Provider health changes
        connection.On<ProviderHealthChangeEvent>("ProviderHealthChange", (eventData) =>
        {
            _logger?.LogInformation("Provider health changed: {ProviderName} from {PreviousStatus} to {CurrentStatus}", 
                eventData.ProviderName, eventData.PreviousStatus, eventData.CurrentStatus);
            OnProviderHealthChange?.Invoke(eventData);
        });

        _logger?.LogDebug("Configured handlers for NavigationStateHub");
    }

    /// <summary>
    /// Subscribes to navigation state updates.
    /// </summary>
    /// <param name="groupName">Optional group name for filtered updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SubscribeToUpdates(string? groupName = null, CancellationToken cancellationToken = default)
    {
        var args = groupName != null ? new object[] { groupName } : Array.Empty<object>();
        await InvokeAsync("SubscribeToUpdates", args, cancellationToken);
        _logger?.LogInformation("Subscribed to navigation state updates{Group}", 
            groupName != null ? $" for group '{groupName}'" : "");
    }

    /// <summary>
    /// Unsubscribes from navigation state updates.
    /// </summary>
    /// <param name="groupName">Optional group name for filtered updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UnsubscribeFromUpdates(string? groupName = null, CancellationToken cancellationToken = default)
    {
        var args = groupName != null ? new object[] { groupName } : Array.Empty<object>();
        await InvokeAsync("UnsubscribeFromUpdates", args, cancellationToken);
        _logger?.LogInformation("Unsubscribed from navigation state updates{Group}", 
            groupName != null ? $" for group '{groupName}'" : "");
    }

    private static List<string> GetChangedEntities(ChangedEntities entities)
    {
        var changed = new List<string>();
        if (entities.ModelMappings == true) changed.Add("ModelMappings");
        if (entities.Providers == true) changed.Add("Providers");
        if (entities.VirtualKeys == true) changed.Add("VirtualKeys");
        if (entities.Settings == true) changed.Add("Settings");
        return changed;
    }
}