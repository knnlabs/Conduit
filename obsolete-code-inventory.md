# Obsolete Code Inventory

## Obsolete Services
| Obsolete Service | Replacement Adapter | Interface |
|------------------|---------------------|-----------|
| GlobalSettingService | GlobalSettingServiceAdapter | IGlobalSettingService |
| ProviderHealthService | ProviderHealthServiceAdapter | IProviderHealthService |
| ModelCostService | ModelCostServiceAdapter | IModelCostService |
| ProviderCredentialService | ProviderCredentialServiceAdapter | IProviderCredentialService | 
| VirtualKeyService | VirtualKeyServiceAdapter | IVirtualKeyService |
| IpFilterService | IpFilterServiceAdapter | IIpFilterService |
| CostDashboardService | CostDashboardServiceAdapter | ICostDashboardService |
| ModelProviderMappingService | ModelProviderMappingServiceAdapter | IModelProviderMappingService |
| RouterService | RouterServiceAdapter | IRouterService |
| DatabaseBackupService | DatabaseBackupServiceAdapter | IDatabaseBackupService |

## Obsolete DTOs
| Obsolete DTO | Replacement DTO |
|--------------|----------------|
| ConduitLLM.Configuration.Services.Dtos.LogsSummaryDto | ConduitLLM.Configuration.DTOs.LogsSummaryDto |
| ConduitLLM.Configuration.DTOs.CostDashboardDto | ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto |

## Obsolete Utility Classes
| Obsolete Class | Replacement |
|----------------|-------------|
| DbConnectionHelper | IConnectionStringManager |

## Registration Mechanisms
Services are registered conditionally in `AdminClientExtensions.cs` based on the `CONDUIT_USE_ADMIN_API` setting:
- When true, adapter implementations are registered
- When false, direct repository implementations are registered (with deprecation warnings)

## Dependencies
All obsolete services depend on database context and repository implementations directly.
All adapter implementations depend on `IAdminApiClient` which accesses APIs via HTTP.

## Migration Status
- Current: Mixed mode using both database repositories and API client
  - GlobalSettingService migrated to always use adapter implementation ✅
  - ProviderCredentialService migrated to always use adapter implementation ✅
  - ModelCostService migrated to always use adapter implementation ✅
  - VirtualKeyService migrated to always use adapter implementation ✅
  - RequestLogService migrated to always use adapter implementation ✅ (was already done previously)
- Target: Complete use of Admin API with removal of direct database access

## Suggested Migration Strategy
1. Update the application to set `CONDUIT_USE_ADMIN_API=true` by default
2. Remove the obsolete service implementations entirely (not just the registrations)
3. Update any remaining code that might bypass the DI system and instantiate services directly
4. Delete obsolete DTOs once references are updated
5. Remove conditional registration in AdminClientExtensions
6. Adjust tests to use adapter implementations exclusively