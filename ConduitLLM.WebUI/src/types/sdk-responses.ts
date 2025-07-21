/**
 * TypeScript type definitions for WebUI-specific types
 * SDK types are imported from @knn_labs/conduit-admin-client and @knn_labs/conduit-core-client
 */

// All UI types have been removed - use SDK types directly

// Import SDK types
export type {
  // Virtual Key types
  VirtualKeyDto,
  CreateVirtualKeyRequest,
  CreateVirtualKeyResponse,
  UpdateVirtualKeyRequest,
  VirtualKeyValidationResult,
  VirtualKeyFilters,
  VirtualKeyStatistics,
  VirtualKeyMaintenanceRequest,
  VirtualKeyMaintenanceResponse,
  
  // Provider types
  ProviderCredentialDto,
  CreateProviderCredentialDto,
  UpdateProviderCredentialDto,
  ProviderHealthStatusDto,
  ProviderHealthSummaryDto,
  ProviderHealthRecordDto,
  ProviderHealthStatisticsDto,
  ProviderConnectionTestRequest,
  ProviderConnectionTestResultDto,
  ProviderDataDto,
  ProviderFilters,
  ProviderStatus,
  
  // Model types
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  ModelMappingFilters,
  ModelCapabilities,
  DiscoveredModel,
  ModelProviderInfo,
  ModelRoutingInfo,
  
  // System types
  SystemInfoDto,
  HealthStatusDto,
  SystemConfiguration,
  
  // Analytics types
  UsageMetricsDto,
  ModelUsageDto,
  KeyUsageDto,
  CostSummaryDto,
  CostByPeriodDto,
  CostForecastDto,
  RequestLogDto,
  RequestLogFilters,
  RequestLogStatistics,
  RequestLogSummary,
  
  // Security types
  SecurityEvent,
  SecurityEventFilters,
  ThreatAnalytics,
  ThreatDetection,
  ComplianceMetrics,
  
  // Backup types
  BackupDto,
  CreateBackupRequest,
  RestoreBackupRequest,
  BackupResult,
  RestoreResult,
  BackupInfo,
  BackupMetadata,
  
  // Notification types
  NotificationDto,
  CreateNotificationDto,
  UpdateNotificationDto,
  NotificationFilters,
  NotificationSummary,
  NotificationStatistics,
  NotificationType,
  NotificationSeverity,
  
  // Settings types
  GlobalSettingDto,
  CreateGlobalSettingDto,
  UpdateGlobalSettingDto,
  AudioConfigurationDto,
  RouterConfigurationDto,
  
  // Common types
  PaginatedResponse,
  ApiResponse,
  PagedResponse,
  ErrorResponse,
  BudgetDuration,
  FilterType,
  FilterMode,
  StatusType
} from '@knn_labs/conduit-admin-client';

// All UI types and mapping functions have been removed - use SDK types directly

// WebUI-specific types that don't exist in SDK
export interface ProviderIncident {
  id: string;
  providerId: string;
  startTime: string;
  endTime?: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  description: string;
  affectedModels: string[];
  status: 'active' | 'resolved';
}

export interface ServiceHealth {
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  version?: string;
  uptime?: number;
  lastCheck: string;
  message?: string;
}

export interface DependencyHealth {
  name: string;
  type: 'database' | 'cache' | 'queue' | 'external';
  status: 'connected' | 'disconnected' | 'error';
  latency?: number;
  lastCheck: string;
  error?: string;
}

export interface SystemFeature {
  name: string;
  enabled: boolean;
  version?: string;
}

export interface DailyUsage {
  date: string;
  requests: number;
  cost: number;
  tokens: number;
  successRate: number;
}

export interface AudioDailyUsage {
  date: string;
  requests: number;
  cost: number;
  transcriptions?: number;
  ttsGenerations?: number;
  totalMinutes?: number;
}

export interface LanguageUsage {
  language: string;
  count: number;
  percentage: number;
}

export interface AudioModelPerformance {
  model: string;
  requests: number;
  minutesProcessed: number;
  avgProcessingTime?: string;
  successRate?: number;
  totalCost: number;
  costPerMinute: number;
}

export interface MediaRecord {
  id: string;
  virtualKeyId: number;
  mediaType: string;
  mediaUrl: string;
  thumbnailUrl?: string;
  cdnUrl?: string;
  storageKey: string;
  size: number;
  createdDate: string;
  expirationDate?: string;
  metadata?: {
    model?: string;
    prompt?: string;
    width?: number;
    height?: number;
    duration?: number;
    format?: string;
  };
}

export interface MediaStorageStats {
  virtualKeyId: number;
  totalSize: number;
  totalCount: number;
  imageCount: number;
  videoCount: number;
  oldestMedia?: string;
  newestMedia?: string;
}

export interface OverallMediaStorageStats extends MediaStorageStats {
  byVirtualKey: MediaStorageStats[];
  byProvider: Record<string, number>;
  byType: Record<string, number>;
}

export interface CostDashboard {
  timeframe: string;
  startDate: string;
  endDate: string;
  totalCost: number;
  totalRequests: number;
  averageCostPerRequest: number;
  costByProvider: ProviderCost[];
  costByModel: ModelCost[];
  costByVirtualKey: VirtualKeyCost[];
  costTrend: CostTrendPoint[];
}

export interface ProviderCost {
  provider: string;
  totalCost: number;
  requestCount: number;
  percentageOfTotal: number;
}

export interface ModelCost {
  model: string;
  provider: string;
  totalCost: number;
  requestCount: number;
  averageCostPerRequest: number;
}

export interface VirtualKeyCost {
  virtualKeyId: number;
  virtualKeyName: string;
  totalCost: number;
  requestCount: number;
  budgetUsed: number;
  remainingBudget: number;
}

export interface CostTrendPoint {
  date: string;
  totalCost: number;
  requestCount: number;
  providers: Record<string, number>;
}

export interface AudioUsageSummary {
  startDate: string;
  endDate: string;
  totalRequests: number;
  totalCost: number;
  totalDuration: number;
  averageLatency: number;
  transcriptionGrowth?: number;
  ttsGrowth?: number;
  costGrowth?: number;
  topModels: AudioModelUsage[];
  dailyUsage: AudioDailyUsage[];
  modelUsage?: AudioModelUsage[];
  languageDistribution?: LanguageUsage[];
  modelPerformance?: AudioModelPerformance[];
}

export interface AudioModelUsage {
  model: string;
  requests: number;
  cost: number;
  duration?: number;
  percentage?: number;
}

