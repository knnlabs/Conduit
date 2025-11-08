# WebUI SDK Migration Guide

## Overview

This guide helps update the WebUI to work with the major SDK breaking changes after the provider refactor. The core change is that the system now supports multiple providers of the same type (e.g., multiple OpenAI configurations), with Provider ID as the canonical identifier.

## Critical Breaking Changes

### 1. Provider Identification
**Old Model**: One provider per ProviderType, identified by provider name or type
**New Model**: Multiple providers per type, identified by numeric Provider ID

```typescript
// ❌ OLD - Using provider name
await providerModelsService.getProviderModelsByName('openai');

// ✅ NEW - Using provider ID
await providerModelsService.getProviderModels(providerId);
```

### 2. Removed Features
- **AdminNotificationHub**: Completely removed from backend
- **ProviderCredentials endpoints**: Replaced with Provider endpoints
- **Pattern matching for model costs**: Now uses direct model mappings
- **Model routing**: Currently broken and stubbed out

### 3. Provider Management

#### Provider DTOs Changed
```typescript
// OLD ProviderCredentialDto structure
interface ProviderCredentialDto {
  id: number;
  providerName: string;  // This was the identifier
  providerType: ProviderType;
  apiKey: string;
  organization?: string;
  // ...
}

// NEW ProviderDto structure
interface ProviderDto {
  id: number;  // This is now the canonical identifier
  providerType: ProviderType;
  providerName: string;  // Display name only, can be changed
  baseUrl?: string | null;
  isEnabled: boolean;
  // Note: apiKey and organization moved to ProviderKeyCredential sub-resource
}
```

#### API Key Management
API keys are now managed as a separate sub-resource with support for multiple keys per provider:

```typescript
interface ProviderKeyCredentialDto {
  id: number;
  providerId: number;
  keyName: string;
  apiKeyMasked: string;  // Only last 4 characters visible
  isPrimary: boolean;    // One key must be primary
  isEnabled: boolean;
  // ...
}
```

### 4. Model Provider Mappings

The ModelProviderMappingDto now includes provider information via a reference object:

```typescript
interface ModelProviderMappingDto {
  id: number;
  modelId: string;  // The alias used in requests
  providerModelId: string;  // Provider's actual model ID
  providerId: number;  // References the provider
  provider?: ProviderReferenceDto;  // Populated on retrieval
  // ... capability flags
}

interface ProviderReferenceDto {
  id: number;
  providerType: ProviderType;
  displayName: string;  // Was providerName
  isEnabled: boolean;
}
```

### 5. Model Costs

Pattern matching has been removed. Model costs are now directly mapped:

```typescript
interface ModelCostDto {
  id: number;
  costName: string;  // User-friendly name
  associatedModelAliases: string[];  // Models using this cost
  modelType: 'chat' | 'embedding' | 'image' | 'audio' | 'video';
  inputTokenCost: number;  // Cost per token (not per million)
  outputTokenCost: number;
  // ...
}
```

## WebUI Update Checklist

### 1. Update Provider Selection Components
- [ ] Replace provider name dropdowns with provider ID selection
- [ ] Update provider displays to show `provider.providerName` as display text
- [ ] Store and use `provider.id` for all API calls

### 2. Update Model Configuration Pages
- [ ] Change model-provider mapping forms to use provider ID
- [ ] Remove any pattern matching UI for model costs
- [ ] Add UI for direct model cost mapping

### 3. Update API Key Management
- [ ] Create new UI for managing multiple API keys per provider
- [ ] Add primary key selection functionality
- [ ] Update key rotation workflows

