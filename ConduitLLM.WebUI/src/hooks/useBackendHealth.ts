'use client';

import { useState, useEffect, useCallback } from 'react';
import type { HealthStatusDto } from '@knn_labs/conduit-admin-client';
import { processHealthStatus, createErrorHealthStatus } from '@/lib/utils/health-status';

export interface BackendHealthStatus {
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  lastChecked: Date;
  adminApiDetails?: HealthStatusDto;
  coreApiDetails?: HealthStatusDto;
  adminApiChecks?: Record<string, import('@/types/health').HealthCheckDetail>;
  coreApiChecks?: Record<string, import('@/types/health').HealthCheckDetail>;
  coreApiMessage?: string;
}

export function useBackendHealth(autoRefresh: boolean = true, refreshInterval: number = 30000) {
  const [healthStatus, setHealthStatus] = useState<BackendHealthStatus>({
    adminApi: 'healthy',
    coreApi: 'healthy',
    lastChecked: new Date(),
  });
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const checkHealth = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/admin/system/health');
      
      if (response.ok) {
        const health = await response.json() as HealthStatusDto;
        
        // Use the shared health processing function
        const processed = processHealthStatus(health);
        
        setHealthStatus({
          adminApi: processed.adminApi,
          coreApi: processed.coreApi,
          lastChecked: processed.lastChecked,
          adminApiDetails: processed.adminApiDetails,
          coreApiDetails: processed.coreApiDetails,
          adminApiChecks: processed.adminApiChecks,
          coreApiChecks: processed.coreApiChecks,
          coreApiMessage: processed.coreApiMessage,
        });
      } else {
        // Non-OK response means the API is reachable but returning an error
        const errorStatus = createErrorHealthStatus(`Health check returned ${response.status} status`);
        setHealthStatus({
          adminApi: errorStatus.adminApi,
          coreApi: errorStatus.coreApi,
          lastChecked: errorStatus.lastChecked,
          coreApiMessage: errorStatus.coreApiMessage,
        });
        setError(`Health check returned ${response.status} status`);
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Health check failed';
      const errorStatus = createErrorHealthStatus(errorMessage);
      setHealthStatus({
        adminApi: errorStatus.adminApi,
        coreApi: errorStatus.coreApi,
        lastChecked: errorStatus.lastChecked,
        coreApiMessage: errorStatus.coreApiMessage,
      });
      setError(errorMessage);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (autoRefresh) {
      // Initial check
      void checkHealth();

      // Set up interval
      const interval = setInterval(() => {
        void checkHealth();
      }, refreshInterval);

      return () => clearInterval(interval);
    }
  }, [autoRefresh, refreshInterval, checkHealth]);

  const isHealthy = healthStatus.adminApi === 'healthy' && healthStatus.coreApi === 'healthy';
  const isDegraded = healthStatus.adminApi === 'degraded' || healthStatus.coreApi === 'degraded';
  const isUnavailable = healthStatus.adminApi === 'unavailable' || healthStatus.coreApi === 'unavailable';

  return {
    healthStatus,
    isHealthy,
    isDegraded,
    isUnavailable,
    isLoading,
    error,
    refetch: checkHealth,
  };
}