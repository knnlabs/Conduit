'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';

export type ExportFormat = 'csv' | 'json' | 'excel';
export type ExportType = 'analytics' | 'virtualKeys' | 'usage' | 'requestLogs' | 'systemPerformance' | 'providerHealth';

interface ExportRequest {
  type: ExportType;
  format: ExportFormat;
  startDate?: string;
  endDate?: string;
  filters?: Record<string, unknown>;
}

interface ExportStatus {
  id: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  progress: number;
  downloadUrl?: string;
  error?: string;
  createdAt: string;
  completedAt?: string;
}

export function useExportApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exportProgress, setExportProgress] = useState<Record<string, number>>({});

  const startExport = useCallback(async (request: ExportRequest): Promise<{ exportId: string }> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const endpoint = getExportEndpoint(request.type);
      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          format: request.format,
          startDate: request.startDate,
          endDate: request.endDate,
          ...request.filters,
        }),
      });

      const result = await response.json() as unknown;

      if (!response.ok) {
        const errorData = result as { error?: string };
        throw new Error(errorData.error ?? 'Failed to start export');
      }

      notifications.show({
        title: 'Export Started',
        message: 'Your export is being processed',
        color: 'blue',
      });

      return result as { exportId: string };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to start export';
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

  const getExportStatus = useCallback(async (exportId: string): Promise<ExportStatus> => {
    try {
      const response = await fetch(`/api/export/status/${exportId}`, {
        method: 'GET',
      });

      const result = await response.json() as unknown;

      if (!response.ok) {
        const errorData = result as { error?: string };
        throw new Error(errorData.error ?? 'Failed to get export status');
      }

      const statusData = result as { progress?: number };
      // Update progress tracking
      if (statusData.progress !== undefined) {
        const progress = statusData.progress;
        setExportProgress(prev => ({
          ...prev,
          [exportId]: progress,
        }));
      }

      return result as ExportStatus;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to get export status';
      setError(message);
      throw err;
    }
  }, []);

  const downloadExport = useCallback(async (downloadUrl: string, filename: string): Promise<void> => {
    try {
      const response = await fetch(downloadUrl);
      
      if (!response.ok) {
        throw new Error('Failed to download export');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      notifications.show({
        title: 'Success',
        message: 'Export downloaded successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to download export';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    }
  }, []);

  const exportAnalytics = useCallback(async (format: ExportFormat, filters?: Record<string, unknown>): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/analytics/export', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          format,
          ...filters,
        }),
      });

      if (!response.ok) {
        const result = await response.json() as unknown;
        const errorData = result as { error?: string };
        throw new Error(errorData.error ?? 'Failed to export analytics');
      }

      // For direct download endpoints
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = url;
      a.download = `analytics-export-${new Date().toISOString()}.${format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      notifications.show({
        title: 'Success',
        message: 'Analytics exported successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to export analytics';
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
    startExport,
    getExportStatus,
    downloadExport,
    exportAnalytics,
    isLoading,
    error,
    exportProgress,
  };
}

function getExportEndpoint(type: ExportType): string {
  const endpoints: Record<ExportType, string> = {
    analytics: '/api/admin/analytics/export',
    virtualKeys: '/api/virtual-keys-analytics/export',
    usage: '/api/usage-analytics/export',
    requestLogs: '/api/request-logs/export',
    systemPerformance: '/api/system-performance/export',
    providerHealth: '/api/provider-health/export',
  };
  
  return endpoints[type];
}