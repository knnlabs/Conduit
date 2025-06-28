import { BaseApiClient } from '../client/BaseApiClient';
import { ApiClientConfig } from '../client/types';
import type {
  BackupInfo,
  BackupResult,
  RestoreResult,
  BackupOptions,
  RestoreOptions,
  BackupValidationResult,
  BackupStorageStats,
  BackupDownloadResponse,
  BackupFilters,
  BackupProgress,
  BackupSystemStatus
} from '../models/databaseBackup';

/**
 * Service for managing database backups and restoration through the Admin API
 */
export class DatabaseBackupService extends BaseApiClient {
  private readonly baseEndpoint = '/api/database';

  constructor(config: ApiClientConfig) {
    super(config);
  }

  /**
   * Creates a new database backup
   * 
   * @param options - Optional backup configuration options
   * @returns Promise<BackupResult> Information about the created backup
   */
  async createBackup(options?: BackupOptions): Promise<BackupResult> {
    try {
      const backupInfo = await this.post<BackupInfo>(
        `${this.baseEndpoint}/backup`,
        options
      );

      return {
        success: true,
        backupInfo
      };
    } catch (error) {
      return {
        success: false,
        errorMessage: error instanceof Error ? error.message : 'Unknown error occurred'
      };
    }
  }

  /**
   * Retrieves a list of all available database backups
   * 
   * @returns Promise<BackupInfo[]> A list of backup information
   */
  async getBackups(): Promise<BackupInfo[]> {
    return this.get<BackupInfo[]>(`${this.baseEndpoint}/backups`);
  }

  /**
   * Gets information about a specific backup
   * 
   * @param backupId - The ID of the backup to retrieve
   * @returns Promise<BackupInfo | null> Backup information if found, null otherwise
   */
  async getBackupInfo(backupId: string): Promise<BackupInfo | null> {
    if (!backupId?.trim()) {
      throw new Error('Backup ID cannot be null or empty');
    }

    try {
      const backups = await this.getBackups();
      return backups.find(b => b.id === backupId) || null;
    } catch {
      return null;
    }
  }

  /**
   * Restores the database from a specific backup
   * 
   * @param backupId - The ID of the backup to restore from
   * @param options - Optional restore configuration options
   * @returns Promise<RestoreResult> The result of the restore operation
   */
  async restoreBackup(backupId: string, options?: RestoreOptions): Promise<RestoreResult> {
    if (!backupId?.trim()) {
      throw new Error('Backup ID cannot be null or empty');
    }

    try {
      await this.post(
        `${this.baseEndpoint}/restore/${encodeURIComponent(backupId)}`,
        options
      );

      return {
        success: true
      };
    } catch (error) {
      return {
        success: false,
        errorMessage: error instanceof Error ? error.message : 'Unknown error occurred'
      };
    }
  }

  /**
   * Downloads a backup file as ArrayBuffer
   * 
   * @param backupId - The ID of the backup to download
   * @returns Promise<BackupDownloadResponse> The backup file content and metadata
   */
  async downloadBackup(backupId: string): Promise<BackupDownloadResponse> {
    if (!backupId?.trim()) {
      throw new Error('Backup ID cannot be null or empty');
    }

    const response = await this.axios.get(
      `${this.baseEndpoint}/download/${encodeURIComponent(backupId)}`,
      {
        responseType: 'arraybuffer'
      }
    );

    const contentType = response.headers['content-type'] || 'application/octet-stream';
    const contentDisposition = response.headers['content-disposition'] || '';
    const fileNameMatch = contentDisposition.match(/filename="?([^"]+)"?/);
    const fileName = fileNameMatch ? fileNameMatch[1] : `backup-${backupId}.sql`;

