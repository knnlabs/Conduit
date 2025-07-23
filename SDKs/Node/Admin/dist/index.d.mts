import { V as VirtualKeyMetadata, E as ExportDestinationConfig, a as ExtendedMetadata, C as ConfigValue, F as FetchBaseApiClient, b as CreateProviderCredentialDto, P as ProviderCredentialDto, c as ProviderFilters, U as UpdateProviderCredentialDto, d as ProviderConnectionTestResultDto, e as ProviderConnectionTestRequest$1, f as ProviderHealthConfigurationDto, g as UpdateProviderHealthConfigurationDto, h as ProviderHealthSummaryDto, i as ProviderHealthStatusDto, j as ProviderHealthFilters, k as ProviderHealthRecordDto, l as ProviderUsageStatistics, m as ProviderDataDto, n as CreateModelProviderMappingDto, M as ModelProviderMappingDto, o as ModelMappingFilters, p as UpdateModelProviderMappingDto, B as BulkMappingRequest, q as BulkMappingResponse, D as DiscoveredModel, r as CapabilityTestResult, s as ModelRoutingInfo, t as ModelMappingSuggestion, S as SettingFilters, G as GlobalSettingDto, u as CreateGlobalSettingDto, v as UpdateGlobalSettingDto, A as AudioConfigurationDto, w as CreateAudioConfigurationDto, x as UpdateAudioConfigurationDto, R as RouterConfigurationDto, y as UpdateRouterConfigurationDto, z as RouterRule, H as SystemConfiguration, I as CreateIpFilterDto, J as IpFilterDto, K as IpFilterFilters, L as UpdateIpFilterDto, N as IpFilterSettingsDto, O as UpdateIpFilterSettingsDto, Q as IpCheckResult, T as FilterType, W as BulkOperationResult, X as CreateTemporaryIpFilterDto, Y as IpFilterImport, Z as IpFilterImportResult, _ as BlockedRequestStats, $ as IpFilterStatistics, a0 as BulkIpFilterResponse, a1 as IpFilterValidationResult, a2 as CreateModelCostDto, a3 as ModelCost, a4 as PagedResult, a5 as ModelCostFilters, a6 as ModelCostDto, a7 as UpdateModelCostDto, a8 as ModelCostCalculation, a9 as ImportResult, aa as ModelCostOverview, ab as CostTrend, ac as BulkModelCostUpdate, ad as ModelCostHistory, ae as CostEstimate, af as ModelCostComparison, ag as RequestLogFilters, ah as RequestLogDto, ai as UsageMetricsDto, aj as ModelUsageDto, ak as KeyUsageDto, al as AnalyticsFilters, am as CostForecastDto, an as AnomalyDto, ao as AnalyticsOptions, ap as SystemInfoDto, aq as HealthStatusDto, ar as BackupDto, as as CreateBackupRequest, at as RestoreBackupRequest, au as BackupRestoreResult, av as NotificationDto, aw as CreateNotificationDto, ax as MaintenanceTaskDto, ay as RunMaintenanceTaskRequest, az as MaintenanceTaskResult, aA as AuditLogFilters, aB as AuditLogDto, aC as DiagnosticChecks, aD as FeatureAvailability, aE as ApiClientConfig, aF as CreateProviderHealthConfigurationDto, aG as ProviderHealthStatisticsDto, aH as ProviderStatus, aI as UpdateNotificationDto, aJ as NotificationType, aK as NotificationSeverity, aL as NotificationStatistics, aM as NotificationBulkResponse, aN as NotificationFilters, aO as NotificationSummary, aP as SecurityEventFilters, aQ as SecurityEvent, aR as CreateSecurityEventDto, aS as ThreatFilters, aT as ThreatDetection, aU as ThreatAction, aV as ThreatAnalytics, aW as ComplianceMetrics, aX as RoutingConfiguration, aY as UpdateRoutingConfigDto, aZ as TestResult, a_ as LoadBalancerHealth, a$ as CachingConfiguration, b0 as UpdateCachingConfigDto, b1 as CachePolicy, b2 as CreateCachePolicyDto, b3 as UpdateCachePolicyDto, b4 as CacheRegion, b5 as ClearCacheResult, b6 as CacheStatistics, b7 as RoutingConfigDto, b8 as UpdateRoutingConfigDto$1, b9 as RoutingRule, ba as CreateRoutingRuleDto, bb as UpdateRoutingRuleDto, bc as LoadBalancerHealthDto, bd as FetchConduitAdminClient } from './FetchConduitAdminClient-Db_qplg5.mjs';
export { bn as AccessPolicy, bm as ActiveThreat, fH as AdditionalProviderInfo, cy as AdminComponents, cz as AdminOperations, cA as AdminPaths, eB as AlertAction, eA as AlertCondition, ez as AlertDto, eE as AlertHistoryEntry, ff as AlertMetadata, d2 as AlertParams, ew as AlertSeverity, ex as AlertStatus, ey as AlertTriggerType, fe as AnalyticsMetadata, fj as AudioConfigMetadata, cx as AudioConfigurationService, e8 as AudioCostConfigDto, e7 as AudioCostConfigRequest, en as AudioCurrencies, eo as AudioCurrency, eb as AudioKeyUsageDto, ek as AudioOperationType, ej as AudioOperationTypes, ed as AudioOperationUsageDto, e6 as AudioProviderConfigDto, e5 as AudioProviderConfigRequest, fq as AudioProviderSettings, eg as AudioProviderTestResult, ec as AudioProviderUsageDto, em as AudioUnitType, el as AudioUnitTypes, e9 as AudioUsageDto, eh as AudioUsageFilters, ea as AudioUsageSummaryDto, ei as AudioUsageSummaryFilters, bs as AuditLog, bt as AuditLogPage, br as AuditLogParams, fc as BaseMetadata, ds as BulkIpFilterRequest, bB as CacheClearParams, bC as CacheClearResult, bA as CacheCondition, bx as CacheConfigDto, bE as CacheKeyStats, f8 as CacheNode, cC as CacheProvider, bz as CacheRule, bD as CacheStatsDto, dG as CapabilityUsage, cH as ConduitConfig, d4 as ConnectionTestResult, c9 as CostDashboardDto, ca as CostModelCostDto, cd as CostTrendDto, eP as CpuCoreMetrics, bp as CreateAccessPolicyDto, eC as CreateAlertDto, eL as CreateDashboardDto, fA as CustomSettings, cc as DailyCostDto, eF as DashboardDto, eG as DashboardLayout, eH as DashboardWidget, dS as DatabaseMetrics, fs as DiagnosticResult, eS as DiskDeviceMetrics, eR as DiskMetrics, dV as EndpointHealth, fa as EndpointStatistics, dO as EndpointUsageBreakdown, cl as ErrorDetails, cj as ErrorMessage, ck as ErrorMessageDetail, cm as ErrorMessageListResponse, c_ as ErrorMetric, ct as ErrorQueueHealth, cg as ErrorQueueInfo, ci as ErrorQueueListResponse, cq as ErrorQueueStatistics, ch as ErrorQueueSummary, cn as ErrorRateTrend, bN as ErrorSummary, d7 as ErrorTypeCount, fC as EventData, fh as ExportConfigMetadata, f5 as ExportFormat, dI as ExportParams, dJ as ExportResult, co as FailingMessageType, bO as FeatureFlag, bP as FeatureFlagCondition, fo as FeatureFlagContext, c0 as FetchAnalyticsService, c3 as FetchConfigurationService, c7 as FetchCostDashboardService, c6 as FetchErrorQueueService, c5 as FetchIpFilterService, c8 as FetchModelCostService, bW as FetchModelMappingsService, c4 as FetchMonitoringService, c1 as FetchProviderHealthService, bX as FetchProviderModelsService, c2 as FetchSecurityService, bY as FetchSettingsService, bV as FetchSystemService, dq as FilterMode, d3 as HealthAlert, da as HealthAlertListResponseDto, cW as HealthCheck, fv as HealthCheckDetails, d1 as HealthDataPoint, e1 as HealthEventDto, e4 as HealthEventSubscription, e3 as HealthEventSubscriptionOptions, e2 as HealthEventsResponseDto, d0 as HealthHistory, dW as HealthHistoryPoint, cs as HealthIssue, cr as HealthStatusCounts, cT as HealthSummaryDto, c$ as HistoryParams, cD as HttpError, d8 as Incident, dr as IpCheckRequest, bf as IpEntry, be as IpWhitelistDto, cY as LatencyMetric, bF as LoadBalancerConfigDto, bH as LoadBalancerNode, e_ as LogEntry, e$ as LogQueryParams, f0 as LogStreamOptions, cB as Logger, fE as MaintenanceTaskConfig, fF as MaintenanceTaskResultData, d9 as MaintenanceWindow, cw as MessageDeleteResponse, cv as MessageReplayResponse, fz as Metadata, es as MetricDataPoint, fG as MetricDimensions, f3 as MetricExportParams, f4 as MetricExportResult, et as MetricTimeSeries, eu as MetricsQueryParams, ev as MetricsResponse, bS as ModelCapabilities, fi as ModelConfigMetadata, ce as ModelCostDataDto, cM as ModelDetailsDto, cL as ModelDto, cN as ModelExample, cQ as ModelListResponseDto, dH as ModelPerformanceMetrics, bR as ModelProviderInfo, fr as ModelQueryParams, cO as ModelSearchFilters, cP as ModelSearchResult, dz as ModelUsage, dM as ModelUsageBreakdown, eO as MonitoringCpuMetrics, fu as MonitoringFields, f1 as MonitoringHealthStatus, eQ as MonitoringMemoryMetrics, eU as NetworkInterfaceMetrics, eT as NetworkMetrics, bI as PerformanceConfigDto, bM as PerformanceDataPoint, d6 as PerformanceMetrics, d5 as PerformanceParams, bK as PerformanceTestParams, bL as PerformanceTestResult, bo as PolicyRule, eV as ProcessMetrics, fd as ProviderConfigMetadata, cb as ProviderCostDto, dg as ProviderEndpointHealth, dl as ProviderHealthDataPoint, dU as ProviderHealthDetails, cV as ProviderHealthDto, dj as ProviderHealthHistoryOptions, dk as ProviderHealthHistoryResponse, di as ProviderHealthIncident, dd as ProviderHealthItem, db as ProviderHealthListResponseDto, df as ProviderHealthMetricsDto, dc as ProviderHealthStatusResponse, cU as ProviderHealthSummary, dX as ProviderIncident, dh as ProviderModelHealth, f7 as ProviderPriority, fp as ProviderSettings, dx as ProviderUsage, dL as ProviderUsageBreakdown, de as ProviderWithHealthDto, bU as ProvidersService, cu as QueueClearResponse, cp as QueueGrowthPattern, dR as QueueMetrics, dZ as QuotaAlert, ee as RealtimeSessionDto, ef as RealtimeSessionMetricsDto, cR as RefreshModelsRequest, cS as RefreshModelsResponse, f9 as RegionStatistics, cI as RequestConfig, cF as RequestConfigInfo, du as RequestLogPage, dt as RequestLogParams, dP as RequestLogStatisticsParams, cJ as ResponseInfo, cG as RetryConfig, bu as RetryPolicy, dp as RouterAction, fD as RouterActionParameters, dn as RouterCondition, f6 as RoutingRule, bw as RuleAction, bv as RuleCondition, fx as SecurityChangeRecord, fw as SecurityEventDetails, bi as SecurityEventExtended, fg as SecurityEventMetadata, bj as SecurityEventPage, bg as SecurityEventParams, bh as SecurityEventType, dQ as ServiceHealthMetrics, f2 as ServiceHealthStatus, e0 as ServiceStatusDto, ft as SessionMetadata, dm as SettingCategory, bZ as SettingUpdate, b_ as SettingsDto, b$ as SettingsListResponseDto, cE as SignalRConfig, eX as SpanDto, eY as SpanLog, fb as StatisticPoint, cK as StatusType, dT as SystemAlert, d_ as SystemHealthDto, d$ as SystemMetricsDto, fy as SystemParameters, eN as SystemResourceMetrics, bl as ThreatCategory, bk as ThreatSummaryDto, cZ as ThroughputMetric, dA as TimeSeriesData, dK as TimeSeriesDataPoint, eW as TraceDto, eZ as TraceQueryParams, dF as TrendData, bq as UpdateAccessPolicyDto, eD as UpdateAlertDto, by as UpdateCacheConfigDto, eM as UpdateDashboardDto, bQ as UpdateFeatureFlagDto, bG as UpdateLoadBalancerConfigDto, bJ as UpdatePerformanceConfigDto, cX as UptimeMetric, dw as UsageAnalytics, dv as UsageParams, fB as ValidationFunction, fk as VideoGenerationMetadata, dC as VirtualKeyAnalytics, cf as VirtualKeyCostDataDto, dY as VirtualKeyDetail, dB as VirtualKeyParams, dE as VirtualKeyRanking, bT as VirtualKeyService, dy as VirtualKeyUsage, dN as VirtualKeyUsageBreakdown, dD as VirtualKeyUsageSummary, eJ as WidgetConfig, eK as WidgetDataSource, eI as WidgetPosition, fl as isValidMetadata, fm as parseMetadata, fn as stringifyMetadata, eq as validateAudioCostConfigRequest, ep as validateAudioProviderRequest, er as validateAudioUsageFilters } from './FetchConduitAdminClient-Db_qplg5.mjs';
import { FilterOptions, HttpTransportType, SignalRLogLevel, HubConnectionState, PaginatedResponse, DateRange, PagedResponse, BaseSignalRConnection as BaseSignalRConnection$1, ModelCapability } from '@knn_labs/conduit-common';
export { ApiResponse, AuthError, AuthenticationError, AuthorizationError, CONTENT_TYPES, ConduitError, ConflictError, DateRange, DefaultTransports, ErrorResponse, ErrorResponseFormat, FilterOptions, HTTP_HEADERS, HttpMethod, HttpTransportType, HubConnectionState, ModelCapability, NetworkError, NotFoundError, NotImplementedError, PagedResponse, PaginatedResponse, RateLimitError, RequestOptions, ServerError, SignalRLogLevel, SortDirection, SortOptions, StreamError, TimeoutError, ValidationError, createErrorFromResponse, deserializeError, getErrorMessage, getErrorStatusCode, handleApiError, isAuthError, isAuthorizationError, isConduitError, isConflictError, isErrorLike, isHttpError, isHttpNetworkError, isNetworkError, isNotFoundError, isRateLimitError, isSerializedConduitError, isStreamError, isTimeoutError, isValidationError, serializeError } from '@knn_labs/conduit-common';
import { HubConnection } from '@microsoft/signalr';

