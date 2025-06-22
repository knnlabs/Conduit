using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Types of toast notifications
    /// </summary>
    public enum ToastType
    {
        /// <summary>
        /// Success notification (green)
        /// </summary>
        Success,

        /// <summary>
        /// Information notification (blue)
        /// </summary>
        Info,

        /// <summary>
        /// Warning notification (yellow)
        /// </summary>
        Warning,

        /// <summary>
        /// Error notification (red)
        /// </summary>
        Error
    }

    /// <summary>
    /// Toast notification model
    /// </summary>
    public class ToastNotification
    {
        /// <summary>
        /// Unique identifier for the toast
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of toast notification
        /// </summary>
        public ToastType Type { get; set; }

        /// <summary>
        /// Title of the notification
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Message content
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Duration in milliseconds (0 = sticky)
        /// </summary>
        public int Duration { get; set; } = 5000;

        /// <summary>
        /// Whether the toast can be dismissed manually
        /// </summary>
        public bool Dismissible { get; set; } = true;

        /// <summary>
        /// When the toast was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional data for the toast
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Arguments for toast events
    /// </summary>
    public class ToastEventArgs : EventArgs
    {
        /// <summary>
        /// The toast notification
        /// </summary>
        public ToastNotification Toast { get; }

        /// <summary>
        /// Initializes a new instance of ToastEventArgs
        /// </summary>
        public ToastEventArgs(ToastNotification toast)
        {
            Toast = toast;
        }
    }

    /// <summary>
    /// Interface for toast notification service
    /// </summary>
    public interface IToastNotificationService
    {
        /// <summary>
        /// Event raised when a new toast is added
        /// </summary>
        event EventHandler<ToastEventArgs>? ToastAdded;

        /// <summary>
        /// Event raised when a toast is removed
        /// </summary>
        event EventHandler<ToastEventArgs>? ToastRemoved;

        /// <summary>
        /// Gets all active toasts
        /// </summary>
        IReadOnlyList<ToastNotification> ActiveToasts { get; }

        /// <summary>
        /// Shows a success toast
        /// </summary>
        void ShowSuccess(string title, string message, int duration = 5000);

        /// <summary>
        /// Shows an info toast
        /// </summary>
        void ShowInfo(string title, string message, int duration = 5000);

        /// <summary>
        /// Shows a warning toast
        /// </summary>
        void ShowWarning(string title, string message, int duration = 8000);

        /// <summary>
        /// Shows an error toast
        /// </summary>
        void ShowError(string title, string message, int duration = 10000);

        /// <summary>
        /// Shows a rate limit error toast with helpful information
        /// </summary>
        void ShowRateLimitError(string scope, int? retryAfter = null, string? additionalInfo = null);

        /// <summary>
        /// Shows a custom toast
        /// </summary>
        void ShowToast(ToastNotification toast);

        /// <summary>
        /// Removes a specific toast
        /// </summary>
        void RemoveToast(string toastId);

        /// <summary>
        /// Removes all toasts
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Removes toasts of a specific type
        /// </summary>
        void ClearType(ToastType type);
    }

    /// <summary>
    /// Implementation of toast notification service
    /// </summary>
    public class ToastNotificationService : IToastNotificationService, INotifyPropertyChanged
    {
        private readonly List<ToastNotification> _toasts = new();
        private readonly object _lock = new();

        /// <inheritdoc />
        public event EventHandler<ToastEventArgs>? ToastAdded;

        /// <inheritdoc />
        public event EventHandler<ToastEventArgs>? ToastRemoved;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc />
        public IReadOnlyList<ToastNotification> ActiveToasts
        {
            get
            {
                lock (_lock)
                {
                    return _toasts.ToList();
                }
            }
        }

        /// <inheritdoc />
        public void ShowSuccess(string title, string message, int duration = 5000)
        {
            ShowToast(new ToastNotification
            {
                Type = ToastType.Success,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        /// <inheritdoc />
        public void ShowInfo(string title, string message, int duration = 5000)
        {
            ShowToast(new ToastNotification
            {
                Type = ToastType.Info,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        /// <inheritdoc />
        public void ShowWarning(string title, string message, int duration = 8000)
        {
            ShowToast(new ToastNotification
            {
                Type = ToastType.Warning,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        /// <inheritdoc />
        public void ShowError(string title, string message, int duration = 10000)
        {
            ShowToast(new ToastNotification
            {
                Type = ToastType.Error,
                Title = title,
                Message = message,
                Duration = duration
            });
        }

        /// <inheritdoc />
        public void ShowRateLimitError(string scope, int? retryAfter = null, string? additionalInfo = null)
        {
            var title = "Rate Limit Exceeded";
            var message = $"Too many {scope} requests. ";

            if (retryAfter.HasValue)
            {
                if (retryAfter.Value < 60)
                {
                    message += $"Please wait {retryAfter.Value} seconds before trying again.";
                }
                else
                {
                    var minutes = retryAfter.Value / 60;
                    message += $"Please wait {minutes} minute(s) before trying again.";
                }
            }
            else
            {
                message += "Please wait before trying again.";
            }

            if (!string.IsNullOrEmpty(additionalInfo))
            {
                message += $" {additionalInfo}";
            }

            ShowToast(new ToastNotification
            {
                Type = ToastType.Error,
                Title = title,
                Message = message,
                Duration = 12000, // Longer duration for rate limit errors
                Data = new Dictionary<string, object>
                {
                    ["scope"] = scope,
                    ["retryAfter"] = retryAfter ?? 0,
                    ["isRateLimit"] = true
                }
            });
        }

        /// <inheritdoc />
        public void ShowToast(ToastNotification toast)
        {
            lock (_lock)
            {
                _toasts.Add(toast);
            }

            ToastAdded?.Invoke(this, new ToastEventArgs(toast));
            OnPropertyChanged(nameof(ActiveToasts));

            // Auto-remove after duration (if not sticky)
            if (toast.Duration > 0)
            {
                _ = Task.Delay(toast.Duration).ContinueWith(_ => RemoveToast(toast.Id));
            }
        }

        /// <inheritdoc />
        public void RemoveToast(string toastId)
        {
            ToastNotification? removedToast = null;

            lock (_lock)
            {
                var index = _toasts.FindIndex(t => t.Id == toastId);
                if (index >= 0)
                {
                    removedToast = _toasts[index];
                    _toasts.RemoveAt(index);
                }
            }

            if (removedToast != null)
            {
                ToastRemoved?.Invoke(this, new ToastEventArgs(removedToast));
                OnPropertyChanged(nameof(ActiveToasts));
            }
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            List<ToastNotification> toastsToRemove;

            lock (_lock)
            {
                toastsToRemove = _toasts.ToList();
                _toasts.Clear();
            }

            foreach (var toast in toastsToRemove)
            {
                ToastRemoved?.Invoke(this, new ToastEventArgs(toast));
            }

            OnPropertyChanged(nameof(ActiveToasts));
        }

        /// <inheritdoc />
        public void ClearType(ToastType type)
        {
            List<ToastNotification> toastsToRemove;

            lock (_lock)
            {
                toastsToRemove = _toasts.Where(t => t.Type == type).ToList();
                _toasts.RemoveAll(t => t.Type == type);
            }

            foreach (var toast in toastsToRemove)
            {
                ToastRemoved?.Invoke(this, new ToastEventArgs(toast));
            }

            if (toastsToRemove.Any())
            {
                OnPropertyChanged(nameof(ActiveToasts));
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}