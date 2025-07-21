# Admin API Integration

This document explains the integration approach used between the WebUI and the Admin API service.

## Overview

The ConduitLLM solution has been refactored to break circular dependencies between projects and implement a clean architecture. Previously:

- WebUI directly referenced repositories and services from other projects
- Admin needed access to WebUI models and services
- This created circular dependencies that made the solution harder to maintain
- WebUI had direct database access through Entity Framework Core

The new architecture:

- Moves shared DTOs to the Configuration project, which both WebUI and Admin can reference
- Creates an Admin API client in WebUI that communicates with Admin's endpoints
- Admin API client directly implements service interfaces, removing the need for adapter classes
- WebUI has no direct database access - all data operations go through the Admin API

## Implementation Evolution

The implementation evolved through several phases:

1. **Original Direct Repository Access** (Phase 1):
   ```
   WebUI Component → WebUI Service → Repository → Database
   ```

2. **Adapter Pattern** (Phase 2):
   ```
   WebUI Component → WebUI Service Adapter → Admin API Client → HTTP → Admin API → Repository → Database
   ```

3. **Direct Implementation Pattern** (Phase 3-4):
   ```
   WebUI Component → Admin API Client → HTTP → Admin API → Repository → Database
   ```

## Admin API Client Implementation

The Admin API client directly implements all service interfaces:

```csharp
public partial class AdminApiClient : IVirtualKeyService, IRequestLogService, ICostDashboardService, /* other interfaces */
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminApiClient> _logger;
    
    public AdminApiClient(HttpClient httpClient, ILogger<AdminApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    // Implementation of interface methods directly in AdminApiClient
    public async Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/virtualkeys/{id}");
            
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VirtualKeyDto>(_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving virtual key with ID {VirtualKeyId} from Admin API", id);
            return null;
        }
    }
    
    // Other interface implementations...
}
```

The AdminApiClient class is split into partial classes by domain for better organization:

- AdminApiClient.cs - Core implementation
- AdminApiClient.VirtualKeys.cs - Virtual key management
- AdminApiClient.RequestLogs.cs - Request logging
- AdminApiClient.CostDashboard.cs - Cost dashboard
- etc.

## Implemented Interfaces

The AdminApiClient directly implements these interfaces:

| Interface | Purpose |
|-----------|---------|
| IGlobalSettingService | Manage global application settings |
| IVirtualKeyService | Manage virtual API keys |
| IRequestLogService | Access and analyze request logs |
| IModelCostService | Manage and retrieve model cost information |
| IProviderHealthService | Monitor LLM provider health |
| IProviderCredentialService | Manage provider credentials |
| IIpFilterService | Manage IP whitelist/blacklist filtering |
| ICostDashboardService | Retrieve cost analytics and dashboard data |
| IRouterService | Configure and manage the LLM router |

## Configuration

The Admin API client is configured through environment variables:

```bash
CONDUIT_ADMIN_API_BASE_URL=http://localhost:5000
CONDUIT_MASTER_KEY=your-master-key
```

In docker-compose, this configuration looks like:

```yaml
webui:
  environment:
    CONDUIT_MASTER_KEY: alpha
    CONDUIT_ADMIN_API_BASE_URL: http://admin:8080
```

## Service Registration

The AdminApiClient is registered in `Program.cs` and implements multiple interfaces:

```csharp
// Register the Admin API client
builder.Services.AddAdminApiClient(builder.Configuration);

// Add caching decorator for Admin API client
builder.Services.Decorate<IAdminApiClient, CachingAdminApiClient>();

// Register direct interface implementation
services.AddScoped<IVirtualKeyService>(sp => sp.GetRequiredService<IAdminApiClient>());
services.AddScoped<ICostDashboardService>(sp => sp.GetRequiredService<IAdminApiClient>());
// ... other interface registrations
```

## Error Handling

The AdminApiClient implements comprehensive error handling:

1. Every method is wrapped in a try/catch block
2. Exceptions are logged with contextual information
3. Appropriate fallback values are returned (empty collections, null, default values)
4. Errors don't propagate to UI components, ensuring graceful degradation

## Benefits

This direct implementation architecture provides several benefits:

1. **Decoupled Projects**: WebUI and Admin no longer have circular dependencies
2. **Simplified Architecture**: Removes unnecessary adapter layer
3. **Deployment Flexibility**: WebUI and Admin can be deployed separately
4. **Service Boundaries**: Clear API boundaries between UI and back-end services
5. **Interface Compatibility**: Existing code continues to work without changes
6. **Simplified Testing**: Services can be tested independently
7. **Graceful Degradation**: Comprehensive error handling at client level
8. **Configuration Control**: Centralized configuration through environment variables