    return {
      data: response.data,
      contentType,
      fileName,
      sizeBytes: response.data.byteLength
    };
  }

  /**
   * Downloads a backup file and returns it as a Blob (for browser environments)
   * 
   * @param backupId - The ID of the backup to download
   * @returns Promise<Blob> The backup file as a Blob
   */
  async downloadBackupAsBlob(backupId: string): Promise<Blob> {
    const downloadResponse = await this.downloadBackup(backupId);
    return new Blob([downloadResponse.data], { type: downloadResponse.contentType });
  }

  /**
   * Checks if a backup with the specified ID exists
   * 
   * @param backupId - The ID of the backup to check
   * @returns Promise<boolean> True if the backup exists, false otherwise
   */
  async backupExists(backupId: string): Promise<boolean> {
    if (!backupId?.trim()) {
      return false;
    }

    try {
      const backup = await this.getBackupInfo(backupId);
      return backup !== null;
    } catch {
      return false;
    }
  }

  /**
   * Gets the most recent backup
   * 
   * @returns Promise<BackupInfo | null> The most recent backup, or null if no backups exist
   */
  async getMostRecentBackup(): Promise<BackupInfo | null> {
    try {
      const backups = await this.getBackups();
      return backups
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0] || null;
    } catch {
      return null;
    }
  }

  /**
   * Gets backups created within a specific date range
   * 
   * @param startDate - The start date (inclusive)
   * @param endDate - The end date (inclusive)
   * @returns Promise<BackupInfo[]> Backups created within the specified date range
   */
  async getBackupsByDateRange(startDate: Date, endDate: Date): Promise<BackupInfo[]> {
    if (startDate > endDate) {
      throw new Error('Start date cannot be greater than end date');
    }

    const backups = await this.getBackups();
    return backups
      .filter(b => {
        const backupDate = new Date(b.createdAt);
        return backupDate >= startDate && backupDate <= endDate;
      })
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }

  /**
   * Gets backup storage statistics
   * 
   * @returns Promise<BackupStorageStats> Storage statistics for all backups
   */
  async getBackupStorageStats(): Promise<BackupStorageStats> {
    const backups = await this.getBackups();
    
    const totalBackups = backups.length;
    const totalSizeBytes = backups.reduce((sum, backup) => sum + backup.sizeBytes, 0);
    const averageSizeBytes = totalBackups > 0 ? Math.round(totalSizeBytes / totalBackups) : 0;

    const sortedByDate = backups.sort((a, b) => 
      new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
    );
    
    const oldestBackup = sortedByDate[0]?.createdAt;
    const newestBackup = sortedByDate[sortedByDate.length - 1]?.createdAt;
    
    const largestBackup = backups.sort((a, b) => b.sizeBytes - a.sizeBytes)[0];

    return {
      totalBackups,
      totalSizeBytes,
      totalSizeFormatted: this.formatBytes(totalSizeBytes),
      averageSizeBytes,
      averageSizeFormatted: this.formatBytes(averageSizeBytes),
      oldestBackup: oldestBackup ? new Date(oldestBackup) : undefined,
      newestBackup: newestBackup ? new Date(newestBackup) : undefined,
      largestBackup
    };
  }

  /**
   * Creates a backup and immediately downloads it
   * 
   * @param options - Optional backup configuration options
   * @returns Promise<BackupDownloadResponse> The backup file content and metadata
   */
  async createAndDownloadBackup(options?: BackupOptions): Promise<BackupDownloadResponse> {
    const backupResult = await this.createBackup(options);
    if (!backupResult.success || !backupResult.backupInfo) {
      throw new Error(`Failed to create backup: ${backupResult.errorMessage}`);
    }

    return this.downloadBackup(backupResult.backupInfo.id);
  }

  /**
   * Validates a backup file
   * 
   * @param backupId - The ID of the backup to validate
   * @returns Promise<BackupValidationResult> Validation results
   */
  async validateBackup(backupId: string): Promise<BackupValidationResult> {
    if (!backupId?.trim()) {
      throw new Error('Backup ID cannot be null or empty');
    }

    try {
      return this.post<BackupValidationResult>(
        `${this.baseEndpoint}/validate/${encodeURIComponent(backupId)}`
      );
    } catch (error) {
      return {
        isValid: false,
        errors: [error instanceof Error ? error.message : 'Validation failed'],
        warnings: []
      };
    }
  }

  /**
   * Deletes a backup file
   * 
   * @param backupId - The ID of the backup to delete
   */
  async deleteBackup(backupId: string): Promise<void> {
    if (!backupId?.trim()) {
      throw new Error('Backup ID cannot be null or empty');
    }

    await this.delete(`${this.baseEndpoint}/backups/${encodeURIComponent(backupId)}`);
  }

  /**
   * Gets filtered backups based on the provided criteria
   * 
   * @param filters - The filters to apply
   * @returns Promise<BackupInfo[]> Filtered list of backups
   */
  async getFilteredBackups(filters: BackupFilters): Promise<BackupInfo[]> {
    let backups = await this.getBackups();

    // Apply date filters
    if (filters.startDate) {
      backups = backups.filter(b => new Date(b.createdAt) >= filters.startDate!);
    }

    if (filters.endDate) {
      backups = backups.filter(b => new Date(b.createdAt) <= filters.endDate!);
    }

    // Apply size filters
    if (filters.minSizeBytes !== undefined) {
      backups = backups.filter(b => b.sizeBytes >= filters.minSizeBytes!);
    }

    if (filters.maxSizeBytes !== undefined) {
      backups = backups.filter(b => b.sizeBytes <= filters.maxSizeBytes!);
    }

    // Apply sorting
    if (filters.sortBy) {
      const sortDirection = filters.sortDirection === 'asc' ? 1 : -1;
      backups.sort((a, b) => {
        let aValue: any;
        let bValue: any;

        switch (filters.sortBy) {
          case 'createdAt':
            aValue = new Date(a.createdAt).getTime();
            bValue = new Date(b.createdAt).getTime();
            break;
          case 'sizeBytes':
            aValue = a.sizeBytes;
            bValue = b.sizeBytes;
            break;
          case 'fileName':
            aValue = a.fileName.toLowerCase();
            bValue = b.fileName.toLowerCase();
            break;
          default:
            return 0;
        }

        if (aValue < bValue) return -1 * sortDirection;
        if (aValue > bValue) return 1 * sortDirection;
        return 0;
      });
    } else {
      // Default sort by creation date (newest first)
      backups.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    }

    // Apply pagination
    if (filters.page !== undefined && filters.pageSize !== undefined) {
      const startIndex = (filters.page - 1) * filters.pageSize;
      const endIndex = startIndex + filters.pageSize;
      backups = backups.slice(startIndex, endIndex);
    }

    return backups;
  }

  /**
   * Gets the current backup system status
   * 
   * @returns Promise<BackupSystemStatus> Current backup system status
   */
  async getBackupSystemStatus(): Promise<BackupSystemStatus> {
    try {
      return this.get<BackupSystemStatus>(`${this.baseEndpoint}/status`);
    } catch {
      // Fallback to basic status if endpoint is not available
      const backups = await this.getBackups();
      const mostRecent = await this.getMostRecentBackup();
      const stats = await this.getBackupStorageStats();

      return {
        isOperational: true,
        lastBackupDate: mostRecent?.createdAt ? new Date(mostRecent.createdAt) : undefined,
        availableBackups: backups.length,
        totalStorageUsed: stats.totalSizeFormatted,
        healthStatus: 'healthy',
        statusMessages: []
      };
    }
  }

  /**
   * Gets backup operation progress (if supported)
   * 
   * @param operationId - The operation ID to track
   * @returns Promise<BackupProgress | null> Progress information or null if not found
   */
  async getBackupProgress(operationId: string): Promise<BackupProgress | null> {
    if (!operationId?.trim()) {
      throw new Error('Operation ID cannot be null or empty');
    }

    try {
      return this.get<BackupProgress>(
        `${this.baseEndpoint}/progress/${encodeURIComponent(operationId)}`
      );
    } catch {
      return null;
    }
  }

  /**
   * Formats bytes into a human-readable string
   * 
   * @param bytes - The number of bytes
   * @returns A formatted string representation of the size
   */
  private formatBytes(bytes: number): string {
    const suffixes = ['B', 'KB', 'MB', 'GB', 'TB'];
    let counter = 0;
    let number = bytes;
    
    while (Math.round(number / 1024) >= 1 && counter < suffixes.length - 1) {
      number /= 1024;
      counter++;
    }
    
    return `${number.toFixed(1)} ${suffixes[counter]}`;
  }

  /**
   * Creates multiple backups in sequence
   * 
   * @param count - Number of backups to create
   * @param options - Optional backup configuration options
   * @returns Promise<BackupResult[]> Results of all backup operations
   */
  async createMultipleBackups(count: number, options?: BackupOptions): Promise<BackupResult[]> {
    if (count <= 0) {
      throw new Error('Count must be greater than 0');
    }

    const results: BackupResult[] = [];
    
    for (let i = 0; i < count; i++) {
      try {
        const result = await this.createBackup(options);
        results.push(result);
        
        // Add a small delay between backups to avoid overwhelming the system
        if (i < count - 1) {
          await new Promise(resolve => setTimeout(resolve, 1000));
        }
      } catch (error) {
        results.push({
          success: false,
          errorMessage: error instanceof Error ? error.message : 'Unknown error occurred'
        });
      }
    }

    return results;
  }

  /**
   * Deletes multiple backups
   * 
   * @param backupIds - Array of backup IDs to delete
   * @returns Promise<{ successCount: number; failedIds: string[]; errors: string[] }> Bulk operation results
   */
  async deleteMultipleBackups(backupIds: string[]): Promise<{
    successCount: number;
    failedIds: string[];
    errors: string[];
  }> {
    if (!backupIds || backupIds.length === 0) {
      return {
        successCount: 0,
        failedIds: [],
        errors: []
      };
    }

    let successCount = 0;
    const failedIds: string[] = [];
    const errors: string[] = [];

    for (const backupId of backupIds) {
      try {
        await this.deleteBackup(backupId);
        successCount++;
      } catch (error) {
        failedIds.push(backupId);
        errors.push(`Failed to delete backup ${backupId}: ${error instanceof Error ? error.message : 'Unknown error'}`);
      }
    }

    return {
      successCount,
      failedIds,
      errors
    };
  }
}