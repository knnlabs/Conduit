import { FilterOptions } from './common';

export interface SystemInfoDto {
  version: string;
  buildDate: string;
  environment: string;
  uptime: number;
  systemTime: string;
  features: {
    ipFiltering: boolean;
    providerHealth: boolean;
    costTracking: boolean;
    audioSupport: boolean;
  };
  runtime: {
    dotnetVersion: string;
    os: string;
    architecture: string;
    memoryUsage: number;
    cpuUsage?: number;
  };
  database: {
    provider: string;
    connectionString?: string;
    isConnected: boolean;
    pendingMigrations?: string[];
  };
}

export interface HealthStatusDto {
  status: 'healthy' | 'degraded' | 'unhealthy';
  timestamp: string;
  checks: {
    [key: string]: {
      status: 'healthy' | 'degraded' | 'unhealthy';
      description?: string;
      duration?: number;
      error?: string;
    };
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

export interface UpdateNotificationDto {
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
  parameters?: Record<string, any>;
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
  oldValue?: any;
  newValue?: any;
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

export interface SystemMetricsDto {
  timestamp: string;
  cpu: {
    usage: number;
    cores: number;
  };
  memory: {
    used: number;
    total: number;
    percentage: number;
  };
  disk: {
    used: number;
    total: number;
    percentage: number;
  };
  network: {
    bytesIn: number;
    bytesOut: number;
    requestsPerSecond: number;
  };
  database: {
    connections: number;
    maxConnections: number;
    queryTime: number;
  };
}