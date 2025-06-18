import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
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
  SystemMetricsDto,
} from '../models/system';
import { PaginatedResponse } from '../models/common';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

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

  async getSystemMetrics(): Promise<SystemMetricsDto> {
    return super.get<SystemMetricsDto>('/api/systeminfo/metrics');
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
        throw new ValidationError('Invalid backup request', error);
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
      throw new ValidationError('Invalid restore request', error);
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

  async streamSystemMetrics(
    _onMessage?: (metrics: SystemMetricsDto) => void,
    _onError?: (error: Error) => void
  ): Promise<() => void> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'streamSystemMetrics requires Admin API SSE endpoint implementation. ' +
        'Consider implementing GET /api/systeminfo/metrics/stream as Server-Sent Events'
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}