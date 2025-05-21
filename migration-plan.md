# Detailed Migration Plan: WebUI to Admin API

This plan outlines a systematic approach to remove direct database access from the ConduitLLM.WebUI project and standardize on the Admin API architecture.

## 1. Technical Assessment and Current State

### Direct Database Access Components:

- **Entity Framework Dependencies**: Direct registration in Program.cs (lines 50-70)
- **Repository Registrations**: Via AddRepositoryServices() in Program.cs (line 191)
- **Direct Service Implementations**: 10+ service classes with direct database access
- **Dual-Mode Architecture**: Controlled by CONDUIT_USE_ADMIN_API environment variable
- **Middleware Dependencies**: IpFilterMiddleware has direct database access

### Adapter Implementation Status:

- **Implementation Gap**: Some adapter methods like `ResetSpendAsync` in VirtualKeyServiceAdapter need implementation
- **Missing API Endpoints**: Several required endpoints need to be added to AdminApiClient
- **Core Adapter Handling**: Special handling needed for RepositoryVirtualKeyService

## 2. Migration Phases

### Phase 1: Complete API Client Implementation (Week 1)

1. **Complete AdminApiClient Implementation**
   - Add the following API endpoints to the Admin project:
     - `POST /api/virtualkeys/{id}/reset-spend` endpoint for ResetSpendAsync
     - `POST /api/virtualkeys/validate` endpoint for ValidateVirtualKeyAsync
     - `POST /api/virtualkeys/{id}/spend` endpoint for UpdateSpendAsync
     - `POST /api/virtualkeys/{id}/check-budget` endpoint for ResetBudgetIfExpiredAsync
     - `GET /api/virtualkeys/{id}/validation-info` endpoint for GetVirtualKeyInfoForValidationAsync

2. **Complete Adapter Implementations**
   - Update VirtualKeyServiceAdapter to use new endpoints
   - Ensure all adapter methods are fully implemented
   - Add proper error handling and tests
   
3. **Verify IpFilterMiddleware Adapter**
   - Review IpFilterServiceAdapter implementation
   - Implement local caching to improve performance for IP checking
   - Add circuit breaker pattern for API calls

### Phase 2: Standardize on Admin API (Week 2)

1. **Update Program.cs Configuration**
   ```csharp
   // Update around line 63-64 in Program.cs
   // Remove EF DbContext registration
   
   // Update around line 191 in Program.cs
   // Remove: builder.Services.AddRepositoryServices();
   
   // Add default configuration
   builder.Configuration["CONDUIT_USE_ADMIN_API"] = "true";
   ```

2. **Update AdminClientExtensions.cs**
   ```csharp
   // Simplify AddAdminApiAdapters method
   public static IServiceCollection AddAdminApiAdapters(this IServiceCollection services, IConfiguration configuration)
   {
       // Always register adapters - remove conditional logic
       services.AddScoped<Interfaces.IGlobalSettingService, Services.Adapters.GlobalSettingServiceAdapter>();
       services.AddScoped<Interfaces.IProviderHealthService, Services.Adapters.ProviderHealthServiceAdapter>();
       // ... register other adapters
       
       return services;
   }
   ```

3. **Update Core Integration**
   - Update RepositoryVirtualKeyService to use AdminApiClient
   - This service is special because it implements Core's IVirtualKeyService

4. **Add Connection Resilience**
   - Add retry policy for Admin API connection
   ```csharp
   // In AdminClientExtensions.cs
   services.AddHttpClient<IAdminApiClient, AdminApiClient>()
       .AddTransientHttpErrorPolicy(builder => 
           builder.WaitAndRetryAsync(3, retryAttempt => 
               TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
   ```

### Phase 3: Remove Direct Implementations (Week 3)

1. **Remove Direct Service Classes**
   - Delete all direct database services after testing:
     - VirtualKeyService.cs
     - RequestLogService.cs
     - CostDashboardService.cs
     - RouterService.cs
     - GlobalSettingService.cs
     - IpFilterService.cs

2. **Remove Repository Extensions**
   - Delete RepositoryServiceExtensions.cs
   - Remove references to AddRepositoryServices()

3. **Update Project References**
   ```xml
   <!-- Update ConduitLLM.WebUI.csproj -->
   <ItemGroup>
     <!-- Remove Entity Framework packages -->
     <PackageReference Remove="Microsoft.EntityFrameworkCore.Design" />
     <PackageReference Remove="Microsoft.EntityFrameworkCore.Sqlite" />
     <PackageReference Remove="Npgsql.EntityFrameworkCore.PostgreSQL" />
   </ItemGroup>
   ```

