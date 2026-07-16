# Repository Pattern and Data Access

This document describes the Repository Pattern implementation and Entity Framework Core data access patterns used in ConduitLLM.

## Table of Contents

1. [Overview](#overview)
2. [Repository Pattern Implementation](#repository-pattern-implementation)
3. [DbContext Patterns](#dbcontext-patterns)
4. [Best Practices](#best-practices)
5. [Testing](#testing)
6. [Migration Guide](#migration-guide)

---

## Overview

The Repository Pattern provides an abstraction layer between the domain model and data access code. In ConduitLLM, we've implemented this pattern alongside specific Entity Framework Core patterns to improve maintainability, testability, and flexibility.

### Benefits

1. **Separation of Concerns**: Clean separation between domain logic (services) and data access
2. **Testability**: Services can be unit tested by mocking repositories
3. **Consistency**: Standardized approach to data access across the codebase
4. **Flexibility**: Services can work with either direct database access or API calls without changes
5. **Error Handling**: Centralized error handling for data access operations

---

## Repository Pattern Implementation

### Architecture

The repository pattern is the standard approach for data access in Conduit:

1. Repository interfaces define data access contracts
2. Repository implementations use Entity Framework Core for database operations
3. Services use repositories through dependency injection
4. Controllers depend on services, not directly on repositories
5. All data access goes through the repository layer

### Repository Interfaces

Repository interfaces define the contract for data access operations. They are located in the `ConduitLLM.Configuration.Repositories` namespace and follow a naming convention of `I[EntityName]Repository`.

**Example: IVirtualKeyRepository**

```csharp
public interface IVirtualKeyRepository
{
    Task<VirtualKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<VirtualKey>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
```

Each repository interface typically includes methods for:
- **Retrieving** entities (GetById, GetAll, etc.)
- **Creating** new entities
- **Updating** existing entities
- **Deleting** entities

### Repository Implementations

Repository implementations contain the actual data access logic. They use Entity Framework Core and depend on `ConfigurationDbContext` (concrete class, not interface - see [DbContext Patterns](#dbcontext-patterns) below).

**Example: VirtualKeyRepository**

```csharp
public class VirtualKeyRepository : IVirtualKeyRepository
{
    private readonly ConfigurationDbContext _context;
    private readonly ILogger<VirtualKeyRepository> _logger;

    public VirtualKeyRepository(
        ConfigurationDbContext context,
        ILogger<VirtualKeyRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VirtualKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.VirtualKeys
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

### Implemented Repositories

#### Core Repositories
- `IVirtualKeyRepository` - Virtual key management
- `IGlobalSettingRepository` - Global settings
- `IProviderCredentialRepository` - Provider credentials
- `IModelProviderMappingRepository` - Model mappings
- `IModelCostRepository` - Model costs
- `IRequestLogRepository` - Request logs

#### Extended Repositories
- `INotificationRepository` - Notifications
- `IVirtualKeySpendHistoryRepository` - Spend tracking
- `IRouterConfigRepository` - Router configuration
- `IModelDeploymentRepository` - Model deployments
- `IFallbackConfigurationRepository` - Fallback configs
- `IFallbackModelMappingRepository` - Fallback mappings

#### Audio Repositories
- `IAudioProviderConfigRepository` - Audio provider configurations
- `IAudioCostRepository` - Audio operation costs
- `IAudioUsageLogRepository` - Audio usage logs

### Service Layer

Services use repositories to perform business operations. They depend on repository interfaces, not concrete implementations, following the Dependency Injection principle.

**Example: VirtualKeyService**

```csharp
public class VirtualKeyService
{
    private readonly IVirtualKeyRepository _virtualKeyRepository;
    private readonly ILogger<VirtualKeyService> _logger;

    public VirtualKeyService(
        IVirtualKeyRepository virtualKeyRepository,
        ILogger<VirtualKeyService> logger)
    {
        _virtualKeyRepository = virtualKeyRepository;
        _logger = logger;
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
}
```

### Dependency Registration

Repositories and services are registered using extension methods:

**Repository Registration:**

```csharp
public static class RepositoryExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Register all repositories
        services.AddScoped<IVirtualKeyRepository, VirtualKeyRepository>();
        services.AddScoped<IVirtualKeySpendHistoryRepository, VirtualKeySpendHistoryRepository>();
        services.AddScoped<IRequestLogRepository, RequestLogRepository>();
        services.AddScoped<IGlobalSettingRepository, GlobalSettingRepository>();
        // ... other repositories

        return services;
    }
}
```

**Service Registration:**

```csharp
public static class RepositoryServiceExtensions
{
    public static IServiceCollection AddRepositoryServices(this IServiceCollection services)
    {
        // Register repositories from ConduitLLM.Configuration
        services.AddRepositories();

        // Register the repository-based service implementations
        services.AddScoped<VirtualKeyServiceNew>();
        services.AddScoped<RequestLogServiceNew>();
        services.AddScoped<CostDashboardServiceNew>();

        return services;
    }
}
```

---

## DbContext Patterns

ConduitLLM uses Entity Framework Core with PostgreSQL as the primary data store. We employ two complementary patterns for DbContext management.

### 1. DbContext Factory Pattern

Used for scenarios requiring multiple short-lived contexts:

```csharp
services.AddDbContextFactory<ConfigurationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});
```

**Use cases:**
- High-throughput controllers that need independent contexts
- Background services processing multiple requests
- Avoiding DbContext thread-safety issues

**Example:**

```csharp
public class HighThroughputService
{
    private readonly IDbContextFactory<ConfigurationDbContext> _contextFactory;

    public HighThroughputService(IDbContextFactory<ConfigurationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task ProcessBatchAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            // Process each item with its own context
        }
    }
}
```

### 2. Scoped DbContext Pattern

For repositories and services that expect a single context per request:

```csharp
services.AddScoped<ConfigurationDbContext>(provider =>
{
    var factory = provider.GetRequiredService<IDbContextFactory<ConfigurationDbContext>>();
    return factory.CreateDbContext();
});
```

**Use cases:**
- Repository pattern implementations
- Services that need to track entities across multiple operations
- Ensuring Include() statements work correctly

### 3. Concrete DbContext Usage ⚠️ Important

**Repositories MUST use the concrete `ConfigurationDbContext` class, not an interface:**

```csharp
// ✅ Correct - allows EF Core features to work properly
public class VirtualKeyGroupRepository
{
    private readonly ConfigurationDbContext _context;

    public VirtualKeyGroupRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VirtualKeyGroup>> GetAllWithKeysAsync()
    {
        return await _context.VirtualKeyGroups
            .Include(g => g.VirtualKeys)  // This works!
            .ToListAsync();
    }
}

// ❌ Incorrect - prevents Include() and other EF features
public class VirtualKeyGroupRepository
{
    private readonly IConfigurationDbContext _context;

    // Include() won't work properly with interfaces
}
```

**Rationale:**
- EF Core's Include(), change tracking, and lazy loading require the concrete DbContext
- Interfaces hide important EF Core functionality
- The concrete class provides compile-time checking of EF Core features

### Common Issues and Solutions

#### Issue: Include() Not Loading Navigation Properties

**Symptom:** Navigation properties are null despite using Include()

**Cause:** Using IConfigurationDbContext interface instead of concrete class

**Solution:** Change repository to use ConfigurationDbContext directly

#### Issue: Cannot Resolve DbContext

**Symptom:** DI container cannot resolve ConfigurationDbContext

**Cause:** Only DbContextFactory is registered, not the scoped context

**Solution:** Add scoped registration as shown above

#### Issue: DbContext Already Disposed

**Symptom:** ObjectDisposedException when accessing navigation properties

**Cause:** Context created from factory is disposed too early

**Solution:** Use scoped context for operations that need entity tracking

---

## Best Practices

### Repository Guidelines

1. **Keep repositories focused on CRUD operations** - Complex business logic belongs in services
2. **Use async methods with cancellation token support** - All repository methods should be asynchronous
3. **Include error handling** - Log exceptions and rethrow them for handling by higher layers
4. **Return null for not found entities** - When an entity is not found, return null (not exceptions)
5. **Consider using DTOs at service boundaries** - Convert between entities and DTOs in services
6. **Implement repository-specific queries** - For complex filtering or searching operations
7. **Use concrete DbContext in repositories** - Never abstract EF Core's DbContext behind an interface

### DbContext Guidelines

1. **Choose the right lifetime** - Scoped for repositories, factory for high-throughput scenarios
2. **Be explicit with Includes** - Always use Include() for navigation properties you need
3. **Avoid mixing patterns** - Don't mix factory-created and scoped contexts in the same operation
4. **Keep database contexts short-lived** - Always use `using` to ensure proper disposal (when using factory)
5. **Include entity relations when needed** - Use Include() for specific operations that need related data
6. **Use AsNoTracking for read-only queries** - Improves performance when you don't need change tracking

### Code Quality

1. **Use IQueryable Sparingly** - Prefer materialized collections to avoid leaking EF concerns
2. **Test navigation loading** - Write integration tests to verify Include() works
3. **Monitor connection pool usage** - Ensure current patterns don't exhaust connections
4. **Consider compiled queries** - For frequently-used queries to improve performance

---

## Unbounded Query Prevention

To prevent accidental full table scans and protect production systems, ConduitLLM enforces query result size limits across all repositories.

### The Problem

Unbounded queries like `GetAllAsync()` can cause severe performance issues:
- **Memory pressure**: Loading millions of records into memory
- **Database strain**: Full table scans blocking other queries
- **Response timeouts**: Requests timing out under load
- **Cascading failures**: One bad query affecting entire system

### Solution: Query Monitoring and Explicit Opt-In

#### 1. Query Monitoring Interceptor

All database queries are monitored via `QueryMonitoringInterceptor`:

```csharp
// Configuration (appsettings.json or environment variables)
{
  "QueryMonitoring": {
    "Enabled": true,
    "SlowQueryThresholdMs": 5000,      // Log warning for queries > 5 seconds
    "LargeResultSetThreshold": 1000,   // Log warning for result sets > 1000 rows
    "LogFullCommand": false            // Set true to include SQL in logs (dev only)
  }
}
```

#### 2. Deprecated GetAllAsync Methods

The `GetAllAsync()` method is deprecated on all repository interfaces:

```csharp
// ❌ DEPRECATED - Triggers compile-time warning
var allItems = await repository.GetAllAsync();

// ✅ PREFERRED - Bounded pagination
var (items, totalCount) = await repository.GetPaginatedAsync(page: 1, pageSize: 50);

// ✅ EXPLICIT OPT-IN - For legitimate batch operations (cache warming, exports)
var allItems = await repository.GetAllUnboundedAsync();
```

#### 3. Legitimate Unbounded Queries

Use `GetAllUnboundedAsync()` only for:
- **Cache warming**: Loading small reference tables at startup
- **Data exports**: Admin-initiated bulk exports with user awareness
- **Migrations**: One-time data migration scripts
- **Small reference tables**: Tables guaranteed to have <100 records

```csharp
// Safe - Small reference table, used for cache warming
var settings = await _globalSettingRepository.GetAllUnboundedAsync();
var ipFilters = await _ipFilterRepository.GetAllUnboundedAsync();

// UNSAFE - High-risk tables, always use pagination
// var logs = await _requestLogRepository.GetAllUnboundedAsync(); // DON'T DO THIS
var (logs, total) = await _requestLogRepository.GetPaginatedAsync(1, 100);
```

### Table Risk Classification

| Risk Level | Tables | Policy |
|------------|--------|--------|
| **Critical** | RequestLog, VirtualKeySpendHistory, MediaRecord, AsyncTask | Pagination required, no unbounded access |
| **High** | VirtualKey, Notification, BatchOperationHistory | Pagination strongly recommended |
| **Low** | GlobalSetting, IpFilter, Provider, ModelSeries, ModelAuthor | Unbounded allowed (small tables) |

### Best Practices

1. **Default to pagination**: Always use `GetPaginatedAsync()` unless you have a specific need
2. **Set reasonable page sizes**: Max 100 items per page, default 20
3. **Implement cursor-based pagination**: For real-time data streams
4. **Add WHERE clauses**: Filter at the database level, not in memory
5. **Monitor query performance**: Check logs for `SlowQueryThresholdMs` warnings

### Example: Migrating from GetAllAsync

**Before (anti-pattern):**
```csharp
public async Task<IEnumerable<ItemDto>> GetAllItemsAsync()
{
    var items = await _repository.GetAllAsync(); // Loads ALL records
    return items.Select(MapToDto);
}
```

**After (pagination):**
```csharp
public async Task<PagedResult<ItemDto>> GetItemsAsync(int page, int pageSize)
{
    var (items, totalCount) = await _repository.GetPaginatedAsync(page, pageSize);
    return new PagedResult<ItemDto>
    {
        Items = items.Select(MapToDto).ToList(),
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
    };
}
```

---

## Testing

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

### Unit Test Coverage

We've implemented comprehensive unit tests for repository-based services:

- **VirtualKeyServiceNewTests** - Key generation, validation, budget management
- **CostDashboardServiceNewTests** - Data retrieval and filtering
- **GlobalSettingServiceNewTests** - Settings management and master key operations
- **RouterServiceNewTests** - Router configuration and model deployment management

---

## Migration Guide

### Adding a New Repository

1. **Create the repository interface:**
   ```csharp
   public interface IMyEntityRepository
   {
       Task<MyEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
       Task<List<MyEntity>> GetAllAsync(CancellationToken cancellationToken = default);
       // ... other methods
   }
   ```

2. **Implement the repository:**
   ```csharp
   public class MyEntityRepository : IMyEntityRepository
   {
       private readonly ConfigurationDbContext _context;  // Use concrete class!
       private readonly ILogger<MyEntityRepository> _logger;

       public MyEntityRepository(ConfigurationDbContext context, ILogger<MyEntityRepository> logger)
       {
           _context = context;
           _logger = logger;
       }

       public async Task<MyEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
       {
           return await _context.MyEntities
               .Include(e => e.RelatedEntities)  // Include navigation properties
               .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
       }
   }
   ```

3. **Register the repository:**
   ```csharp
   services.AddScoped<IMyEntityRepository, MyEntityRepository>();
   ```

4. **Use in services:**
   ```csharp
   public class MyService
   {
       private readonly IMyEntityRepository _repository;

       public MyService(IMyEntityRepository repository)
       {
           _repository = repository;
       }
   }
   ```

### Migrating from Direct Database Access

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

### High-Throughput Scenarios

For high-throughput scenarios, use the factory directly:

```csharp
public class BatchProcessor
{
    private readonly IDbContextFactory<ConfigurationDbContext> _contextFactory;

    public BatchProcessor(IDbContextFactory<ConfigurationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task ProcessBatchAsync(List<int> ids)
    {
        foreach (var id in ids)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            // Each iteration gets its own context
            // Perform operations
            await context.SaveChangesAsync();
        }
    }
}
```

---

## Implementation Status

✅ **Complete** - The repository pattern implementation is production-ready:

1. ✅ Core Repositories - All core functionality uses repositories
2. ✅ Audio Repositories - Audio features integrated
3. ✅ Service Layer - All services use repositories
4. ✅ Unit Tests - Comprehensive test coverage
5. ✅ WebAdmin Integration - WebAdmin uses Admin API exclusively (no direct DB access)

---

## Future Considerations

- Consider removing IConfigurationDbContext interface entirely (if it exists)
- Evaluate if all repositories need the same pattern
- Consider using compiled queries for frequently-used queries
- Monitor connection pool usage with current patterns
- Add pagination support for large result sets
- Enhance caching strategies for read-heavy operations

---

## Related Documentation

- [Background Services Pattern](./background-services-and-workers.md) - Background service implementations
- [DTO Guidelines](../data-transfer/dto-guidelines.md) - Data transfer object patterns
- [Provider Architecture](../provider-system/provider-architecture.md) - Provider system design
