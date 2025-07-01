# SDK Connection Namespace Changelog

## Overview

Added a dedicated `connection` namespace to both Core and Admin SDKs for managing SignalR connections. This provides a clean, consistent API for controlling real-time connections across both SDKs.

## Changes

### Core SDK (@knn_labs/conduit-core-client)

#### New Features
- Added `ConnectionService` class in `src/services/ConnectionService.ts`
- Added `connection` namespace to `ConduitCoreClient`
- Exported `ConnectionService` from main index

#### Connection Methods
- `connect()` - Connect all SignalR hubs
- `disconnect()` - Disconnect all SignalR hubs  
- `reconnect()` - Disconnect then reconnect all hubs
- `getStatus()` - Get connection status for all hubs
- `isConnected()` - Check if all hubs are connected
- `waitForConnection(timeoutMs)` - Wait for all connections with timeout
- `updateConfiguration(config)` - Update SignalR configuration
- `getDetailedStatus()` - Get detailed status with descriptions
- `onConnectionStateChange(callback)` - Subscribe to state changes (placeholder)

### Admin SDK (@knn_labs/conduit-admin-client)

#### New Features
- Added `ConnectionService` class in `src/services/ConnectionService.ts`
- Added `connection` namespace to `ConduitAdminClient`
- Exported `ConnectionService` from main index

#### Connection Methods
All Core SDK methods plus:
- `isFullyConnected()` - Check if ALL hubs are connected (vs any)
- `connectHub(hubType)` - Connect specific hub ('navigation' or 'notifications')
- `disconnectHub(hubType)` - Disconnect specific hub
- `getHubStatus(hubType)` - Get status of specific hub

## Usage Examples

### Core SDK
```typescript
const client = ConduitCoreClient.fromApiKey('key');

// Manual connection control
await client.connection.connect();
const status = client.connection.getStatus();
await client.connection.disconnect();
```

### Admin SDK  
```typescript
const client = new ConduitAdminClient({ masterKey: 'key', adminApiUrl: 'url' });

// Hub-specific control
await client.connection.connectHub('notifications');
const navStatus = client.connection.getHubStatus('navigation');
await client.connection.disconnect();
```

## Migration

The existing `signalr` property remains available for backward compatibility. New code should use the `connection` namespace for connection management.

### Before
```typescript
await client.signalr.startAllConnections();
```

### After
```typescript
await client.connection.connect();
```

## Testing

- Added comprehensive unit tests for both SDKs
- Added integration examples demonstrating real-world usage
- All tests passing, both SDKs build successfully

## Documentation

- Created `docs/SDK-Connection-Management.md` with complete API documentation
- Added connection management examples for both SDKs
- Updated exports in both SDK index files