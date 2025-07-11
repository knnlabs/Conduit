// Main clients - export only fetch-based client
export { FetchConduitAdminClient as ConduitAdminClient } from './FetchConduitAdminClient';
export { FetchConduitAdminClient } from './FetchConduitAdminClient';

// Types
export * from './client/types';
export * from './models/common';
// discovery models removed - discovery types are in modelMapping
export * from './models/virtualKey';
export * from './models/provider';
export * from './models/settings';
export * from './models/ipFilter';
export * from './models/modelCost';
export * from './models/analytics';
export * from './models/analyticsExport';
export * from './models/system';
export * from './models/audioConfiguration';
export * from './models/metrics';
export * from './models/databaseBackup';
export * from './models/signalr';
export * from './models/notifications';
export * from './models/security';
export * from './models/configuration';

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
export { FetchVirtualKeyService as VirtualKeyService } from './services/FetchVirtualKeyService';
export { FetchProvidersService as ProvidersService } from './services/FetchProvidersService';
export { ProviderService } from './services/ProviderService';
export { ProviderModelsService } from './services/ProviderModelsService';
export { ModelMappingService } from './services/ModelMappingService';
export { SettingsService } from './services/SettingsService';
export { IpFilterService } from './services/IpFilterService';
export { ModelCostService } from './services/ModelCostService';
export { AnalyticsService } from './services/AnalyticsService';
export { SystemService } from './services/SystemService';
// DiscoveryService removed - use ModelMappingService.discoverProviderModels() instead
export { AudioConfigurationService } from './services/AudioConfigurationService';
export { MetricsService } from './services/MetricsService';
export { ProviderHealthService } from './services/ProviderHealthService';
export { NotificationsService } from './services/NotificationsService';
// export { DatabaseBackupService } from './services/DatabaseBackupService'; // Removed
export { SignalRService } from './services/SignalRService';
// export { ConnectionService } from './services/ConnectionService'; // Removed
export { RealtimeNotificationsService } from './services/RealtimeNotificationsService';
export { SecurityService } from './services/SecurityService';
export { ConfigurationService } from './services/ConfigurationService';

// SignalR Hub Clients
export { NavigationStateHubClient } from './signalr/NavigationStateHubClient';
export { AdminNotificationHubClient } from './signalr/AdminNotificationHubClient';

// Utilities
export * from './utils/errors';
export * from './utils/capabilities';
export * from './utils/webui-auth';

// Constants
export * from './constants';

// Re-export generated types
export type { 
  components as AdminComponents, 
  operations as AdminOperations, 
  paths as AdminPaths 
} from './generated/admin-api';

// Default export - using FetchConduitAdminClient as the recommended client
import { FetchConduitAdminClient } from './FetchConduitAdminClient';
export default FetchConduitAdminClient;