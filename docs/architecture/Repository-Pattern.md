# Repository Pattern in ConduitLLM

## Overview

The Repository Pattern provides an abstraction layer between the domain model and data access code. In ConduitLLM, we've implemented this pattern to improve maintainability and testability while maintaining flexibility in our architecture.

## Benefits for ConduitLLM

1. **Separation of Concerns**: Clean separation between domain logic (services) and data access.
2. **Testability**: Services can be unit tested by mocking repositories.
3. **Consistency**: Standardized approach to data access across the codebase.
4. **Flexibility**: Services can work with either direct database access or API calls without changes.
5. **Error Handling**: Centralized error handling for data access operations.

## Implementation in ConduitLLM

In ConduitLLM, the repository pattern is implemented with the following components:

### 1. Repository Interfaces

Repository interfaces define the contract for data access operations. They are located in the `ConduitLLM.Configuration.Repositories` namespace and follow a naming convention of `I[EntityName]Repository`.

Example: `IVirtualKeyRepository` for the `VirtualKey` entity.

Each repository interface typically includes methods for:
- **Retrieving** entities (GetById, GetAll, etc.)
- **Creating** new entities
- **Updating** existing entities
- **Deleting** entities

```csharp
// Example repository interface
public interface IVirtualKeyRepository
{
    Task<VirtualKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<VirtualKey>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
```

### 2. Repository Implementations

Repository implementations contain the actual data access logic. They are located in the `ConduitLLM.Configuration.Repositories` namespace and follow a naming convention of `[EntityName]Repository`.

In our current implementation, all repository implementations use Entity Framework Core for data access. They depend on `IDbContextFactory<ConfigurationDbContext>` to create database contexts as needed.

```csharp
// Example repository implementation
public class VirtualKeyRepository : IVirtualKeyRepository
{
    private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
    private readonly ILogger<VirtualKeyRepository> _logger;

    public VirtualKeyRepository(
        IDbContextFactory<ConfigurationDbContext> dbContextFactory,
        ILogger<VirtualKeyRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VirtualKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.VirtualKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key with ID {KeyId}", id);
            throw;
        }
    }

    // Other method implementations...
}
```

### 3. Service Layer

Services use repositories to perform business operations. They depend on repository interfaces, not concrete implementations, following the Dependency Injection principle.

```csharp
// Example service using repository pattern
public class VirtualKeyService
{
    private readonly IVirtualKeyRepository _virtualKeyRepository;
    private readonly ILogger<VirtualKeyService> _logger;

    public VirtualKeyService(
        IVirtualKeyRepository virtualKeyRepository,
        ILogger<VirtualKeyService> logger)
    {
        _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VirtualKeyDto?> GetVirtualKeyByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var virtualKey = await _virtualKeyRepository.GetByIdAsync(id, cancellationToken);
            return virtualKey != null ? MapToDto(virtualKey) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key with ID {KeyId}", id);
            throw;
        }
    }

    // Other method implementations...
}
```

### 4. Dependency Registration

Repositories and services are registered in the dependency injection container in the `ServiceCollectionExtensions` class:

```csharp
// Extension method to register repositories
public static IServiceCollection AddRepositories(this IServiceCollection services)
{
    // Register repositories
    services.AddScoped<IVirtualKeyRepository, VirtualKeyRepository>();
    services.AddScoped<IProviderCredentialRepository, ProviderCredentialRepository>();
    services.AddScoped<IGlobalSettingRepository, GlobalSettingRepository>();
    // ... more repositories

    return services;
}
```

## Gradual Adoption Strategy

We're adopting the Repository Pattern gradually in ConduitLLM, focusing first on core entities while maintaining backward compatibility.

### Phase 1: Core Entities (Completed)
- VirtualKey
- GlobalSettings
- ProviderCredentials
- ModelProviderMappings
- ModelCosts
- RequestLogs

### Phase 2: Extended Entities (Implemented)
- Notifications
- VirtualKeySpendHistory
- RouterConfig
- ModelDeployment
- FallbackConfiguration
- FallbackModelMapping

### Phase 3: Additional Features (Pending)
- Implement additional query methods for specific use cases
- Enhance caching strategies for read-heavy operations
- Add pagination support for large result sets

## Repository vs. Direct Database Access

The Repository Pattern doesn't completely replace direct database access. For ConduitLLM's architecture:

- **WebUI Admin Pages**: Continue using repositories through services for consistency
- **API Controllers**: Should use repositories for consistency and testability
- **Background Services**: Should use repositories for testability and maintainability

## Code Example: Before and After

**Before (Direct Database Access):**

```csharp
public class VirtualKeyService
{
    private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
    
    public async Task<bool> ResetSpendAsync(int id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var virtualKey = await context.VirtualKeys.FindAsync(id);
        if (virtualKey == null) return false;
        
        virtualKey.CurrentSpend = 0;
        // ...
        await context.SaveChangesAsync();
        return true;
    }
}
```

**After (Repository Pattern):**

```csharp
public class VirtualKeyService
{
    private readonly IVirtualKeyRepository _repository;
    private readonly ILogger<VirtualKeyService> _logger;
    
    public async Task<bool> ResetSpendAsync(int id)
    {
        try
        {
            var virtualKey = await _repository.GetByIdAsync(id);
            if (virtualKey == null) return false;
            
            virtualKey.CurrentSpend = 0;
            // ...
            return await _repository.UpdateAsync(virtualKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting spend for virtual key {Id}", id);
            throw;
        }
    }
}
```

## Testing with Repositories

Repositories make it easier to write tests that don't depend on a database:

```csharp
[Fact]
public async Task ResetSpend_ShouldResetCurrentSpend()
{
    // Arrange
    var mockRepo = new Mock<IVirtualKeyRepository>();
    var virtualKey = new VirtualKey { Id = 1, CurrentSpend = 100.0m };
    
    mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
        .ReturnsAsync(virtualKey);
    mockRepo.Setup(r => r.UpdateAsync(It.IsAny<VirtualKey>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);
    
    var service = new VirtualKeyService(mockRepo.Object, Mock.Of<ILogger<VirtualKeyService>>());
    
    // Act
    var result = await service.ResetSpendAsync(1);
    
    // Assert
    Assert.True(result);
    Assert.Equal(0, virtualKey.CurrentSpend);
}
```

## Best Practices for ConduitLLM Repositories

When working with repositories in ConduitLLM, follow these guidelines:

1. **Keep repositories focused on CRUD operations** - Complex business logic belongs in services
2. **Use async methods with cancellation token support** - All repository methods should be asynchronous
3. **Include error handling** - Log exceptions and rethrow them for handling by higher layers
4. **Keep database contexts short-lived** - Always use `using` to ensure proper disposal
5. **Include entity relations when needed** - Use Include() for specific operations that need related data
6. **Implement repository-specific queries** - For complex filtering or searching operations
7. **Return null for not found entities** - When an entity is not found, return null (not exceptions)
8. **Consider using DTOs at service boundaries** - Convert between entities and DTOs in services