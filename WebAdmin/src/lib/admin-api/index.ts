// Main clients - export only fetch-based client
export { FetchConduitAdminClient as ConduitAdminClient } from './FetchConduitAdminClient';
export { FetchConduitAdminClient } from './FetchConduitAdminClient';

// Export generated types
export type { paths, components } from './generated/admin-api';

// Types
export * from './client/types';
export { HttpMethod } from './client/HttpMethod';
export type { RequestOptions, ApiResponse } from './client/HttpMethod';
export * from './models/common';

// Provider types and validation
export * from './types/providers';
export * from './types/function-providers';
export * from './types/models';
export * from './validation/modelValidation';
export * from './errors/modelErrors';
export * from './models/virtualKey';
export * from './models/provider';
export * from './models/providerType';
export * from './models/modelType';
export * from './models/providerConfiguration';
export * from './models/settings';
export * from './models/ipFilter';
export * from './models/media';
export * from './models/functions';
export * from './models/promptCaching';
export * from './models/providerSync';
export { ModelType } from './models/model';
export type {
  ModelDto,
  CreateModelDto,
  UpdateModelDto,
  ModelProviderAvailabilityDto,
  ModelSeriesDto,
  CreateModelSeriesDto,
  UpdateModelSeriesDto,
  SimpleModelSeriesDto,
  SeriesSimpleModelDto,
  ModelAuthorDto,
  CreateModelAuthorDto,
  UpdateModelAuthorDto,
  Model,
  ModelSeries,
  ModelAuthor
} from './models/model';
// Re-export modelCost types except CostTrend (conflicts with analytics)
export { PricingModel } from './models/modelCost';
export type {
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
export type {
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
export * from './models/databaseBackup';
export * from './models/signalr';
// notifications/metrics/monitoring models removed - observability is served by Grafana
export * from './models/configuration';
// Re-export configurationExtended types except RoutingRule and UpdateRoutingConfigDto (conflicts with configuration)
export type {
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
export type {
  RoutingRule as ExtendedRoutingRule,
  UpdateRoutingConfigDto as ExtendedUpdateRoutingConfigDto
} from './models/configurationExtended';

// Export modelMapping types with explicit re-exports to avoid conflicts
export type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  BulkModelMappingItemDto,
  BulkModelMappingPreviewRequest,
  BulkModelMappingPreviewResponse,
  BulkModelMappingCreateRequest,
  BulkModelMappingCreateResponse,
  BulkModelMappingResolutionDto,
  ModelMappingFilterOptions
} from './models/modelMapping';


// Services
export { FetchVirtualKeyService as VirtualKeyService } from './services/FetchVirtualKeyService';
export type { VirtualKeyListResponseDto } from './services/FetchVirtualKeyService';
export { FetchProvidersService as ProvidersService } from './services/FetchProvidersService';
export { FetchSystemService } from './services/FetchSystemService';
export { FetchModelMappingsService } from './services/FetchModelMappingsService';
export { FetchSettingsService } from './services/FetchSettingsService';
export type { SettingUpdate, SettingsDto } from './services/FetchSettingsService';
export { FetchAnalyticsService } from './services/FetchAnalyticsService';
export { FetchConfigurationService } from './services/FetchConfigurationService';
export { FetchIpFilterService } from './services/FetchIpFilterService';
export { FetchMediaService } from './services/FetchMediaService';
export { FetchModelCostService } from './services/FetchModelCostService';
export type {
  CostDashboardDto,
  CostTrendDto,
  CostTrendDataDto,
  DetailedCostDataDto,
} from './services/FetchAnalyticsService';
export { FetchModelService } from './services/FetchModelService';
export { FetchModelSeriesService } from './services/FetchModelSeriesService';
export { FetchModelAuthorService } from './services/FetchModelAuthorService';
export { ProviderToolsService } from './services/ProviderToolsService';
export type {
  ProviderTool,
  CreateProviderTool,
  UpdateProviderTool,
  ImportResult as ProviderToolImportResult,
  ProviderOption as ToolProviderOption
} from './services/ProviderToolsService';
// SignalR services removed - WebAdmin uses React Query instead of SignalR for data fetching
export {
  FetchFunctionConfigurationsService,
  FetchFunctionCredentialsService,
  FetchFunctionCostsService,
  FetchFunctionExecutionsService
} from './services/FetchFunctionsService';
export { FetchPricingService } from './services/FetchPricingService';
export * from './models/pricing';
export { FetchProviderSyncService } from './services/FetchProviderSyncService';

// SignalR Hub Clients removed - WebAdmin uses React Query instead of SignalR for data fetching

// Utilities
export * from './utils/errors';
export * from './utils/costFormatters';

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
