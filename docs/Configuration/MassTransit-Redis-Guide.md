# MassTransit Redis Configuration Guide

## Issue Summary

The compilation error `'IBusRegistrationConfigurator' does not contain a definition for 'UsingRedis'` occurred because **MassTransit does not support Redis as a message transport**. The `UsingRedis` method does not exist in MassTransit version 8.1.3 or any other version.

## Understanding MassTransit Redis Support

### What Redis IS Used For in MassTransit
- **Saga Persistence**: Storing saga state data
- **Distributed caching**: Via separate Redis integration
- **NOT for message transport**

### What Redis IS NOT Used For in MassTransit
- **Message Transport**: Redis cannot be used as a message broker
- **Message Queuing**: Use RabbitMQ, Azure Service Bus, or other supported transports instead

## Solution Implemented

### Before (Incorrect)
```csharp
x.UsingRedis((context, cfg) =>
{
    cfg.Host(redisConnectionString);
    // ... configuration
});
```

### After (Correct)
```csharp
x.UsingInMemory((context, cfg) =>
{
    // Configure retry policy for reliability
    cfg.UseMessageRetry(r => r.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
    
    // Configure delayed redelivery for failed messages
    cfg.UseDelayedRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30)));
    
    // Configure endpoints
    cfg.ConfigureEndpoints(context);
});
```

## Transport Options in MassTransit

### 1. In-Memory Transport (Current Implementation)
- **Use Case**: Development, single-instance deployments
- **Pros**: Simple, no external dependencies
- **Cons**: Messages lost on restart, no cross-instance communication

### 2. RabbitMQ Transport (Recommended for Production)
- **Use Case**: Production deployments, multiple instances
- **Package**: `MassTransit.RabbitMQ`
- **Configuration**:
```csharp
x.UsingRabbitMq((context, cfg) =>
{
    cfg.Host("rabbitmq://localhost");
    cfg.ConfigureEndpoints(context);
});
```

### 3. Azure Service Bus Transport
- **Use Case**: Azure cloud deployments
- **Package**: `MassTransit.Azure.ServiceBus.Core`

### 4. Amazon SQS Transport
- **Use Case**: AWS cloud deployments
- **Package**: `MassTransit.AmazonSqs`

## Using Redis for Saga Persistence (If Needed)

If you need Redis for saga state storage:

```csharp
builder.Services.AddMassTransit(x =>
{
    // Add saga with Redis persistence
    x.AddSagaStateMachine<YourStateMachine, YourSagaState>()
        .RedisRepository(redisConnectionString);
    
    // Use supported transport (not Redis)
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

## Files Modified

1. `/home/nbn/Code/Conduit/ConduitLLM.Http/Program.cs` - Lines 224-246
2. `/home/nbn/Code/Conduit/ConduitLLM.Admin/Program.cs` - Lines 111-131

## MassTransit.Redis Package Usage

The `MassTransit.Redis` package in your project provides:
- Redis saga repository functionality
- Redis-based distributed lock mechanisms
- NOT transport capabilities

## Alternative Approaches for Production

### Option 1: Upgrade to RabbitMQ
```xml
<PackageReference Include="MassTransit.RabbitMQ" Version="8.1.3" />
```

### Option 2: Use Azure Service Bus
```xml
<PackageReference Include="MassTransit.Azure.ServiceBus.Core" Version="8.1.3" />
```

### Option 3: Continue with In-Memory (Development Only)
Keep current configuration for development/testing environments.

## Current State

- ✅ Compilation errors resolved
- ✅ Build successful
- ✅ In-memory transport configured
- ✅ Event consumers properly registered
- ✅ Retry and redelivery policies configured

The application now uses MassTransit's in-memory transport, which is suitable for development and single-instance deployments. For production with multiple instances, consider upgrading to RabbitMQ or another supported message broker.