### Phase 4: Update Health Checks and Finalize (Week 4)

1. **Update Database Initialization**
   - Replace direct database initialization with Admin API health check
   ```csharp
   // In Program.cs, replace database initialization around line 290
   using (var scope = app.Services.CreateScope())
   {
       var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
       var adminClient = scope.ServiceProvider.GetRequiredService<IAdminApiClient>();
       
       try {
           var systemInfo = await adminClient.GetSystemInfoAsync();
           logger.LogInformation("Connected to Admin API successfully");
       }
       catch (Exception ex) {
           logger.LogError(ex, "Error connecting to Admin API. Application may have limited functionality.");
       }
   }
   ```

2. **Documentation and Testing**
   - Update architecture documentation
   - Create new deployment guides
   - Perform comprehensive integration testing

## 3. Implementation Steps

1. **Create Feature Branch**
   ```bash
   git checkout -b feature/admin-api-only
   ```

2. **Commits Structure**

   **Commit 1: Complete AdminApiClient and Adapters**
   - Add missing API endpoints to Admin project
   - Update adapter implementations to use new endpoints
   - Add proper error handling and tests
   
   **Commit 2: Update Configuration and Startup**
   - Make Admin API mode the default
   - Update AdminClientExtensions.cs to always use adapters
   - Add connection resilience
   
   **Commit 3: Update Core Integration**
   - Update RepositoryVirtualKeyService 
   - Add caching for performance-critical paths
   
   **Commit 4: Remove Direct Database Access**
   - Remove DbContext registrations from Program.cs
   - Remove repository service registrations
   - Remove unused imports and code
   
   **Commit 5: Remove Direct Service Implementations**
   - Delete service classes that directly access database
   - Remove repository extensions
   
   **Commit 6: Update Project Dependencies**
   - Remove Entity Framework packages from project file
   - Update database initialization code
   
   **Commit 7: Documentation and Final Testing**
   - Update architecture documentation
   - Test with Admin API only

## 4. Testing Strategy

1. **Unit Tests**
   - Create unit tests for all adapter implementations
   - Mock AdminApiClient in tests

2. **Integration Tests**
   - Test WebUI with a running Admin API
   - Test error handling and fallback mechanisms

3. **System Tests**
   - Test full system with docker-compose
   - Validate performance under load

4. **Key Testing Scenarios**
   - Admin API unavailable at startup
   - Intermittent API failures during operation
   - Performance testing for high-frequency API calls

## 5. Risk Assessment and Mitigation

1. **Risk**: Performance degradation from API calls
   - **Mitigation**: Add caching for frequently accessed data
   - **Mitigation**: Optimize request batching for bulk operations

2. **Risk**: IpFilterMiddleware performance impact
   - **Mitigation**: Implement local in-memory cache with periodic refreshes
   - **Mitigation**: Consider keeping direct DB access for this critical path only

3. **Risk**: Missing API endpoints for core functionality
   - **Mitigation**: Comprehensive testing of all WebUI features before removing direct DB access

4. **Risk**: Deployment complexity
   - **Mitigation**: Updated docker-compose examples with proper dependencies

## 6. API Endpoints Required

### Virtual Key Management
- `POST /api/virtualkeys/validate` - For validating key tokens
- `POST /api/virtualkeys/{id}/spend` - For updating key spending
- `POST /api/virtualkeys/{id}/reset-spend` - For resetting spending
- `GET /api/virtualkeys/{id}/validation-info` - For getting key details
- `POST /api/virtualkeys/{id}/check-budget` - For budget period checking

### Request Logging
- `POST /api/logs/batch` - For batched request logging
- `GET /api/logs/virtual-key-id/{key}` - For getting virtual key ID from key value

### IP Filtering
- `GET /api/ipfilters/check/{ipAddress}` - For checking if an IP is allowed (performance critical)

## 7. Dependencies Between Services

- **WebUI → Admin API**: Direct HTTP API calls
- **Admin API → Database**: Entity Framework repositories
- **WebUI ↔ HTTP API**: Both must use Admin API for consistency

## 8. Rollback Plan

If issues arise during deployment:

1. Revert to dual-mode architecture by setting `CONDUIT_USE_ADMIN_API=false`
2. Revert removed Entity Framework dependencies if necessary
3. Document any issues encountered for future migration attempts

This migration plan provides a structured approach to complete the transition from direct database access to Admin API usage in ConduitLLM.WebUI while maintaining functionality and ensuring system reliability.