# Refactoring Plan: Remove Intermediate ModelProviderMapping

## Overview
This document outlines the plan to remove the redundant intermediate `ConduitLLM.Configuration.ModelProviderMapping` class and refactor the codebase to map directly between the Entity and API DTO.

## Current Architecture
```
[Database] ↔ Entity ↔ Intermediate Model ↔ Service ↔ API DTO ↔ [Admin API]
                                               ↓
                                          Core Model ↔ [Core Services]
```

## Target Architecture
```
[Database] ↔ Entity ↔ Service ↔ API DTO ↔ [Admin API]
                           ↓
                      Core Model ↔ [Core Services]
```

## Impact Analysis

### Affected Components
1. **ConduitLLM.Configuration**
   - `ModelProviderMapping.cs` - TO BE REMOVED
   - `IModelProviderMappingService.cs` - Interface needs updating
   - `ModelProviderMappingService.cs` - Implementation needs updating
   - `ModelProviderMappingMapper.cs` - Needs refactoring

2. **ConduitLLM.Admin**
   - `ModelProviderMappingController.cs` - May need updates
   - `ConfigurationAdapters.cs` - Adapter logic needs review

3. **ConduitLLM.Core**
   - No changes needed (has its own domain model)

## Step-by-Step Refactoring Plan

### Phase 1: Update Service Interface (Breaking Change)
```csharp
// OLD: IModelProviderMappingService.cs
public interface IModelProviderMappingService
{
    Task<ModelProviderMapping?> GetMappingByIdAsync(int id);
    Task<List<ModelProviderMapping>> GetAllMappingsAsync();
    // ... uses intermediate model
}

// NEW: IModelProviderMappingService.cs
public interface IModelProviderMappingService
{
    Task<ModelProviderMappingDto?> GetMappingByIdAsync(int id);
    Task<List<ModelProviderMappingDto>> GetAllMappingsAsync();
    Task<ModelProviderMappingDto> AddMappingAsync(ModelProviderMappingDto mapping);
    Task<ModelProviderMappingDto> UpdateMappingAsync(ModelProviderMappingDto mapping);
    // ... uses DTO directly
}
```

### Phase 2: Update Mapper
```csharp
// Refactor ModelProviderMappingMapper.cs
public static class ModelProviderMappingMapper
{
    // Map Entity → DTO
    public static ModelProviderMappingDto? ToDto(Entities.ModelProviderMapping? entity)
    {
        if (entity == null) return null;
        
        return new ModelProviderMappingDto
        {
            Id = entity.Id,
            ModelId = entity.ModelAlias,
            ProviderId = entity.ProviderCredentialId.ToString(),
            ProviderName = entity.ProviderCredential?.ProviderName ?? string.Empty,
            ProviderModelId = entity.ProviderModelName,
            Priority = entity.Priority,
            IsEnabled = entity.IsEnabled,
            // ... map all capabilities
        };
    }
    
    // Map DTO → Entity
    public static Entities.ModelProviderMapping ToEntity(
        ModelProviderMappingDto dto,
        Entities.ModelProviderMapping? existing = null)
    {
        var entity = existing ?? new Entities.ModelProviderMapping();
        
        entity.ModelAlias = dto.ModelId;
        entity.ProviderModelName = dto.ProviderModelId;
        // Note: ProviderCredentialId must be set separately by service
        entity.Priority = dto.Priority;
        entity.IsEnabled = dto.IsEnabled;
        // ... map all capabilities
        
        return entity;
    }
}
```

### Phase 3: Update Service Implementation
1. Change all return types from `ModelProviderMapping` to `ModelProviderMappingDto`
2. Update mapper calls to use the refactored mapper
3. Handle provider credential resolution in service layer

### Phase 4: Update Admin Controller
1. Update to work with DTOs directly
2. Remove any intermediate model references
3. Ensure proper validation

### Phase 5: Update Adapters
1. Modify `ConfigurationAdapters.cs` to map from DTO to Core model
2. Remove intermediate model from adapter logic

### Phase 6: Delete Intermediate Model
1. Remove `ConduitLLM.Configuration/ModelProviderMapping.cs`
2. Update all using statements
3. Rebuild and fix any remaining references

## Migration Strategy

### Option A: Big Bang (Recommended for small team)
1. Create feature branch
2. Make all changes at once
3. Thorough testing
4. Merge as single PR

### Option B: Incremental (For larger teams)
1. Add new methods alongside old ones
2. Mark old methods as `[Obsolete]`
3. Migrate consumers one by one
4. Remove old methods in final step

## Testing Requirements

### Unit Tests
- Update mapper tests
- Update service tests
- Verify DTO validation

### Integration Tests
- Test Admin API endpoints
- Verify Core services still work
- Test database operations

### Manual Testing
- Create new model mapping
- Update existing mapping
- Delete mapping
- Verify all capabilities persist

## Rollback Plan
- Keep backup of current code
- Use feature flags if doing incremental migration
- Database schema unchanged, so data is safe

## Timeline Estimate
- Planning: 2 hours
- Implementation: 4-6 hours
- Testing: 2-3 hours
- Total: 1-2 days

## Success Criteria
- [ ] All tests passing
- [ ] No intermediate model references remain
- [ ] API behavior unchanged
- [ ] All capabilities correctly persisted
- [ ] Performance improved (fewer allocations)