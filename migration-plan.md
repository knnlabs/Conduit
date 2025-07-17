# WebUI Type Migration Plan

## Overview
This document outlines the plan to remove the mapping layer from WebUI and use SDK types directly.

## Analysis of Current Mappings

### Virtual Keys Mapping
**UIVirtualKey → VirtualKeyDto mappings:**
- `name` → `keyName`
- `key` → `apiKey` (only available on creation) / `keyPrefix` (display)
- `isActive` → `isEnabled`
- `budget` → `maxBudget`
- `budgetPeriod` → `budgetDuration` (with case conversion)
- `expirationDate` → `expiresAt`
- `createdDate` → `createdAt`
- `modifiedDate` → `updatedAt`
- `lastUsedDate` → `lastUsedAt`
- `metadata` → JSON.parse(metadata)
- `allowedProviders` → Not available in SDK (null)

### Issues Identified
1. **JSON Parsing**: `metadata` is stored as JSON string in backend, needs parsing
2. **Missing Fields**: `allowedProviders` not available in SDK
3. **Key Display**: `apiKey` only returned on creation, using `keyPrefix` as workaround
4. **Case Conversions**: Budget period enum case differences

## Migration Strategy

### Phase 1: Direct SDK Type Usage
1. Replace `UIVirtualKey` with `VirtualKeyDto` in components
2. Update all field references to use SDK field names
3. Handle JSON parsing at the API layer instead of mappers
4. Add TODO comments for missing fields

### Phase 2: SDK Enhancements (Future)
1. Add missing fields to SDK DTOs (allowedProviders)
2. Return masked key for display purposes
3. Consider returning parsed objects instead of JSON strings

## Implementation Steps for Virtual Keys Page

### 1. Update API Route
```typescript
// /app/api/virtualkeys/route.ts
// Parse metadata JSON at API layer
const virtualKeys = data.items.map(key => ({
  ...key,
  metadata: key.metadata ? JSON.parse(key.metadata) : null,
  // Add display key field
  displayKey: key.keyPrefix || `key_${key.id}`
}));
```

### 2. Update Components
Replace field references:
- `key.name` → `key.keyName`
- `key.isActive` → `key.isEnabled`
- `key.budget` → `key.maxBudget`
- `key.lastUsedDate` → `key.lastUsedAt`
- etc.

### 3. Update Table Component
```typescript
// Before
<Text fw={500}>{key.name}</Text>

// After
<Text fw={500}>{key.keyName}</Text>
```

### 4. Remove Mappers
- Delete mapping functions from mappers.ts
- Remove UIVirtualKey interface
- Update all imports

## Benefits
1. Eliminates ~200 lines of mapping code per entity
2. Reduces bugs from mapping inconsistencies
3. Single source of truth (SDK types)
4. Easier to maintain

## Risks & Mitigations
1. **Breaking Changes**: Thorough testing of each page
2. **Missing Fields**: Document TODOs for SDK enhancements
3. **JSON Parsing**: Centralize at API layer with error handling