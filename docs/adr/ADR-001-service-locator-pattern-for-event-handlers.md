# ADR-001: Service Locator Pattern for Event Handlers

## Status
Accepted

## Context
Event handlers in Conduit may run in different API contexts (Core API vs Admin API) with different available services. When an event handler has a dependency on a service that only exists in one context (e.g., `IProviderDiscoveryService` only in Admin API), constructor injection fails during DI resolution, causing messages to be moved to error queues.

This issue manifested when the `ProviderCredentialEventHandler` in the Core API failed to instantiate because it had a dependency on `IProviderDiscoveryService`, which is only registered in the Admin API. This resulted in all provider credential events failing and accumulating in the `ProviderCredentialEventHandler_error` queue.

## Decision
Use the service locator pattern for optional dependencies in event handlers. Event handlers will:
1. Accept `IServiceProvider` in their constructor for resolving optional dependencies
2. Keep required services (that exist in all contexts) as constructor parameters
3. Resolve optional services at runtime using `IServiceProvider.GetService<T>()`
4. Handle gracefully when optional services are not available

## Consequences

### Positive
- ✅ Prevents DI resolution failures when services are not available in a context
- ✅ Enables graceful degradation when optional services are missing
- ✅ Allows event handlers to work across different API contexts
- ✅ Maintains functionality where services are available
- ✅ Prevents error queue accumulation due to DI failures

### Negative
- ⚠️ Hides dependencies from the constructor signature
- ⚠️ Makes unit testing slightly more complex (need to mock IServiceProvider)
- ⚠️ Reduces compile-time type safety for optional dependencies

### Mitigation
- Comprehensive logging when optional services are not available
- Clear documentation of which services are optional vs required
- Unit tests covering both scenarios (service available and unavailable)
- Metrics tracking when optional services are skipped

## Implementation Example

### Before (Problematic)
```csharp
public class ProviderCredentialEventHandler : IConsumer<ProviderCredentialUpdated>
{
    private readonly IProviderDiscoveryService? _discoveryService;
    
    public ProviderCredentialEventHandler(
        IProviderDiscoveryService? discoveryService)  // Still fails DI resolution
    {
        _discoveryService = discoveryService;
    }
}
```

### After (Resilient)
```csharp
public class ProviderCredentialEventHandler : IConsumer<ProviderCredentialUpdated>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProviderCredentialEventHandler> _logger;
    
    public ProviderCredentialEventHandler(
        IServiceProvider serviceProvider,
        ILogger<ProviderCredentialEventHandler> logger)  // Required services only
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task Consume(ConsumeContext<ProviderCredentialUpdated> context)
    {
        // Resolve optional service at runtime
        var discoveryService = _serviceProvider.GetService<IProviderDiscoveryService>();
        
        if (discoveryService != null)
        {
            try
            {
                await discoveryService.RefreshProviderCapabilitiesAsync(context.Message.ProviderName);
                _logger.LogDebug("Provider capabilities refreshed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh capabilities - continuing without update");
                // Don't rethrow - graceful degradation
            }
        }
        else
        {
            _logger.LogDebug("Provider discovery service not available - skipping capability refresh");
        }
        
        // Continue with required processing...
    }
}
```

## Affected Components
1. **ModelCapabilitiesDiscoveredHandler** - May need `IModelMappingService`
2. **SpendUpdateProcessor** - Needs `IVirtualKeyRepository` 
3. **VirtualKeyCacheInvalidationHandler** - Already uses nullable pattern correctly
4. **GlobalSettingCacheInvalidationHandler** - Already uses nullable pattern correctly

## References
- Issue #391: Implement Service Locator Pattern for Cross-Service Event Handlers
- Epic #385: Event Bus Resilience and Error Queue Management
- MassTransit Documentation on Consumer Dependencies