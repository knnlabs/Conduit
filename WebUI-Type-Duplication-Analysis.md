# WebUI Type Duplication Analysis

## Executive Summary

The WebUI currently has significant type duplication with the SDK packages. Many types are redefined locally instead of importing from the SDK, creating maintenance overhead and potential inconsistencies.

## Key Findings

### 1. Duplicate Type Definitions

#### Analytics Types (`analytics-types.ts`)
The WebUI defines many analytics-related types that could be standardized in the Common package:
- `TimeRangeFilter` - Common pattern for time-based filtering
- `ExportRequest/Response` - Standard export functionality
- `CostSummary`, `CostTrendData`, `ModelCostData` - Cost analytics types
- `UsageMetrics`, `RequestVolumeData`, `TokenUsageData` - Usage tracking
- `ErrorAnalyticsData`, `LatencyMetrics` - Performance monitoring
- Virtual key analytics types - Over 10 different interfaces for key analytics

**Recommendation**: Move these to `@knn_labs/conduit-common/analytics` module

#### SDK Extensions (`sdk-extensions.ts`)
This file extends SDK types to add properties not yet in the SDK:
- Extended `VirtualKeyDto` with `keyHash`, `budgetLimit`, `requestsPerMinute`
- Extended `UsageMetricsDto` with token tracking
- New types like `RequestLogEntry`, `CostByModelResponse`, `CostByKeyResponse`

**Recommendation**: Update SDK types to include these fields, then remove extensions

#### SDK Response Types (`sdk-responses.ts`)
Defines WebUI-specific types that don't exist in SDK:
- `ProviderIncident` - Provider health tracking
- `ServiceHealth`, `DependencyHealth` - System monitoring
- `MediaRecord`, `MediaStorageStats` - Media management
- `CostDashboard` and related cost types
- `AudioUsageSummary` - Audio feature analytics

**Recommendation**: Evaluate which are truly UI-specific vs should be in SDK

### 2. Type Mapping Layer (`mappers.ts`)

The WebUI maintains a complex mapping layer between SDK types and UI types:
- `UIVirtualKey` - Maps from `VirtualKeyDto` with field renames
- `UIProvider` - Maps from `ProviderCredentialDto`
- `UIModelMapping` - Maps from `ModelProviderMappingDto`
- `UIProviderHealth`, `UISystemHealth`, `UIRequestLog` - All mapped types

**Issues with mapping approach**:
1. Field name inconsistencies (e.g., `keyName` → `name`, `isEnabled` → `isActive`)
2. Manual JSON parsing for metadata fields
3. Fallback logic for missing fields
4. Maintenance overhead of keeping mappers in sync

### 3. Existing Common Package Structure

The Common package currently only contains:
- Basic response types (`PaginatedResponse`, `PagedResponse`, `ErrorResponse`)
- Filter and sort options
- Basic usage tracking
- Performance metrics

**Gap**: Missing domain-specific shared types for analytics, monitoring, and media

## Recommendations

### Phase 1: Expand Common Package
1. Create new modules in Common:
   - `analytics/` - Cost, usage, and performance analytics types
   - `monitoring/` - Health, incidents, and system monitoring types
   - `media/` - Media storage and management types
   - `filters/` - Enhanced filtering types (time ranges, etc.)

2. Move non-UI-specific types from WebUI to Common

### Phase 2: Update SDK Types
1. Add missing fields to SDK DTOs:
   - Add `keyHash`, `requestsPerMinute` to `VirtualKeyDto`
   - Add token tracking to usage metrics
   - Include provider health incident tracking

2. Standardize field naming across SDKs (breaking change)

### Phase 3: Eliminate Mapping Layer
1. Update WebUI to use SDK types directly
2. Remove mapper functions
3. Handle any UI-specific transformations in components

### Phase 4: Type Consistency
1. Ensure all API responses match SDK types
2. Update backend to return consistent field names
3. Add type validation/runtime checks

## Migration Strategy

### Step 1: Non-Breaking Additions (1-2 days)
- Add new types to Common package
- Update WebUI to import from Common where possible
- Keep existing code working

### Step 2: SDK Updates (2-3 days)
- Add missing fields to SDK types
- Deprecate old field names
- Add migration guides

### Step 3: WebUI Refactor (3-4 days)
- Replace local types with Common/SDK imports
- Update components to use new types
- Remove mapping layer gradually

### Step 4: Testing & Validation (2 days)
- Comprehensive type checking
- Runtime validation
- Update tests

## Benefits

1. **Reduced Maintenance**: Single source of truth for types
2. **Better Type Safety**: Consistent types across frontend/backend
3. **Easier Updates**: Changes propagate automatically
4. **Smaller Bundle**: Less duplicate code
5. **Developer Experience**: Clear type imports, better IntelliSense

## Risks

1. **Breaking Changes**: Field name standardization will break existing code
2. **Migration Effort**: Significant refactoring required
3. **Version Management**: Need to coordinate SDK/WebUI updates
4. **Testing Coverage**: Must ensure all type changes are tested

## Conclusion

The WebUI has significant type duplication that should be addressed through:
1. Expanding the Common package with domain-specific types
2. Updating SDKs to include missing fields
3. Standardizing field names across the codebase
4. Eliminating the mapping layer in favor of direct SDK usage

This will improve maintainability, type safety, and developer experience across the Conduit ecosystem.