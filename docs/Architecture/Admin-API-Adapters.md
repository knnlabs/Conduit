# Admin API Adapters

This document explains the adapter pattern used to integrate the WebUI with the Admin API service.

## Overview

The ConduitLLM solution has been refactored to break circular dependencies between projects. Previously:

- WebUI directly referenced repositories and services from other projects
- Admin needed access to WebUI models and services
- This created circular dependencies that made the solution harder to maintain

The new architecture:

- Moves shared DTOs to the Configuration project, which both WebUI and Admin can reference
- Creates an Admin API client in WebUI that communicates with Admin's endpoints
- Implements adapters in WebUI that maintain the same service interfaces but delegate to the Admin API client

## Adapter Pattern

The adapter pattern allows WebUI to continue using the same interfaces while changing the underlying implementation:

1. **Original Direct Repository Access**:
   ```
   WebUI Component → WebUI Service → Repository → Database
   ```

2. **New Adapter Pattern with API Client**:
   ```
   WebUI Component → WebUI Service Adapter → Admin API Client → HTTP → Admin API → Repository → Database
   ```

## Adapter Implementation

Each adapter follows this pattern:

```csharp
public class SomeServiceAdapter : ISomeService
{
    private readonly IAdminApiClient _adminApiClient;
    private readonly ILogger<SomeServiceAdapter> _logger;
    
    public SomeServiceAdapter(IAdminApiClient adminApiClient, ILogger<SomeServiceAdapter> logger)
    {
        _adminApiClient = adminApiClient;
        _logger = logger;
    }
    
    // Implement ISomeService methods by delegating to _adminApiClient
    public async Task<Result> DoSomethingAsync(...)
    {
        try
        {
            return await _adminApiClient.DoSomethingAsync(...);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DoSomethingAsync");
            return default; // Appropriate error handling
        }
    }
}
```

## Available Adapters

The following service adapters have been implemented:

| Adapter | Interface | Purpose |
|---------|-----------|---------|
| GlobalSettingServiceAdapter | IGlobalSettingService | Manage global application settings |
| VirtualKeyServiceAdapter | IVirtualKeyService | Manage virtual API keys |
| RequestLogServiceAdapter | IRequestLogService | Access and analyze request logs |
| ModelCostServiceAdapter | IModelCostService | Manage and retrieve model cost information |
| ProviderHealthServiceAdapter | IProviderHealthService | Monitor LLM provider health |
| ProviderCredentialServiceAdapter | IProviderCredentialService | Manage provider credentials |
| IpFilterServiceAdapter | IIpFilterService | Manage IP whitelist/blacklist filtering |
| CostDashboardServiceAdapter | ICostDashboardService | Retrieve cost analytics and dashboard data |

## Configuration

The use of adapters vs. direct repository access is controlled by a configuration setting:

```json
{
  "AdminApi": {
    "BaseUrl": "http://localhost:5000",
    "MasterKey": "your-master-key",
    "TimeoutSeconds": 30,
    "UseAdminApi": true
  }
}
```

Or via environment variables:

```bash
CONDUIT_ADMIN_API_URL=http://localhost:5000
CONDUIT_MASTER_KEY=your-master-key
CONDUIT_USE_ADMIN_API=true
```

When `UseAdminApi` is true, the system registers adapter implementations; otherwise, it uses the default direct repository access implementations.

## Service Registration

Adapters are registered in `AdminClientExtensions.cs`:

```csharp
public static IServiceCollection AddAdminApiAdapters(this IServiceCollection services, IConfiguration configuration)
{
    bool useAdminApi = /* get from configuration */;
    
    if (useAdminApi)
    {
        // Register adapters that use the Admin API client
        services.AddScoped<IVirtualKeyService, VirtualKeyServiceAdapter>();
        // ...other adapters
    }
    else
    {
        // Use direct repository access (default registrations)
    }
    
    return services;
}
```

## Error Handling

All adapters implement comprehensive error handling:

1. Every method is wrapped in a try/catch block
2. Exceptions are logged with contextual information
3. Appropriate fallback values are returned (empty collections, null, default values)
4. Errors don't propagate to UI components, ensuring graceful degradation

## Benefits

This adapter-based architecture provides several benefits:

1. **Decoupled Projects**: WebUI and Admin no longer have circular dependencies
2. **Deployment Flexibility**: WebUI and Admin can be deployed separately
3. **Service Boundaries**: Clear API boundaries between UI and back-end services
4. **Interface Compatibility**: Existing code continues to work without changes
5. **Simplified Testing**: Services can be tested independently
6. **Graceful Degradation**: Comprehensive error handling at adapter level
7. **Configuration Control**: Easy to switch between direct and API-based access