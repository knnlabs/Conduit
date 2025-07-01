# SDK Connection Management

Both the Core and Admin SDKs now provide a dedicated `connection` namespace for managing SignalR connections. This namespace provides a clean, consistent API for controlling real-time connections.

## Overview

The `connection` namespace is available on both SDK clients:

```typescript
// Core SDK
const coreClient = ConduitCoreClient.fromApiKey('your-api-key');
await coreClient.connection.connect();

// Admin SDK  
const adminClient = new ConduitAdminClient({ masterKey: 'your-master-key', adminApiUrl: 'url' });
await adminClient.connection.connect();
```

## Core SDK Connection Methods

### Connect/Disconnect

```typescript
// Connect all SignalR hubs
await client.connection.connect();

// Disconnect all SignalR hubs
await client.connection.disconnect();

// Reconnect (disconnect then connect)
await client.connection.reconnect();
```

### Connection Status

```typescript
// Get connection status for all hubs
const status = client.connection.getStatus();
// Returns: { TaskHubClient: 'Connected', VideoGenerationHubClient: 'Connected', ... }

// Check if all hubs are connected
const isConnected = client.connection.isConnected(); // boolean

// Get detailed status with descriptions
const detailed = client.connection.getDetailedStatus();
// Returns array of: { hub: string, state: HubConnectionState, stateDescription: string, isConnected: boolean }
```

### Waiting for Connections

```typescript
// Wait for all connections with timeout (default 30s)
const connected = await client.connection.waitForConnection(10000); // 10 second timeout
if (connected) {
  console.log('All hubs connected');
} else {
  console.log('Timeout - some hubs may not be connected');
}
```

### Configuration Updates

```typescript
// Update SignalR configuration (requires reconnection)
await client.connection.updateConfiguration({
  reconnectDelay: [0, 2000, 5000, 10000], // Custom reconnect delays in ms
  connectionTimeout: 20000, // 20 second timeout
  logLevel: SignalRLogLevel.Information,
});
```

## Admin SDK Connection Methods

The Admin SDK provides all the same methods as Core, plus additional hub-specific controls:

### Hub-Specific Operations

```typescript
// Connect specific hub
await client.connection.connectHub('navigation'); // or 'notifications'

// Disconnect specific hub  
await client.connection.disconnectHub('notifications');

// Get status of specific hub
const navStatus = client.connection.getHubStatus('navigation');
// Returns: HubConnectionState enum value
```

### Additional Status Methods

```typescript
// Check if ANY hub is connected
const anyConnected = client.connection.isConnected(); // boolean

// Check if ALL hubs are connected
const allConnected = client.connection.isFullyConnected(); // boolean
```

## Connection States

Both SDKs use the same `HubConnectionState` enum:

```typescript
enum HubConnectionState {
  Disconnected = 'Disconnected',
  Connecting = 'Connecting', 
  Connected = 'Connected',
  Disconnecting = 'Disconnecting',
  Reconnecting = 'Reconnecting'
}
```

## Auto-Connect Behavior

By default, both SDKs auto-connect SignalR when initialized. You can disable this:

### Core SDK
```typescript
const client = new ConduitCoreClient({
  apiKey: 'your-api-key',
  signalR: {
    enabled: true,
    autoConnect: false // Disable auto-connect
  }
});

// Manually connect when ready
await client.connection.connect();
```

### Admin SDK
```typescript
const client = new ConduitAdminClient({
  masterKey: 'your-master-key',
  adminApiUrl: 'url',
  options: {
    signalR: {
      enabled: true,
      autoConnect: false // Disable auto-connect
    }
  }
});

// Manually connect when ready
await client.connection.connect();
```

## Error Handling

All connection methods throw errors if SignalR is not properly initialized:

```typescript
try {
  await client.connection.connect();
} catch (error) {
  if (error.message.includes('SignalR service is not initialized')) {
    console.error('Client not properly configured for SignalR');
  }
}
```

## Best Practices

1. **Check Connection Before Real-time Operations**
   ```typescript
   if (client.connection.isConnected()) {
     // Safe to use real-time features
   } else {
     await client.connection.connect();
   }
   ```

2. **Handle Connection Failures Gracefully**
   ```typescript
   const connected = await client.connection.waitForConnection(5000);
   if (!connected) {
     console.warn('Real-time features may be unavailable');
     // Fall back to polling or other mechanisms
   }
   ```

3. **Disconnect When Done**
   ```typescript
   // In cleanup/shutdown code
   await client.connection.disconnect();
   ```

4. **Use Hub-Specific Controls (Admin SDK)**
   ```typescript
   // Only connect the hubs you need
   await client.connection.connectHub('notifications');
   // Don't connect navigation hub if not needed
   ```

## Migration from Direct SignalR Access

If you were previously accessing SignalR directly:

### Before
```typescript
// Core SDK
await client.signalr.startAllConnections();
const status = client.signalr.getConnectionStatus();

// Admin SDK  
await client.signalr.connectAll();
const states = client.signalr.getConnectionStates();
```

### After
```typescript
// Core SDK
await client.connection.connect();
const status = client.connection.getStatus();

// Admin SDK
await client.connection.connect();  
const status = client.connection.getStatus();
```

The `signalr` property is still available for advanced use cases, but the `connection` namespace is the recommended approach for connection management.