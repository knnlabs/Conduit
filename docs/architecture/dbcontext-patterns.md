# DbContext Patterns in ConduitLLM

This document describes the Entity Framework Core patterns used in ConduitLLM and the rationale behind recent architectural changes.

## Overview

ConduitLLM uses Entity Framework Core with PostgreSQL as the primary data store. The architecture has evolved to address specific challenges with DbContext management and navigation property loading.

## Key Patterns

### 1. DbContext Factory Pattern

We use `IDbContextFactory<ConfigurationDbContext>` for scenarios requiring multiple short-lived contexts:

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

### 2. Scoped DbContext Pattern

For repositories and services that expect a single context per request, we also register a scoped DbContext:

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

### 3. Concrete DbContext Usage

**Important:** Repositories should use the concrete `ConfigurationDbContext` class, not an interface:

```csharp
// ✅ Correct - allows EF Core features to work properly
public class VirtualKeyGroupRepository
{
    private readonly ConfigurationDbContext _context;
    
    public VirtualKeyGroupRepository(ConfigurationDbContext context)
    {
        _context = context;
    }
}

// ❌ Incorrect - prevents Include() and other EF features
public class VirtualKeyGroupRepository
{
    private readonly IConfigurationDbContext _context;
}
```

**Rationale:**
- EF Core's Include(), change tracking, and lazy loading require the concrete DbContext
- Interfaces hide important EF Core functionality
- The concrete class provides compile-time checking of EF Core features

## Migration Guide

If you're adding a new repository:

1. **Inject the concrete DbContext:**
   ```csharp
   public MyRepository(ConfigurationDbContext context)
   ```

2. **Use Include() for navigation properties:**
   ```csharp
   return await _context.MyEntities
       .Include(e => e.RelatedEntities)
       .ToListAsync();
   ```

3. **For high-throughput scenarios, use the factory directly:**
   ```csharp
   public MyService(IDbContextFactory<ConfigurationDbContext> contextFactory)
   {
       using var context = contextFactory.CreateDbContext();
       // Perform operations
   }
   ```

## Common Issues and Solutions

### Issue: Include() Not Loading Navigation Properties

**Symptom:** Navigation properties are null despite using Include()

**Cause:** Using IConfigurationDbContext interface instead of concrete class

**Solution:** Change repository to use ConfigurationDbContext directly

### Issue: Cannot Resolve DbContext

**Symptom:** DI container cannot resolve ConfigurationDbContext

**Cause:** Only DbContextFactory is registered, not the scoped context

**Solution:** Add scoped registration as shown above

### Issue: DbContext Already Disposed

**Symptom:** ObjectDisposedException when accessing navigation properties

**Cause:** Context created from factory is disposed too early

**Solution:** Use scoped context for operations that need entity tracking

## Best Practices

1. **Use concrete DbContext in repositories** - Never abstract EF Core's DbContext behind an interface
2. **Choose the right lifetime** - Scoped for repositories, factory for high-throughput
3. **Be explicit with Includes** - Always use Include() for navigation properties you need
4. **Avoid mixing patterns** - Don't mix factory-created and scoped contexts in the same operation
5. **Test navigation loading** - Write integration tests to verify Include() works

## Future Considerations

- Consider removing IConfigurationDbContext interface entirely
- Evaluate if all repositories need the same pattern
- Consider using compiled queries for frequently-used queries
- Monitor connection pool usage with current patterns