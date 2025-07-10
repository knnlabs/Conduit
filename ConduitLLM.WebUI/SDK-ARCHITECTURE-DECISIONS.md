# SDK Architecture Decisions and Tech Debt

This document tracks architectural decisions made during SDK improvements and technical debt discovered.

## Architectural Decisions

### 1. SDK Error Handling Pattern
**Decision Needed**: How should SDK methods handle and return errors?
- Option A: Throw exceptions (current pattern)
- Option B: Return `{ data, error }` tuples like Supabase
- Option C: Use Result<T, E> pattern

**Current Choice**: Keeping throw pattern for consistency with existing code, but wrapping in typed errors.

### 2. Provider Type Management
**Decision Needed**: Where should provider type definitions live?
- Option A: Shared package between Admin and Core SDKs
- Option B: Duplicate in both SDKs
- Option C: Admin SDK only (since it manages providers)

**Current Choice**: Admin SDK only, since Core SDK doesn't manage providers.

## Technical Debt Discovered

### SDK Issues

1. **Inconsistent Method Naming**
   - Some methods use `create`, others use `add`
   - Some use `list`, others use `getAll`
   - Need standardization
   - Example: `providers.deleteById()` vs potential `providers.delete()`

2. **Missing TypeScript Strict Mode**
   - Both SDK packages don't enforce strict TypeScript
   - Allows implicit `any` types

3. **No Shared Types Package**
   - Types are duplicated between Admin and Core SDKs
   - Could benefit from `@knn_labs/conduit-types` package

4. **Missing Provider Configuration Test Method**
   - No way to test provider config without creating a record
   - Current workaround: Create disabled provider, test it, delete it
   - Need: `adminClient.providers.testConfig(config)` method

5. **Provider Types Not Exported**
   - Provider type strings not available as constants/enum
   - WebUI has to maintain its own list
   - Should be single source of truth in SDK

### WebUI Issues

1. **Hardcoded Provider Lists** ✅ FIXED
   - Created `/lib/constants/providers.ts` with centralized provider types
   - Includes display names, categories, and configuration requirements

2. **No Error Boundary Components**
   - API errors can crash the entire UI
   - Need error boundaries around major sections

3. **Inconsistent Loading States**
   - Some components use `isLoading`, others use `loading`
   - No consistent loading skeleton components

4. **Legacy Capability Format**
   - CreateModelMappingModal uses old capability format
   - SDK now has proper `ModelCapability` enum
   - Need migration from string-based to enum-based capabilities

## Questions for Product Team

1. **Provider Test Behavior**: Should test results be persisted for audit, or just ephemeral validation?
2. **Session Refresh Strategy**: Silent refresh or user notification when session extends?
3. **Real-time Updates**: SSE vs WebSocket for live updates?