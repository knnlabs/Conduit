# Toast Notification Migration Guide

This guide explains how to migrate Blazor pages from inline alerts to the new toast notification system.

## Overview

The toast notification system provides a modern, non-intrusive way to display feedback to users. It replaces:
- Inline alert divs that take up space in the UI
- JavaScript `alert()` calls that block user interaction
- Static error/success messages that require manual dismissal

## Key Components

### 1. **ToastService** (`IToastService`)
The core service for managing toast notifications.

### 2. **ToastContainer**
The component that displays active toast notifications. Already included in `MainLayout.razor`.

### 3. **Toast**
Individual toast notification component with animations and auto-dismiss functionality.

### 4. **ToastServiceExtensions**
Helper methods for common notification patterns.

## Migration Steps

### Step 1: Inject the Toast Service

Add the toast service injection to your page:

```razor
@inject IToastService ToastService
```

### Step 2: Remove Inline Alert HTML

Replace patterns like this:

```razor
@if (!string.IsNullOrEmpty(errorMessage))
{
    <div class="alert alert-danger">
        @errorMessage
    </div>
}
```

With toast notifications in your code.

### Step 3: Update Error Handling

Replace error message assignments:

```csharp
// Old pattern
catch (Exception ex)
{
    errorMessage = $"Error: {ex.Message}";
}

// New pattern
catch (Exception ex)
{
    ToastService.ShowException(ex);
    // Or with custom prefix
    ToastService.ShowException(ex, "Failed to save settings");
}
```

### Step 4: Update Success Messages

Replace success patterns:

```csharp
// Old pattern
successMessage = "Settings saved successfully";

// New pattern
ToastService.ShowSuccess("Settings saved successfully");
// Or use extension methods
ToastService.ShowSaveSuccess("Settings");
```

### Step 5: Replace JavaScript Alerts

Replace JavaScript alert calls:

```csharp
// Old pattern
await JSRuntime.InvokeVoidAsync("alert", "Copied to clipboard!");

// New pattern
ToastService.ShowCopySuccess();
```

## Common Patterns

### CRUD Operations

```csharp
// Create
ToastService.ShowCreateSuccess("Virtual key");

// Update
ToastService.ShowUpdateSuccess("Configuration");

// Delete
ToastService.ShowDeleteSuccess("Provider");

// Save
ToastService.ShowSaveSuccess("Settings");
```

### Error Handling

```csharp
// Generic error
ToastService.ShowError("An unexpected error occurred");

// Exception with context
ToastService.ShowException(ex, "Failed to load data");

// Validation error
ToastService.ShowValidationError("Please fill in all required fields");

// API connection error
ToastService.ShowApiConnectionError();
```

### Long-Running Operations

```csharp
// Start operation
ToastService.ShowOperationStarted("Database backup");

// Complete operation
ToastService.ShowOperationCompleted("Database backup");
```

### Advanced Features

```csharp
// Custom duration (in milliseconds)
ToastService.ShowInfo("This will disappear in 3 seconds", durationMs: 3000);

// No auto-dismiss (user must close manually)
ToastService.ShowWarning("Important message", durationMs: 0);

// With action button
ToastService.ShowWithRetry(
    "Failed to connect to server", 
    retryAction: async () => await RetryConnection()
);

// Custom styling
ToastService.Show(
    "Custom notification",
    ToastSeverity.Info,
    title: "Custom Title",
    additionalCssClass: "my-custom-toast"
);
```

## Best Practices

### 1. **Remove State Variables**
After migration, remove unused state variables:
```csharp
// Remove these
private string? errorMessage;
private string? successMessage;
```

### 2. **Use Appropriate Severity**
- **Success**: Successful operations
- **Error**: Failures and exceptions
- **Warning**: Important notices
- **Info**: General information

### 3. **Keep Messages Concise**
Toast notifications should be brief and actionable.

### 4. **Duration Guidelines**
- Success: 5 seconds (default)
- Error: 8-10 seconds
- Warning: 6 seconds
- Info: 5 seconds

### 5. **Avoid Notification Spam**
Don't show multiple notifications for a single action. Group related messages when possible.

## Example: Complete Page Migration

See `VirtualKeyEditToast.razor` for a complete example of a migrated page. Key changes:

1. Removed all inline alert HTML
2. Replaced error message state with toast calls
3. Converted JavaScript alerts to toast notifications
4. Added appropriate success/error notifications for all operations
5. Used extension methods for common patterns

## Testing Your Migration

1. Test all error scenarios
2. Verify success messages appear
3. Check auto-dismiss timing
4. Ensure notifications don't overlap important UI
5. Test on mobile devices
6. Verify keyboard accessibility

## Troubleshooting

### Notifications Not Appearing
- Ensure `ToastContainer` is in `MainLayout.razor`
- Verify `IToastService` is registered in `Program.cs`
- Check browser console for errors

### Styling Issues
- Ensure `toast.css` is referenced in `App.razor`
- Check for CSS conflicts with existing styles
- Verify z-index is high enough (9999)

### Performance
- Limit concurrent notifications (max 5 by default)
- Use appropriate durations
- Don't create notifications in tight loops

## Summary

The toast notification system provides a modern, user-friendly way to display feedback. By following this guide, you can quickly migrate pages to use this improved notification system, resulting in a cleaner UI and better user experience.