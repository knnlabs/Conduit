import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { DiagnosticChecks } from '../models/common-types';
import { ApiClientConfig } from '../client/types';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  SystemInfoDto,
  HealthStatusDto,
  NotificationDto,
  CreateNotificationDto,
  MaintenanceTaskDto,
  RunMaintenanceTaskRequest,
  MaintenanceTaskResult,
  AuditLogDto,
  AuditLogFilters,
  FeatureAvailability,
  CreateBackupRequest,
} from '../models/system';
import { PaginatedResponse } from '../models/common';
import { NotImplementedError } from '../utils/errors';
import { FetchVirtualKeyService as VirtualKeyService } from './FetchVirtualKeyService';
import { SettingsService } from './SettingsService';
// Get SDK version from package.json at build time
const SDK_VERSION = process.env.npm_package_version ?? '1.0.0';

// C# API response types
interface SystemInfoApiResponse {
  Version?: {
    AppVersion?: string;
    BuildDate?: string;
  };
  environment?: string;
  Runtime?: {
    Uptime?: string;
    RuntimeVersion?: string;
  };
  OperatingSystem?: {
    Description?: string;
    Architecture?: string;
  };
  Database?: {
    Provider?: string;
    ConnectionString?: string;
    Connected?: boolean;
  };
}


export class SystemService extends FetchBaseApiClient {
  // System Information
  async getSystemInfo(): Promise<SystemInfoDto> {
    const cacheKey = 'system-info';
    return this.withCache(
      cacheKey,
      async () => {
        const response = await super.get<SystemInfoApiResponse>(ENDPOINTS.SYSTEM.INFO);
        // Transform the C# response to TypeScript-friendly format
        return {
          version: response.Version?.AppVersion ?? 'Unknown',
          buildDate: response.Version?.BuildDate ?? '',
          environment: response.environment ?? 'production',
          uptime: this.parseTimeSpan(response.Runtime?.Uptime) ?? 0,
          systemTime: new Date().toISOString(),
          features: {
            ipFiltering: false,
            providerHealth: true,
            costTracking: false,
            audioSupport: false
          },
          runtime: {
            dotnetVersion: response.Runtime?.RuntimeVersion ?? 'Unknown',
            os: response.OperatingSystem?.Description ?? 'Unknown',
            architecture: response.OperatingSystem?.Architecture ?? 'Unknown',
            memoryUsage: 0,
            cpuUsage: undefined
          },
          database: {
            provider: response.Database?.Provider ?? 'Unknown',
            connectionString: response.Database?.ConnectionString,
            isConnected: response.Database?.Connected ?? false,
            pendingMigrations: []
          }
        };
      },
      CACHE_TTL.SHORT
    );
  }

  private parseTimeSpan(timespan: string | undefined): number {
    if (!timespan) return 0;
    
    // Parse .NET TimeSpan format (e.g., "00:05:30" or "1.02:03:04.5")
    const match = timespan.match(/^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/);
    if (!match) return 0;
    
    const days = parseInt(match[1] || '0', 10);
    const hours = parseInt(match[2], 10);
    const minutes = parseInt(match[3], 10);
    const seconds = parseInt(match[4], 10);
    
    return (days * 24 * 60 * 60) + (hours * 60 * 60) + (minutes * 60) + seconds;
  }

  async getHealth(): Promise<HealthStatusDto> {
    return super.get<HealthStatusDto>(ENDPOINTS.SYSTEM.HEALTH);
  }

  // Backup Management - removed (endpoints no longer exist)

  // Notifications
  async getNotifications(unreadOnly?: boolean): Promise<NotificationDto[]> {
    const params = unreadOnly ? { unreadOnly } : undefined;
    const cacheKey = this.getCacheKey('notifications', params);
    return this.withCache(
      cacheKey,
      () => super.get<NotificationDto[]>(ENDPOINTS.SYSTEM.NOTIFICATIONS, params),
      CACHE_TTL.SHORT
    );
  }

  async getNotification(id: number): Promise<NotificationDto> {
    return super.get<NotificationDto>(ENDPOINTS.SYSTEM.NOTIFICATION_BY_ID(id));
  }

  async createNotification(request: CreateNotificationDto): Promise<NotificationDto> {
    const response = await this.post<NotificationDto>(
      ENDPOINTS.SYSTEM.NOTIFICATIONS,
      request
    );
    await this.invalidateCache();
    return response;
  }

