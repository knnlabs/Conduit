using System.Collections.Concurrent;

using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Services;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentQueue<Notification> _notifications = new();
    private readonly int _maxNotifications;

    // Event that clients can subscribe to for real-time notification updates
    public event Action? OnNotification;

    public NotificationService(ILogger<NotificationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _maxNotifications = configuration.GetValue<int>("Notifications:MaxCount", 100);
    }

    /// <summary>
    /// Adds a new notification to the queue
    /// </summary>
    public void AddNotification(NotificationType type, string message, string? source = null, string? details = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = type,
            Message = message,
            Source = source,
            Details = details,
            Timestamp = DateTime.UtcNow,
            IsRead = false
        };

        _notifications.Enqueue(notification);

        // Trim the queue if it exceeds max size
        while (_notifications.Count > _maxNotifications && _notifications.TryDequeue(out _))
        {
            // Just removing excess notifications
        }

        _logger.LogInformation("New notification: [{Type}] {Message}", type, message);
        
        // Trigger the notification event for real-time updates
        OnNotification?.Invoke();
    }

    /// <summary>
    /// Adds a notification for a virtual key validation failure
    /// </summary>
    public void AddKeyValidationFailure(string keyPrefix, string reason, string? model = null)
    {
        var details = string.IsNullOrEmpty(model) 
            ? $"Validation failed: {reason}" 
            : $"Validation failed for model '{model}': {reason}";
            
        AddNotification(
            NotificationType.VirtualKeyValidation,
            $"Virtual Key validation failed: {reason}",
            $"Key: {keyPrefix}...",
            details);
    }

    /// <summary>
    /// Adds a notification for a virtual key approaching its budget limit
    /// </summary>
    public void AddKeyApproachingBudget(string keyName, int keyId, decimal currentSpend, decimal maxBudget, decimal percentage)
    {
        AddNotification(
            NotificationType.BudgetWarning,
            $"Virtual Key '{keyName}' has reached {percentage:F1}% of its budget",
            $"Key ID: {keyId}",
            $"Current spend: ${currentSpend:F2} of ${maxBudget:F2}");
    }

    /// <summary>
    /// Adds a notification for a key expiration
    /// </summary>
    public void AddKeyExpired(string keyName, int keyId, DateTime expiryDate)
    {
        AddNotification(
            NotificationType.KeyExpiration,
            $"Virtual Key '{keyName}' has expired",
            $"Key ID: {keyId}",
            $"Expiry date: {expiryDate:g} UTC");
    }

    /// <summary>
    /// Adds a notification for a key that had its budget reset
    /// </summary>
    public void AddKeyBudgetReset(string keyName, int keyId, string budgetDuration)
    {
        AddNotification(
            NotificationType.System,
            $"Budget reset for Virtual Key '{keyName}'",
            $"Key ID: {keyId}",
            $"Budget type: {budgetDuration}");
    }

    /// <summary>
    /// Gets all notifications
    /// </summary>
    public IEnumerable<Notification> GetNotifications(bool includeRead = false)
    {
        return includeRead
            ? _notifications.ToArray()
            : _notifications.Where(n => !n.IsRead).ToArray();
    }

    /// <summary>
    /// Marks a notification as read
    /// </summary>
    public bool MarkAsRead(Guid id)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == id);
        if (notification != null)
        {
            notification.IsRead = true;
            OnNotification?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Marks all notifications as read
    /// </summary>
    public void MarkAllAsRead()
    {
        foreach (var notification in _notifications)
        {
            notification.IsRead = true;
        }
        OnNotification?.Invoke();
    }

    /// <summary>
    /// Clears all read notifications
    /// </summary>
    public void ClearReadNotifications()
    {
        var unreadNotifications = _notifications.Where(n => !n.IsRead).ToList();
        _notifications.Clear();
        foreach (var notification in unreadNotifications)
        {
            _notifications.Enqueue(notification);
        }
        OnNotification?.Invoke();
    }
}
