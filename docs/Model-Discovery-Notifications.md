# Model Discovery Notifications

## Overview

Model Discovery Notifications provide real-time updates when LLM providers add new models, change capabilities, update pricing, or deprecate models. This feature helps administrators and developers stay informed about changes in the AI landscape without manually checking provider documentation.

## Architecture

### Event Flow

1. **Discovery Process**: The `ProviderDiscoveryService` periodically checks each configured provider for available models
2. **Event Publishing**: When changes are detected, a `ModelCapabilitiesDiscovered` event is published via MassTransit
3. **Notification Handler**: The `ModelDiscoveryNotificationHandler` consumes these events and:
   - Compares with cached state to detect changes
   - Generates specific notifications for different types of changes
   - Sends real-time updates via SignalR
4. **Client Updates**: WebUI components receive notifications and display them to users

### Components

#### Backend Components

- **ModelDiscoveryHub** (`/hubs/model-discovery`): SignalR hub for real-time notifications
- **ModelDiscoveryNotificationHandler**: Event consumer that detects changes and sends notifications
- **Model Discovery DTOs**: Notification types for different events

#### Frontend Components

- **ModelDiscoveryListener**: Blazor component that connects to the hub and displays notifications

## Notification Types

### 1. New Models Discovered
Triggered when a provider adds new models to their API.

```json
{
  "provider": "OpenAI",
  "newModels": [
    {
      "modelId": "gpt-4-turbo-2024-01-25",
      "displayName": "GPT-4 Turbo",
      "capabilities": {
        "chat": true,
        "vision": true,
        "functionCalling": true,
        "maxTokens": 128000
      }
    }
  ],
  "totalModelCount": 24,
  "discoveredAt": "2024-01-25T10:30:00Z"
}
```

### 2. Model Capabilities Changed
Triggered when a model's capabilities are modified (e.g., vision support added).

```json
{
  "provider": "Anthropic",
  "modelId": "claude-3-opus-20240229",
  "changes": [
    "Vision: false → true",
    "Max tokens: 100000 → 200000"
  ],
  "changedAt": "2024-01-25T11:00:00Z"
}
```

### 3. Pricing Updated
Triggered when model pricing changes.

```json
{
  "provider": "OpenAI",
  "modelId": "gpt-4",
  "previousPricing": {
    "inputTokenCost": 0.03,
    "outputTokenCost": 0.06
  },
  "newPricing": {
    "inputTokenCost": 0.01,
    "outputTokenCost": 0.03
  },
  "percentageChange": -66.67,
  "updatedAt": "2024-01-25T12:00:00Z"
}
```

### 4. Model Deprecated
Triggered when a provider announces model deprecation.

```json
{
  "provider": "OpenAI",
  "modelId": "text-davinci-003",
  "deprecationDate": "2024-02-01T00:00:00Z",
  "sunsetDate": "2024-03-01T00:00:00Z",
  "replacementModel": "gpt-3.5-turbo-instruct",
  "notes": "Legacy completion model being replaced",
  "announcedAt": "2024-01-25T09:00:00Z"
}
```

## SignalR Integration

### Hub Methods

#### Client-to-Server Methods

- `SubscribeToProvider(string providerName)`: Subscribe to updates for a specific provider
- `UnsubscribeFromProvider(string providerName)`: Unsubscribe from provider updates
- `SubscribeToAll()`: Subscribe to all provider updates (requires admin permissions)
- `RefreshProviderModels(string providerName)`: Request immediate model discovery

#### Server-to-Client Events

- `NewModelsDiscovered`: New models added by a provider
- `ModelCapabilitiesChanged`: Model capabilities modified
- `ModelPricingUpdated`: Pricing changes detected
- `ModelDeprecated`: Deprecation announcements

### Authentication

The ModelDiscoveryHub requires virtual key authentication. Admin keys can subscribe to all providers, while regular keys can only subscribe to specific providers.

## Usage Examples

### WebUI Component Usage

```razor
@* Subscribe to OpenAI model updates *@
<ModelDiscoveryListener Provider="OpenAI" VirtualKey="@virtualKey" />

@* Subscribe to all providers (admin only) *@
<ModelDiscoveryListener VirtualKey="@adminKey" />
```

### JavaScript Client

```javascript
// Connect to the hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/model-discovery", {
        headers: {
            "X-Virtual-Key": virtualKey
        }
    })
    .withAutomaticReconnect()
    .build();

// Register event handlers
connection.on("NewModelsDiscovered", (notification) => {
    console.log(`${notification.newModels.length} new models discovered for ${notification.provider}`);
});

connection.on("ModelPricingUpdated", (notification) => {
    console.log(`${notification.modelId} price changed by ${notification.percentageChange}%`);
});

// Start connection and subscribe
await connection.start();
await connection.invoke("SubscribeToProvider", "OpenAI");
```

## Configuration

### Enable Model Discovery Notifications

Model discovery notifications are automatically enabled when:
1. Provider credentials are configured
2. Model discovery is enabled for the provider
3. MassTransit event bus is configured

### Discovery Frequency

Model discovery runs on a configurable schedule (default: every 6 hours). This can be adjusted in the provider configuration.

## Benefits

1. **Real-time Awareness**: Stay informed about new models and changes without manual checking
2. **Cost Optimization**: Immediate notification of pricing changes helps optimize costs
3. **Migration Planning**: Early deprecation warnings allow time to plan migrations
4. **Capability Tracking**: Know when models gain new features like vision or function calling
5. **Competitive Advantage**: Be first to adopt new models and capabilities

## Security Considerations

- Only authenticated virtual keys can connect to the hub
- Regular keys can only subscribe to specific providers
- Admin keys required for global subscriptions
- All notifications are filtered based on virtual key permissions

## Performance Impact

- Notifications use SignalR for efficient real-time delivery
- Model comparison uses in-memory caching to minimize database queries
- Discovery events are processed asynchronously without blocking API requests
- Notification history is limited to prevent memory growth

## Troubleshooting

### No Notifications Received

1. Check virtual key has permission to access the provider
2. Verify SignalR connection is established
3. Ensure model discovery is enabled for the provider
4. Check MassTransit event bus is running

### Duplicate Notifications

1. Verify only one instance of the notification handler is running
2. Check for multiple SignalR connections from the same client
3. Review event deduplication settings

## Future Enhancements

1. **Notification Filtering**: Allow subscribing to specific types of changes
2. **Webhook Support**: Send notifications to external webhooks
3. **Historical View**: Store and query notification history
4. **Custom Alerts**: Define rules for specific notification conditions
5. **Regional Availability**: Track model availability by region