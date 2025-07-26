# API Breaking Changes - Phase 3c (Issue #625)

## Overview
This document describes the breaking changes introduced in Phase 3c of the ProviderType migration, which updates all DTOs and API contracts to use ProviderType enum values instead of string provider names.

## Breaking Changes

### 1. Provider Identification
All API endpoints and DTOs now use numeric ProviderType values instead of string provider names.

**Before:**
```json
{
  "provider": "OpenAI",
  "providerName": "OpenAI"
}
```

**After:**
```json
{
  "providerType": 1
}
```

### 2. ProviderType Enum Values
Use the new `/api/admin/providertypes` endpoint to get the complete list of provider types and their numeric values:

| Provider | Value |
|----------|-------|
| OpenAI | 1 |
| Anthropic | 2 |
| AzureOpenAI | 3 |
| Google | 4 |
| Perplexity | 5 |
| OpenRouter | 6 |
| ... | ... |

### 3. Affected DTOs

#### Configuration DTOs
- `ModelProviderMappingDto`: `ProviderId` (string) → `ProviderType` (int)
- `CreateModelProviderMappingDto`: `ProviderId` (string) → `ProviderType` (int)
- `DiscoveredModelDto`: `Provider` (string) → `ProviderType` (int)

#### Audio DTOs
- `AudioUsageDto`: `Provider` (string) → `ProviderType` (int)
- `AudioCostDto`: `Provider` (string) → `ProviderType` (int)
- `AudioProviderConfigDto`: `ProviderName` (string) → `ProviderType` (int)
- `RealtimeSessionDto`: `Provider` (string) → `ProviderType` (int)
- `AudioUsageQueryDto`: `Provider` (string) → `ProviderType` (int?)
- `ProviderBreakdown`: `Provider` (string) → `ProviderType` (int)
- `AudioProviderUsageDto`: `Provider` (string) → `ProviderType` (int)

#### SignalR Notification DTOs
- `ModelDiscoveryNotifications`: All `Provider` (string) → `ProviderType` (int)
- `SystemNotifications`: `Provider`/`ProviderName` (string) → `ProviderType` (int)
- `AdminNotifications`: `ProviderName` (string) → `ProviderType` (int)
- `ImageGenerationNotifications`: `Provider` (string) → `ProviderType` (int)
- `VideoGenerationNotifications`: `Provider` (string) → `ProviderType` (int)
- `SpendNotifications`: `Provider` (string) → `ProviderType` (int)
- `UsageAnalyticsNotifications`: `ProviderName` (string) → `ProviderType` (int), `AffectedProvider` (string) → `AffectedProviderType` (int?)
- `ModelDiscoverySubscriptionFilter`: `Providers` (List<string>) → `ProviderTypes` (List<int>)

#### HTTP DTOs
- `SpendUpdateDto`: `Provider` (string) → `ProviderType` (int)
- `ModelUsageStats`: `Provider` (string) → `ProviderType` (int)
- `ProviderHealthStatus`: `ProviderName` (string) → `ProviderType` (int)

### 4. API Endpoints
All API endpoints that previously accepted provider names as strings now expect ProviderType enum values (integers).

### 5. New Endpoint
A new endpoint has been added to help with the migration:
- `GET /api/admin/providertypes` - Returns all provider types with their names, numeric values, and display names

## Migration Guide

### For API Clients
1. Call `/api/admin/providertypes` to get the mapping of provider names to numeric values
2. Update your client code to send numeric values instead of strings
3. Update deserialization to expect numeric values for provider types

### Example Migration
```typescript
// Before
const request = {
  provider: "OpenAI",
  model: "gpt-4"
};

// After
const request = {
  providerType: 1, // OpenAI
  model: "gpt-4"
};
```

### Error Handling
Invalid provider type values will result in 400 Bad Request errors. Ensure you're using valid ProviderType values from the enum.

## Rollback Considerations
This is a breaking change that cannot be rolled back without updating all clients. Plan your migration carefully and update all clients simultaneously if possible.