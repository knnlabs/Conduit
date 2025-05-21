# Implementation Plan for Removing Obsolete Code

This plan outlines the steps to remove obsolete code and fully migrate to the Admin API-based architecture.

## Overview

The codebase currently supports two modes:
1. **Direct Database Access** (Legacy/Obsolete): Services directly interact with database repositories
2. **Admin API** (Target): Services use the AdminApiClient to interact with a separate Admin API service

The migration is already partially complete, with adapter implementations available for all obsolete services. The system can be configured to use adapters instead of direct database services by setting `CONDUIT_USE_ADMIN_API=true`.

## Migration Approach

### Phase 1: Environment and Configuration Changes
1. Update documentation to explicitly recommend `CONDUIT_USE_ADMIN_API=true`
2. Modify `AdminApiOptions` to keep `.UseAdminApi = true` as the default
3. Add clear migration warnings in logs for direct database access mode

### Phase 2: Clean Up GlobalSettingService (First Service) ✅
1. ✅ Simplify service configuration to always use the adapter
2. ✅ Keep obsolete implementation for reference but prevent its usage
3. ✅ Tests pass with the adapter implementation
4. ✅ Verified that all functionality continues to work

### Phase 3: Migrate Core Services One by One
Following the same pattern as GlobalSettingService, migrate these services:
1. ✅ ProviderCredentialService
2. ✅ ModelCostService
3. ✅ VirtualKeyService
4. ✅ RequestLogService (was already migrated previously)
5. IpFilterService
6. CostDashboardService
7. ModelProviderMappingService
8. RouterService
9. DatabaseBackupService
10. ProviderHealthService

### Phase 4: Remove Conditional Registration
1. Remove the conditional registration logic in `AdminClientExtensions.cs`
2. Update `Program.cs` to remove direct database access code paths
3. Remove deprecation warnings since direct access is no longer an option

### Phase 5: Clean Up Unused DTOs and Utilities
1. Remove any remaining obsolete DTOs
2. Remove unused database utility classes
3. Clean up any remaining direct database access patterns

### Phase 6: Documentation and Final Testing
1. Update documentation to reflect API-only architecture
2. Run comprehensive tests to ensure all functionality works
3. Finalize migration and remove all obsolete code markers

## Implementation Details

### Phase 1: Environment and Configuration Changes

1. Update AdminApiOptions.cs to keep UseAdminApi = true as the default
2. Modify AdminClientExtensions.cs to log warnings when direct database access is used
3. Update documentation to clearly recommend Admin API mode

### Phase 2: GlobalSettingService Migration ✅

1. ✅ Updated extension methods to register only the adapter implementation
2. ✅ Kept GlobalSettingService.cs but disabled its registration in DI
3. ✅ Tests continue to pass with the adapter implementation
4. ✅ Hash-based security features continue to function correctly

### Phase 3-6: Service-by-Service Migration

For each service:
1. Check its usage in the codebase (using grep)
2. Verify adapter provides equivalent functionality
3. Remove direct implementation
4. Update and run tests
5. Verify functionality

## Testing Strategy

1. Unit tests: Update to use adapter implementations and mock the AdminApiClient
2. Integration tests: Ensure they work with Admin API mode
3. Verify each functionality after migration:
   - Global settings and security
   - Virtual key management
   - Cost tracking and reporting
   - IP filtering
   - Model routing
   - Provider health monitoring

## Fallback Plan

If issues arise:
1. Keep both implementations temporarily
2. Restore dual-mode functionality with UseAdminApi toggle
3. Fix issues identified during migration

## Timeline

- Phase 1: 1 day
- Phase 2: 1-2 days
- Phase 3: 5-7 days (0.5-1 day per service)
- Phase 4: 1 day
- Phase 5: 1 day
- Phase 6: 1-2 days

Total: 10-14 days