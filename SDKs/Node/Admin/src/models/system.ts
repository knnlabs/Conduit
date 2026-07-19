import { FilterOptions } from './common';
import type { MaintenanceTaskConfig, ConfigValue } from './common-types';

// Reconciled to the wire `SystemInfoDto` in issue #1038: the endpoint returns nested
// version/os/database/runtime/recordCounts objects, not the previous flat shape.
export interface VersionInfo {
  appVersion?: string;
  buildDate?: string | null;
}

export interface OsInfo {
  description?: string;
  architecture?: string;
}

export interface DatabaseInfo {
  provider?: string;
  version?: string;
  connected?: boolean;
  connectionString?: string;
  location?: string;
  size?: string;
  tableCount?: number;
}

export interface RuntimeInfo {
  runtimeVersion?: string;
  startTime?: string;
  uptime?: string;
}

export interface RecordCountsDto {
  virtualKeys?: number;
  requests?: number;
  settings?: number;
  providers?: number;
  modelMappings?: number;
}

export interface SystemInfoDto {
  version?: VersionInfo;
  operatingSystem?: OsInfo;
  database?: DatabaseInfo;
  runtime?: RuntimeInfo;
  recordCounts?: RecordCountsDto;
}

export interface HealthCheckData {
  // Flexible structure for additional health check data
  // This allows for different data structures per health check type
  [key: string]: string | number | boolean | HealthCheckData | HealthCheckData[];
}

export interface HealthCheckDetail {
  status: 'healthy' | 'degraded' | 'unhealthy';
  description?: string;
  duration?: number;
  error?: string;
  data?: HealthCheckData;
}

export interface HealthStatusDto {
  status: 'healthy' | 'degraded' | 'unhealthy';
  timestamp: string;
  checks: {
    [key: string]: HealthCheckDetail;
  };
  totalDuration: number;
}

export interface BackupDto {
  id: string;
  filename: string;
  createdAt: string;
  size: number;
  type: 'manual' | 'scheduled';
  status: 'completed' | 'in_progress' | 'failed';
  error?: string;
  downloadUrl?: string;
  expiresAt?: string;
}

export interface CreateBackupRequest {
  description?: string;
  includeKeys?: boolean;
  includeProviders?: boolean;
  includeSettings?: boolean;
  includeLogs?: boolean;
  encryptionPassword?: string;
}

export interface RestoreBackupRequest {
  backupId: string;
  decryptionPassword?: string;
  overwriteExisting?: boolean;
  selectedItems?: {
    keys?: boolean;
    providers?: boolean;
    settings?: boolean;
    logs?: boolean;
  };
}

export interface BackupRestoreResult {
  success: boolean;
  restoredItems: {
    keys?: number;
    providers?: number;
    settings?: number;
    logs?: number;
  };
  errors?: string[];
  warnings?: string[];
}

export interface NotificationDto {
  id: number;
  virtualKeyId?: number;
  virtualKeyName?: string;
  type: NotificationType;
  severity: NotificationSeverity;
  message: string;
  isRead: boolean;
  createdAt: Date;
}

export enum NotificationType {
  BudgetWarning = 0,
  ExpirationWarning = 1,
  System = 2
}

export enum NotificationSeverity {
  Info = 0,
  Warning = 1,
  Error = 2
}

export interface CreateNotificationDto {
  virtualKeyId?: number;
  type: NotificationType;
  severity: NotificationSeverity;
  message: string;
}

// Reconciled to wire shape — issue #1038 (wire adds id)
export interface UpdateNotificationDto {
  id?: number;
  message?: string;
  isRead?: boolean;
}

export interface NotificationFilters {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  type?: NotificationType;
  severity?: NotificationSeverity;
  isRead?: boolean;
  virtualKeyId?: number;
  startDate?: Date;
  endDate?: Date;
}

export interface NotificationSummary {
  totalNotifications: number;
  unreadNotifications: number;
  readNotifications: number;
  notificationsByType: Record<NotificationType, number>;
  notificationsBySeverity: Record<NotificationSeverity, number>;
  mostRecentNotification?: NotificationDto;
  oldestUnreadNotification?: NotificationDto;
}

export interface NotificationBulkResponse {
  successCount: number;
  totalCount: number;
  failedIds?: number[];
  errors?: string[];
}

