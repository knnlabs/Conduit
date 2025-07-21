# DTO Guidelines for Conduit

## Overview
This document establishes guidelines for when and how to use Data Transfer Objects (DTOs) in the Conduit codebase, helping maintain consistency and reduce unnecessary complexity.

## Core Principles

### 1. **Purpose-Driven DTOs**
Every DTO should have a clear purpose. If you can't articulate why a DTO exists separately from an entity or domain model, it probably shouldn't exist.

### 2. **Single Source of Truth**
Avoid having multiple representations of the same data unless there's a compelling reason (security, API versioning, etc.).

### 3. **Explicit Over Implicit**
Make the purpose of each DTO clear through naming and documentation.

## When to Use DTOs

### ✅ **Always Use DTOs For:**

1. **External API Contracts**
   ```csharp
   // API DTOs define the contract with external consumers
   public class VirtualKeyDto  // Good: Hides internal KeyHash from API
   {
       public string Id { get; set; }
       public string Name { get; set; }
       // KeyHash intentionally excluded for security
   }
   ```

2. **Security Boundaries**
   ```csharp
   // Hide sensitive data from external consumers
   public class ProviderCredentialDto
   {
       public string ApiKey => "********";  // Always masked
   }
   ```

3. **Aggregated Data**
   ```csharp
   // Combine data from multiple sources
   public class ProviderHealthDto
   {
       public string ProviderId { get; set; }
       public HealthStatus Status { get; set; }
       public List<HealthCheckResult> RecentChecks { get; set; }
   }
   ```

4. **View-Specific Projections**
   ```csharp
   // Optimized for specific UI needs
   public class DashboardStatsDto
   {
       public int TotalRequests { get; set; }
       public decimal TotalCost { get; set; }
       public Dictionary<string, int> RequestsByModel { get; set; }
   }
   ```

### ❌ **Avoid DTOs When:**

1. **Simple CRUD Operations**
   - If Entity properties map 1:1 to API, consider using the entity directly
   - Only add DTO if you need to hide/transform data

2. **Internal Service Communication**
   - Services within the same bounded context can share domain models
   - Use DTOs only at system boundaries

3. **No Value Added**
   - Don't create DTOs that are exact copies of entities
   - Don't create intermediate DTOs between entity and API DTO

## DTO Patterns

### Pattern 1: Direct Entity to API DTO (Recommended for CRUD)
```
[Database] ↔ Entity ↔ Service ↔ API DTO ↔ [API/Client]
```

**When to use:** Simple CRUD operations with minimal business logic

**Example:**
```csharp
// Service returns DTO directly
public async Task<VirtualKeyDto> GetVirtualKeyAsync(string id)
{
    var entity = await _repository.GetByIdAsync(id);
    return entity.ToDto();  // Direct mapping
}
```

### Pattern 2: Entity to Domain Model to DTO (For Complex Logic)
```
[Database] ↔ Entity ↔ Domain Model ↔ Service ↔ API DTO ↔ [API/Client]
```

**When to use:** Complex business logic, domain-driven design

**Example:**
```csharp
// Domain model encapsulates business logic
public class ProviderRouting  // Domain model
{
    public bool CanHandleRequest(Request request) { /* logic */ }
    public Provider SelectProvider() { /* logic */ }
}

// Service orchestrates domain logic
public async Task<RoutingResultDto> RouteRequestAsync(RequestDto request)
{
    var routing = await _repository.GetRoutingAsync();
    var result = routing.SelectProvider();
    return result.ToDto();
}
```

### Pattern 3: Query-Specific DTOs (For Read Operations)
```
[Database] → Query → Query DTO → [API/Client]
```

**When to use:** Optimized read operations, reporting, dashboards

**Example:**
```csharp
// Optimized query returning DTO directly
public async Task<List<ModelUsageDto>> GetModelUsageAsync()
{
    return await _context.Requests
        .GroupBy(r => r.ModelId)
        .Select(g => new ModelUsageDto
        {
            ModelId = g.Key,
            RequestCount = g.Count(),
            TotalCost = g.Sum(r => r.Cost)
        })
        .ToListAsync();
}
```

