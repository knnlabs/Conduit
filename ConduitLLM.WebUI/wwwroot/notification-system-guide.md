# ConduitLLM Notification System Configuration Guide

## Overview

The ConduitLLM notification system keeps administrators informed about important events related to virtual keys, system status, and API usage. This guide explains how to configure and use the notification features.

## Notification Types

ConduitLLM generates several types of notifications:

### Virtual Key Notifications

- **Budget Warnings**: Alerts when keys approach or exceed budget limits
  - 80% of budget reached
  - 95% of budget reached
  - Budget exceeded
- **Expiration Notices**: Notifications about keys nearing or reaching expiration
  - Key expires in 7 days
  - Key expires in 1 day
  - Key expired
- **Usage Alerts**: Notifications about unusual usage patterns
  - Sudden increase in usage
  - Usage from unusual IP addresses
  - Repeated failed authentication attempts

### System Notifications

- **Maintenance Notices**: Information about scheduled maintenance
- **Update Notifications**: Details about system updates and new features
- **Error Alerts**: Notifications about system errors or performance issues

## Configuring Notification Settings

### Accessing Notification Settings

1. Log in to your ConduitLLM WebUI
2. Navigate to "Settings"
3. Select "Notifications" from the submenu

### General Notification Settings

Configure global notification preferences:

- **Notification Retention**: How long notifications are kept before automatic deletion
- **Default Notification Priority**: Set the default priority level for new notifications
- **Notification Batching**: Group similar notifications to reduce volume

### Delivery Methods

ConduitLLM supports multiple notification delivery methods:

#### In-App Notifications

Always enabled, notifications appear in the notification panel within the WebUI.

#### Email Notifications

Configure email delivery:

1. Expand "Email Settings" section
2. Enter SMTP server details:
   - SMTP Server
   - Port
   - Username
   - Password
   - From Email Address
3. Specify recipient email addresses
4. Select which notification types should be sent via email
5. Set minimum priority level for email delivery

#### Webhook Notifications

Set up webhook integration for third-party systems:

1. Expand "Webhook Settings" section
2. Enter webhook URL
3. Configure authentication if required
4. Select notification format (JSON, XML)
5. Choose which notification types to send via webhook
6. Test the webhook connection

### Budget Alert Configuration

Customize budget notification thresholds:

1. Navigate to "Budget Alerts" section
2. Configure warning thresholds (default: 80%, 95%)
3. Set frequency of repeat notifications
4. Define cool-down period between repeated alerts

### Expiration Alert Configuration

Set up key expiration notifications:

1. Navigate to "Expiration Alerts" section
2. Configure notification lead times (default: 7 days, 1 day)
3. Enable/disable expired key notifications
4. Set automatic key disability on expiration

## Using the Notification Panel

### Accessing Notifications

The notification panel is accessible from any page in the WebUI:

1. Click the bell icon in the top navigation bar
2. View notification count indicator for unread notifications
3. Open the panel to see all notifications

### Managing Notifications

Within the notification panel:

- **Read/Unread**: Click a notification to mark it as read
- **Mark All Read**: Button to mark all notifications as read
- **Delete**: Remove individual notifications using the trash icon
- **Clear All**: Remove all notifications at once
- **Filter**: Filter notifications by type, date, or priority

### Notification Dashboard

For more advanced notification management:

1. Click "View All" at the bottom of the notification panel
2. Access the full notification dashboard
3. Use advanced filters and sorting options
4. View notification statistics and trends
5. Export notification history

## Integration with VirtualKeysDashboard

The notification system is integrated with the VirtualKeysDashboard:

- **Live Alerts**: Critical notifications appear as pop-ups on the dashboard
- **Alert Panel**: Dedicated notification panel within the dashboard
- **Context-Aware Notifications**: Clicking a notification related to a specific key can take you to that key's details

## NotificationService API

For programmatic access to notifications, use the NotificationService API:

```csharp
// Get notifications
var notifications = await _notificationService.GetNotificationsAsync(
    filter: NotificationFilter.Unread,
    page: 1,
    pageSize: 20);

// Mark notification as read
await _notificationService.MarkAsReadAsync(notificationId);

// Create a custom notification
await _notificationService.CreateNotificationAsync(
    type: NotificationType.SystemInfo,
    message: "Custom notification message",
    priority: NotificationPriority.Medium,
    virtualKeyId: null);
```

## Best Practices

- **Priority Levels**: Use appropriate priority levels to distinguish between critical and informational notifications
- **Webhook Integration**: Integrate with your existing monitoring systems through webhooks
- **Regular Cleanup**: Periodically clear old notifications to maintain system performance
- **Targeted Distribution**: Configure email recipients based on responsibility areas
- **Notification Testing**: Use the test function to verify notification delivery before deploying

## Troubleshooting

- **Missing Notifications**: Check delivery method configuration and threshold settings
- **Email Delivery Issues**: Verify SMTP settings and recipient email addresses
- **Webhook Failures**: Check webhook URL and authentication details
- **Notification Overload**: Consider adjusting thresholds or batching settings
- **Performance Impact**: Reduce notification retention period if the system shows performance issues
