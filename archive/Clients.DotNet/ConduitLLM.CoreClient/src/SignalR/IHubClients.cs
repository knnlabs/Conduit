using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.CoreClient.SignalR;

#region Task Hub Interfaces

/// <summary>
/// Client-side interface for receiving task hub notifications from the server.
/// </summary>
public interface ITaskHubClient
{
    /// <summary>
    /// Called when a task is started.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="taskType">Type of task being started.</param>
    /// <param name="metadata">Task metadata.</param>
    Task TaskStarted(string taskId, string taskType, object metadata);
    
    /// <summary>
    /// Called when task progress is updated.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="progress">Progress percentage (0-100).</param>
    /// <param name="message">Optional progress message.</param>
    Task TaskProgress(string taskId, int progress, string? message = null);
    
    /// <summary>
    /// Called when a task completes successfully.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="result">Task result data.</param>
    Task TaskCompleted(string taskId, object result);
    
    /// <summary>
    /// Called when a task fails.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="error">Error message.</param>
    /// <param name="isRetryable">Whether the task can be retried.</param>
    Task TaskFailed(string taskId, string error, bool isRetryable = false);
    
    /// <summary>
    /// Called when a task is cancelled.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    Task TaskCancelled(string taskId, string? reason = null);
    
    /// <summary>
    /// Called when a task times out.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="timeoutSeconds">Timeout duration in seconds.</param>
    Task TaskTimedOut(string taskId, int timeoutSeconds);
}

/// <summary>
/// Server-side interface for sending commands to the task hub.
/// </summary>
public interface ITaskHubServer
{
    /// <summary>
    /// Subscribe to notifications for a specific task.
    /// </summary>
    /// <param name="taskId">Task identifier to subscribe to.</param>
    Task SubscribeToTask(string taskId);
    
    /// <summary>
    /// Unsubscribe from notifications for a specific task.
    /// </summary>
    /// <param name="taskId">Task identifier to unsubscribe from.</param>
    Task UnsubscribeFromTask(string taskId);
    
    /// <summary>
    /// Subscribe to notifications for all tasks of a specific type.
    /// </summary>
    /// <param name="taskType">Task type to subscribe to.</param>
    Task SubscribeToTaskType(string taskType);
    
    /// <summary>
    /// Unsubscribe from notifications for a task type.
    /// </summary>
    /// <param name="taskType">Task type to unsubscribe from.</param>
    Task UnsubscribeFromTaskType(string taskType);
}

#endregion

#region Video Generation Hub Interfaces

/// <summary>
/// Client-side interface for receiving video generation hub notifications.
/// </summary>
public interface IVideoGenerationHubClient
{
    /// <summary>
    /// Called when video generation starts.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="metadata">Video generation metadata.</param>
    Task VideoGenerationStarted(string taskId, object metadata);
    
    /// <summary>
    /// Called when video generation progress is updated.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="progress">Progress percentage (0-100).</param>
    /// <param name="message">Optional progress message.</param>
    Task VideoGenerationProgress(string taskId, int progress, string? message = null);
    
    /// <summary>
    /// Called when video generation completes successfully.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="result">Video generation result.</param>
    Task VideoGenerationCompleted(string taskId, AsyncVideoGenerationResponse result);
    
    /// <summary>
    /// Called when video generation fails.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="error">Error message.</param>
    Task VideoGenerationFailed(string taskId, string error);
}

/// <summary>
/// Server-side interface for video generation hub commands.
/// </summary>
public interface IVideoGenerationHubServer
{
    /// <summary>
    /// Subscribe to notifications for a specific video generation task.
    /// </summary>
    /// <param name="taskId">Video generation task identifier.</param>
    Task SubscribeToTask(string taskId);
    
    /// <summary>
    /// Unsubscribe from notifications for a specific video generation task.
    /// </summary>
    /// <param name="taskId">Video generation task identifier.</param>
    Task UnsubscribeFromTask(string taskId);
}

#endregion

#region Image Generation Hub Interfaces

/// <summary>
/// Client-side interface for receiving image generation hub notifications.
/// </summary>
public interface IImageGenerationHubClient
{
    /// <summary>
    /// Called when image generation starts.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="metadata">Image generation metadata.</param>
    Task ImageGenerationStarted(string taskId, object metadata);
    