## Naming Conventions

### DTOs
- **API DTOs:** `{Entity}Dto` (e.g., `VirtualKeyDto`)
- **Request DTOs:** `{Action}{Entity}Request` (e.g., `CreateVirtualKeyRequest`)
- **Response DTOs:** `{Action}{Entity}Response` (e.g., `CreateVirtualKeyResponse`)
- **Query DTOs:** `{Purpose}Dto` (e.g., `ModelUsageDto`)

### Mappers
- **Extension Methods:** `{Entity}Extensions.cs` with `ToDto()` and `ToEntity()`
- **Mapper Classes:** `{Entity}Mapper.cs` for complex mappings

## Mapping Strategies

### 1. **Extension Methods (Preferred for Simple Mappings)**
```csharp
public static class VirtualKeyExtensions
{
    public static VirtualKeyDto ToDto(this VirtualKey entity)
    {
        return new VirtualKeyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            // Simple property mapping
        };
    }
}
```

### 2. **Static Mapper Classes (For Complex Mappings)**
```csharp
public static class ModelProviderMappingMapper
{
    public static ModelProviderMappingDto ToDto(
        Entities.ModelProviderMapping entity,
        ProviderCredential provider)
    {
        // Complex mapping logic involving multiple entities
    }
}
```

### 3. **AutoMapper (Use Sparingly)**
- Only for very complex mappings with many properties
- Document why manual mapping isn't sufficient
- Profile performance impact

## Common Pitfalls to Avoid

### 1. **DTO Inception**
```csharp
// ❌ Bad: Too many layers
Entity → InternalDto → ServiceDto → ApiDto

// ✅ Good: Direct mapping
Entity → ApiDto
```

### 2. **Leaky Abstractions**
```csharp
// ❌ Bad: Exposing internal structure
public class OrderDto
{
    public EntityState EntityState { get; set; }  // EF Core specific
}

// ✅ Good: Clean contract
public class OrderDto
{
    public string Status { get; set; }  // Business concept
}
```

### 3. **Anemic DTOs**
```csharp
// ❌ Bad: Logic in DTO
public class PriceDto
{
    public decimal Amount { get; set; }
    public decimal CalculateTax() => Amount * 0.08m;  // Business logic!
}

// ✅ Good: Pure data transfer
public class PriceDto
{
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }  // Pre-calculated
}
```

## Backwards Compatibility

### Deprecating DTO Properties
```csharp
public class ProviderCredentialDto
{
    // Current property
    public string ApiBase { get; set; }

    // Deprecated property maintained for compatibility
    [Obsolete("Use ApiBase instead. This property will be removed in v3.0.")]
    public string EndpointUrl 
    { 
        get => ApiBase;
        set => ApiBase = value;
    }
}
```

## Checklist for New DTOs

- [ ] **Clear purpose:** Can I explain why this DTO exists?
- [ ] **No duplication:** Is there already a DTO serving this purpose?
- [ ] **Appropriate location:** Is the DTO in the right project/namespace?
- [ ] **Security considered:** Am I exposing any sensitive data?
- [ ] **Performance considered:** Am I creating unnecessary allocations?
- [ ] **Documented:** Have I added XML comments explaining the DTO's purpose?
- [ ] **Tested:** Have I tested the mapping logic?

## Examples from Conduit

### ✅ **Good Examples:**
- `VirtualKeyDto` - Hides sensitive KeyHash
- `ProviderHealthDto` - Aggregates health information
- `BulkMappingRequest` - Special-purpose API operation

### ❌ **Bad Examples (Now Fixed):**
- `Configuration.ModelProviderMapping` - Redundant intermediate DTO
- Multiple representations of provider settings - Confusing and error-prone

## Summary

The key to good DTO design is intentionality. Every DTO should have a clear, documented purpose. When in doubt, start with the simplest approach (direct entity-to-DTO mapping) and only add complexity when you have a specific need.

Remember: The goal is to make the codebase easier to understand and maintain, not to follow patterns blindly.