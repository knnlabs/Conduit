using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing transient toast notifications.
    /// </summary>
    public interface IToastService
    {
        /// <summary>
        /// Gets the current list of active toast notifications.
        /// </summary>
        IReadOnlyList<ToastNotification> Notifications { get; }

        /// <summary>
        /// Event raised when the notifications list changes.
        /// </summary>
        event Action? OnChange;

        /// <summary>
        /// Shows a toast notification with the specified message and severity.
        /// </summary>
        /// <param name="message">The notification message.</param>
        /// <param name="severity">The severity level.</param>
        /// <param name="title">Optional title.</param>
        /// <param name="durationMs">Duration before auto-dismiss (0 = no auto-dismiss).</param>
        /// <param name="actionText">Optional action button text.</param>
        /// <param name="actionCallback">Optional action callback.</param>
        void Show(string message, ToastSeverity severity = ToastSeverity.Info, string? title = null,
            int? durationMs = null, string? actionText = null, Action? actionCallback = null);

        /// <summary>
        /// Shows a success toast notification.
        /// </summary>
        void ShowSuccess(string message, string? title = null, int? durationMs = null);

        /// <summary>
        /// Shows an error toast notification.
        /// </summary>
        void ShowError(string message, string? title = null, int? durationMs = null);

        /// <summary>
        /// Shows a warning toast notification.
        /// </summary>
        void ShowWarning(string message, string? title = null, int? durationMs = null);

        /// <summary>
        /// Shows an info toast notification.
        /// </summary>
        void ShowInfo(string message, string? title = null, int? durationMs = null);

        /// <summary>
        /// Dismisses a specific toast notification.
        /// </summary>
        /// <param name="notificationId">The notification ID to dismiss.</param>
        void Dismiss(Guid notificationId);

        /// <summary>
        /// Dismisses all toast notifications.
        /// </summary>
        void DismissAll();
    }

    /// <summary>
    /// Default implementation of the toast notification service.
    /// </summary>
    public class ToastService : IToastService
    {
        private readonly List<ToastNotification> _notifications = new();
        private readonly Dictionary<Guid, Timer> _timers = new();
        private readonly object _lock = new();

        /// <summary>
        /// Gets or sets the maximum number of notifications to display simultaneously.
        /// </summary>
        public int MaxNotifications { get; set; } = 5;

        /// <summary>
        /// Gets or sets the default duration for notifications in milliseconds.
        /// </summary>
        public int DefaultDurationMs { get; set; } = 5000;

        /// <inheritdoc />
        public IReadOnlyList<ToastNotification> Notifications
        {
            get
            {
                lock (_lock)
                {
                    return _notifications.AsReadOnly();
                }
            }
        }

        /// <inheritdoc />
        public event Action? OnChange;

        /// <inheritdoc />
        public void Show(string message, ToastSeverity severity = ToastSeverity.Info, string? title = null,
            int? durationMs = null, string? actionText = null, Action? actionCallback = null)
        {
            var notification = new ToastNotification
            {
                Message = message,
                Severity = severity,
                Title = title,
                DurationMs = durationMs ?? DefaultDurationMs,
                ActionText = actionText,
                ActionCallback = actionCallback
            };

            lock (_lock)
            {
                // Remove oldest notifications if we exceed the max
                while (_notifications.Count >= MaxNotifications)
                {
                    var oldest = _notifications.First();
                    DismissInternal(oldest.Id);
                }

                _notifications.Add(notification);

                // Set up auto-dismiss timer if duration > 0
                if (notification.DurationMs > 0)
                {
                    var timer = new Timer(_ => Dismiss(notification.Id), null, notification.DurationMs, Timeout.Infinite);
                    _timers[notification.Id] = timer;
                }
            }

            NotifyStateChanged();
        }

        /// <inheritdoc />
        public void ShowSuccess(string message, string? title = null, int? durationMs = null)
        {
            Show(message, ToastSeverity.Success, title ?? "Success", durationMs);
        }

        /// <inheritdoc />
        public void ShowError(string message, string? title = null, int? durationMs = null)
        {
            Show(message, ToastSeverity.Error, title ?? "Error", durationMs ?? 8000); // Errors stay longer
        }

        /// <inheritdoc />
        public void ShowWarning(string message, string? title = null, int? durationMs = null)
        {
            Show(message, ToastSeverity.Warning, title ?? "Warning", durationMs ?? 6000);
        }

        /// <inheritdoc />
        public void ShowInfo(string message, string? title = null, int? durationMs = null)
        {
            Show(message, ToastSeverity.Info, title ?? "Info", durationMs);
        }

        /// <inheritdoc />
        public void Dismiss(Guid notificationId)
        {
            lock (_lock)
            {
                DismissInternal(notificationId);
            }
            NotifyStateChanged();
        }

        /// <inheritdoc />
        public void DismissAll()
        {
            lock (_lock)
            {
                var ids = _notifications.Select(n => n.Id).ToList();
                foreach (var id in ids)
                {
                    DismissInternal(id);
                }
            }
            NotifyStateChanged();
        }

        private void DismissInternal(Guid notificationId)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                // Clean up timer if exists
                if (_timers.TryGetValue(notificationId, out var timer))
                {
                    timer.Dispose();
                    _timers.Remove(notificationId);
                }

                _notifications.Remove(notification);
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