type BudgetDuration = 'Total' | 'Daily' | 'Weekly' | 'Monthly';
interface VirtualKeyDto {
    id: number;
    keyName: string;
    apiKey?: string;
    keyPrefix?: string;
    allowedModels: string;
    maxBudget: number;
    currentSpend: number;
    budgetDuration: BudgetDuration;
    budgetStartDate: string;
    isEnabled: boolean;
    expiresAt?: string;
    createdAt: string;
    updatedAt: string;
    metadata?: string;
    rateLimitRpm?: number;
    rateLimitRpd?: number;
    lastUsedAt?: string;
    requestCount?: number;
}
interface CreateVirtualKeyRequest {
    keyName: string;
    allowedModels?: string;
    maxBudget?: number;
    budgetDuration?: BudgetDuration;
    expiresAt?: string;
    metadata?: string;
    rateLimitRpm?: number;
    rateLimitRpd?: number;
}
interface CreateVirtualKeyResponse {
    virtualKey: string;
    keyInfo: VirtualKeyDto;
}
interface UpdateVirtualKeyRequest {
    keyName?: string;
    allowedModels?: string;
    maxBudget?: number;
    budgetDuration?: BudgetDuration;
    isEnabled?: boolean;
    expiresAt?: string;
    metadata?: string;
    rateLimitRpm?: number;
    rateLimitRpd?: number;
}
interface VirtualKeyValidationRequest {
    key: string;
}
interface VirtualKeyValidationResult {
    isValid: boolean;
    virtualKeyId?: number;
    keyName?: string;
    reason?: string;
    allowedModels?: string[];
    maxBudget?: number;
    currentSpend?: number;
    budgetRemaining?: number;
    expiresAt?: string;
    rateLimitRpm?: number;
    rateLimitRpd?: number;
}
interface UpdateSpendRequest {
    amount: number;
    description?: string;
}
interface RefundSpendRequest {
    amount: number;
    reason: string;
    originalTransactionId?: string;
}
interface CheckBudgetRequest {
    estimatedCost: number;
}
interface CheckBudgetResponse {
    hasAvailableBudget: boolean;
    availableBudget: number;
    estimatedCost: number;
    currentSpend: number;
    maxBudget: number;
}
interface VirtualKeyValidationInfo {
    keyId: number;
    keyName: string;
    isValid: boolean;
    validationErrors: string[];
    allowedModels: string[];
    budgetInfo: {
        maxBudget: number;
        currentSpend: number;
        remaining: number;
        duration: BudgetDuration;
    };
    rateLimits?: {
        rpm?: number;
        rpd?: number;
    };
    metadata?: VirtualKeyMetadata;
}
interface VirtualKeyMaintenanceRequest {
    cleanupExpiredKeys?: boolean;
    resetDailyBudgets?: boolean;
    resetWeeklyBudgets?: boolean;
    resetMonthlyBudgets?: boolean;
}
interface VirtualKeyMaintenanceResponse {
    expiredKeysDeleted?: number;
    dailyBudgetsReset?: number;
    weeklyBudgetsReset?: number;
    monthlyBudgetsReset?: number;
    errors?: string[];
}
interface VirtualKeyFilters extends FilterOptions {
    isEnabled?: boolean;
    hasExpired?: boolean;
    budgetDuration?: BudgetDuration;
    minBudget?: number;
    maxBudget?: number;
    allowedModels?: string[];
    createdAfter?: string;
    createdBefore?: string;
    lastUsedAfter?: string;
    lastUsedBefore?: string;
}
interface VirtualKeyStatistics {
    totalKeys: number;
    activeKeys: number;
    expiredKeys: number;
    totalSpend: number;
    averageSpendPerKey: number;
    keysNearBudgetLimit: number;
    keysByDuration: Record<BudgetDuration, number>;
}

/**
 * Analytics export-related models for the Admin SDK
 */

/**
 * Base export parameters common to all export types
 */
interface ExportParams {
    /** Export format */
    format: 'json' | 'csv' | 'excel';
    /** Date range for the export */
    dateRange: {
        startDate: string;
        endDate: string;
    };
    /** Whether to compress the export file */
    compression?: boolean;
    /** Whether to include metadata in the export */
    includeMetadata?: boolean;
}
/**
 * Parameters for exporting usage analytics
 */
interface ExportUsageParams extends ExportParams {
    /** Group results by this field */
    groupBy?: 'virtualKey' | 'model' | 'provider' | 'day' | 'hour';
    /** Filter by virtual key IDs */
    virtualKeyIds?: string[];
    /** Filter by model patterns */
    modelPatterns?: string[];
    /** Include detailed token information */
    includeTokenDetails?: boolean;
}
/**
 * Parameters for exporting cost analytics
 */
interface ExportCostParams extends ExportParams {
    /** Group results by this field */
    groupBy?: 'virtualKey' | 'model' | 'provider' | 'day' | 'month';
    /** Include cost predictions */
    includePredictions?: boolean;
    /** Currency for cost values */
    currency?: 'USD' | 'EUR' | 'GBP';
}
/**
 * Parameters for exporting virtual key analytics
 */
interface ExportVirtualKeyParams extends ExportParams {
    /** Filter by virtual key IDs */
    virtualKeyIds?: string[];
    /** Include usage statistics */
    includeUsageStats?: boolean;
    /** Include key settings */
    includeSettings?: boolean;
    /** Include audit log */
    includeAuditLog?: boolean;
}
/**
 * Parameters for exporting provider analytics
 */
interface ExportProviderParams extends ExportParams {
    /** Filter by provider IDs */
    providerIds?: string[];
    /** Include health metrics */
    includeHealthMetrics?: boolean;
    /** Include performance statistics */
    includePerformanceStats?: boolean;
    /** Include error analysis */
    includeErrorAnalysis?: boolean;
}
/**
 * Parameters for exporting security analytics
 */
interface ExportSecurityParams extends ExportParams {
    /** Filter by event types */
    eventTypes?: string[];
    /** Filter by severity levels */
    severities?: string[];
    /** Include resolved incidents */
    includeResolvedIncidents?: boolean;
    /** Anonymize sensitive data */
    anonymizeData?: boolean;
}
/**
 * Result of an export operation
 */
interface ExportResult {
    /** Unique identifier for the export */
    exportId: string;
    /** Current status of the export */
    status: 'pending' | 'processing' | 'completed' | 'failed';
    /** Export format */
    format: string;
    /** Size of the export file in bytes */
    sizeBytes?: number;
    /** URL to download the export */
    downloadUrl?: string;
    /** When the download URL expires */
    expiresAt?: string;
    /** Error message if the export failed */
    error?: string;
}
/**
 * DTO for creating an export schedule
 */
interface CreateExportScheduleDto {
    /** Name of the schedule */
    name: string;
    /** Type of export */
    exportType: 'usage' | 'cost' | 'security' | 'full';
    /** Cron expression for scheduling */
    schedule: string;
    /** Export parameters */
    params: ExportParams;
    /** Export destination configuration */
    destination?: {
        type: 's3' | 'email' | 'webhook';
        config: ExportDestinationConfig;
    };
}
/**
 * Scheduled export configuration
 */
interface ExportSchedule {
    /** Unique identifier */
    id: string;
    /** Schedule name */
    name: string;
    /** Export type */
    exportType: string;
    /** Cron schedule */
    schedule: string;
    /** Export parameters */
    params: ExportParams;
    /** Destination configuration */
    destination?: {
        type: 's3' | 'email' | 'webhook';
        config: ExportDestinationConfig;
    };
    /** Last execution time */
    lastRun?: string;
    /** Next scheduled execution */
    nextRun?: string;
    /** Whether the schedule is enabled */
    enabled: boolean;
}
/**
 * Export history entry
 */
interface ExportHistory {
    /** Export ID */
    exportId: string;
    /** Export type */
    type: string;
    /** When the export was requested */
    requestedAt: string;
    /** When the export completed */
    completedAt?: string;
    /** Export status */
    status: string;
    /** Export format */
    format: string;
    /** File size in bytes */
    sizeBytes?: number;
    /** Number of records exported */
    recordCount?: number;
    /** Download URL */
    downloadUrl?: string;
    /** Error message if failed */
    error?: string;
}
/**
 * Parameters for exporting request logs
 */
interface ExportRequestLogsParams {
    /** Export format */
    format: 'json' | 'csv' | 'excel';
    /** Filters for request logs */
    filters: {
        startDate?: string;
        endDate?: string;
        statusCodes?: number[];
        methods?: string[];
        endpoints?: string[];
        virtualKeyIds?: string[];
        providerNames?: string[];
        minResponseTime?: number;
        maxResponseTime?: number;
        minCost?: number;
        ipAddresses?: string[];
    };
    /** Specific fields to include in export */
    fields?: string[];
    /** Sort configuration */
    sortBy?: {
        field: string;
        direction: 'asc' | 'desc';
    };
    /** Whether to compress the export */
    compression?: boolean;
    /** Maximum number of records to export */
    maxRecords?: number;
}
/**
 * Request log statistics
 */
interface RequestLogStatistics {
    totalRequests: number;
    uniqueVirtualKeys: number;
    uniqueIpAddresses: number;
    averageResponseTime: number;
    medianResponseTime: number;
    p95ResponseTime: number;
    p99ResponseTime: number;
    totalCost: number;
    totalTokensUsed: number;
    errorRate: number;
    statusCodeDistribution: Record<number, number>;
    endpointDistribution: Array<{
        endpoint: string;
        count: number;
        avgResponseTime: number;
    }>;
    hourlyDistribution: Array<{
        hour: number;
        count: number;
    }>;
}
/**
 * Parameters for request log summary
 */
interface RequestLogSummaryParams {
    /** Field to group by */
    groupBy: 'virtualKey' | 'endpoint' | 'provider' | 'hour' | 'day' | 'statusCode';
    /** Start date filter */
    startDate?: string;
    /** End date filter */
    endDate?: string;
    /** Maximum number of groups to return */
    limit?: number;
}
/**
 * Request log summary
 */
interface RequestLogSummary {
    /** Field used for grouping */
    groupBy: string;
    /** Time period of the summary */
    period: {
        start: string;
        end: string;
    };
    /** Summary groups */
    groups: Array<{
        key: string;
        count: number;
        totalCost: number;
        avgResponseTime: number;
        errorRate: number;
        totalTokens: number;
    }>;
}
/**
 * Enhanced request log type with additional fields
 */
interface RequestLog {
    id: string;
    timestamp: string;
    method: string;
    endpoint: string;
    statusCode: number;
    responseTime: number;
    virtualKeyId: string;
    virtualKeyName?: string;
    providerName?: string;
    modelName?: string;
    ipAddress: string;
    userAgent?: string;
    requestSize: number;
    responseSize: number;
    tokensUsed?: {
        prompt: number;
        completion: number;
        total: number;
    };
    cost?: number;
    error?: {
        type: string;
        message: string;
    };
    metadata?: ExtendedMetadata;
}
/**
 * Export status details
 */
interface ExportStatus {
    exportId: string;
    status: 'pending' | 'processing' | 'completed' | 'failed';
    progress?: {
        current: number;
        total: number;
        percentage: number;
    };
    startedAt?: string;
    completedAt?: string;
    error?: string;
    result?: {
        recordCount: number;
        sizeBytes: number;
        downloadUrl: string;
        expiresAt: string;
    };
}

/**
 * Database connection pool statistics
 */
interface DatabasePoolMetrics {
    /** Current number of active connections */
    activeConnections: number;
    /** Current number of idle connections */
    idleConnections: number;
    /** Total number of connections in the pool */
    totalConnections: number;
    /** Maximum allowed connections */
    maxConnections: number;
    /** Total number of connections created */
    totalConnectionsCreated: number;
    /** Total number of connections closed */
    totalConnectionsClosed: number;
    /** Current wait count for connections */
    waitCount: number;
    /** Current wait time in milliseconds for new connections */
    waitTimeMs: number;
    /** Pool efficiency as a percentage (0-100) */
    poolEfficiency: number;
    /** Time when metrics were collected */
    collectedAt: string;
}
/**
 * System metrics for the Admin API
 */
interface SystemMetrics {
    /** Database connection pool metrics */
    databasePool: DatabasePoolMetrics;
    /** Memory usage statistics */
    memory: MemoryMetrics;
    /** CPU usage statistics */
    cpu: CpuMetrics;
    /** Garbage collection statistics */
    gc?: GcMetrics;
    /** Request statistics */
    requests: RequestMetrics;
    /** Time when metrics were collected */
    collectedAt: string;
}
/**
 * Memory usage statistics
 */
