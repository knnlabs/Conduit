# Event-Driven Architecture

Conduit uses a comprehensive event-driven architecture to ensure data consistency, eliminate race conditions, and optimize performance across the Core API and Admin API services.

## Overview

The event-driven system is built on **MassTransit** with domain events for critical operations:
- **Virtual Key management** - CRUD operations and spend tracking
- **Provider Credential management** - Provider changes and capability refresh
- **Model Capability discovery** - Automated discovery and caching

## Architecture Benefits

- **Eliminates Race Conditions**: Ordered event processing per entity prevents concurrent update conflicts
- **Cache Consistency**: Event-driven cache invalidation ensures immediate data consistency across services
- **Performance Optimization**: Eliminates N+1 query patterns and redundant external API calls
- **Service Decoupling**: Admin API and Core API coordinate through events instead of direct calls
- **Horizontal Scaling**: Partitioned event processing supports multiple service instances

## Domain Events

### Virtual Key Events

**VirtualKeyCreated**
- **Trigger**: When a new virtual key is created in Admin API
- **Consumers**: Cache invalidation in Core API to ensure immediate recognition
- **Partition Key**: Virtual Key ID (ensures ordered processing)

**VirtualKeyUpdated**
- **Trigger**: When virtual key properties are modified in Admin API
- **Consumers**: Cache invalidation in Core API
- **Partition Key**: Virtual Key ID (ensures ordered processing)

**VirtualKeyDeleted**
- **Trigger**: When virtual key is deleted in Admin API  
- **Consumers**: Cache cleanup in Core API
- **Partition Key**: Virtual Key ID

**SpendUpdateRequested**
- **Trigger**: When spend needs to be updated for a virtual key
- **Consumers**: Ordered spend processing in Core API
- **Partition Key**: Virtual Key ID (prevents race conditions)

**SpendUpdated**
- **Trigger**: After spend is successfully updated
- **Consumers**: Cache invalidation in Core API
- **Partition Key**: Virtual Key ID

### Provider Credential Events

**ProviderCredentialUpdated**
- **Trigger**: When provider credentials are modified in Admin API
- **Consumers**: Capability refresh in Core API
- **Partition Key**: Provider ID

**ProviderCredentialDeleted**
- **Trigger**: When provider credentials are deleted in Admin API
- **Consumers**: Cache cleanup in Core API
- **Partition Key**: Provider ID

### Model Capability Events

**ModelCapabilitiesDiscovered**
- **Trigger**: When model capabilities are discovered for a provider
- **Consumers**: Cache updates, eliminates redundant discovery calls
- **Partition Key**: Provider ID

## Event Processing

### Message Transport

- **Development/Single-instance**: In-memory transport via MassTransit
- **Production/Multi-instance**: Can be upgraded to RabbitMQ, Azure Service Bus, or Amazon SQS
- **Redis Usage**: Used for caching and data protection, not message transport

### Reliability Features

- **Retry Policy**: 3 retries with incremental backoff (1s, 2s, 3s)
- **Dead Letter Queue**: Failed messages with delayed redelivery (5min, 15min, 30min intervals)
- **Graceful Degradation**: Services function normally when event bus is unavailable
- **Error Isolation**: Event failures don't break main business operations

### Ordered Processing

Events are partitioned by entity ID to ensure:
- **Virtual Key events** process in order per virtual key
- **Provider events** process in order per provider
- **Spend updates** eliminate race conditions between individual and batch operations

## Implementation Details

### Event Publishers

**AdminVirtualKeyService** (`ConduitLLM.Admin`)
```csharp
// Publishes VirtualKeyCreated when a new key is created
await _publishEndpoint.Publish(new VirtualKeyCreated
{
    KeyId = virtualKey.Id,
    KeyHash = virtualKey.KeyHash,
    KeyName = virtualKey.KeyName,
    CreatedAt = virtualKey.CreatedAt,
    IsEnabled = virtualKey.IsEnabled,
    AllowedModels = virtualKey.AllowedModels,
    MaxBudget = virtualKey.MaxBudget,
    CorrelationId = Guid.NewGuid().ToString()
});

// Publishes VirtualKeyUpdated when properties change
await _publishEndpoint.Publish(new VirtualKeyUpdated
{
    KeyId = key.Id,
    KeyHash = key.KeyHash,
    ChangedProperties = changedProperties.ToArray(),
    CorrelationId = Guid.NewGuid().ToString()
});
```

**AdminProviderCredentialService** (`ConduitLLM.Admin`)
```csharp
// Publishes ProviderCredentialUpdated when credentials change
await _publishEndpoint.Publish(new ProviderCredentialUpdated
{
    ProviderId = existingCredential.Id,
    ProviderName = existingCredential.ProviderName,
    IsEnabled = existingCredential.IsEnabled,
    ChangedProperties = changedProperties.ToArray()
});
```

**ProviderDiscoveryService** (`ConduitLLM.Core`)
```csharp
// Publishes ModelCapabilitiesDiscovered after discovery
await _publishEndpoint.Publish(new ModelCapabilitiesDiscovered
{
    ProviderId = providerId,
    ProviderName = providerName,
    ModelCapabilities = modelCapabilities,
    DiscoveredAt = DateTime.UtcNow
});
```

### Event Consumers

**VirtualKeyCacheInvalidationHandler** (`ConduitLLM.Http`)
- Handles `VirtualKeyCreated`, `VirtualKeyUpdated`, `VirtualKeyDeleted`, `SpendUpdated`
- Invalidates Redis cache entries for affected virtual keys
- Ensures cache consistency across service instances

