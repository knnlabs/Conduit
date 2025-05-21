# Clean Architecture Migration Plan

## Current Issues

1. **Adapter Layers**: The project contains multiple adapter classes that translate between interfaces, adding unnecessary complexity.
2. **Interface Duplicates**: There are duplicate interface definitions for the same functionality (e.g., IVirtualKeyService exists in multiple namespaces).
3. **Mixed Database Access**: The code mixes direct database access with API calls.
4. **Inconsistent Return Types**: Interfaces with similar methods have different return types.

## Implementation Plan

### Phase 1: Standardize DTOs and Interfaces

1. **Consolidate DTOs**:
   - Move all DTOs to `ConduitLLM.Configuration.DTOs` namespace
   - Create domain-specific subdirectories for organization
   - Update all references to use the consolidated DTOs

2. **Standardize Interface Definitions**:
   - Select one version of each duplicate interface as the standard
   - Deprecate other versions with clear migration paths
   - Update implementations to target the standard interfaces

### Phase 2: Clean AdminAPI Client Implementation

1. **Direct Interface Implementation**:
   - Make AdminApiClient directly implement all service interfaces
   - Use explicit interface implementation where needed to resolve conflicts
   - Implement all required methods to match interface contracts

2. **Organized File Structure**:
   - Split AdminApiClient into partial classes by domain
   - Example: AdminApiClient.VirtualKeys.cs, AdminApiClient.Logs.cs
   - Maintain consistent error handling and logging

3. **Proper Type Handling**:
   - Implement proper conversion between API types and domain types
   - Add extension methods for type conversion where needed
   - Handle nullability correctly to prevent runtime errors

### Phase 3: Remove Adapter Classes

1. **Service Registration Update**:
   - Update Program.cs to register AdminApiClient directly for interfaces
   - Remove adapter registrations in AdminClientExtensions.cs
   - Update any dependencies on adapter-specific functionality

2. **Repository Adapter Removal**:
   - Remove adapter classes one by one, verifying functionality
   - Update tests to work with direct implementation
   - Fix any compile errors from adapter removal

3. **Dependency Resolution**:
   - Update components that depended on adapter-specific methods
   - Migrate any UI components that used adapter implementations
   - Fix dependency injection configuration

### Phase 4: Database Access Cleanup

1. **Remove Direct Database Access**:
   - Delete remaining direct database access code
   - Update all code to use the Admin API
   - Remove EF Core dependencies from WebUI

2. **Configuration Update**:
   - Update environment variables and configuration
   - Document the new architecture
   - Update docker-compose.yml for the new structure

## Implementation Sequence

To minimize disruption, we'll implement changes in this order:

1. Start with less-used services (ProviderHealth, IpFilter)
2. Move to core services (VirtualKey, RequestLog)
3. Finally address complex interrelated services (Router, CostDashboard)

## Testing Strategy

1. **Unit Tests**:
   - Update unit tests to work with direct implementation
   - Add tests for new functionality
   - Verify all interfaces are properly implemented

2. **Integration Tests**:
   - Test actual API calls between WebUI and Admin API
   - Verify error handling works correctly
   - Test performance impact of the changes

## Migration Completion Criteria

1. All adapter classes are removed
2. AdminApiClient directly implements all required interfaces
3. No direct database access in WebUI
4. All tests pass
5. Application runs correctly in docker-compose environment