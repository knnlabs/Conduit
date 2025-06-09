using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for IToastService to provide common notification patterns.
    /// </summary>
    public static class ToastServiceExtensions
    {
        /// <summary>
        /// Shows a success notification for a save operation.
        /// </summary>
        public static void ShowSaveSuccess(this IToastService toastService, string itemName)
        {
            toastService.ShowSuccess($"{itemName} saved successfully");
        }

        /// <summary>
        /// Shows a success notification for a create operation.
        /// </summary>
        public static void ShowCreateSuccess(this IToastService toastService, string itemName)
        {
            toastService.ShowSuccess($"{itemName} created successfully");
        }

        /// <summary>
        /// Shows a success notification for an update operation.
        /// </summary>
        public static void ShowUpdateSuccess(this IToastService toastService, string itemName)
        {
            toastService.ShowSuccess($"{itemName} updated successfully");
        }

        /// <summary>
        /// Shows a success notification for a delete operation.
        /// </summary>
        public static void ShowDeleteSuccess(this IToastService toastService, string itemName)
        {
            toastService.ShowSuccess($"{itemName} deleted successfully");
        }

        /// <summary>
        /// Shows an error notification from an exception.
        /// </summary>
        public static void ShowException(this IToastService toastService, Exception ex, string? prefix = null)
        {
            var message = string.IsNullOrEmpty(prefix) 
                ? ex.Message 
                : $"{prefix}: {ex.Message}";
            
            toastService.ShowError(message, "Error", durationMs: 10000);
        }

        /// <summary>
        /// Shows a validation error notification.
        /// </summary>
        public static void ShowValidationError(this IToastService toastService, string message)
        {
            toastService.ShowError(message, "Validation Error");
        }

        /// <summary>
        /// Shows a notification for a long-running operation starting.
        /// </summary>
        public static void ShowOperationStarted(this IToastService toastService, string operationName)
        {
            toastService.ShowInfo($"{operationName} started...", durationMs: 3000);
        }

        /// <summary>
        /// Shows a notification for a long-running operation completing.
        /// </summary>
        public static void ShowOperationCompleted(this IToastService toastService, string operationName)
        {
            toastService.ShowSuccess($"{operationName} completed successfully");
        }

        /// <summary>
        /// Shows a notification for API connection issues.
        /// </summary>
        public static void ShowApiConnectionError(this IToastService toastService)
        {
            toastService.ShowError(
                "Unable to connect to the Admin API. Please check the connection.", 
                "Connection Error", 
                durationMs: 10000
            );
        }

        /// <summary>
        /// Shows a notification with a retry action.
        /// </summary>
        public static void ShowWithRetry(this IToastService toastService, string message, Action retryAction)
        {
            toastService.Show(
                message, 
                ToastSeverity.Error, 
                title: "Error", 
                durationMs: 0, // Don't auto-dismiss
                actionText: "Retry", 
                actionCallback: retryAction
            );
        }

        /// <summary>
        /// Shows a copy to clipboard success notification.
        /// </summary>
        public static void ShowCopySuccess(this IToastService toastService, string itemName = "Text")
        {
            toastService.ShowSuccess($"{itemName} copied to clipboard", durationMs: 2000);
        }

        /// <summary>
        /// Shows a permission denied notification.
        /// </summary>
        public static void ShowPermissionDenied(this IToastService toastService)
        {
            toastService.ShowError("You don't have permission to perform this action", "Access Denied");
        }
    }
}