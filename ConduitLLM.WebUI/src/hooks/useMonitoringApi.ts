'use client';

import { useState, useCallback, useEffect, useRef } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';
import type { 
  SystemMetrics,
  ServiceHealthStatus,
  AlertDto
} from '@knn_labs/conduit-admin-client';

// Use SDK types
type ServiceHealth = ServiceHealthStatus;
type Alert = AlertDto;

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
      const result = await withAdminClient(client => 
        client.monitoring.getSystemMetrics()
      );

      setMetrics(result as unknown as SystemMetrics);
      return result as unknown as SystemMetrics;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch system metrics';
      setError(message);
      throw err;
    }
  }, []);

  const fetchServiceHealth = useCallback(async (): Promise<ServiceHealth[]> => {
    try {
      // Service health endpoint may not exist, return empty array
      const result: ServiceHealth[] = [];
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
      const result = await withAdminClient(client => 
        client.monitoring.listAlerts({
          status: params?.unresolved ? 'active' : undefined,
        })
      );

      const alerts = Array.isArray(result) ? result : (result.data || []);
      setAlerts(alerts as Alert[]);
      return alerts as Alert[];
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch alerts';
      setError(message);
      throw err;
    }
  }, []);

  const resolveAlert = useCallback(async (alertId: string): Promise<void> => {
    try {
      await withAdminClient(client => 
        client.monitoring.resolveAlert(alertId)
      );

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