**SpendUpdateProcessor** (`ConduitLLM.Http`)
- Handles `SpendUpdateRequested` events
- Processes spend updates in order per virtual key
- Publishes `SpendUpdated` events after successful processing

**ProviderCredentialEventHandler** (`ConduitLLM.Http`)
- Handles `ProviderCredentialUpdated`, `ProviderCredentialDeleted`
- Triggers automatic model capability refresh
- Cleans up cached provider data

## Event Flow Examples

### Virtual Key Creation Flow
1. **Admin API**: User creates a new virtual key via REST API
2. **AdminVirtualKeyService**: Creates key in database and publishes `VirtualKeyCreated`
3. **Core API**: `VirtualKeyCacheInvalidationHandler` receives event
4. **Redis Cache**: Any stale entries for the key hash are invalidated
5. **Next Request**: Fresh data loaded from database, new key immediately available

### Virtual Key Update Flow
1. **Admin API**: User updates virtual key via REST API
2. **AdminVirtualKeyService**: Updates database and publishes `VirtualKeyUpdated`
3. **Core API**: `VirtualKeyCacheInvalidationHandler` receives event
4. **Redis Cache**: Key invalidated immediately across all instances
5. **Next Request**: Fresh data loaded from database

### Provider Credential Change Flow
1. **Admin API**: User updates provider credentials
2. **AdminProviderCredentialService**: Updates database and publishes `ProviderCredentialUpdated`
3. **Core API**: `ProviderCredentialEventHandler` receives event
4. **Discovery Service**: Automatically refreshes model capabilities for provider
5. **Model Capabilities**: New `ModelCapabilitiesDiscovered` event published
6. **Cache**: Provider capability cache updated

### Spend Update Flow
1. **Core API**: Request incurs cost for virtual key
2. **CachedApiVirtualKeyService**: Publishes `SpendUpdateRequested`
3. **SpendUpdateProcessor**: Processes spend update in order
4. **Database**: Spend amount updated atomically
5. **Cache**: `SpendUpdated` event triggers cache invalidation

## Configuration

### RabbitMQ Configuration

For production deployments with multiple service instances, configure RabbitMQ:

```bash
# Core API and Admin API RabbitMQ Configuration
export CONDUITLLM__RABBITMQ__HOST=rabbitmq              # RabbitMQ host (use actual hostname in production)
export CONDUITLLM__RABBITMQ__PORT=5672                  # AMQP port
export CONDUITLLM__RABBITMQ__USERNAME=conduit           # RabbitMQ username
export CONDUITLLM__RABBITMQ__PASSWORD=<secure-password> # RabbitMQ password
export CONDUITLLM__RABBITMQ__VHOST=/                    # Virtual host
export CONDUITLLM__RABBITMQ__PREFETCHCOUNT=10           # Consumer prefetch count
export CONDUITLLM__RABBITMQ__PARTITIONCOUNT=10          # Number of partitions for ordered processing
```

### MassTransit Registration

The event bus automatically detects RabbitMQ configuration and switches from in-memory to RabbitMQ transport:

**Core API** (`ConduitLLM.Http/Program.cs`)
- Configures consumers for cache invalidation, spend processing, and provider events
- Uses partitioned queues for ordered event processing per entity
- Supports both in-memory (single instance) and RabbitMQ (multi-instance) transports

**Admin API** (`ConduitLLM.Admin/Program.cs`)
- Publisher-only configuration (no consumers)
- Simplified RabbitMQ setup for event publishing
- Supports both in-memory and RabbitMQ transports

### Service Registration

Services automatically receive `IPublishEndpoint` when MassTransit is configured:

```csharp
// Optional dependency - gracefully degrades when not available
public AdminVirtualKeyService(
    IVirtualKeyRepository virtualKeyRepository,
    IVirtualKeySpendHistoryRepository spendHistoryRepository,
    IVirtualKeyCache? cache,
    IPublishEndpoint? publishEndpoint, // Optional
    ILogger<AdminVirtualKeyService> logger)
```

## Troubleshooting

### Event Processing Issues

**Check MassTransit Registration**
```bash
# Look for these log messages on startup
[Conduit] Event bus configured with in-memory transport (single-instance mode)
[ConduitLLM.Admin] Event bus configured with in-memory transport (single-instance mode)
```

**Verify Event Publishing**
```bash
# Check logs for event publishing
Published VirtualKeyUpdated event for key {KeyId} with changes: {ChangedProperties}
Published ProviderCredentialUpdated event for provider {ProviderName}
```

**Confirm Event Consumption**
```bash
# Check logs for event processing
Cache invalidated for Virtual Key: {KeyHash}
Spend update processed for key {KeyId}: {Amount}
```

### Performance Monitoring

- **Cache Hit Rates**: Monitor Redis cache statistics
- **Event Processing Time**: Track event handler execution time
- **Database Query Reduction**: Compare before/after query counts
- **External API Calls**: Monitor provider discovery call frequency

## Migration Notes

### From Direct Database Access
- **Before**: Admin API directly invalidated Core API caches
- **After**: Admin API publishes events, Core API handles cache invalidation
- **Benefit**: Eliminates tight coupling between services

### From Polling-Based Updates
- **Before**: NavigationStateService polled APIs every 30 seconds
- **After**: Event-driven updates provide real-time data consistency
- **Benefit**: Reduces unnecessary API calls and database queries

## Future Enhancements

### Additional Events
- **UserActivity events** for audit logging
- **PerformanceMetrics events** for monitoring
- **SecurityAlert events** for threat detection