interface MemoryMetrics {
    /** Total memory allocated in bytes */
    totalAllocated: number;
    /** Working set memory in bytes */
    workingSet: number;
    /** Private memory in bytes */
    privateMemory: number;
    /** GC heap size in bytes */
    gcHeapSize: number;
    /** Gen 0 heap size in bytes */
    gen0HeapSize: number;
    /** Gen 1 heap size in bytes */
    gen1HeapSize: number;
    /** Gen 2 heap size in bytes */
    gen2HeapSize: number;
    /** Large object heap size in bytes */
    largeObjectHeapSize: number;
}
/**
 * CPU usage statistics
 */
interface CpuMetrics {
    /** CPU usage percentage (0-100) */
    usage: number;
    /** Total processor time in milliseconds */
    totalProcessorTime: number;
    /** User processor time in milliseconds */
    userProcessorTime: number;
    /** Privileged processor time in milliseconds */
    privilegedProcessorTime: number;
    /** Number of threads */
    threadCount: number;
}
/**
 * Garbage collection statistics
 */
interface GcMetrics {
    /** Total number of GC collections across all generations */
    totalCollections: number;
    /** Gen 0 collection count */
    gen0Collections: number;
    /** Gen 1 collection count */
    gen1Collections: number;
    /** Gen 2 collection count */
    gen2Collections: number;
    /** Total time spent in GC in milliseconds */
    totalTimeInGc: number;
    /** Total allocated bytes */
    totalAllocatedBytes: number;
    /** Total bytes allocated since last collection */
    allocatedSinceLastGc: number;
}
/**
 * Request statistics
 */
interface RequestMetrics {
    /** Total number of requests processed */
    totalRequests: number;
    /** Number of requests per second (current rate) */
    requestsPerSecond: number;
    /** Average response time in milliseconds */
    averageResponseTime: number;
    /** Number of failed requests */
    failedRequests: number;
    /** Error rate as a percentage (0-100) */
    errorRate: number;
    /** Number of active requests currently being processed */
    activeRequests: number;
    /** Longest running request duration in milliseconds */
    longestRunningRequest?: number;
}
/**
 * Response containing database pool metrics
 */
interface DatabasePoolMetricsResponse {
    /** Database connection pool statistics */
    metrics: DatabasePoolMetrics;
    /** Whether the pool is healthy */
    isHealthy: boolean;
    /** Optional health message */
    healthMessage?: string;
}
/**
 * Response containing all Admin API metrics
 */
interface AdminMetricsResponse {
    /** Comprehensive system metrics */
    metrics: SystemMetrics;
    /** Overall system health status */
    isHealthy: boolean;
    /** Optional health message */
    healthMessage?: string;
    /** API version */
    apiVersion: string;
    /** Service uptime in milliseconds */
    uptime: number;
}

/**
 * Represents information about a database backup
 */
interface BackupInfo {
    /** Unique identifier for the backup */
    id: string;
    /** File name of the backup */
    fileName: string;
    /** When the backup was created */
    createdAt: Date;
    /** Size of the backup file in bytes */
    sizeBytes: number;
    /** Human-readable formatted size of the backup file */
    sizeFormatted: string;
}
/**
 * Represents the result of a backup operation
 */
interface BackupResult {
    /** Whether the backup operation was successful */
    success: boolean;
    /** Error message if the backup operation failed */
    errorMessage?: string;
    /** Backup information if the operation was successful */
    backupInfo?: BackupInfo;
}
/**
 * Represents the result of a restore operation
 */
interface RestoreResult {
    /** Whether the restore operation was successful */
    success: boolean;
    /** Error message if the restore operation failed */
    errorMessage?: string;
    /** Additional details about the restore operation */
    details?: string;
}
/**
 * Options for backup operations
 */
interface BackupOptions {
    /** Whether to include schema in the backup */
    includeSchema?: boolean;
    /** Whether to include data in the backup */
    includeData?: boolean;
    /** Whether to compress the backup file */
    compress?: boolean;
    /** Custom description for the backup */
    description?: string;
    /** Tags to associate with the backup */
    tags?: string[];
}
/**
 * Options for restore operations
 */
interface RestoreOptions {
    /** Whether to overwrite existing data during restore */
    overwriteExisting?: boolean;
    /** Whether to verify the backup before restoring */
    verifyBeforeRestore?: boolean;
    /** Whether to create a backup before restoring */
    backupBeforeRestore?: boolean;
    /** Specific tables to restore (empty list means all tables) */
    specificTables?: string[];
}
/**
 * Represents backup validation results
 */
interface BackupValidationResult {
    /** Whether the backup is valid */
    isValid: boolean;
    /** Validation errors if any */
    errors: string[];
    /** Validation warnings if any */
    warnings: string[];
    /** Metadata about the backup contents */
    metadata?: BackupMetadata;
}
/**
 * Represents metadata about backup contents
 */
interface BackupMetadata {
    /** Database type (SQLite, PostgreSQL, etc.) */
    databaseType: string;
    /** Database version */
    databaseVersion: string;
    /** Backup format version */
    backupFormatVersion: string;
    /** List of tables included in the backup */
    tables: string[];
    /** Total number of records in the backup */
    totalRecords: number;
    /** When the backup was created */
    createdAt: Date;
}
/**
 * Represents backup storage statistics
 */
interface BackupStorageStats {
    /** Total number of backups */
    totalBackups: number;
    /** Total storage size in bytes */
    totalSizeBytes: number;
    /** Formatted total storage size */
    totalSizeFormatted: string;
    /** Average backup size in bytes */
    averageSizeBytes: number;
    /** Formatted average backup size */
    averageSizeFormatted: string;
    /** Date of the oldest backup */
    oldestBackup?: Date;
    /** Date of the newest backup */
    newestBackup?: Date;
    /** Information about the largest backup */
    largestBackup?: BackupInfo;
}
/**
 * Response for backup download operations
 */
interface BackupDownloadResponse {
    /** The backup file content as ArrayBuffer */
    data: ArrayBuffer;
    /** Content type of the backup file */
    contentType: string;
    /** File name of the backup */
    fileName: string;
    /** Size of the backup file in bytes */
    sizeBytes: number;
}
/**
 * Filter options for backup queries
 */
interface BackupFilters {
    /** Start date for filtering backups */
    startDate?: Date;
    /** End date for filtering backups */
    endDate?: Date;
    /** Minimum size in bytes */
    minSizeBytes?: number;
    /** Maximum size in bytes */
    maxSizeBytes?: number;
    /** Tags to filter by */
    tags?: string[];
    /** Sort field */
    sortBy?: 'createdAt' | 'sizeBytes' | 'fileName';
    /** Sort direction */
    sortDirection?: 'asc' | 'desc';
    /** Page number for pagination */
    page?: number;
    /** Page size for pagination */
    pageSize?: number;
}
/**
 * Backup operation progress information
 */
interface BackupProgress {
    /** Operation ID for tracking */
    operationId: string;
    /** Current operation stage */
    stage: BackupStage;
    /** Progress percentage (0-100) */
    percentage: number;
    /** Current status message */
    message: string;
    /** Whether the operation is complete */
    isComplete: boolean;
    /** Whether the operation failed */
    isFailed: boolean;
    /** Error message if operation failed */
    errorMessage?: string;
    /** Estimated time remaining in seconds */
    estimatedTimeRemaining?: number;
}
/**
 * Backup operation stages
 */
declare enum BackupStage {
    /** Initializing backup operation */
    Initializing = "initializing",
    /** Analyzing database structure */
    Analyzing = "analyzing",
    /** Backing up schema */
    BackingUpSchema = "backing_up_schema",
    /** Backing up data */
    BackingUpData = "backing_up_data",
    /** Compressing backup file */
    Compressing = "compressing",
    /** Finalizing backup */
    Finalizing = "finalizing",
    /** Backup completed successfully */
    Completed = "completed",
    /** Backup failed */
    Failed = "failed"
}
/**
 * Summary of backup system status
 */
interface BackupSystemStatus {
    /** Whether backup system is operational */
    isOperational: boolean;
    /** Last successful backup date */
    lastBackupDate?: Date;
    /** Number of available backups */
    availableBackups: number;
    /** Total storage used by backups */
    totalStorageUsed: string;
    /** Available storage space */
    availableStorage?: string;
    /** Current backup operation in progress */
    currentOperation?: BackupProgress;
    /** System health status */
    healthStatus: 'healthy' | 'warning' | 'error';
    /** Status messages or warnings */
    statusMessages: string[];
}

interface SignalRConnectionOptions {
    hubUrl: string;
    masterKey?: string;
    virtualKey?: string;
    accessToken?: string | (() => string | Promise<string>);
    transport?: HttpTransportType;
    logLevel?: SignalRLogLevel;
    withCredentials?: boolean;
    headers?: Record<string, string>;
    skipNegotiation?: boolean;
    reconnectDelay?: number[];
    connectionTimeout?: number;
    onConnectionStateChanged?: (state: HubConnectionState) => void;
    onReconnecting?: (error?: Error) => void;
    onReconnected?: (connectionId?: string) => void;
    onClose?: (error?: Error) => void;
}
/**
 * SignalR endpoints for Admin API
 */
declare const SignalREndpoints: {
    readonly NavigationState: "/hubs/navigation-state";
    readonly AdminNotifications: "/hubs/admin-notifications";
};
/**
 * Navigation state update event
 */
interface NavigationStateUpdateEvent {
    timestamp: string;
    changedEntities: {
        modelMappings?: boolean;
        providers?: boolean;
        virtualKeys?: boolean;
        settings?: boolean;
    };
    summary: {
        totalProviders: number;
        enabledProviders: number;
        totalMappings: number;
        activeMappings: number;
        totalVirtualKeys: number;
        activeVirtualKeys: number;
    };
}
/**
 * Model discovered event
 */
interface ModelDiscoveredEvent {
    providerId: number;
    providerName: string;
    model: {
        id: string;
        name: string;
        capabilities: string[];
        contextWindow?: number;
        maxOutput?: number;
    };
    timestamp: string;
}
/**
 * Provider health change event
 */
interface ProviderHealthChangeEvent {
    providerId: number;
    providerName: string;
    previousStatus: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    currentStatus: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    healthScore: number;
    issues?: string[];
    timestamp: string;
}
/**
 * Virtual key event types
 */
type VirtualKeyEventType = 'created' | 'updated' | 'deleted' | 'enabled' | 'disabled' | 'spend_updated';
/**
 * Virtual key event
 */
interface VirtualKeyEvent {
    eventType: VirtualKeyEventType;
    virtualKeyId: number;
    virtualKeyHash: string;
    virtualKeyName?: string;
    changes?: {
        field: string;
        oldValue: ConfigValue;
        newValue: ConfigValue;
    }[];
    metadata?: {
        currentSpend?: number;
        spendLimit?: number;
        isEnabled?: boolean;
    };
    timestamp: string;
}
/**
 * Configuration change event
 */
interface ConfigurationChangeEvent {
    category: string;
    setting: string;
    oldValue: ConfigValue;
    newValue: ConfigValue;
    changedBy?: string;
    timestamp: string;
}
/**
 * Admin notification event
 */
interface AdminNotificationEvent {
    id: string;
    type: 'info' | 'warning' | 'error' | 'success';
    title: string;
    message: string;
    details?: ExtendedMetadata;
    actionRequired?: boolean;
    actions?: {
        label: string;
        action: string;
        data?: ExtendedMetadata;
    }[];
    timestamp: string;
}
/**
 * Hub server interfaces
 */
interface INavigationStateHubServer {
    SubscribeToUpdates(groupName?: string): Promise<void>;
    UnsubscribeFromUpdates(groupName?: string): Promise<void>;
}
interface IAdminNotificationHubServer {
    SubscribeToVirtualKey(virtualKeyId: number): Promise<void>;
    UnsubscribeFromVirtualKey(virtualKeyId: number): Promise<void>;
    SubscribeToProvider(providerName: string): Promise<void>;
    UnsubscribeFromProvider(providerName: string): Promise<void>;
    RefreshProviderHealth(): Promise<void>;
    AcknowledgeNotification?(notificationId: string): Promise<void>;
}
/**
 * Hub client interfaces
 */
interface INavigationStateHubClient {
    onNavigationStateUpdate(callback: (event: NavigationStateUpdateEvent) => void): void;
    onModelDiscovered(callback: (event: ModelDiscoveredEvent) => void): void;
    onProviderHealthChange(callback: (event: ProviderHealthChangeEvent) => void): void;
}
interface IAdminNotificationHubClient {
    onVirtualKeyEvent(callback: (event: VirtualKeyEvent) => void): void;
    onConfigurationChange(callback: (event: ConfigurationChangeEvent) => void): void;
    onAdminNotification(callback: (event: AdminNotificationEvent) => void): void;
}

/**
 * Callback function types for event subscriptions
 */
type NavigationStateUpdateCallback = (event: NavigationStateUpdateEvent) => void;
type ModelDiscoveredCallback = (event: ModelDiscoveredEvent) => void;
type ProviderHealthChangeCallback = (event: ProviderHealthChangeEvent) => void;
type VirtualKeyEventCallback = (event: VirtualKeyEvent) => void;
type ConfigurationChangeCallback = (event: ConfigurationChangeEvent) => void;
type AdminNotificationCallback = (event: AdminNotificationEvent) => void;
/**
 * Subscription handle returned when subscribing to events
 */
interface NotificationSubscription {
    /**
     * Unique identifier for this subscription
     */
    id: string;
    /**
     * The event type this subscription is for
     */
    eventType: 'navigationStateUpdate' | 'modelDiscovered' | 'providerHealthChange' | 'virtualKeyEvent' | 'configurationChange' | 'adminNotification';
    /**
     * Unsubscribe from this event
     */
    unsubscribe: () => void;
}
/**
 * Options for notification subscriptions
 */