  async markNotificationRead(id: number): Promise<void> {
    await this.put(
      `${ENDPOINTS.SYSTEM.NOTIFICATION_BY_ID(id)}/read`
    );
    await this.invalidateCache();
  }

  async deleteNotification(id: number): Promise<void> {
    await this.delete(ENDPOINTS.SYSTEM.NOTIFICATION_BY_ID(id));
    await this.invalidateCache();
  }

  async clearNotifications(): Promise<void> {
    await this.delete(ENDPOINTS.SYSTEM.NOTIFICATIONS);
    await this.invalidateCache();
  }

  // Maintenance Tasks
  async getMaintenanceTasks(): Promise<MaintenanceTaskDto[]> {
    const cacheKey = 'maintenance-tasks';
    return this.withCache(
      cacheKey,
      () => super.get<MaintenanceTaskDto[]>('/api/maintenance/tasks'),
      CACHE_TTL.MEDIUM
    );
  }

  async runMaintenanceTask(
    request: RunMaintenanceTaskRequest
  ): Promise<MaintenanceTaskResult> {
    return this.post<MaintenanceTaskResult>(
      '/api/maintenance/run',
      request
    );
  }

  // Stub methods
  getAuditLogs(_filters?: AuditLogFilters): Promise<PaginatedResponse<AuditLogDto>> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getAuditLogs requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/audit'
    );
  }

  scheduledBackup(
    _schedule: string,
    _config: CreateBackupRequest
  ): Promise<{
    id: string;
    schedule: string;
    nextRun: string;
    config: CreateBackupRequest;
  }> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'scheduledBackup requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/databasebackup/schedule'
    );
  }

  getScheduledBackups(): Promise<Array<{
    id: string;
    schedule: string;
    nextRun: string;
    lastRun?: string;
    config: CreateBackupRequest;
  }>> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getScheduledBackups requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/databasebackup/schedules'
    );
  }

  exportAuditLogs(
    _filters: AuditLogFilters,
    _format: 'csv' | 'json'
  ): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportAuditLogs requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/audit/export'
    );
  }

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
  }> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getDiagnostics requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/systeminfo/diagnostics'
    );
  }

  async getFeatureAvailability(): Promise<FeatureAvailability> {
    const cacheKey = 'feature-availability';
    return this.withCache(
      cacheKey,
      () => super.get<FeatureAvailability>('/api/systeminfo/features'),
      CACHE_TTL.MEDIUM
    );
  }

  /**
   * Gets or creates the special WebUI virtual key.
   * This key is stored unencrypted in GlobalSettings for WebUI/TUI access.
   * @returns The actual (unhashed) virtual key value
   */
  async getWebUIVirtualKey(): Promise<string> {
    // Use the same config as the current service instance
    const baseConfig: ApiClientConfig = {
      baseUrl: this.baseUrl.replace('/api', ''),
      masterKey: this.masterKey,
      logger: this.logger,
      cache: this.cache,
      retries: this.retryConfig,
      timeout: this.timeout,
      defaultHeaders: this.defaultHeaders,
    };
    
    const settingsService = new SettingsService(baseConfig);
    
    try {
      // First try to get existing key from GlobalSettings
      const setting = await settingsService.getGlobalSetting('WebUI_VirtualKey');
      if (setting?.value) {
        return setting.value;
      }
    } catch {
      // Key doesn't exist, we'll create it
      this.log('debug', 'WebUI virtual key not found in GlobalSettings, creating new one');
    }

    // Create metadata
    const metadata = {
      visibility: 'hidden',
      created: new Date().toISOString(),
      originator: `Admin SDK ${SDK_VERSION}`
    };

    // Create the virtual key via VirtualKeyService
    const virtualKeyService = new VirtualKeyService(this);
    const response = await virtualKeyService.create({
      keyName: 'WebUI Internal Key',
      metadata: JSON.stringify(metadata)
    });
    
    if (!response.virtualKey) {
      throw new Error('Failed to create virtual key: No key returned');
    }

    // Store the unhashed key in GlobalSettings
    await settingsService.createGlobalSetting({
      key: 'WebUI_VirtualKey',
      value: response.virtualKey,
      isSecret: true,
      category: 'WebUI',
      description: 'Virtual key for WebUI Core API access'
    });
    
    this.log('info', 'Created new WebUI virtual key');
    return response.virtualKey;
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}