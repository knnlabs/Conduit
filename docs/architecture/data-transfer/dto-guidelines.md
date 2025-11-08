# DTO Guidelines for Conduit

This document establishes guidelines for when and how to use Data Transfer Objects (DTOs) in the Conduit codebase, including the standardization approach and current implementation status.

## Table of Contents

1. [Overview](#overview)
2. [Implementation Status](#implementation-status)
3. [Core Principles](#core-principles)
4. [When to Use DTOs](#when-to-use-dtOs)
5. [DTO Patterns](#dto-patterns)
6. [Naming Conventions](#naming-conventions)
7. [DTO Location and Organization](#dto-location-and-organization)
8. [Mapping Strategies](#mapping-strategies)
9. [Common Pitfalls](#common-pitfalls)
10. [Checklist for New DTOs](#checklist-for-new-dtos)

---

## Overview

This document explains the DTO (Data Transfer Object) standardization approach used in the ConduitLLM application. As part of the architectural improvements to break circular dependencies between projects, DTOs have been centralized in the Configuration project to provide a clean separation of concerns.

---

## Implementation Status

### ✅ COMPLETED

The DTO standardization has been successfully completed with the following achievements:

1. **✅ Centralized Location**: All 136+ DTOs are now properly located in the `ConduitLLM.Configuration.DTOs` namespace

2. **✅ Domain Organization**: DTOs are organized into domain-specific subdirectories for clean separation:
   - `Audio/` - Audio-related DTOs (6 DTOs)
   - `BatchOperations/` - Batch operation DTOs (8 DTOs)
   - `Cache/` - Cache management DTOs (15 DTOs)
   - `Costs/` - Cost tracking DTOs (5 DTOs)
   - `HealthMonitoring/` - Health monitoring DTOs (13 DTOs)
   - `Metrics/` - System metrics DTOs (20 DTOs)
   - `Security/` - Security-related DTOs (6 DTOs)
   - `VirtualKey/` - Virtual key management DTOs (11 DTOs)
   - Plus additional domain-specific folders

3. **✅ Eliminated Duplicates**: Removed all duplicate DTOs from other projects

4. **✅ Zero Technical Debt**: No backward compatibility properties or legacy aliases maintained

5. **✅ Clean Build**: Solution builds with 0 warnings and 0 errors

6. **✅ Proper References**: All projects correctly reference the Configuration project

---

## Core Principles

### 1. Purpose-Driven DTOs
Every DTO should have a clear purpose. If you can't articulate why a DTO exists separately from an entity or domain model, it probably shouldn't exist.

### 2. Single Source of Truth
Avoid having multiple representations of the same data unless there's a compelling reason (security, API versioning, etc.).

### 3. Explicit Over Implicit
Make the purpose of each DTO clear through naming and documentation.

### 4. No Backward Compatibility Debt ⚠️
When moving DTOs from other projects (such as WebUI or Http) to the Configuration project, revise the consuming code to use the new DTOs. DO NOT allow backward compatibility to proliferate through the codebase. We do not want tech debt!

---

## When to Use DTOs

### ✅ Always Use DTOs For:

#### 1. External API Contracts
```csharp
// API DTOs define the contract with external consumers
public class VirtualKeyDto  // Good: Hides internal KeyHash from API
{
    public string Id { get; set; }
    public string Name { get; set; }
    // KeyHash intentionally excluded for security
}
```

#### 2. Security Boundaries
```csharp
// Hide sensitive data from external consumers
public class ProviderCredentialDto
{
    public string ApiKey => "********";  // Always masked
}
```

#### 3. Aggregated Data
```csharp
// Combine data from multiple sources
public class ProviderHealthDto
{
    public string ProviderId { get; set; }
    public HealthStatus Status { get; set; }
    public List<HealthCheckResult> RecentChecks { get; set; }
}
```

#### 4. View-Specific Projections
```csharp
// Optimized for specific UI needs
public class DashboardStatsDto
{
    public int TotalRequests { get; set; }
    public decimal TotalCost { get; set; }
    public Dictionary<string, int> RequestsByModel { get; set; }
}
```

### ❌ Avoid DTOs When:

1. **Simple CRUD Operations**
   - If Entity properties map 1:1 to API, consider using the entity directly
   - Only add DTO if you need to hide/transform data

2. **Internal Service Communication**
   - Services within the same bounded context can share domain models
   - Use DTOs only at system boundaries

3. **No Value Added**
   - Don't create DTOs that are exact copies of entities
   - Don't create intermediate DTOs between entity and API DTO

---

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

---

## Naming Conventions

DTOs should follow these naming conventions:

### DTO Types

**API DTOs:**
- Format: `{Entity}Dto`
- Examples: `VirtualKeyDto`, `ModelCostDto`, `GlobalSettingDto`

**Request DTOs:**
- Format: `{Action}{Entity}Request` or `Create{Entity}RequestDto`
- Examples: `CreateVirtualKeyRequest`, `UpdateVirtualKeyRequestDto`

**Response DTOs:**
- Format: `{Action}{Entity}Response` or `Create{Entity}ResponseDto`
- Examples: `CreateVirtualKeyResponse`, `CreateVirtualKeyResponseDto`

**Query/Specialized DTOs:**
- Format: `{Purpose}Dto`
- Examples: `CostDashboardDto`, `ModelUsageDto`, `DailyUsageStatsDto`

### Property Naming

1. Use PascalCase for all property names
2. Use descriptive names that indicate the purpose
3. Avoid abbreviations unless widely understood

### Mappers

**Extension Methods:**
- Format: `{Entity}Extensions.cs`
- Methods: `ToDto()` and `ToEntity()`

**Mapper Classes:**
- Format: `{Entity}Mapper.cs`
- Use for complex mappings

---

## DTO Location and Organization

### Namespace and Location

All DTOs should be defined in the `ConduitLLM.Configuration.DTOs` namespace, organized into appropriate subdirectories based on their domain.

**Location:** `/ConduitLLM.Configuration/DTOs/{Domain}/`

**Examples:**
- `/ConduitLLM.Configuration/DTOs/VirtualKey/` - DTOs related to virtual keys
- `/ConduitLLM.Configuration/DTOs/IpFilter/` - DTOs related to IP filtering
- `/ConduitLLM.Configuration/DTOs/Costs/` - Cost-related DTOs
- `/ConduitLLM.Configuration/DTOs/Audio/` - Audio-related DTOs

### DTO Categories

The ConduitLLM application uses several categories of DTOs:

#### 1. Entity DTOs
These DTOs represent database entities and are used for data retrieval and persistence.

Examples: `GlobalSettingDto`, `VirtualKeyDto`, `ModelCostDto`

#### 2. Request/Response DTOs
These DTOs are specifically designed for API endpoints and represent the data sent to (request) or returned from (response) these endpoints.

Examples: `CreateVirtualKeyRequestDto`, `CreateVirtualKeyResponseDto`

#### 3. Specialized DTOs
These DTOs serve specific purposes within the application, such as providing dashboard data or statistics.

Examples: `CostDashboardDto`, `DailyUsageStatsDto`, `LogsSummaryDto`

---

## Mapping Strategies

### 1. Extension Methods (Preferred for Simple Mappings)

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

    public static VirtualKey ToEntity(this VirtualKeyDto dto)
    {
        return new VirtualKey
        {
            Id = dto.Id,
            Name = dto.Name,
            // Simple property mapping
        };
    }
}
```

### 2. Static Mapper Classes (For Complex Mappings)

```csharp
public static class ModelProviderMappingMapper
{
    public static ModelProviderMappingDto ToDto(
        Entities.ModelProviderMapping entity,
        ProviderCredential provider)
    {
        // Complex mapping logic involving multiple entities
        return new ModelProviderMappingDto
        {
            Id = entity.Id,
            ModelAlias = entity.ModelAlias,
            ProviderName = provider.Name,
            // ... complex mapping
        };
    }
}
```

### 3. AutoMapper (Use Sparingly)

- Only for very complex mappings with many properties
- Document why manual mapping isn't sufficient
- Profile performance impact

---

## Common Pitfalls

### 1. DTO Inception

```csharp
// ❌ Bad: Too many layers
Entity → InternalDto → ServiceDto → ApiDto

// ✅ Good: Direct mapping
Entity → ApiDto
```

### 2. Leaky Abstractions

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

### 3. Anemic DTOs (Logic in DTOs)

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

---

## Checklist for New DTOs

Before creating a new DTO, verify:

- [ ] **Clear purpose:** Can I explain why this DTO exists?
- [ ] **No duplication:** Is there already a DTO serving this purpose?
- [ ] **Appropriate location:** Is the DTO in the right project/namespace (`ConduitLLM.Configuration.DTOs.{Domain}`)?
- [ ] **Security considered:** Am I exposing any sensitive data?
- [ ] **Performance considered:** Am I creating unnecessary allocations?
- [ ] **Documented:** Have I added XML comments explaining the DTO's purpose?
- [ ] **Tested:** Have I tested the mapping logic?
- [ ] **No backward compatibility:** Am I creating technical debt with legacy aliases?

---

## Documentation Requirements

All DTOs must include:

1. **XML documentation comments** for the class and all properties
2. **Clear descriptions** of each property's purpose and valid values
3. **Indication of required vs. optional** properties
4. **Examples** where appropriate

**Example:**

```csharp
/// <summary>
/// Represents a virtual API key with budget and rate limiting information.
/// </summary>
public class VirtualKeyDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the virtual key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the friendly name for this virtual key.
    /// Required. Maximum length: 100 characters.
    /// </summary>
    public string KeyName { get; set; }

    /// <summary>
    /// Gets or sets the maximum budget allowed for this key.
    /// Optional. If null, no budget limit is enforced.
    /// </summary>
    public decimal? MaxBudget { get; set; }
}
```

---

## Examples of Standardized DTOs

### Cost Dashboard DTOs

- `CostDashboardDto`: Summarized cost data for displaying on the dashboard
- `CostTrendDataDto`: Single data point in cost trend charts
- `ModelCostDataDto`: Cost data broken down by model
- `DetailedCostDataDto`: Detailed cost data for export and analysis
- `VirtualKeyCostDataDto`: Cost data broken down by virtual key

### IP Filtering DTOs

- `IpFilterDto`: Represents an IP filter rule
- `CreateIpFilterDto`: Used to create a new IP filter
- `UpdateIpFilterDto`: Used to update an existing IP filter

### Virtual Key DTOs

- `VirtualKeyDto`: Represents a virtual API key
- `CreateVirtualKeyRequestDto`: Used to create a new virtual key
- `CreateVirtualKeyResponseDto`: Returned when a new virtual key is created
- `UpdateVirtualKeyRequestDto`: Used to update an existing virtual key

---

## Backwards Compatibility (When Absolutely Necessary)

In rare cases where backward compatibility is required during a migration period:

```csharp
public class ProviderCredentialDto
{
    // Current property
    public string ApiBase { get; set; }

    // Deprecated property maintained for compatibility
    [Obsolete("Use ApiBase instead. This property will be removed in v3.0.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string EndpointUrl
    {
        get => ApiBase;
        set => ApiBase = value;
    }
}
```

**Important:** These should be temporary and removed in the next major version.

---

## Summary

The key to good DTO design is intentionality. Every DTO should have a clear, documented purpose. When in doubt, start with the simplest approach (direct entity-to-DTO mapping) and only add complexity when you have a specific need.

### Good Examples from Conduit

✅ `VirtualKeyDto` - Hides sensitive KeyHash
✅ `ProviderHealthDto` - Aggregates health information
✅ `BulkMappingRequest` - Special-purpose API operation

### Bad Examples (Now Fixed)

❌ `Configuration.ModelProviderMapping` - Redundant intermediate DTO
❌ Multiple representations of provider settings - Confusing and error-prone

**Remember:** The goal is to make the codebase easier to understand and maintain, not to follow patterns blindly.

---

## Related Documentation

- [Repository Pattern](../patterns/repository-and-data-access.md) - Data access patterns
- [Admin API Integration](../api/admin-api-integration.md) - API client implementation
- [Provider Architecture](../provider-system/provider-architecture.md) - Provider system design