### 4. Remove Deprecated Features
- [ ] Remove all AdminNotificationHub subscriptions
- [ ] Remove virtual key real-time notifications
- [ ] Remove configuration change notifications
- [ ] Update or remove model routing UI (it's broken)

### 5. Update Service Calls

#### Provider Services
```typescript
// ❌ OLD
const providers = await adminClient.providerCredentials.list();
const provider = await adminClient.providerCredentials.getByName('openai');

// ✅ NEW
const providers = await adminClient.providers.list();
const provider = await adminClient.providers.getById(providerId);
```

#### Provider Models
```typescript
// ❌ OLD
const models = await coreClient.providerModels.getByProviderName('openai');

// ✅ NEW
const models = await coreClient.providerModels.getProviderModels(providerId);
```

#### Model Costs
```typescript
// ❌ OLD - Pattern matching
const cost = await adminClient.modelCosts.getByPattern('gpt-4*');

// ✅ NEW - Direct mapping
const mappings = await adminClient.modelCosts.getMappingsByCostId(costId);
await adminClient.modelCosts.createMappings({
  modelCostId: costId,
  modelProviderMappingIds: [mappingId1, mappingId2]
});
```

### 6. Update State Management

If using Redux/Zustand/etc, update your state shape:

```typescript
// ❌ OLD State
interface AppState {
  providers: Record<string, Provider>;  // Keyed by name
  selectedProvider: string;  // Provider name
}

// ✅ NEW State
interface AppState {
  providers: Record<number, Provider>;  // Keyed by ID
  selectedProviderId: number;  // Provider ID
}
```

### 7. Update Type Imports

```typescript
// Update imports to use new types
import type {
  ProviderDto,
  ProviderCreateDto,
  ProviderUpdateDto,
  ProviderKeyCredentialDto,
  ProviderReferenceDto,
  ModelProviderMappingDto,
  ModelCostDto,
  ModelCostMappingDto
} from '@knn_labs/conduit-admin-client';
```

### 8. Handle Backward Compatibility

For migrating existing data:
- Provider IDs are numeric and immutable
- Provider names are now just display text
- Multiple providers of the same type are allowed
- Primary API key concept for key selection

### 9. Error Handling

Update error handling for new error cases:
- Provider not found (use ID, not name)
- No primary key set for provider
- Model routing errors (feature is broken)
- Removed notification endpoints

### 10. Testing Scenarios

Test these scenarios after updates:
1. Creating multiple providers of the same type
2. Switching between providers with same type
3. Managing multiple API keys per provider
4. Setting and changing primary keys
5. Model cost direct mapping (no patterns)
6. Provider ID persistence across sessions

## Common Pitfalls

1. **Don't use Provider Name as identifier** - It's mutable display text
2. **Don't expect model routing to work** - It's currently broken
3. **Don't subscribe to AdminNotificationHub** - It's been removed
4. **Don't use pattern matching for costs** - Use direct mappings
5. **Don't assume one provider per type** - Multiple are supported

## Example Migration

### Old Code
```typescript
// Provider selection by name
const provider = providers.find(p => p.providerName === 'openai');
const models = await getModelsByProvider(provider.providerName);

// Pattern-based cost lookup
const cost = await findCostByPattern(`${provider.providerName}-*`);

// Admin notifications
adminHub.on('providerHealthChanged', (data) => {
  updateProviderStatus(data.provider);
});
```

### New Code
```typescript
// Provider selection by ID
const provider = providers.find(p => p.id === selectedProviderId);
const models = await providerModelsService.getProviderModels(provider.id);

// Direct cost mapping
const costMappings = await modelCostService.getMappingsByCostId(costId);

// No admin notifications - poll or use navigation state hub
// adminHub references should be removed
```

## Migration Steps

1. **Update SDK packages** to latest versions
2. **Search and replace** provider name usage with provider ID
3. **Remove** AdminNotificationHub subscriptions
4. **Update forms** to use new DTO structures
5. **Test** with multiple providers of same type
6. **Verify** API key management works correctly

## Getting Help

If you encounter issues:
1. Check the generated TypeScript types in the SDK
2. Verify the API endpoints match the new structure
3. Look for [Obsolete] or @deprecated markers in the SDK
4. Test API calls directly with the Swagger UI first

Remember: Provider ID is king. When in doubt, use the numeric ID, not the name.