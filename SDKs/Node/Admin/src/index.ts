// Main clients - export only fetch-based client
export { FetchConduitAdminClient as ConduitAdminClient } from './FetchConduitAdminClient';
export { FetchConduitAdminClient } from './FetchConduitAdminClient';

// Types
export * from './client/types';
export { HttpMethod } from './client/HttpMethod';
export type { RequestOptions, ApiResponse } from './client/HttpMethod';
export * from './models/common';
// discovery models removed - discovery types are in modelMapping
export * from './models/virtualKey';
export * from './models/provider';
export * from './models/providerType';
export * from './models/providerModels';
export * from './models/providerHealth';
export * from './models/settings';
export * from './models/ipFilter';
// Re-export modelCost types except CostTrend (conflicts with analytics)
export {
  ModelCost,
  ModelCostDto,
  CreateModelCostDto,
  UpdateModelCostDto,
  ModelCostFilters,
  ModelCostCalculation,
  BulkModelCostUpdate,
  ModelCostHistory,
  CostEstimate,
  ModelCostComparison,
  ModelCostOverview,
  ImportResult,
} from './models/modelCost';
// Re-export analytics types (includes the main ExportParams/ExportResult we'll use)
export * from './models/analytics';
// Re-export analyticsExport types except ExportParams and ExportResult (conflicts with analytics)
export {
  ExportUsageParams,
  ExportCostParams,
  ExportVirtualKeyParams,
  ExportProviderParams,
  ExportSecurityParams,
  CreateExportScheduleDto,
  ExportSchedule,
  ExportHistory,
  ExportRequestLogsParams,
  RequestLogStatistics,
  RequestLogSummaryParams,
  RequestLogSummary,
  RequestLog,
  ExportStatus,
} from './models/analyticsExport';
export * from './models/system';
export * from './models/audioConfiguration';
export * from './models/metrics';
export * from './models/databaseBackup';
export * from './models/signalr';
export * from './models/notifications';
export * from './models/monitoring';
export * from './models/security';
// Re-export securityExtended types except ExportParams and ExportResult (conflicts with analytics)
export {
  IpWhitelistDto,
  IpEntry,
  SecurityEventParams,
  SecurityEventType,
  SecurityEventExtended,
  SecurityEventPage,
  ThreatSummaryDto,
  ThreatCategory,
  ActiveThreat,
  AccessPolicy,
  PolicyRule,
  CreateAccessPolicyDto,
  UpdateAccessPolicyDto,
  AuditLogParams,
  AuditLog,
  AuditLogPage,
} from './models/securityExtended';
export * from './models/configuration';
// Re-export configurationExtended types except RoutingRule and UpdateRoutingConfigDto (conflicts with configuration)
export {
  RoutingConfigDto,
  RetryPolicy,
  // UpdateRoutingConfigDto, // conflicts with configuration
  // RoutingRule, // conflicts with configuration
  RuleCondition,
  RuleAction,
  CreateRoutingRuleDto,
  UpdateRoutingRuleDto,
  CacheConfigDto,
  UpdateCacheConfigDto,
  CacheRule,
  CacheCondition,
  CacheClearParams,
  CacheClearResult,
  CacheStatsDto,
  CacheKeyStats,
  LoadBalancerConfigDto,
  UpdateLoadBalancerConfigDto,
  LoadBalancerHealthDto,
  LoadBalancerNode,
  PerformanceConfigDto,
  UpdatePerformanceConfigDto,
  PerformanceTestParams,
  PerformanceTestResult,
  PerformanceDataPoint,
  ErrorSummary,
  FeatureFlag,
  FeatureFlagCondition,
  UpdateFeatureFlagDto,
} from './models/configurationExtended';
// Re-export the extended versions of conflicting types
export { 
  RoutingRule as ExtendedRoutingRule,
  UpdateRoutingConfigDto as ExtendedUpdateRoutingConfigDto
} from './models/configurationExtended';

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
export type { VirtualKeyListResponseDto } from './services/FetchVirtualKeyService';
export { FetchProvidersService as ProvidersService } from './services/FetchProvidersService';
export { FetchSystemService } from './services/FetchSystemService';
export { FetchModelMappingsService } from './services/FetchModelMappingsService';
export { FetchProviderModelsService } from './services/FetchProviderModelsService';
export { FetchSettingsService } from './services/FetchSettingsService';
export type { SettingUpdate, SettingsDto, SettingsListResponseDto } from './services/FetchSettingsService';
export { FetchAnalyticsService } from './services/FetchAnalyticsService';
export { FetchProviderHealthService } from './services/FetchProviderHealthService';
export { FetchSecurityService } from './services/FetchSecurityService';
export { FetchConfigurationService } from './services/FetchConfigurationService';
export { FetchMonitoringService } from './services/FetchMonitoringService';
export { FetchIpFilterService } from './services/FetchIpFilterService';
export { FetchErrorQueueService } from './services/FetchErrorQueueService';
export { FetchCostDashboardService } from './services/FetchCostDashboardService';
export { FetchModelCostService } from './services/FetchModelCostService';
export type {
  CostDashboardDto,
  ModelCostDto as CostModelCostDto,
  ProviderCostDto,
  DailyCostDto,
  CostTrendDto,
  ModelCostDataDto,
  VirtualKeyCostDataDto,
} from './services/FetchCostDashboardService';
export type {
  ErrorQueueInfo,
  ErrorQueueSummary,
  ErrorQueueListResponse,
  ErrorMessage,
  ErrorMessageDetail,
  ErrorDetails,
  ErrorMessageListResponse,
  ErrorRateTrend,
  FailingMessageType,
  QueueGrowthPattern,
  ErrorQueueStatistics,
  HealthStatusCounts,
  HealthIssue,
  ErrorQueueHealth,
  QueueClearResponse,
  MessageReplayResponse,
  MessageDeleteResponse
} from './services/FetchErrorQueueService';
export { ProviderService } from './services/ProviderService';
export { ProviderModelsService } from './services/ProviderModelsService';
export { ModelMappingService } from './services/ModelMappingService';
export { SettingsService } from './services/SettingsService';
export { IpFilterService } from './services/IpFilterService';
export { FetchModelCostService as ModelCostService } from './services/FetchModelCostService'; // Alias for backward compatibility
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
export { FetchSecurityService as SecurityService } from './services/FetchSecurityService'; // Alias for backward compatibility
export { FetchConfigurationService as ConfigurationService } from './services/FetchConfigurationService'; // Alias for backward compatibility

// SignalR Hub Clients
export { NavigationStateHubClient } from './signalr/NavigationStateHubClient';
// AdminNotificationHubClient removed - AdminNotificationHub has been removed from the backend

// Utilities
export * from './utils/errors';
export * from './utils/capabilities';

// Models
export * from './models/metadata';
export * from './models/common-types';

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