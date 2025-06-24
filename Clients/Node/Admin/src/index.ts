// Main client
export { ConduitAdminClient } from './client/ConduitAdminClient';

// Types
export * from './client/types';
export * from './models/common';
export * from './models/discovery';
export * from './models/virtualKey';
export * from './models/provider';
export * from './models/settings';
export * from './models/ipFilter';
export * from './models/modelCost';
export * from './models/analytics';
export * from './models/system';
export * from './models/audioConfiguration';

// Export modelMapping types with explicit re-exports to avoid conflicts
export type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  ModelMappingFilters,
  ModelProviderInfo,
  ModelRoutingInfo,
  BulkMappingRequest,
  BulkMappingResponse,
  ModelMappingSuggestion,
  DiscoveredModel,
  ModelCapabilities
} from './models/modelMapping';

// Re-export CapabilityTestResult from modelMapping (more complete version)
export type { CapabilityTestResult } from './models/modelMapping';

// Services
export { VirtualKeyService } from './services/VirtualKeyService';
export { ProviderService } from './services/ProviderService';
export { ProviderModelsService } from './services/ProviderModelsService';
export { ModelMappingService } from './services/ModelMappingService';
export { SettingsService } from './services/SettingsService';
export { IpFilterService } from './services/IpFilterService';
export { ModelCostService } from './services/ModelCostService';
export { AnalyticsService } from './services/AnalyticsService';
export { SystemService } from './services/SystemService';
export { DiscoveryService } from './services/DiscoveryService';
export { AudioConfigurationService } from './services/AudioConfigurationService';

// Utilities
export * from './utils/errors';
export * from './utils/capabilities';

// Constants
export * from './constants';

// Default export
import { ConduitAdminClient } from './client/ConduitAdminClient';
export default ConduitAdminClient;