export interface NotificationStatistics {
  total: number;
  unread: number;
  read: number;
  byType: Record<string, number>;
  bySeverity: Record<string, number>;
  recent: {
    lastHour: number;
    last24Hours: number;
    lastWeek: number;
  };
}

export interface MaintenanceTaskDto {
  name: string;
  description: string;
  lastRun?: string;
  nextRun?: string;
  status: 'idle' | 'running' | 'failed';
  canRunManually: boolean;
  schedule?: string;
}

export interface RunMaintenanceTaskRequest {
  taskName: string;
  parameters?: MaintenanceTaskConfig;
}

export interface MaintenanceTaskResult {
  taskName: string;
  startTime: string;
  endTime: string;
  success: boolean;
  itemsProcessed?: number;
  errors?: string[];
  logs?: string[];
}

export interface AuditLogDto {
  id: string;
  timestamp: string;
  action: string;
  category: 'auth' | 'config' | 'data' | 'system';
  userId?: string;
  ipAddress?: string;
  userAgent?: string;
  resourceType?: string;
  resourceId?: string;
  oldValue?: ConfigValue;
  newValue?: ConfigValue;
  result: 'success' | 'failure';
  errorMessage?: string;
}

export interface AuditLogFilters extends FilterOptions {
  startDate?: string;
  endDate?: string;
  action?: string;
  category?: string;
  userId?: string;
  ipAddress?: string;
  resourceType?: string;
  result?: 'success' | 'failure';
}

export interface FeatureAvailability {
  features: Record<string, {
    available: boolean;
    status: 'available' | 'coming_soon' | 'in_development' | 'not_planned';
    message?: string;
    version?: string;
    releaseDate?: string;
  }>;
  timestamp: string;
}

// Issue #427 - System Health SDK Methods
export interface SystemHealthDto {
  overall: 'healthy' | 'degraded' | 'unhealthy';
  components: {
    api: {
      status: 'healthy' | 'degraded' | 'unhealthy';
      message?: string;
      lastChecked: string;
    };
    database: {
      status: 'healthy' | 'degraded' | 'unhealthy';
      message?: string;
      lastChecked: string;
    };
    cache: {
      status: 'healthy' | 'degraded' | 'unhealthy';
      message?: string;
      lastChecked: string;
    };
    queue: {
      status: 'healthy' | 'degraded' | 'unhealthy';
      message?: string;
      lastChecked: string;
    };
  };
  metrics: {
    cpu: number;
    memory: number;
    disk: number;
    activeConnections: number;
  };
}

// Client-side normalized resource view (CPU/memory/disk percentages). Distinct from the
// wire `SystemMetricsDto` (process diagnostics: cpuCount/gcMemoryMb/threadCount/workingSetMb),
// so it is intentionally named differently to avoid a false type-drift pairing. See issue #1038.
export interface SystemResourceMetricsDto {
  cpuUsage: number;
  memoryUsage: number;
  diskUsage: number;
  activeConnections: number;
  uptime: number;
}

// Client-side per-service health map produced by transforming the /api/health/services
// response. Distinct from the wire `ServiceStatusDto` (a single per-service record), so it is
// intentionally named differently to avoid a false type-drift pairing. See issue #1038.
export interface ServiceStatusMapDto {
  coreApi: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    latency: number;
    endpoint: string;
  };
  adminApi: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    latency: number;
    endpoint: string;
  };
  database: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    latency: number;
    connections: number;
  };
  cache: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    latency: number;
    hitRate: number;
  };
}

// Issue #428 - Health Events SDK Methods
export interface HealthEventDto {
  id: string;
  timestamp: string;
  type: 'provider_down' | 'provider_up' | 'system_issue' | 'system_recovered';
  message: string;
  severity: 'info' | 'warning' | 'error';
  source?: string;
  metadata?: {
    providerId?: string;
    componentName?: string;
    errorDetails?: string;
    duration?: number;
  };
}

export interface HealthEventsResponseDto {
  events: HealthEventDto[];
}

export interface HealthEventSubscriptionOptions {
  severityFilter?: ('info' | 'warning' | 'error')[];
  typeFilter?: ('provider_down' | 'provider_up' | 'system_issue' | 'system_recovered')[];
  sourceFilter?: string[];
}

export interface HealthEventSubscription {
  unsubscribe(): void;
  isConnected(): boolean;
  onEvent(callback: (event: HealthEventDto) => void): void;
  onConnectionStateChanged(callback: (connected: boolean) => void): void;
}