# AdminApiClient Organization

The `AdminApiClient` class is a large class that serves as the main interface to the Admin API. It implements multiple interfaces and contains over 1,700 lines of code.

## Current Structure

The class is organized using C# partial classes:

### Main File: AdminApiClient.cs
Contains the core functionality including:
- Constructor and initialization
- Virtual Keys CRUD operations
- Global Settings management
- Provider Health configuration
- Model Costs management
- Model Provider Mappings
- Provider Credentials
- IP Filters
- Request Logs
- Cost Dashboard
- Router configuration
- Database Backup operations

### Partial Classes (Additional Functionality):
- **AdminApiClient.VirtualKeys.cs** - Extended virtual key operations (validation, spend tracking, maintenance)
- **AdminApiClient.RequestLogs.cs** - IRequestLogService implementation
- **AdminApiClient.ProviderStatus.cs** - Provider status and health monitoring
- **AdminApiClient.ModelProviderMapping.cs** - IModelProviderMappingService implementation
- **AdminApiClient.IpFilters.cs** - IIpFilterService implementation
- **AdminApiClient.HttpConfig.cs** - HTTP configuration management
- **AdminApiClient.CostDashboard.cs** - ICostDashboardService implementation

## Architecture Considerations

### Current Issues:
1. **God Object Pattern**: The class implements 5+ interfaces and handles too many responsibilities
2. **Large File Size**: Main file is 1,714 lines making it hard to navigate
3. **Mixed Concerns**: Combines multiple unrelated domains in a single class
4. **Testing Complexity**: Difficult to unit test specific functionality in isolation

### Recommended Refactoring (Future):
1. **Service Decomposition**: Split into focused service classes (VirtualKeyService, SettingsService, etc.)
2. **Shared Base Class**: Create base class for common HTTP operations
3. **Interface Segregation**: Each service implements only its specific interface
4. **Gradual Migration**: Can be done incrementally without breaking existing code

### Benefits of Current Approach:
- Single HTTP client with shared configuration
- Centralized error handling
- Consistent authentication
- Simplified dependency injection
- Connection pooling benefits

## Usage

The AdminApiClient is registered in DI and various interfaces resolve to it:

```csharp
builder.Services.AddScoped<IGlobalSettingService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<IVirtualKeyService>(sp => sp.GetRequiredService<AdminApiClient>());
builder.Services.AddScoped<IProviderHealthService>(sp => sp.GetRequiredService<AdminApiClient>());
// etc.
```

Components can inject the specific interface they need rather than the entire AdminApiClient.