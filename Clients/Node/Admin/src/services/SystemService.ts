import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL, HTTP_HEADERS } from '../constants';
import {
  SystemInfoDto,
  HealthStatusDto,
  BackupDto,
  CreateBackupRequest,
  RestoreBackupRequest,
  BackupRestoreResult,
  NotificationDto,
  CreateNotificationDto,
  MaintenanceTaskDto,
  RunMaintenanceTaskRequest,
  MaintenanceTaskResult,
  AuditLogDto,
  AuditLogFilters,
  FeatureAvailability,
} from '../models/system';
import { PaginatedResponse } from '../models/common';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';
import { VirtualKeyService } from './VirtualKeyService';
import { SettingsService } from './SettingsService';
// Get SDK version from package.json at build time
const SDK_VERSION = process.env.npm_package_version || '1.0.0';

const createBackupSchema = z.object({
  description: z.string().max(500).optional(),
  includeKeys: z.boolean().optional(),
  includeProviders: z.boolean().optional(),
  includeSettings: z.boolean().optional(),
  includeLogs: z.boolean().optional(),
  encryptionPassword: z.string().min(8).optional(),
});

const restoreBackupSchema = z.object({
  backupId: z.string().min(1),
  decryptionPassword: z.string().optional(),
  overwriteExisting: z.boolean().optional(),
  selectedItems: z.object({
    keys: z.boolean().optional(),
    providers: z.boolean().optional(),
    settings: z.boolean().optional(),
    logs: z.boolean().optional(),
  }).optional(),
});

export class SystemService extends BaseApiClient {
  // System Information
  async getSystemInfo(): Promise<SystemInfoDto> {
    const cacheKey = 'system-info';
    return this.withCache(
      cacheKey,
      () => super.get<SystemInfoDto>(ENDPOINTS.SYSTEM.INFO),
      CACHE_TTL.SHORT
    );
  }

  async getHealth(): Promise<HealthStatusDto> {
    return super.get<HealthStatusDto>(ENDPOINTS.SYSTEM.HEALTH);
  }

  // Backup Management
  async listBackups(): Promise<BackupDto[]> {
    const cacheKey = 'backups';
    return this.withCache(
      cacheKey,
      () => super.get<BackupDto[]>(ENDPOINTS.SYSTEM.BACKUP),
      CACHE_TTL.SHORT
    );
  }

  async createBackup(request?: CreateBackupRequest): Promise<BackupDto> {
    if (request) {
      try {
        createBackupSchema.parse(request);
      } catch (error) {
        throw new ValidationError('Invalid backup request', { validationError: error });
      }
    }

    const response = await this.post<BackupDto>(
      ENDPOINTS.SYSTEM.BACKUP,
      request || {}
    );

    await this.invalidateCache();
    return response;
  }

  async downloadBackup(backupId: string): Promise<Blob> {
    const response = await super.request<any>({
      method: 'GET',
      url: `${ENDPOINTS.SYSTEM.BACKUP}/${backupId}/download`,
      headers: { Accept: 'application/octet-stream' }
    });
    return new Blob([response]);
  }

  async deleteBackup(backupId: string): Promise<void> {
    await this.delete(`${ENDPOINTS.SYSTEM.BACKUP}/${backupId}`);
    await this.invalidateCache();
  }

  async restoreBackup(request: RestoreBackupRequest): Promise<BackupRestoreResult> {
    try {
      restoreBackupSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid restore request', { validationError: error });
    }

    return this.post<BackupRestoreResult>(
      ENDPOINTS.SYSTEM.RESTORE,
      request
    );
  }

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
  async getAuditLogs(_filters?: AuditLogFilters): Promise<PaginatedResponse<AuditLogDto>> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getAuditLogs requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/audit'
    );
  }

  async scheduledBackup(
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

  async getScheduledBackups(): Promise<Array<{
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

  async exportAuditLogs(
    _filters: AuditLogFilters,
    _format: 'csv' | 'json'
  ): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportAuditLogs requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/audit/export'
    );
  }

  async getDiagnostics(): Promise<{
    timestamp: string;
    checks: Record<string, any>;
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
    const baseConfig = {
      baseUrl: (this.axios.defaults.baseURL || '').replace('/api', ''),
      masterKey: this.axios.defaults.headers[HTTP_HEADERS.X_API_KEY] as string,
      logger: this.logger,
      cache: this.cache,
      retries: this.retryConfig,
    };
    
    const settingsService = new SettingsService(baseConfig);
    
    try {
      // First try to get existing key from GlobalSettings
      const setting = await settingsService.getGlobalSetting('WebUI_VirtualKey');
      if (setting?.value) {
        return setting.value;
      }
    } catch (error) {
      // Key doesn't exist, we'll create it
      this.logger?.debug('WebUI virtual key not found in GlobalSettings, creating new one');
    }

    // Create metadata
    const metadata = {
      visibility: 'hidden',
      created: new Date().toISOString(),
      originator: `Admin SDK ${SDK_VERSION}`
    };

    // Create the virtual key via VirtualKeyService
    const virtualKeyService = new VirtualKeyService(baseConfig);
    const response = await virtualKeyService.create({
      keyName: 'WebUI Internal Key',
      metadata: JSON.stringify(metadata)
    });
    
    // Store the unhashed key in GlobalSettings
    await settingsService.createGlobalSetting({
      key: 'WebUI_VirtualKey',
      value: response.virtualKey,
      isSecret: true,
      category: 'WebUI',
      description: 'Virtual key for WebUI Core API access'
    });
    
    this.logger?.info('Created new WebUI virtual key');
    return response.virtualKey;
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}