    /// <summary>
    /// Called when image generation progress is updated.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="progress">Progress percentage (0-100).</param>
    /// <param name="message">Optional progress message.</param>
    Task ImageGenerationProgress(string taskId, int progress, string? message = null);
    
    /// <summary>
    /// Called when image generation completes successfully.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="result">Image generation result.</param>
    Task ImageGenerationCompleted(string taskId, AsyncImageGenerationResponse result);
    
    /// <summary>
    /// Called when image generation fails.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="error">Error message.</param>
    Task ImageGenerationFailed(string taskId, string error);
    
    /// <summary>
    /// Called when image generation is cancelled.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    Task ImageGenerationCancelled(string taskId, string? reason = null);
}

/// <summary>
/// Server-side interface for image generation hub commands.
/// </summary>
public interface IImageGenerationHubServer
{
    /// <summary>
    /// Subscribe to notifications for a specific image generation task.
    /// </summary>
    /// <param name="taskId">Image generation task identifier.</param>
    Task SubscribeToTask(string taskId);
    
    /// <summary>
    /// Unsubscribe from notifications for a specific image generation task.
    /// </summary>
    /// <param name="taskId">Image generation task identifier.</param>
    Task UnsubscribeFromTask(string taskId);
}

#endregion

#region Spend Notification Hub Interfaces

/// <summary>
/// Client-side interface for receiving spend and budget notifications.
/// </summary>
public interface ISpendNotificationHubClient
{
    /// <summary>
    /// Called when spend is updated for a virtual key.
    /// </summary>
    /// <param name="notification">Spend update details.</param>
    Task SpendUpdate(SpendUpdateNotification notification);
    
    /// <summary>
    /// Called when a budget alert is triggered.
    /// </summary>
    /// <param name="alert">Budget alert details.</param>
    Task BudgetAlert(BudgetAlertNotification alert);
    
    /// <summary>
    /// Called when spend summary is available.
    /// </summary>
    /// <param name="summary">Spend summary details.</param>
    Task SpendSummary(object summary);
    
    /// <summary>
    /// Called when unusual spending pattern is detected.
    /// </summary>
    /// <param name="notification">Unusual spending details.</param>
    Task UnusualSpendingDetected(object notification);
}

#endregion

#region System Notification Hub Interfaces

/// <summary>
/// Client-side interface for receiving system-wide notifications.
/// </summary>
public interface ISystemNotificationHubClient
{
    /// <summary>
    /// Called when provider health status changes.
    /// </summary>
    /// <param name="notification">Provider health notification.</param>
    Task OnProviderHealthChanged(ProviderHealthNotification notification);
    
    /// <summary>
    /// Called when rate limit warnings are issued.
    /// </summary>
    /// <param name="notification">Rate limit notification.</param>
    Task OnRateLimitWarning(RateLimitNotification notification);
    
    /// <summary>
    /// Called when system announcements are made.
    /// </summary>
    /// <param name="notification">System announcement.</param>
    Task OnSystemAnnouncement(SystemNotification notification);
    
    /// <summary>
    /// Called when service degradation is detected.
    /// </summary>
    /// <param name="notification">Service degradation details.</param>
    Task OnServiceDegraded(SystemNotification notification);
    
    /// <summary>
    /// Called when service is restored after degradation.
    /// </summary>
    /// <param name="notification">Service restoration details.</param>
    Task OnServiceRestored(SystemNotification notification);
    
    /// <summary>
    /// Called when model mappings are changed.
    /// </summary>
    /// <param name="notification">Model mapping change details.</param>
    Task OnModelMappingChanged(object notification);
    
    /// <summary>
    /// Called when model capabilities are discovered.
    /// </summary>
    /// <param name="notification">Model capabilities notification.</param>
    Task OnModelCapabilitiesDiscovered(ModelDiscoveryNotification notification);
    
    /// <summary>
    /// Called when model availability changes.
    /// </summary>
    /// <param name="notification">Model availability notification.</param>
    Task OnModelAvailabilityChanged(object notification);
}

