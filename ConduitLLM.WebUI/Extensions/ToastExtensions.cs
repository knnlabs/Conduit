using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for IToastNotificationService to provide backward compatibility.
    /// </summary>
    public static class ToastExtensions
    {
        /// <summary>
        /// Shows a success message with default title.
        /// </summary>
        public static void ShowSuccess(this IToastNotificationService service, string message)
        {
            service.ShowSuccess("Success", message);
        }

        /// <summary>
        /// Shows an error message with default title.
        /// </summary>
        public static void ShowError(this IToastNotificationService service, string message)
        {
            service.ShowError("Error", message);
        }

        /// <summary>
        /// Shows a warning message with default title.
        /// </summary>
        public static void ShowWarning(this IToastNotificationService service, string message)
        {
            service.ShowWarning("Warning", message);
        }

        /// <summary>
        /// Shows an info message with default title.
        /// </summary>
        public static void ShowInfo(this IToastNotificationService service, string message)
        {
            service.ShowInfo("Information", message);
        }

        /// <summary>
        /// Shows a save success message.
        /// </summary>
        public static void ShowSaveSuccess(this IToastNotificationService service, string itemName)
        {
            service.ShowSuccess("Saved", $"{itemName} saved successfully");
        }

        /// <summary>
        /// Shows a create success message.
        /// </summary>
        public static void ShowCreateSuccess(this IToastNotificationService service, string itemName)
        {
            service.ShowSuccess("Created", $"{itemName} created successfully");
        }

        /// <summary>
        /// Shows a copy success message.
        /// </summary>
        public static void ShowCopySuccess(this IToastNotificationService service, string message = "Copied to clipboard")
        {
            service.ShowSuccess("Copied", message);
        }

        /// <summary>
        /// Shows an exception error message.
        /// </summary>
        public static void ShowException(this IToastNotificationService service, Exception ex, string context = "An error occurred")
        {
            service.ShowError("Exception", $"{context}: {ex.Message}");
        }

        /// <summary>
        /// Shows a validation error message.
        /// </summary>
        public static void ShowValidationError(this IToastNotificationService service, string message)
        {
            service.ShowError("Validation Error", message);
        }

        /// <summary>
        /// Shows a delete success message.
        /// </summary>
        public static void ShowDeleteSuccess(this IToastNotificationService service, string itemName)
        {
            service.ShowSuccess("Deleted", $"{itemName} deleted successfully");
        }

        /// <summary>
        /// Shows an update success message.
        /// </summary>
        public static void ShowUpdateSuccess(this IToastNotificationService service, string itemName)
        {
            service.ShowSuccess("Updated", $"{itemName} updated successfully");
        }

        /// <summary>
        /// Shows an API connection error message.
        /// </summary>
        public static void ShowApiConnectionError(this IToastNotificationService service, string message = "Failed to connect to API")
        {
            service.ShowError("Connection Error", message);
        }
    }
}