interface AdminNotificationOptions {
    /**
     * Whether to automatically reconnect on connection loss
     */
    autoReconnect?: boolean;
    /**
     * Filter events by specific criteria
     */
    filter?: {
        providers?: string[];
        virtualKeyIds?: number[];
        categories?: string[];
        severity?: ('info' | 'warning' | 'error' | 'success')[];
    };
    /**
     * Error handler for subscription errors
     */
    onError?: (error: Error) => void;
    /**
     * Handler for connection state changes
     */
    onConnectionStateChange?: (state: 'connected' | 'disconnected' | 'reconnecting') => void;
}
/**
 * Real-time notification service interface
 */
interface IRealtimeNotificationService {
    /**
     * Subscribe to navigation state updates
     */
    onNavigationStateUpdate(callback: NavigationStateUpdateCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to model discovered events
     */
    onModelDiscovered(callback: ModelDiscoveredCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to provider health changes
     */
    onProviderHealthChange(callback: ProviderHealthChangeCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to virtual key events
     */
    onVirtualKeyEvent(callback: VirtualKeyEventCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to configuration changes
     */
    onConfigurationChange(callback: ConfigurationChangeCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to admin notifications
     */
    onAdminNotification(callback: AdminNotificationCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Unsubscribe from all notifications
     */
    unsubscribeAll(): Promise<void>;
    /**
     * Connect to SignalR hubs
     */
    connect(): Promise<void>;
    /**
     * Disconnect from SignalR hubs
     */
    disconnect(): Promise<void>;
    /**
     * Check if connected to SignalR hubs
     */
    isConnected(): boolean;
}

declare class ProviderService extends FetchBaseApiClient {
    create(request: CreateProviderCredentialDto): Promise<ProviderCredentialDto>;
    list(filters?: ProviderFilters): Promise<ProviderCredentialDto[]>;
    getById(id: number): Promise<ProviderCredentialDto>;
    getByName(providerName: string): Promise<ProviderCredentialDto>;
    getProviderNames(): Promise<string[]>;
    update(id: number, request: UpdateProviderCredentialDto): Promise<void>;
    deleteById(id: number): Promise<void>;
    testConnectionById(id: number): Promise<ProviderConnectionTestResultDto>;
    testConnection(request: ProviderConnectionTestRequest$1): Promise<ProviderConnectionTestResultDto>;
    getHealthConfigurations(): Promise<ProviderHealthConfigurationDto[]>;
    getHealthConfiguration(providerName: string): Promise<ProviderHealthConfigurationDto>;
    updateHealthConfiguration(providerName: string, request: UpdateProviderHealthConfigurationDto): Promise<void>;
    getHealthStatus(): Promise<ProviderHealthSummaryDto>;
    getProviderHealthStatus(providerName: string): Promise<ProviderHealthStatusDto>;
    getHealthHistory(filters?: ProviderHealthFilters): Promise<PaginatedResponse<ProviderHealthRecordDto>>;
    checkHealth(providerName: string): Promise<ProviderConnectionTestResultDto>;
    getUsageStatistics(_providerName: string, _startDate?: string, _endDate?: string): Promise<ProviderUsageStatistics>;
    bulkTest(_providerNames: string[]): Promise<ProviderConnectionTestResultDto[]>;
    getAvailableProviders(): Promise<ProviderDataDto[]>;
    private invalidateCache;
    private invalidateCachePattern;
}

interface ProviderModel {
    id: string;
    object: 'model';
    created: number;
    owned_by: string;
}
interface ProviderConnectionTestRequest {
    providerId: string;
    virtualKey?: string;
    testModel?: string;
}
interface ProviderConnectionTestResponse {
    success: boolean;
    providerId: string;
    message: string;
    modelsCount?: number;
    testResults?: {
        modelsRetrieved: boolean;
        authenticationValid: boolean;
        responseTime: number;
    };
    error?: string;
}
/**
 * Service for managing provider models and testing provider connections
 */
declare class ProviderModelsService extends FetchBaseApiClient {
    /**
     * Get available models for a specific provider
     * @param providerId The provider identifier (e.g., 'openai', 'anthropic')
     * @param options Optional parameters
     * @returns Promise resolving to provider models response
     */
    getProviderModels(providerId: string, options?: {
        forceRefresh?: boolean;
        virtualKey?: string;
    }): Promise<ProviderModel[]>;
    /**
     * Test connection to a provider and retrieve available models
     * @param request Provider connection test request
     * @returns Promise resolving to connection test response
     */
    testProviderConnection(request: ProviderConnectionTestRequest): Promise<ProviderConnectionTestResponse>;
    /**
     * Get cached provider models without making external API calls
     * @param providerId The provider identifier
     * @returns Promise resolving to cached provider models response
     */
    getCachedProviderModels(providerId: string): Promise<ProviderModel[]>;
    /**
     * Refresh provider models cache for a specific provider
     * @param providerId The provider identifier
     * @param virtualKey Optional virtual key for authentication
     * @returns Promise resolving to refreshed provider models
     */
    refreshProviderModels(providerId: string, virtualKey?: string): Promise<ProviderModel[]>;
    /**
     * Get all supported providers with their available model counts
     * @returns Promise resolving to provider summary information
     */
    getProviderSummary(): Promise<{
        providers: Array<{
            providerId: string;
            providerName: string;
            isAvailable: boolean;
            modelCount: number;
            lastUpdated?: string;
            capabilities: string[];
        }>;
    }>;
    private invalidateCache;
}

declare class ModelMappingService extends FetchBaseApiClient {
    create(request: CreateModelProviderMappingDto): Promise<ModelProviderMappingDto>;
    list(filters?: ModelMappingFilters): Promise<ModelProviderMappingDto[]>;
    getById(id: number): Promise<ModelProviderMappingDto>;
    getByModel(modelId: string): Promise<ModelProviderMappingDto[]>;
    update(id: number, request: UpdateModelProviderMappingDto): Promise<void>;
    deleteById(id: number): Promise<void>;
    getAvailableProviders(): Promise<string[]>;
    updatePriority(id: number, priority: number): Promise<void>;
    enableMapping(id: number): Promise<void>;
    disableMapping(id: number): Promise<void>;
    reorderMappings(_modelId: string, mappingIds: number[]): Promise<void>;
    bulkCreate(request: BulkMappingRequest): Promise<BulkMappingResponse>;
    importMappings(file: File | Blob, format: 'csv' | 'json'): Promise<BulkMappingResponse>;
    exportMappings(format: 'csv' | 'json'): Promise<Blob>;
    discoverProviderModels(providerName: string): Promise<DiscoveredModel[]>;
    discoverModelCapabilities(providerName: string, modelId: string): Promise<DiscoveredModel>;
    testCapability(modelAlias: string, capability: string): Promise<CapabilityTestResult>;
    getRoutingInfo(modelId: string): Promise<ModelRoutingInfo>;
    suggestOptimalMapping(modelId: string): Promise<ModelMappingSuggestion>;
    /**
     * Discover all available models across all configured providers
     * @returns Array of discovered models with their capabilities
     */
    discoverModels(): Promise<DiscoveredModel[]>;
    private invalidateCache;
}

declare class SettingsService extends FetchBaseApiClient {
    getGlobalSettings(filters?: SettingFilters): Promise<GlobalSettingDto[]>;
    getGlobalSetting(key: string): Promise<GlobalSettingDto>;
    createGlobalSetting(request: CreateGlobalSettingDto): Promise<GlobalSettingDto>;
    updateGlobalSetting(key: string, request: UpdateGlobalSettingDto): Promise<void>;
    deleteGlobalSetting(key: string): Promise<void>;
    getAudioConfigurations(): Promise<AudioConfigurationDto[]>;
    getAudioConfiguration(provider: string): Promise<AudioConfigurationDto>;
    createAudioConfiguration(request: CreateAudioConfigurationDto): Promise<AudioConfigurationDto>;
    updateAudioConfiguration(provider: string, request: UpdateAudioConfigurationDto): Promise<void>;
    deleteAudioConfiguration(provider: string): Promise<void>;
    getRouterConfiguration(): Promise<RouterConfigurationDto>;
    updateRouterConfiguration(request: UpdateRouterConfigurationDto): Promise<RouterConfigurationDto>;
    createRouterRule(rule: Omit<RouterRule, 'id'>): Promise<RouterRule>;
    updateRouterRule(id: number, rule: Partial<RouterRule>): Promise<RouterRule>;
    deleteRouterRule(id: number): Promise<void>;
    reorderRouterRules(ruleIds: number[]): Promise<RouterRule[]>;
    testRouterRule(rule: RouterRule): {
        success: boolean;
        message: string;
        details?: Record<string, unknown>;
    };
    getSetting(key: string): Promise<string>;
    setSetting(key: string, value: string, options?: {
        description?: string;
        dataType?: 'string' | 'number' | 'boolean' | 'json';
        category?: string;
        isSecret?: boolean;
    }): Promise<void>;
    getSettingsByCategory(category: string): Promise<GlobalSettingDto[]>;
    updateCategory(category: string, updates: Record<string, string>): Promise<void>;
    update(key: string, value: string): Promise<void>;
    set(key: string, value: string, options?: {
        description?: string;
        dataType?: 'string' | 'number' | 'boolean' | 'json';
        category?: string;
        isSecret?: boolean;
    }): Promise<void>;
    getSystemConfiguration(): Promise<SystemConfiguration>;
    exportSettings(_format: 'json' | 'env'): Promise<Blob>;
    importSettings(_file: File | Blob, _format: 'json' | 'env'): Promise<{
        imported: number;
        skipped: number;
        errors: string[];
    }>;
    validateConfiguration(): Promise<{
        isValid: boolean;
        errors: string[];
        warnings: string[];
    }>;
    private invalidateCache;
}

declare class IpFilterService extends FetchBaseApiClient {
    create(request: CreateIpFilterDto): Promise<IpFilterDto>;
    list(filters?: IpFilterFilters): Promise<IpFilterDto[]>;
    getById(id: number): Promise<IpFilterDto>;
    getEnabled(): Promise<IpFilterDto[]>;
    update(id: number, request: UpdateIpFilterDto): Promise<void>;
    deleteById(id: number): Promise<void>;
    getSettings(): Promise<IpFilterSettingsDto>;
    updateSettings(request: UpdateIpFilterSettingsDto): Promise<void>;
    checkIp(ipAddress: string): Promise<IpCheckResult>;
    search(query: string): Promise<IpFilterDto[]>;
    enableFilter(id: number): Promise<void>;
    disableFilter(id: number): Promise<void>;
    createAllowFilter(name: string, ipAddressOrCidr: string, description?: string): Promise<IpFilterDto>;
    createDenyFilter(name: string, ipAddressOrCidr: string, description?: string): Promise<IpFilterDto>;
    getFiltersByType(filterType: FilterType): Promise<IpFilterDto[]>;
    bulkCreate(rules: CreateIpFilterDto[]): Promise<BulkOperationResult>;
    bulkUpdate(operation: 'enable' | 'disable', ruleIds: string[]): Promise<IpFilterDto[]>;
    bulkDelete(ruleIds: string[]): Promise<BulkOperationResult>;
    createTemporary(rule: CreateTemporaryIpFilterDto): Promise<IpFilterDto>;
    getExpiring(withinHours: number): Promise<IpFilterDto[]>;
    import(rules: IpFilterImport[]): Promise<IpFilterImportResult>;
    export(format: 'json' | 'csv'): Promise<Blob>;
    getBlockedRequestStats(params: {
        startDate?: string;
        endDate?: string;
        groupBy?: 'rule' | 'country' | 'hour';
    }): Promise<BlockedRequestStats>;
    getStatistics(): Promise<IpFilterStatistics>;
    importFilters(_file: File | Blob, _format: 'csv' | 'json'): Promise<BulkIpFilterResponse>;
    exportFilters(_format: 'csv' | 'json', _filterType?: FilterType): Promise<Blob>;
    validateCidr(_cidrRange: string): Promise<IpFilterValidationResult>;
    testRules(_ipAddress: string, _proposedRules?: CreateIpFilterDto[]): Promise<{
        currentResult: IpCheckResult;
        proposedResult?: IpCheckResult;
        changes?: string[];
    }>;
    private invalidateCache;
}

declare class ModelCostService extends FetchBaseApiClient {
    create(modelCost: CreateModelCostDto): Promise<ModelCost>;
    list(params?: {
        page?: number;
        pageSize?: number;
        provider?: string;
        isActive?: boolean;
    }): Promise<PagedResult<ModelCost>>;
    listLegacy(filters?: ModelCostFilters): Promise<ModelCostDto[]>;
    getById(id: number): Promise<ModelCost>;
    getByModel(modelId: string): Promise<ModelCostDto[]>;
    getByProvider(providerName: string): Promise<ModelCost[]>;
    update(id: number, modelCost: UpdateModelCostDto): Promise<ModelCost>;
    deleteById(id: number): Promise<void>;
    calculateCost(modelId: string, inputTokens: number, outputTokens: number): Promise<ModelCostCalculation>;
    getCurrentCost(modelId: string): Promise<ModelCostDto | null>;
    updateCosts(models: string[], inputCost: number, outputCost: number): Promise<void>;
    import(modelCosts: CreateModelCostDto[]): Promise<ImportResult>;
    bulkUpdate(updates: Array<{
        id: number;
        changes: Partial<UpdateModelCostDto>;
    }>): Promise<ModelCost[]>;
    getOverview(params: {
        startDate?: string;
        endDate?: string;
        groupBy?: 'provider' | 'model';
    }): Promise<ModelCostOverview[]>;
    getCostTrends(params: {
        modelId?: string;
        providerId?: string;
        days?: number;
    }): Promise<CostTrend[]>;
    bulkUpdateLegacy(_request: BulkModelCostUpdate): Promise<{
        updated: ModelCostDto[];
        failed: {
            modelId: string;
            error: string;
        }[];
    }>;
    getHistory(_modelId: string): Promise<ModelCostHistory>;
    estimateCosts(_scenarios: {
        name: string;
        inputTokens: number;
        outputTokens: number;
    }[], _models: string[]): Promise<CostEstimate>;
    compareCosts(_baseModel: string, _comparisonModels: string[], _inputTokens: number, _outputTokens: number): Promise<ModelCostComparison>;
    importCosts(_file: File | Blob, _format: 'csv' | 'json'): Promise<{
        imported: number;
        updated: number;
        failed: {
            row: number;
            error: string;
        }[];
    }>;
    exportCosts(_format: 'csv' | 'json', _activeOnly?: boolean): Promise<Blob>;
    private invalidateCache;
}

declare class AnalyticsService extends FetchBaseApiClient {
    getRequestLogs(filters?: RequestLogFilters): Promise<PaginatedResponse<RequestLogDto>>;
    getRequestLog(id: string): Promise<RequestLogDto>;
    searchLogs(query: string, filters?: RequestLogFilters): Promise<RequestLogDto[]>;
    getUsageMetrics(dateRange: DateRange): Promise<UsageMetricsDto>;
    getModelUsage(modelId: string, dateRange: DateRange): Promise<ModelUsageDto>;
    getKeyUsage(keyId: number, dateRange: DateRange): Promise<KeyUsageDto>;
    exportRequestLogs(params: ExportRequestLogsParams): Promise<ExportResult>;
    getRequestLogStatistics(logs: RequestLog[]): RequestLogStatistics;
    getExportStatus(exportId: string): Promise<ExportStatus>;
    downloadExport(exportId: string): Promise<Blob>;
    getDetailedCostBreakdown(_filters: AnalyticsFilters): Promise<never>;
    predictFutureCosts(_basePeriod: DateRange, _forecastDays: number): Promise<CostForecastDto>;
    export(filters: AnalyticsFilters, format?: 'csv' | 'excel' | 'json'): Promise<Blob>;
    exportAnalytics(filters: AnalyticsFilters, format: 'csv' | 'excel' | 'json'): Promise<Blob>;
    detectAnomalies(_dateRange: DateRange): Promise<AnomalyDto[]>;
    streamRequestLogs(_filters?: RequestLogFilters, _onMessage?: (log: RequestLogDto) => void, _onError?: (error: Error) => void): never;
    generateReport(_type: 'cost' | 'usage' | 'performance', _dateRange: DateRange, _options?: AnalyticsOptions): never;
}

declare class SystemService extends FetchBaseApiClient {
    getSystemInfo(): Promise<SystemInfoDto>;
    getHealth(): Promise<HealthStatusDto>;
    listBackups(): Promise<BackupDto[]>;
    createBackup(request?: CreateBackupRequest): Promise<BackupDto>;
    downloadBackup(backupId: string): Promise<Blob>;
    deleteBackup(backupId: string): Promise<void>;
    restoreBackup(request: RestoreBackupRequest): Promise<BackupRestoreResult>;
    getNotifications(unreadOnly?: boolean): Promise<NotificationDto[]>;
    getNotification(id: number): Promise<NotificationDto>;
    createNotification(request: CreateNotificationDto): Promise<NotificationDto>;
    markNotificationRead(id: number): Promise<void>;
    deleteNotification(id: number): Promise<void>;
    clearNotifications(): Promise<void>;
    getMaintenanceTasks(): Promise<MaintenanceTaskDto[]>;
    runMaintenanceTask(request: RunMaintenanceTaskRequest): Promise<MaintenanceTaskResult>;
    getAuditLogs(_filters?: AuditLogFilters): Promise<PaginatedResponse<AuditLogDto>>;
    scheduledBackup(_schedule: string, _config: CreateBackupRequest): Promise<{
        id: string;
        schedule: string;
        nextRun: string;
        config: CreateBackupRequest;
    }>;
    getScheduledBackups(): Promise<Array<{
        id: string;
        schedule: string;
        nextRun: string;
        lastRun?: string;
        config: CreateBackupRequest;
    }>>;
    exportAuditLogs(_filters: AuditLogFilters, _format: 'csv' | 'json'): Promise<Blob>;
    getDiagnostics(): Promise<{
        timestamp: string;
        checks: DiagnosticChecks;
        recommendations: string[];
        issues: Array<{
            severity: 'low' | 'medium' | 'high';
            component: string;
            message: string;
            solution?: string;
        }>;
    }>;
    getFeatureAvailability(): Promise<FeatureAvailability>;
    /**
     * Gets or creates the special WebUI virtual key.
     * This key is stored unencrypted in GlobalSettings for WebUI/TUI access.
     * @returns The actual (unhashed) virtual key value
     */
    getWebUIVirtualKey(): Promise<string>;
    private invalidateCache;
}

/**
 * Service for accessing Admin API metrics and performance data
 */
declare class MetricsService extends FetchBaseApiClient {
    /**
     * Gets database connection pool metrics for the Admin API
     *
     * @returns Promise<DatabasePoolMetricsResponse> Database pool statistics and health information
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const poolMetrics = await adminClient.metrics.getDatabasePoolMetrics();
     * console.warn(`Active connections: ${poolMetrics.metrics.activeConnections}`);
     * console.warn(`Pool efficiency: ${poolMetrics.metrics.poolEfficiency}%`);
     * console.warn(`Pool is healthy: ${poolMetrics.isHealthy}`);
     * ```
     */
    getDatabasePoolMetrics(): Promise<DatabasePoolMetricsResponse>;
    /**
     * Gets comprehensive Admin API metrics including database, memory, CPU, and request statistics
     *
     * @returns Promise<AdminMetricsResponse> Complete system metrics and health information
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const systemMetrics = await adminClient.metrics.getAllMetrics();
     *
     * console.warn('Database Pool:');
     * console.warn(`  Active connections: ${systemMetrics.metrics.databasePool.activeConnections}`);
     * console.warn(`  Pool efficiency: ${systemMetrics.metrics.databasePool.poolEfficiency}%`);
     *
     * console.warn('Memory Usage:');
     * console.warn(`  Working set: ${Math.round(systemMetrics.metrics.memory.workingSet / 1024 / 1024)} MB`);
     * console.warn(`  GC heap size: ${Math.round(systemMetrics.metrics.memory.gcHeapSize / 1024 / 1024)} MB`);
     *
     * console.warn('CPU Usage:');
     * console.warn(`  CPU usage: ${systemMetrics.metrics.cpu.usage}%`);
     * console.warn(`  Thread count: ${systemMetrics.metrics.cpu.threadCount}`);
     *
     * console.warn('Request Statistics:');
     * console.warn(`  Total requests: ${systemMetrics.metrics.requests.totalRequests}`);
     * console.warn(`  Requests per second: ${systemMetrics.metrics.requests.requestsPerSecond}`);
     * console.warn(`  Average response time: ${systemMetrics.metrics.requests.averageResponseTime}ms`);
     * console.warn(`  Error rate: ${systemMetrics.metrics.requests.errorRate}%`);
     *
     * console.warn(`System is healthy: ${systemMetrics.isHealthy}`);
     * console.warn(`Uptime: ${Math.round(systemMetrics.uptime / 1000 / 60)} minutes`);
     * ```
     */
    getAllMetrics(): Promise<AdminMetricsResponse>;
    /**
     * Checks if the database connection pool is healthy
     *
     * @returns Promise<boolean> True if the pool is healthy, false otherwise
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const isHealthy = await adminClient.metrics.isDatabasePoolHealthy();
     * if (!isHealthy) {
     *   console.warn('Database pool is unhealthy - check connection limits');
     * }
     * ```
     */
    isDatabasePoolHealthy(): Promise<boolean>;
    /**
     * Checks if the overall Admin API system is healthy
     *
     * @returns Promise<boolean> True if the system is healthy, false otherwise
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const isHealthy = await adminClient.metrics.isSystemHealthy();
     * if (!isHealthy) {
     *   console.warn('Admin API system is unhealthy - check logs');
     * }
     * ```
     */
    isSystemHealthy(): Promise<boolean>;
    /**
     * Gets database connection pool efficiency percentage
     *
     * @returns Promise<number> Pool efficiency as a percentage (0-100)
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const efficiency = await adminClient.metrics.getDatabasePoolEfficiency();
     * if (efficiency < 80) {
     *   console.warn(`Database pool efficiency is low: ${efficiency}%`);
     * }
     * ```
     */
    getDatabasePoolEfficiency(): Promise<number>;
    /**
     * Gets current memory usage information
     *
     * @returns Promise<{workingSetMB: number, gcHeapSizeMB: number, usage: string}> Memory usage summary
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const memoryInfo = await adminClient.metrics.getMemoryUsage();
     * console.warn(`Working set: ${memoryInfo.workingSetMB} MB`);
     * console.warn(`GC heap: ${memoryInfo.gcHeapSizeMB} MB`);
     * console.warn(`Usage summary: ${memoryInfo.usage}`);
     * ```
     */
    getMemoryUsage(): Promise<{
        workingSetMB: number;
        gcHeapSizeMB: number;
        usage: string;
    }>;
    /**
     * Gets current request processing statistics
     *
     * @returns Promise<{rps: number, avgResponseTime: number, errorRate: number, activeRequests: number}> Request statistics summary
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const requestStats = await adminClient.metrics.getRequestStatistics();
     * console.warn(`Requests per second: ${requestStats.rps}`);
     * console.warn(`Average response time: ${requestStats.avgResponseTime}ms`);
     * console.warn(`Error rate: ${requestStats.errorRate}%`);
     * console.warn(`Active requests: ${requestStats.activeRequests}`);
     * ```
     */
    getRequestStatistics(): Promise<{
        rps: number;
        avgResponseTime: number;
        errorRate: number;
        activeRequests: number;
    }>;
    /**
     * Gets system uptime information
     *
     * @returns Promise<{uptimeMs: number, uptimeMinutes: number, uptimeHours: number, uptimeString: string}> Uptime information
     * @throws {ConduitAdminError} When the API request fails
     *
     * @example
     * ```typescript
     * const uptime = await adminClient.metrics.getSystemUptime();
     * console.warn(`System uptime: ${uptime.uptimeString}`);
     * console.warn(`Uptime in hours: ${uptime.uptimeHours}`);
     * ```
     */
    getSystemUptime(): Promise<{
        uptimeMs: number;
        uptimeMinutes: number;
        uptimeHours: number;
        uptimeString: string;
    }>;
}

/**
 * Service for managing provider health monitoring and status
 */
declare class ProviderHealthService extends FetchBaseApiClient {
    constructor(config: ApiClientConfig);
    /**
     * Gets the health configuration for a specific provider
     *
     * @param providerName - The name of the provider
     * @returns Promise<ProviderHealthConfigurationDto> The provider health configuration
     */
    getProviderHealthConfiguration(providerName: string): Promise<ProviderHealthConfigurationDto>;
    /**
     * Creates a new provider health configuration
     *
     * @param request - The configuration request
     * @returns Promise<ProviderHealthConfigurationDto> The created configuration
     */
    createProviderHealthConfiguration(request: CreateProviderHealthConfigurationDto): Promise<ProviderHealthConfigurationDto>;
    /**
     * Updates an existing provider health configuration
     *
     * @param providerName - The name of the provider
     * @param request - The update request
     * @returns Promise<ProviderHealthConfigurationDto> The updated configuration
     */
    updateProviderHealthConfiguration(providerName: string, request: UpdateProviderHealthConfigurationDto): Promise<ProviderHealthConfigurationDto>;
    /**
     * Gets health records for a specific provider
     *
     * @param providerName - The name of the provider
     * @param filters - Optional filters for the query
     * @returns Promise<PagedResponse<ProviderHealthRecordDto>> A paginated list of health records
     */
    getProviderHealthRecords(providerName: string, filters?: ProviderHealthFilters): Promise<PagedResponse<ProviderHealthRecordDto>>;
    /**
     * Gets all health records across all providers
     *
     * @param filters - Optional filters for the query
     * @returns Promise<PagedResponse<ProviderHealthRecordDto>> A paginated list of health records
     */
    getAllHealthRecords(filters?: ProviderHealthFilters): Promise<PagedResponse<ProviderHealthRecordDto>>;
    /**
     * Gets the current health status for all providers
     *
     * @returns Promise<ProviderHealthSummaryDto> A summary of all provider health statuses
     */
    getHealthSummary(): Promise<ProviderHealthSummaryDto>;
    /**
     * Gets the health status for a specific provider
     *
     * @param providerName - The name of the provider
     * @returns Promise<ProviderHealthStatusDto> The provider health status
     */
    getProviderHealthStatus(providerName: string): Promise<ProviderHealthStatusDto>;
    /**
     * Gets health statistics for all providers
     *
     * @param periodHours - The time period in hours for statistics calculation
     * @returns Promise<ProviderHealthStatisticsDto> Overall provider health statistics
     */
    getHealthStatistics(periodHours?: number): Promise<ProviderHealthStatisticsDto>;
    /**
     * Gets simple status information for a provider
     *
     * @param providerName - The name of the provider
     * @returns Promise<ProviderStatus> Simple provider status
     */
    getProviderStatus(providerName: string): Promise<ProviderStatus>;
    /**
     * Triggers a manual health check for a specific provider
     *
     * @param providerName - The name of the provider
     * @returns Promise<ProviderHealthRecordDto> The health check result
     */
    triggerHealthCheck(providerName: string): Promise<ProviderHealthRecordDto>;
    /**
     * Deletes a provider health configuration
     *
     * @param providerName - The name of the provider
     */
    deleteProviderHealthConfiguration(providerName: string): Promise<void>;
    /**
     * Checks if a provider is currently healthy
     *
     * @param providerName - The name of the provider
     * @returns Promise<boolean> True if the provider is healthy, false otherwise
     */
    isProviderHealthy(providerName: string): Promise<boolean>;
    /**
     * Gets all unhealthy providers
     *
     * @returns Promise<ProviderHealthStatusDto[]> List of unhealthy providers
     */
    getUnhealthyProviders(): Promise<ProviderHealthStatusDto[]>;
    /**
     * Gets the overall system health percentage
     *
     * @returns Promise<number> The percentage of healthy providers (0-100)
     */
    getOverallHealthPercentage(): Promise<number>;
    /**
     * Gets providers by health status
     *
     * @param isHealthy - Whether to get healthy or unhealthy providers
     * @returns Promise<ProviderHealthStatusDto[]> List of providers with the specified health status
     */
    getProvidersByHealthStatus(isHealthy: boolean): Promise<ProviderHealthStatusDto[]>;
    /**
     * Gets health trend for a provider over time
     *
     * @param providerName - The name of the provider
     * @param startDate - Start date for the trend analysis
     * @param endDate - End date for the trend analysis
     * @returns Promise<ProviderHealthRecordDto[]> Health records for trend analysis
     */
    getProviderHealthTrend(providerName: string, startDate: Date, endDate: Date): Promise<ProviderHealthRecordDto[]>;
    /**
     * Gets health statistics summary for multiple providers
     *
     * @param providerNames - List of provider names to get statistics for
     * @returns Promise<Record<string, ProviderHealthStatusDto>> Statistics by provider name
     */
    getMultipleProviderStatistics(providerNames: string[]): Promise<Record<string, ProviderHealthStatusDto>>;
}

/**
 * Service for managing notifications through the Admin API
 */
declare class NotificationsService extends FetchBaseApiClient {
    private readonly baseEndpoint;
    constructor(config: ApiClientConfig);
    /**
     * Retrieves all notifications ordered by creation date (descending)
     *
     * @returns Promise<NotificationDto[]> A list of all notifications
     */
    getAllNotifications(): Promise<NotificationDto[]>;
    /**
     * Retrieves only unread notifications
     *
     * @returns Promise<NotificationDto[]> A list of unread notifications
     */
    getUnreadNotifications(): Promise<NotificationDto[]>;
    /**
     * Retrieves a specific notification by ID
     *
     * @param notificationId - The ID of the notification to retrieve
     * @returns Promise<NotificationDto | null> The notification if found, null otherwise
     */
    getNotificationById(notificationId: number): Promise<NotificationDto | null>;
    /**
     * Creates a new notification
     *
     * @param request - The notification creation request
     * @returns Promise<NotificationDto> The created notification
     */
    createNotification(request: CreateNotificationDto): Promise<NotificationDto>;
    /**
     * Updates an existing notification
     *
     * @param notificationId - The ID of the notification to update
     * @param request - The notification update request
     * @returns Promise<NotificationDto> The updated notification
     */
    updateNotification(notificationId: number, request: UpdateNotificationDto): Promise<NotificationDto>;
    /**
     * Marks a specific notification as read
     *
     * @param notificationId - The ID of the notification to mark as read
     */
    markAsRead(notificationId: number): Promise<void>;
    /**
     * Marks all notifications as read
     *
     * @returns Promise<number> The number of notifications that were marked as read
     */
    markAllAsRead(): Promise<number>;
    /**
     * Deletes a notification
     *
     * @param notificationId - The ID of the notification to delete
     */
    deleteNotification(notificationId: number): Promise<void>;
    /**
     * Gets notifications by type
     *
     * @param type - The notification type to filter by
     * @returns Promise<NotificationDto[]> Notifications of the specified type
     */
    getNotificationsByType(type: NotificationType): Promise<NotificationDto[]>;
    /**
     * Gets notifications by severity
     *
     * @param severity - The notification severity to filter by
     * @returns Promise<NotificationDto[]> Notifications of the specified severity
     */
    getNotificationsBySeverity(severity: NotificationSeverity): Promise<NotificationDto[]>;
    /**
     * Gets notifications for a specific virtual key
     *
     * @param virtualKeyId - The virtual key ID to filter by
     * @returns Promise<NotificationDto[]> Notifications associated with the specified virtual key
     */
    getNotificationsForVirtualKey(virtualKeyId: number): Promise<NotificationDto[]>;
    /**
     * Gets notifications created within a specific date range
     *
     * @param startDate - The start date (inclusive)
     * @param endDate - The end date (inclusive)
     * @returns Promise<NotificationDto[]> Notifications created within the specified date range
     */
    getNotificationsByDateRange(startDate: Date, endDate: Date): Promise<NotificationDto[]>;
    /**
     * Gets notification statistics including counts by type, severity, and read status
     *
     * @returns Promise<NotificationStatistics> Notification statistics summary
     */
    getNotificationStatistics(): Promise<NotificationStatistics>;
    /**
     * Gets the count of unread notifications
     *
     * @returns Promise<number> The number of unread notifications
     */
    getUnreadCount(): Promise<number>;
    /**
     * Checks if there are any unread notifications
     *
     * @returns Promise<boolean> True if there are unread notifications, false otherwise
     */
    hasUnreadNotifications(): Promise<boolean>;
    /**
     * Marks multiple notifications as read by their IDs
     *
     * @param notificationIds - The IDs of notifications to mark as read
     * @returns Promise<NotificationBulkResponse> The bulk operation response
     */
    markMultipleAsRead(notificationIds: number[]): Promise<NotificationBulkResponse>;
    /**
     * Deletes multiple notifications by their IDs
     *
     * @param notificationIds - The IDs of notifications to delete
     * @returns Promise<NotificationBulkResponse> The bulk operation response
     */
    deleteMultiple(notificationIds: number[]): Promise<NotificationBulkResponse>;
    /**
     * Gets a filtered list of notifications based on the provided filters
     *
     * @param filters - The filters to apply
     * @returns Promise<NotificationDto[]> Filtered list of notifications
     */
    getFilteredNotifications(filters: NotificationFilters): Promise<NotificationDto[]>;
    /**
     * Gets a summary of notification data with key metrics
     *
     * @returns Promise<NotificationSummary> Notification summary object
     */
    getNotificationSummary(): Promise<NotificationSummary>;
}

/**
 * Type-safe SignalR method arguments and return types
 */
/**
 * Valid types that can be sent through SignalR
 */
type SignalRValue = string | number | boolean | null | undefined | SignalRObject | SignalRArray;
interface SignalRObject {
    [key: string]: SignalRValue;
}
type SignalRArray = Array<SignalRValue>;
/**
 * Type for SignalR method arguments
 */
type SignalRArgs = SignalRValue[];

/**
 * Base class for Admin SDK SignalR hub connections.
 * Extends the common base class with Admin-specific authentication.
 */
declare abstract class BaseSignalRConnection extends BaseSignalRConnection$1 {
    protected readonly masterKey: string;
    constructor(baseUrl: string, masterKey: string);
    /**
     * Starts the SignalR connection.
     */
    start(): Promise<void>;
    /**
     * Stops the SignalR connection.
     */
    stop(): Promise<void>;
    /**
     * Waits for the connection to be established.
     */
    waitForConnection(timeoutMs?: number): Promise<boolean>;
    /**
     * Invokes a hub method with retry logic and type-safe arguments.
     */
    protected invokeTyped(methodName: string, ...args: SignalRArgs): Promise<void>;
    /**
     * Invokes a hub method with return value and retry logic.
     */
    protected invokeWithResult<T extends SignalRValue>(methodName: string, ...args: SignalRArgs): Promise<T>;
}

/**
 * SignalR client for navigation state updates
 */
declare class NavigationStateHubClient extends BaseSignalRConnection implements INavigationStateHubClient {
    protected get hubPath(): string;
    private navigationStateCallbacks;
    private modelDiscoveredCallbacks;
    private providerHealthCallbacks;
    /**
     * Configures event handlers for the navigation state hub
     */
    protected configureHubHandlers(connection: HubConnection): void;
    /**
     * Subscribe to navigation state updates
     */
    onNavigationStateUpdate(callback: (event: NavigationStateUpdateEvent) => void): void;
    /**
     * Subscribe to model discovered events
     */
    onModelDiscovered(callback: (event: ModelDiscoveredEvent) => void): void;
    /**
     * Subscribe to provider health changes
     */
    onProviderHealthChange(callback: (event: ProviderHealthChangeEvent) => void): void;
    /**
     * Subscribe to updates for a specific group
     */
    subscribeToUpdates(groupName?: string): Promise<void>;
    /**
     * Unsubscribe from updates for a specific group
     */
    unsubscribeFromUpdates(groupName?: string): Promise<void>;
    /**
     * Clear all callbacks
     */
    clearCallbacks(): void;
    /**
     * Remove a specific callback
     */
    removeNavigationStateCallback(callback: (event: NavigationStateUpdateEvent) => void): void;
    /**
     * Remove a specific model discovered callback
     */
    removeModelDiscoveredCallback(callback: (event: ModelDiscoveredEvent) => void): void;
    /**
     * Remove a specific provider health callback
     */
    removeProviderHealthCallback(callback: (event: ProviderHealthChangeEvent) => void): void;
    /**
     * Get the number of active callbacks
     */
    getActiveCallbackCount(): number;
    /**
     * Dispose the client
     */
    dispose(): Promise<void>;
}

/**
 * SignalR client for admin notifications
 */
declare class AdminNotificationHubClient extends BaseSignalRConnection implements IAdminNotificationHubClient {
    protected get hubPath(): string;
    private virtualKeyCallbacks;
    private configChangeCallbacks;
    private adminNotificationCallbacks;
    private initialProviderHealthCallbacks;
    /**
     * Configures event handlers for the admin notification hub
     */
    protected configureHubHandlers(connection: HubConnection): void;
    /**
     * Subscribe to virtual key events
     */
    onVirtualKeyEvent(callback: (event: VirtualKeyEvent) => void): void;
    /**
     * Subscribe to configuration changes
     */
    onConfigurationChange(callback: (event: ConfigurationChangeEvent) => void): void;
    /**
     * Subscribe to admin notifications
     */
    onAdminNotification(callback: (event: AdminNotificationEvent) => void): void;
    /**
     * Subscribe to initial provider health updates
     */
    onInitialProviderHealth(callback: (data: unknown) => void): void;
    /**
     * Subscribe to notifications for a specific virtual key
     */
    subscribeToVirtualKey(virtualKeyId: number): Promise<void>;
    /**
     * Unsubscribe from notifications for a specific virtual key
     */
    unsubscribeFromVirtualKey(virtualKeyId: number): Promise<void>;
    /**
     * Subscribe to notifications for a specific provider
     */
    subscribeToProvider(providerName: string): Promise<void>;
    /**
     * Unsubscribe from notifications for a specific provider
     */
    unsubscribeFromProvider(providerName: string): Promise<void>;
    /**
     * Request a refresh of provider health status
     */
    refreshProviderHealth(): Promise<void>;
    /**
     * Acknowledge a notification as read
     */
    acknowledgeNotification(notificationId: string): Promise<void>;
    /**
     * Clear all callbacks
     */
    clearCallbacks(): void;
    /**
     * Remove a specific virtual key callback
     */
    removeVirtualKeyCallback(callback: (event: VirtualKeyEvent) => void): void;
    /**
     * Remove a specific configuration change callback
     */
    removeConfigChangeCallback(callback: (event: ConfigurationChangeEvent) => void): void;
    /**
     * Remove a specific admin notification callback
     */
    removeAdminNotificationCallback(callback: (event: AdminNotificationEvent) => void): void;
    /**
     * Get the number of active callbacks
     */
    getActiveCallbackCount(): number;
    /**
     * Dispose the client
     */
    dispose(): Promise<void>;
}

/**
 * Service for managing SignalR connections to the Admin API
 */
declare class SignalRService {
    private readonly baseUrl;
    private readonly masterKey;
    private navigationStateHub?;
    private adminNotificationHub?;
    private disposed;
    constructor(baseUrl: string, masterKey: string, _options?: SignalRConnectionOptions);
    /**
     * Gets or creates the navigation state hub connection
     */
    getOrCreateNavigationStateHub(): NavigationStateHubClient;
    /**
     * Gets or creates the admin notification hub connection
     */
    getOrCreateAdminNotificationHub(): AdminNotificationHubClient;
    /**
     * Gets a connection by type
     */
    getOrCreateConnection(type: 'navigation' | 'notifications'): NavigationStateHubClient | AdminNotificationHubClient;
    /**
     * Connects all hubs
     */
    connectAll(): Promise<void>;
    /**
     * Disconnects all hubs
     */
    disconnectAll(): Promise<void>;
    /**
     * Checks if any hub is connected
     */
    isAnyConnected(): boolean;
    /**
     * Gets the state of all connections
     */
    getConnectionStates(): Record<string, HubConnectionState>;
    /**
     * Disposes all SignalR connections
     */
    dispose(): Promise<void>;
}

/**
 * Service for managing real-time notifications through SignalR
 */
declare class RealtimeNotificationsService implements IRealtimeNotificationService {
    private signalRService;
    private subscriptions;
    private navigationStateHub?;
    private adminNotificationHub?;
    private connectionStateCallbacks;
    constructor(signalRService: SignalRService);
    /**
     * Subscribe to navigation state updates
     */
    onNavigationStateUpdate(callback: NavigationStateUpdateCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to model discovered events
     */
    onModelDiscovered(callback: ModelDiscoveredCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to provider health changes
     */
    onProviderHealthChange(callback: ProviderHealthChangeCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to virtual key events
     */
    onVirtualKeyEvent(callback: VirtualKeyEventCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to configuration changes
     */
    onConfigurationChange(callback: ConfigurationChangeCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Subscribe to admin notifications
     */
    onAdminNotification(callback: AdminNotificationCallback, options?: AdminNotificationOptions): Promise<NotificationSubscription>;
    /**
     * Unsubscribe from all notifications
     */
    unsubscribeAll(): Promise<void>;
    /**
     * Get all active subscriptions
     */
    getActiveSubscriptions(): NotificationSubscription[];
    /**
     * Connect to SignalR hubs
     */
    connect(): Promise<void>;
    /**
     * Disconnect from SignalR hubs
     */
    disconnect(): Promise<void>;
    /**
     * Check if connected to SignalR hubs
     */
    isConnected(): boolean;
    /**
     * Acknowledge an admin notification
     */
    acknowledgeNotification(notificationId: string): Promise<void>;
    private unsubscribe;
    private generateSubscriptionId;
}

/**
 * Service for managing security events, threat detection, and compliance
 */
declare class SecurityService extends FetchBaseApiClient {
    /**
     * Get security events with optional filters
     * @param params - Filtering and pagination parameters
     * @returns Paged result of security events
     */
    getEvents(params?: SecurityEventFilters): Promise<PagedResult<SecurityEvent>>;
    /**
     * Report a new security event
     * @param event - Security event to report
     * @returns Created security event
     */
    reportEvent(event: CreateSecurityEventDto): Promise<SecurityEvent>;
    /**
     * Export security events in specified format
     * @param format - Export format (json or csv)
     * @param filters - Optional filters for events to export
     * @returns Blob containing exported data
     */
    exportEvents(format: 'json' | 'csv', filters?: SecurityEventFilters): Promise<Blob>;
    /**
     * Get detected threats with optional filters
     * @param params - Filtering and pagination parameters
     * @returns Paged result of detected threats
     */
    getThreats(params?: ThreatFilters): Promise<PagedResult<ThreatDetection>>;
    /**
     * Update the status of a detected threat
     * @param threatId - ID of the threat to update
     * @param action - Action to take on the threat
     * @returns Updated threat detection
     */
    updateThreatStatus(threatId: string, action: ThreatAction): Promise<ThreatDetection>;
    /**
     * Get threat analytics and statistics
     * @returns Threat analytics data
     */
    getThreatAnalytics(): Promise<ThreatAnalytics>;
    /**
     * Get compliance metrics
     * @returns Current compliance metrics
     */
    getComplianceMetrics(): Promise<ComplianceMetrics>;
    /**
     * Get compliance report in specified format
     * @param format - Export format (json or pdf)
     * @returns Blob containing compliance report
     */
    getComplianceReport(format: 'json' | 'pdf'): Promise<Blob>;
    /**
     * Invalidate security-related cache entries
     */
    private invalidateSecurityCache;
}

/**
 * Enhanced configuration service with routing and caching management
 */
declare class ConfigurationService extends SettingsService {
    /**
     * Routing configuration management
     */
    routing: {
        /**
         * Get current routing configuration
         */
        get: () => Promise<RoutingConfiguration>;
        /**
         * Update routing configuration
         */
        update: (config: UpdateRoutingConfigDto) => Promise<RoutingConfiguration>;
        /**
         * Test routing configuration
         */
        testConfiguration: (config: RoutingConfiguration) => Promise<TestResult>;
        /**
         * Get load balancer health status
         */
        getLoadBalancerHealth: () => Promise<LoadBalancerHealth[]>;
    };
    /**
     * Caching configuration management
     */
    caching: {
        /**
         * Get current caching configuration
         */
        get: () => Promise<CachingConfiguration>;
        /**
         * Update caching configuration
         */
        update: (config: UpdateCachingConfigDto) => Promise<CachingConfiguration>;
        /**
         * Get all cache policies
         */
        getPolicies: () => Promise<CachePolicy[]>;
        /**
         * Create a new cache policy
         */
        createPolicy: (policy: CreateCachePolicyDto) => Promise<CachePolicy>;
        /**
         * Update an existing cache policy
         */
        updatePolicy: (id: string, policy: UpdateCachePolicyDto) => Promise<CachePolicy>;
        /**
         * Delete a cache policy
         */
        deletePolicy: (id: string) => Promise<void>;
        /**
         * Get all cache regions
         */
        getRegions: () => Promise<CacheRegion[]>;
        /**
         * Clear cache for a specific region
         */
        clearCache: (regionId: string) => Promise<ClearCacheResult>;
        /**
         * Get cache statistics
         */
        getCacheStatistics: () => Promise<CacheStatistics>;
    };
    /**
     * Extended routing configuration management
     */
    extendedRouting: {
        /**
         * Get extended routing configuration
         */
        get: () => Promise<RoutingConfigDto>;
        /**
         * Update extended routing configuration
         */
        update: (config: UpdateRoutingConfigDto$1) => Promise<RoutingConfigDto>;
        /**
         * Get all routing rules
         */
        getRules: () => Promise<RoutingRule[]>;
        /**
         * Create a new routing rule
         */
        createRule: (rule: CreateRoutingRuleDto) => Promise<RoutingRule>;
        /**
         * Update an existing routing rule
         */
        updateRule: (id: string, rule: UpdateRoutingRuleDto) => Promise<RoutingRule>;
        /**
         * Delete a routing rule
         */
        deleteRule: (id: string) => Promise<void>;
        /**
         * Bulk update routing rules
         */
        bulkUpdateRules: (rules: RoutingRule[]) => Promise<RoutingRule[]>;
        /**
         * Get extended load balancer health
         */
        getLoadBalancerHealthExtended: () => Promise<LoadBalancerHealthDto>;
    };
    /**
     * Real-time subscription support (to be implemented with SignalR)
     */
    subscriptions: {
        /**
         * Subscribe to routing status updates
         * @returns Unsubscribe function
         * @todo Implement with SignalR - will accept callback parameter when ready
         */
        subscribeToRoutingStatus: () => (() => void);
        /**
         * Subscribe to health status updates
         * @returns Unsubscribe function
         * @todo Implement with SignalR - will accept callback parameter when ready
         */
        subscribeToHealthStatus: () => (() => void);
    };
    /**
     * Invalidate configuration-related cache entries
     */
    private invalidateConfigurationCache;
}

/**
 * Check if a model mapping supports a specific capability
 * @param mapping The model mapping to check
 * @param capability The capability to check for
 * @returns True if the mapping supports the capability
 */
declare function hasCapability(mapping: ModelProviderMappingDto, capability: ModelCapability): boolean;
/**
 * Get all capabilities supported by a model mapping
 * @param mapping The model mapping to analyze
 * @returns Array of supported capabilities
 */
declare function getCapabilities(mapping: ModelProviderMappingDto): ModelCapability[];
/**
 * Filter model mappings by capability
 * @param mappings Array of model mappings to filter
 * @param capability The capability to filter by
 * @returns Filtered array of mappings that support the capability
 */
declare function filterByCapability(mappings: ModelProviderMappingDto[], capability: ModelCapability): ModelProviderMappingDto[];
/**
 * Find the default mapping for a specific capability
 * @param mappings Array of model mappings to search
 * @param capability The capability to find default for
 * @returns The default mapping for the capability, or undefined if none found
 */
declare function findDefaultMapping(mappings: ModelProviderMappingDto[], capability: ModelCapability): ModelProviderMappingDto | undefined;
/**
 * Get the best mapping for a capability (default first, then highest priority enabled)
 * @param mappings Array of model mappings to search
 * @param capability The capability to find mapping for
 * @returns The best mapping for the capability, or undefined if none found
 */
declare function getBestMapping(mappings: ModelProviderMappingDto[], capability: ModelCapability): ModelProviderMappingDto | undefined;
/**
 * Validate that a model mapping has all required fields for its capabilities
 * @param mapping The model mapping to validate
 * @returns Array of validation errors, empty if valid
 */
declare function validateMappingCapabilities(mapping: ModelProviderMappingDto): string[];
/**
 * Get user-friendly display name for a capability
 * @param capability The capability to get display name for
 * @returns Human-readable display name
 */
declare function getCapabilityDisplayName(capability: ModelCapability): string;

declare const API_VERSION = "v1";
declare const API_PREFIX = "/api";
/**
 * HTTP method constants for type-safe method specification.
 * @deprecated Use HttpMethod enum from '@knn_labs/conduit-common' instead
 */
declare const HTTP_METHODS: {
    readonly GET: "GET";
    readonly POST: "POST";
    readonly PUT: "PUT";
    readonly DELETE: "DELETE";
    readonly PATCH: "PATCH";
};
/**
 * Client information constants.
 */
declare const CLIENT_INFO: {
    readonly NAME: "@conduit/admin";
    readonly VERSION: "0.1.0";
    readonly USER_AGENT: "@conduit/admin/0.1.0";
};
/**
 * Date format constants.
 */
declare const DATE_FORMATS: {
    readonly API_DATETIME: "YYYY-MM-DDTHH:mm:ss[Z]";
    readonly API_DATE: "YYYY-MM-DD";
    readonly DISPLAY_DATETIME: "MMM D, YYYY [at] h:mm A";
    readonly DISPLAY_DATE: "MMM D, YYYY";
};
declare const ENDPOINTS: {
    readonly VIRTUAL_KEYS: {
        readonly BASE: "/api/VirtualKeys";
        readonly BY_ID: (id: number) => string;
        readonly RESET_SPEND: (id: number) => string;
        readonly VALIDATE: "/api/VirtualKeys/validate";
        readonly SPEND: (id: number) => string;
        readonly REFUND: (id: number) => string;
        readonly CHECK_BUDGET: (id: number) => string;
        readonly VALIDATION_INFO: (id: number) => string;
        readonly MAINTENANCE: "/api/VirtualKeys/maintenance";
        readonly DISCOVERY_PREVIEW: (id: number) => string;
    };
    readonly PROVIDERS: {
        readonly BASE: "/api/ProviderCredentials";
        readonly BY_ID: (id: number) => string;
        readonly BY_NAME: (name: string) => string;
        readonly NAMES: "/api/ProviderCredentials/names";
        readonly TEST_BY_ID: (id: number) => string;
        readonly TEST: "/api/ProviderCredentials/test";
    };
    readonly PROVIDER_MODELS: {
        readonly BY_PROVIDER: (providerName: string) => string;
        readonly CACHED: (providerName: string) => string;
        readonly REFRESH: (providerName: string) => string;
        readonly TEST_CONNECTION: "/api/provider-models/test-connection";
        readonly SUMMARY: "/api/provider-models/summary";
        readonly DETAILS: (providerName: string, modelId: string) => string;
        readonly CAPABILITIES: (providerName: string, modelId: string) => string;
        readonly SEARCH: "/api/provider-models/search";
    };
    readonly MODEL_MAPPINGS: {
        readonly BASE: "/api/ModelProviderMapping";
        readonly BY_ID: (id: number) => string;
        readonly BY_MODEL: (modelId: string) => string;
        readonly PROVIDERS: "/api/ModelProviderMapping/providers";
        readonly BULK: "/api/ModelProviderMapping/bulk";
        readonly DISCOVER_PROVIDER: (providerName: string) => string;
        readonly DISCOVER_MODEL: (providerName: string, modelId: string) => string;
        readonly DISCOVER_ALL: "/api/ModelProviderMapping/discover/all";
        readonly TEST_CAPABILITY: (modelAlias: string, capability: string) => string;
        readonly IMPORT: "/api/ModelProviderMapping/import";
        readonly EXPORT: "/api/ModelProviderMapping/export";
        readonly SUGGEST: "/api/ModelProviderMapping/suggest";
        readonly ROUTING: (modelId: string) => string;
    };
    readonly IP_FILTERS: {
        readonly BASE: "/api/IpFilter";
        readonly BY_ID: (id: number) => string;
        readonly ENABLED: "/api/IpFilter/enabled";
        readonly SETTINGS: "/api/IpFilter/settings";
        readonly CHECK: (ipAddress: string) => string;
        readonly BULK_CREATE: "/api/IpFilter/bulk";
        readonly BULK_UPDATE: "/api/IpFilter/bulk-update";
        readonly BULK_DELETE: "/api/IpFilter/bulk-delete";
        readonly CREATE_TEMPORARY: "/api/IpFilter/temporary";
        readonly EXPIRING: "/api/IpFilter/expiring";
        readonly IMPORT: "/api/IpFilter/import";
        readonly EXPORT: "/api/IpFilter/export";
        readonly BLOCKED_STATS: "/api/IpFilter/blocked-stats";
    };
    readonly MODEL_COSTS: {
        readonly BASE: "/api/ModelCosts";
        readonly BY_ID: (id: number) => string;
        readonly BY_MODEL: (modelId: string) => string;
        readonly BY_PROVIDER: (providerName: string) => string;
        readonly BATCH: "/api/ModelCosts/batch";
        readonly IMPORT: "/api/ModelCosts/import";
        readonly BULK_UPDATE: "/api/ModelCosts/bulk-update";
        readonly OVERVIEW: "/api/ModelCosts/overview";
        readonly TRENDS: "/api/ModelCosts/trends";
    };
    readonly ANALYTICS: {
        readonly COST_SUMMARY: "/api/CostDashboard/summary";
        readonly COST_BY_PERIOD: "/api/CostDashboard/by-period";
        readonly COST_BY_MODEL: "/api/CostDashboard/by-model";
        readonly COST_BY_KEY: "/api/CostDashboard/by-key";
        readonly REQUEST_LOGS: "/api/Logs";
        readonly REQUEST_LOG_BY_ID: (id: string) => string;
        readonly EXPORT_REQUEST_LOGS: "/api/analytics/export/request-logs";
        readonly EXPORT_STATUS: (exportId: string) => string;
        readonly EXPORT_DOWNLOAD: (exportId: string) => string;
    };
    readonly COSTS: {
        readonly SUMMARY: "/api/costs/summary";
        readonly TRENDS: "/api/costs/trends";
        readonly MODELS: "/api/costs/models";
        readonly VIRTUAL_KEYS: "/api/costs/virtualkeys";
    };
    readonly HEALTH: {
        readonly CONFIGURATIONS: "/api/ProviderHealth/configurations";
        readonly CONFIG_BY_PROVIDER: (provider: string) => string;
        readonly STATUS: "/api/ProviderHealth/status";
        readonly STATUS_BY_PROVIDER: (provider: string) => string;
        readonly HISTORY: "/api/ProviderHealth/history";
        readonly HISTORY_BY_PROVIDER: (provider: string) => string;
        readonly CHECK: (provider: string) => string;
        readonly SUMMARY: "/api/health/providers";
        readonly ALERTS: "/api/health/alerts";
        readonly PERFORMANCE: (provider: string) => string;
    };
    readonly SYSTEM: {
        readonly INFO: "/api/SystemInfo/info";
        readonly HEALTH: "/api/SystemInfo/health";
        readonly SERVICES: "/api/health/services";
        readonly METRICS: "/api/metrics";
        readonly HEALTH_EVENTS: "/api/health/events";
        readonly BACKUP: "/api/DatabaseBackup";
        readonly RESTORE: "/api/DatabaseBackup/restore";
        readonly NOTIFICATIONS: "/api/Notifications";
        readonly NOTIFICATION_BY_ID: (id: number) => string;
    };
    readonly METRICS: {
        readonly ADMIN_BASIC: "/api/metrics";
        readonly ADMIN_DATABASE_POOL: "/metrics/database/pool";
        readonly REALTIME: "/api/dashboard/metrics/realtime";
    };
    readonly SETTINGS: {
        readonly GLOBAL: "/api/GlobalSettings";
        readonly GLOBAL_BY_KEY: (key: string) => string;
        readonly BATCH_UPDATE: "/api/GlobalSettings/batch";
        readonly AUDIO: "/api/AudioConfiguration";
        readonly AUDIO_BY_PROVIDER: (provider: string) => string;
        readonly ROUTER: "/api/Router";
    };
    readonly SECURITY: {
        readonly EVENTS: "/api/admin/security/events";
        readonly REPORT_EVENT: "/api/admin/security/events";
        readonly EXPORT_EVENTS: "/api/admin/security/events/export";
        readonly THREATS: "/api/admin/security/threats";
        readonly THREAT_BY_ID: (id: string) => string;
        readonly THREAT_ANALYTICS: "/api/admin/security/threats/analytics";
        readonly COMPLIANCE_METRICS: "/api/admin/security/compliance/metrics";
        readonly COMPLIANCE_REPORT: "/api/admin/security/compliance/report";
    };
    readonly ERROR_QUEUES: {
        readonly BASE: "/api/admin/error-queues";
        readonly MESSAGES: (queueName: string) => string;
        readonly MESSAGE_BY_ID: (queueName: string, messageId: string) => string;
        readonly STATISTICS: "/api/admin/error-queues/statistics";
        readonly HEALTH: "/api/admin/error-queues/health";
        readonly REPLAY: (queueName: string) => string;
        readonly CLEAR: (queueName: string) => string;
    };
    readonly CONFIGURATION: {
        readonly ROUTING: "/api/configuration/routing";
        readonly ROUTING_TEST: "/api/configuration/routing/test";
        readonly LOAD_BALANCER_HEALTH: "/api/configuration/routing/health";
        readonly ROUTING_RULES: "/api/config/routing/rules";
        readonly ROUTING_RULE_BY_ID: (id: string) => string;
        readonly CACHING: "/api/configuration/caching";
        readonly CACHE_POLICIES: "/api/configuration/caching/policies";
        readonly CACHE_POLICY_BY_ID: (id: string) => string;
        readonly CACHE_REGIONS: "/api/configuration/caching/regions";
        readonly CACHE_CLEAR: (regionId: string) => string;
        readonly CACHE_STATISTICS: "/api/configuration/caching/statistics";
        readonly CACHE_CONFIG: "/api/config/cache";
        readonly CACHE_STATS: "/api/config/cache/stats";
        readonly LOAD_BALANCER: "/api/config/loadbalancer";
        readonly PERFORMANCE: "/api/config/performance";
        readonly PERFORMANCE_TEST: "/api/config/performance/test";
        readonly FEATURES: "/api/config/features";
        readonly FEATURE_BY_KEY: (key: string) => string;
        readonly ROUTING_HEALTH: "/api/config/routing/health";
        readonly ROUTING_HEALTH_DETAILED: "/api/config/routing/health/detailed";
        readonly ROUTING_HEALTH_HISTORY: "/api/config/routing/health/history";
        readonly ROUTE_HEALTH_BY_ID: (routeId: string) => string;
        readonly ROUTE_PERFORMANCE_TEST: "/api/config/routing/performance/test";
        readonly CIRCUIT_BREAKERS: "/api/config/routing/circuit-breakers";
        readonly CIRCUIT_BREAKER_BY_ID: (breakerId: string) => string;
        readonly ROUTING_EVENTS: "/api/config/routing/events";
        readonly ROUTING_EVENTS_SUBSCRIBE: "/api/config/routing/events/subscribe";
    };
};
declare const DEFAULT_PAGE_SIZE = 20;
declare const MAX_PAGE_SIZE = 100;
declare const CACHE_TTL: {
    readonly SHORT: 60;
    readonly MEDIUM: 300;
    readonly LONG: 3600;
    readonly VERY_LONG: 86400;
};
declare const HTTP_STATUS: {
    readonly RATE_LIMITED: 429;
    readonly INTERNAL_ERROR: 500;
    readonly OK: 200;
    readonly CREATED: 201;
    readonly NO_CONTENT: 204;
    readonly BAD_REQUEST: 400;
    readonly UNAUTHORIZED: 401;
    readonly FORBIDDEN: 403;
    readonly NOT_FOUND: 404;
    readonly CONFLICT: 409;
    readonly TOO_MANY_REQUESTS: 429;
    readonly INTERNAL_SERVER_ERROR: 500;
    readonly BAD_GATEWAY: 502;
    readonly SERVICE_UNAVAILABLE: 503;
    readonly GATEWAY_TIMEOUT: 504;
};
declare const BUDGET_DURATION: {
    readonly TOTAL: "Total";
    readonly DAILY: "Daily";
    readonly WEEKLY: "Weekly";
    readonly MONTHLY: "Monthly";
};
declare const FILTER_TYPE: {
    readonly ALLOW: "whitelist";
    readonly DENY: "blacklist";
};
declare const FILTER_MODE: {
    readonly PERMISSIVE: "permissive";
    readonly RESTRICTIVE: "restrictive";
};

export { API_PREFIX, API_VERSION, type AdminMetricsResponse, type AdminNotificationCallback, type AdminNotificationEvent, AdminNotificationHubClient, type AdminNotificationOptions, AnalyticsFilters, AnalyticsOptions, AnalyticsService, AnomalyDto, ApiClientConfig, AudioConfigurationDto, AuditLogDto, AuditLogFilters, BUDGET_DURATION, type BackupDownloadResponse, BackupDto, type BackupFilters, type BackupInfo, type BackupMetadata, type BackupOptions, type BackupProgress, BackupRestoreResult, type BackupResult, BackupStage, type BackupStorageStats, type BackupSystemStatus, type BackupValidationResult, BlockedRequestStats, type BudgetDuration, BulkIpFilterResponse, BulkMappingRequest, BulkMappingResponse, BulkModelCostUpdate, BulkOperationResult, CACHE_TTL, CLIENT_INFO, CachePolicy, CacheRegion, CacheStatistics, CachingConfiguration, CapabilityTestResult, type CheckBudgetRequest, type CheckBudgetResponse, ClearCacheResult, ComplianceMetrics, FetchConduitAdminClient as ConduitAdminClient, ConfigValue, type ConfigurationChangeCallback, type ConfigurationChangeEvent, ConfigurationService, CostEstimate, CostForecastDto, type CpuMetrics, CreateAudioConfigurationDto, CreateBackupRequest, CreateCachePolicyDto, type CreateExportScheduleDto, CreateGlobalSettingDto, CreateIpFilterDto, CreateModelCostDto, CreateModelProviderMappingDto, CreateNotificationDto, CreateProviderCredentialDto, CreateProviderHealthConfigurationDto, CreateRoutingRuleDto, CreateSecurityEventDto, CreateTemporaryIpFilterDto, type CreateVirtualKeyRequest, type CreateVirtualKeyResponse, DATE_FORMATS, DEFAULT_PAGE_SIZE, type DatabasePoolMetrics, type DatabasePoolMetricsResponse, DiagnosticChecks, DiscoveredModel, ENDPOINTS, type ExportCostParams, ExportDestinationConfig, type ExportHistory, type ExportProviderParams, type ExportRequestLogsParams, type ExportSchedule, type ExportSecurityParams, type ExportStatus, type ExportUsageParams, type ExportVirtualKeyParams, ExtendedMetadata, RoutingRule as ExtendedRoutingRule, UpdateRoutingConfigDto$1 as ExtendedUpdateRoutingConfigDto, FILTER_MODE, FILTER_TYPE, FeatureAvailability, FetchConduitAdminClient, FilterType, type GcMetrics, GlobalSettingDto, HTTP_METHODS, HTTP_STATUS, HealthStatusDto, type IAdminNotificationHubClient, type IAdminNotificationHubServer, type INavigationStateHubClient, type INavigationStateHubServer, type IRealtimeNotificationService, ImportResult, IpCheckResult, IpFilterDto, IpFilterFilters, IpFilterImport, IpFilterImportResult, IpFilterService, IpFilterSettingsDto, IpFilterStatistics, IpFilterValidationResult, KeyUsageDto, LoadBalancerHealth, LoadBalancerHealthDto, MAX_PAGE_SIZE, MaintenanceTaskDto, MaintenanceTaskResult, type MemoryMetrics, MetricsService, ModelCost, ModelCostCalculation, ModelCostComparison, ModelCostDto, ModelCostFilters, ModelCostHistory, ModelCostOverview, ModelCostService, type ModelDiscoveredCallback, type ModelDiscoveredEvent, ModelMappingFilters, ModelMappingService, ModelMappingSuggestion, ModelProviderMappingDto, ModelRoutingInfo, ModelUsageDto, NavigationStateHubClient, type NavigationStateUpdateCallback, type NavigationStateUpdateEvent, NotificationBulkResponse, NotificationDto, NotificationFilters, NotificationSeverity, NotificationStatistics, type NotificationSubscription, NotificationSummary, NotificationType, NotificationsService, PagedResult, ProviderConnectionTestRequest$1 as ProviderConnectionTestRequest, ProviderConnectionTestResultDto, ProviderCredentialDto, ProviderDataDto, ProviderFilters, type ProviderHealthChangeCallback, type ProviderHealthChangeEvent, ProviderHealthConfigurationDto, ProviderHealthFilters, ProviderHealthRecordDto, ProviderHealthService, ProviderHealthStatisticsDto, ProviderHealthStatusDto, ProviderHealthSummaryDto, ProviderModelsService, ProviderService, ProviderStatus, ProviderUsageStatistics, RealtimeNotificationsService, type RefundSpendRequest, type RequestLog, RequestLogDto, RequestLogFilters, type RequestLogStatistics, type RequestLogSummary, type RequestLogSummaryParams, type RequestMetrics, RestoreBackupRequest, type RestoreOptions, type RestoreResult, RouterConfigurationDto, RouterRule, RoutingConfigDto, RoutingConfiguration, RunMaintenanceTaskRequest, SecurityEvent, SecurityEventFilters, SecurityService, SettingFilters, SettingsService, type SignalRConnectionOptions, SignalREndpoints, SignalRService, SystemConfiguration, SystemInfoDto, type SystemMetrics, SystemService, TestResult, ThreatAction, ThreatAnalytics, ThreatDetection, ThreatFilters, UpdateAudioConfigurationDto, UpdateCachePolicyDto, UpdateCachingConfigDto, UpdateGlobalSettingDto, UpdateIpFilterDto, UpdateIpFilterSettingsDto, UpdateModelCostDto, UpdateModelProviderMappingDto, UpdateNotificationDto, UpdateProviderCredentialDto, UpdateProviderHealthConfigurationDto, UpdateRouterConfigurationDto, UpdateRoutingConfigDto, UpdateRoutingRuleDto, type UpdateSpendRequest, type UpdateVirtualKeyRequest, UsageMetricsDto, type VirtualKeyDto, type VirtualKeyEvent, type VirtualKeyEventCallback, type VirtualKeyEventType, type VirtualKeyFilters, type VirtualKeyMaintenanceRequest, type VirtualKeyMaintenanceResponse, VirtualKeyMetadata, type VirtualKeyStatistics, type VirtualKeyValidationInfo, type VirtualKeyValidationRequest, type VirtualKeyValidationResult, FetchConduitAdminClient as default, filterByCapability, findDefaultMapping, getBestMapping, getCapabilities, getCapabilityDisplayName, hasCapability, validateMappingCapabilities };