/// <summary>
/// Server-side interface for system notification hub commands.
/// </summary>
public interface ISystemNotificationHubServer
{
    /// <summary>
    /// Update notification preferences.
    /// </summary>
    /// <param name="preferences">Notification preferences.</param>
    Task UpdatePreferences(object preferences);
}

#endregion

#region Model Discovery Hub Interfaces

/// <summary>
/// Client-side interface for receiving model discovery notifications.
/// </summary>
public interface IModelDiscoveryHubClient
{
    /// <summary>
    /// Called when model refresh is requested.
    /// </summary>
    /// <param name="refreshInfo">Refresh information.</param>
    Task RefreshRequested(object refreshInfo);
    
    /// <summary>
    /// Called when new models are discovered.
    /// </summary>
    /// <param name="notification">Model discovery notification.</param>
    Task ModelDiscovered(ModelDiscoveryNotification notification);
    
    /// <summary>
    /// Called when provider capabilities are updated.
    /// </summary>
    /// <param name="notification">Provider capabilities notification.</param>
    Task ProviderCapabilitiesUpdated(ProviderCapabilitiesNotification notification);
}

/// <summary>
/// Server-side interface for model discovery hub commands.
/// </summary>
public interface IModelDiscoveryHubServer
{
    /// <summary>
    /// Subscribe to notifications for a specific provider.
    /// </summary>
    /// <param name="providerName">Provider name to subscribe to.</param>
    Task SubscribeToProvider(string providerName);
    
    /// <summary>
    /// Unsubscribe from notifications for a specific provider.
    /// </summary>
    /// <param name="providerName">Provider name to unsubscribe from.</param>
    Task UnsubscribeFromProvider(string providerName);
    
    /// <summary>
    /// Subscribe to all provider notifications (requires admin permissions).
    /// </summary>
    Task SubscribeToAll();
    
    /// <summary>
    /// Request refresh of provider models.
    /// </summary>
    /// <param name="providerName">Provider name to refresh.</param>
    Task RefreshProviderModels(string providerName);
}

#endregion

#region Webhook Delivery Hub Interfaces

/// <summary>
/// Client-side interface for receiving webhook delivery notifications.
/// </summary>
public interface IWebhookDeliveryHubClient
{
    /// <summary>
    /// Called when webhook delivery is attempted.
    /// </summary>
    /// <param name="attempt">Delivery attempt details.</param>
    Task DeliveryAttempted(WebhookDeliveryAttempt attempt);
    
    /// <summary>
    /// Called when webhook delivery succeeds.
    /// </summary>
    /// <param name="success">Delivery success details.</param>
    Task DeliverySucceeded(WebhookDeliverySuccess success);
    
    /// <summary>
    /// Called when webhook delivery fails.
    /// </summary>
    /// <param name="failure">Delivery failure details.</param>
    Task DeliveryFailed(WebhookDeliveryFailure failure);
    
    /// <summary>
    /// Called when webhook retry is scheduled.
    /// </summary>
    /// <param name="retry">Retry information.</param>
    Task RetryScheduled(object retry);
    
    /// <summary>
    /// Called when webhook delivery statistics are updated.
    /// </summary>
    /// <param name="stats">Delivery statistics.</param>
    Task DeliveryStatisticsUpdated(object stats);
    
    /// <summary>
    /// Called when circuit breaker state changes.
    /// </summary>
    /// <param name="circuitBreaker">Circuit breaker state information.</param>
    Task CircuitBreakerStateChanged(object circuitBreaker);
}

/// <summary>
/// Server-side interface for webhook delivery hub commands.
/// </summary>
public interface IWebhookDeliveryHubServer
{
    /// <summary>
    /// Subscribe to notifications for specific webhook URLs.
    /// </summary>
    /// <param name="webhookUrls">Webhook URLs to subscribe to.</param>
    Task SubscribeToWebhooks(string[] webhookUrls);
    
    /// <summary>
    /// Unsubscribe from notifications for specific webhook URLs.
    /// </summary>
    /// <param name="webhookUrls">Webhook URLs to unsubscribe from.</param>
    Task UnsubscribeFromWebhooks(string[] webhookUrls);
    
    /// <summary>
    /// Request webhook delivery statistics.
    /// </summary>
    Task RequestStatistics();
}

#endregion