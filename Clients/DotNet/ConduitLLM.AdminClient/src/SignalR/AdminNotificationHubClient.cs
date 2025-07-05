using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.AdminClient.SignalR;

/// <summary>
/// SignalR hub client for admin notifications including virtual key events,
/// configuration changes, and system notifications.
/// </summary>
public class AdminNotificationHubClient : BaseSignalRConnection
{
    /// <summary>
    /// Event raised when a virtual key event occurs.
    /// </summary>
    public event Action<VirtualKeyEvent>? OnVirtualKeyEvent;
    
    /// <summary>
    /// Event raised when a configuration change occurs.
    /// </summary>
    public event Action<ConfigurationChangeEvent>? OnConfigurationChange;
    
    /// <summary>
    /// Event raised when an admin notification is received.
    /// </summary>
    public event Action<AdminNotificationEvent>? OnAdminNotification;

    /// <summary>
    /// Initializes a new instance of the AdminNotificationHubClient class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Conduit Admin API.</param>
    /// <param name="masterKey">Master key for authentication.</param>
    /// <param name="logger">Optional logger instance.</param>
    public AdminNotificationHubClient(string baseUrl, string masterKey, ILogger<AdminNotificationHubClient>? logger = null)
        : base(baseUrl, masterKey, logger)
    {
    }

    /// <summary>
    /// Gets the hub path for admin notifications.
    /// </summary>
    protected override string HubPath => SignalREndpoints.AdminNotificationsHub;

    /// <summary>
    /// Configures hub-specific event handlers.
    /// </summary>
    /// <param name="connection">The hub connection to configure.</param>
    protected override void ConfigureHubHandlers(HubConnection connection)
    {
        // Virtual key events
        connection.On<VirtualKeyEvent>("VirtualKeyEvent", (eventData) =>
        {
            _logger?.LogDebug("Received virtual key event: {EventType} for key {VirtualKeyId}", 
                eventData.EventType, eventData.VirtualKeyId);
            OnVirtualKeyEvent?.Invoke(eventData);
        });

        // Configuration changes
        connection.On<ConfigurationChangeEvent>("ConfigurationChange", (eventData) =>
        {
            _logger?.LogDebug("Received configuration change: {Category}.{Setting}", 
                eventData.Category, eventData.Setting);
            OnConfigurationChange?.Invoke(eventData);
        });

        // Admin notifications
        connection.On<AdminNotificationEvent>("AdminNotification", (notification) =>
        {
            _logger?.LogInformation("Received admin notification: [{Type}] {Title}", 
                notification.Type, notification.Title);
            OnAdminNotification?.Invoke(notification);
        });

        _logger?.LogDebug("Configured handlers for AdminNotificationHub");
    }

    /// <summary>
    /// Subscribes to events for a specific virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID to subscribe to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SubscribeToVirtualKey(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        await InvokeAsync("SubscribeToVirtualKey", new object[] { virtualKeyId }, cancellationToken);
        _logger?.LogInformation("Subscribed to virtual key events for key ID {VirtualKeyId}", virtualKeyId);
    }

    /// <summary>
    /// Unsubscribes from events for a specific virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID to unsubscribe from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UnsubscribeFromVirtualKey(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        await InvokeAsync("UnsubscribeFromVirtualKey", new object[] { virtualKeyId }, cancellationToken);
        _logger?.LogInformation("Unsubscribed from virtual key events for key ID {VirtualKeyId}", virtualKeyId);
    }

    /// <summary>
    /// Subscribes to events for a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name to subscribe to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SubscribeToProvider(string providerName, CancellationToken cancellationToken = default)
    {
        await InvokeAsync("SubscribeToProvider", new object[] { providerName }, cancellationToken);
        _logger?.LogInformation("Subscribed to provider events for {ProviderName}", providerName);
    }

    /// <summary>
    /// Unsubscribes from events for a specific provider.
    /// </summary>
    /// <param name="providerName">The provider name to unsubscribe from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UnsubscribeFromProvider(string providerName, CancellationToken cancellationToken = default)
    {
        await InvokeAsync("UnsubscribeFromProvider", new object[] { providerName }, cancellationToken);
        _logger?.LogInformation("Unsubscribed from provider events for {ProviderName}", providerName);
    }

    /// <summary>
    /// Requests a refresh of provider health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RefreshProviderHealth(CancellationToken cancellationToken = default)
    {
        await InvokeAsync("RefreshProviderHealth", Array.Empty<object>(), cancellationToken);
        _logger?.LogInformation("Requested provider health refresh");
    }

    /// <summary>
    /// Acknowledges receipt of a notification.
    /// </summary>
    /// <param name="notificationId">The notification ID to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AcknowledgeNotification(string notificationId, CancellationToken cancellationToken = default)
    {
        await InvokeAsync("AcknowledgeNotification", new object[] { notificationId }, cancellationToken);
        _logger?.LogDebug("Acknowledged notification {NotificationId}", notificationId);
    }
}