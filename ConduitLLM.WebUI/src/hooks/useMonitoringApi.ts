'use client';

import { useState, useCallback, useEffect, useRef } from 'react';
import type { 
  SystemMetrics
} from '@knn_labs/conduit-admin-client';
import type { ErrorResponse } from '@knn_labs/conduit-common';

// Define ServiceHealth interface based on the available SDK types
interface ServiceHealth {
  service: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  lastCheck: string;
  responseTime: number;
  error?: string;
}

// Define Alert interface based on the available SDK types
interface Alert {
  id: string;
  type: 'error' | 'warning' | 'info';
  title: string;
  message: string;
  timestamp: string;
  resolved: boolean;
}

interface MonitoringConfig {
  refreshInterval?: number; // milliseconds
  enableAlerts?: boolean;
  enableMetrics?: boolean;
  enableHealth?: boolean;
}

export function useMonitoringApi(config: MonitoringConfig = {}) {
  const {
    refreshInterval = 30000, // 30 seconds default
    enableAlerts = true,
    enableMetrics = true,
    enableHealth = true,
  } = config;

  const [metrics, setMetrics] = useState<SystemMetrics | null>(null);
  const [health, setHealth] = useState<ServiceHealth[]>([]);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const intervalRef = useRef<NodeJS.Timeout | null>(null);

  const fetchSystemMetrics = useCallback(async (): Promise<SystemMetrics> => {
    try {
      const response = await fetch('/api/monitoring/metrics', {
        method: 'GET',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch system metrics');
      }

      const result = await response.json() as SystemMetrics;

      setMetrics(result);
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch system metrics';
      setError(message);
      throw err;
    }
  }, []);

  const fetchServiceHealth = useCallback(async (): Promise<ServiceHealth[]> => {
    try {
      const response = await fetch('/api/monitoring/health', {
        method: 'GET',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch service health');
      }

      const result = await response.json() as ServiceHealth[];

      setHealth(result);
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch service health';
      setError(message);
      throw err;
    }
  }, []);

  const fetchAlerts = useCallback(async (params?: {
    unresolved?: boolean;
    limit?: number;
  }): Promise<Alert[]> => {
    try {
      const queryParams = new URLSearchParams();
      if (params?.unresolved) queryParams.append('unresolved', 'true');
      if (params?.limit) queryParams.append('limit', params.limit.toString());

      const response = await fetch(`/api/monitoring/alerts?${queryParams}`, {
        method: 'GET',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch alerts');
      }

      const result = await response.json() as Alert[];

      setAlerts(result);
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch alerts';
      setError(message);
      throw err;
    }
  }, []);

  const resolveAlert = useCallback(async (alertId: string): Promise<void> => {
    try {
      const response = await fetch(`/api/monitoring/alerts/${alertId}/resolve`, {
        method: 'POST',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to resolve alert');
      }

      // Update local state
      setAlerts(prev => prev.map(alert => 
        alert.id === alertId ? { ...alert, resolved: true } : alert
      ));
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to resolve alert';
      setError(message);
      throw err;
    }
  }, []);

  const refreshAll = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const promises: Promise<unknown>[] = [];
      
      if (enableMetrics) promises.push(fetchSystemMetrics());
      if (enableHealth) promises.push(fetchServiceHealth());
      if (enableAlerts) promises.push(fetchAlerts({ unresolved: true }));

      await Promise.all(promises);
    } catch {
      // Errors are already handled in individual functions
    } finally {
      setIsLoading(false);
    }
  }, [enableMetrics, enableHealth, enableAlerts, fetchSystemMetrics, fetchServiceHealth, fetchAlerts]);

  // Set up auto-refresh
  useEffect(() => {
    if (refreshInterval > 0) {
      // Initial fetch
      void refreshAll();

      // Set up interval
      intervalRef.current = setInterval(() => {
        void refreshAll();
      }, refreshInterval);

      return () => {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
        }
      };
    }
  }, [refreshInterval, refreshAll]);

  // Manual refresh control
  const startAutoRefresh = useCallback(() => {
    if (!intervalRef.current && refreshInterval > 0) {
      intervalRef.current = setInterval(() => {
        void refreshAll();
      }, refreshInterval);
    }
  }, [refreshInterval, refreshAll]);

  const stopAutoRefresh = useCallback(() => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  return {
    metrics,
    health,
    alerts,
    isLoading,
    error,
    fetchSystemMetrics,
    fetchServiceHealth,
    fetchAlerts,
    resolveAlert,
    refreshAll,
    startAutoRefresh,
    stopAutoRefresh,
  };
}