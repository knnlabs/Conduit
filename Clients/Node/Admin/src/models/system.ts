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
  type: 'info' | 'warning' | 'error' | 'success';
  title: string;
  message: string;
  timestamp: string;
  isRead: boolean;
  metadata?: Record<string, any>;
  actionUrl?: string;
  expiresAt?: string;
}

export interface CreateNotificationDto {
  type: 'info' | 'warning' | 'error' | 'success';
  title: string;
  message: string;
  metadata?: Record<string, any>;
  actionUrl?: string;
  expiresAt?: string;
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