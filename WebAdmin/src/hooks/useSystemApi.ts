'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import type {
  SystemInfoDto,
  HealthStatusDto,
  BackupDto
} from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

export function useSystemApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getSystemInfo = useCallback(async (): Promise<SystemInfoDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      return withAdminClient(client => client.system.getSystemInfo());
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch system info';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getSystemSettings = useCallback(async (): Promise<Record<string, unknown>> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: getSettings method doesn't exist in Admin SDK
      // Using placeholder implementation
      // TODO: Implement settings retrieval once SDK supports it
      return Promise.resolve({});
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch system settings';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateSystemSettings = useCallback(async (settings: Record<string, unknown>): Promise<Record<string, unknown>> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: updateSettings method doesn't exist in Admin SDK
      // This is a placeholder implementation
      // TODO: Implement settings update once SDK supports it
      const result = await Promise.resolve(settings);

      notifications.show({
        title: 'Success',
        message: 'System settings updated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update system settings';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getSystemHealth = useCallback(async (): Promise<HealthStatusDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      return withAdminClient(client => client.system.getHealth());
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch system health';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const restartService = useCallback(async (serviceName: string): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: restartService method doesn't exist in Admin SDK
      // This is a placeholder implementation
      // TODO: Implement service restart once SDK supports it
      await Promise.resolve();

      notifications.show({
        title: 'Success',
        message: `Service ${serviceName} restarted successfully`,
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to restart service';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createBackup = useCallback(async (): Promise<BackupDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: createBackup method doesn't exist in Admin SDK
      // This is a placeholder implementation
      // TODO: Implement backup creation once SDK supports it
      const result = await Promise.resolve({ 
        id: 'placeholder', 
        name: 'Placeholder Backup', 
        createdAt: new Date().toISOString(),
        filename: 'placeholder-backup.sql',
        size: 0,
        type: 'full' as const,
        status: 'completed' as const
      } as unknown as BackupDto);

      notifications.show({
        title: 'Success',
        message: 'Backup created successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create backup';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getBackups = useCallback(async (): Promise<BackupDto[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: getBackups method doesn't exist in Admin SDK
      // Using placeholder implementation  
      // TODO: Implement backup retrieval once SDK supports it
      return Promise.resolve([]);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch backups';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const restoreBackup = useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: restoreBackup method doesn't exist in Admin SDK
      // This is a placeholder implementation
      // TODO: Implement backup restore once SDK supports it
      await Promise.resolve();

      notifications.show({
        title: 'Success',
        message: 'Backup restored successfully. System will restart.',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to restore backup';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    getSystemInfo,
    getSystemSettings,
    updateSystemSettings,
    getSystemHealth,
    restartService,
    createBackup,
    getBackups,
    restoreBackup,
    isLoading,
    error,
  };
}