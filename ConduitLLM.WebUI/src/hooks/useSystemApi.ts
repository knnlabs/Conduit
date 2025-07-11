'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';

interface SystemInfo {
  version: string;
  environment: string;
  uptime: number;
  startTime: string;
  hostname: string;
  platform: string;
  nodeVersion: string;
  features: {
    signalR: boolean;
    redis: boolean;
    eventBus: boolean;
    mediaStorage: string;
  };
}

interface SystemSettings {
  maintenanceMode: boolean;
  debugMode: boolean;
  logLevel: 'debug' | 'info' | 'warn' | 'error';
  maxRequestSize: number;
  requestTimeout: number;
  corsOrigins: string[];
}

interface SystemHealth {
  status: 'healthy' | 'degraded' | 'unhealthy';
  checks: {
    name: string;
    status: 'healthy' | 'unhealthy';
    message?: string;
    duration: number;
  }[];
  timestamp: string;
}

interface BackupInfo {
  id: string;
  createdAt: string;
  size: number;
  type: 'manual' | 'scheduled';
  status: 'completed' | 'failed' | 'in-progress';
}

export function useSystemApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getSystemInfo = useCallback(async (): Promise<SystemInfo> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/system/info', {
        method: 'GET',
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to fetch system info');
      }

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch system info';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getSystemSettings = useCallback(async (): Promise<SystemSettings> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/system/settings', {
        method: 'GET',
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to fetch system settings');
      }

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch system settings';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateSystemSettings = useCallback(async (settings: Partial<SystemSettings>): Promise<SystemSettings> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/system/settings', {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(settings),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to update system settings');
      }

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

  const getSystemHealth = useCallback(async (): Promise<SystemHealth> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/system/health', {
        method: 'GET',
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to fetch system health');
      }

      return result;
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
      const response = await fetch(`/api/admin/system/services/${serviceName}/restart`, {
        method: 'POST',
      });

      if (!response.ok) {
        const result = await response.json();
        throw new Error(result.error || 'Failed to restart service');
      }

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

  const createBackup = useCallback(async (): Promise<BackupInfo> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/system/backup', {
        method: 'POST',
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to create backup');
      }

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

  const getBackups = useCallback(async (): Promise<BackupInfo[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/system/backups', {
        method: 'GET',
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to fetch backups');
      }

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch backups';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const restoreBackup = useCallback(async (backupId: string): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/admin/system/backups/${backupId}/restore`, {
        method: 'POST',
      });

      if (!response.ok) {
        const result = await response.json();
        throw new Error(result.error || 'Failed to restore backup');
      }

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