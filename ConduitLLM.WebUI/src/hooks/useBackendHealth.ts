'use client';

import { useState, useEffect, useCallback } from 'react';

export interface BackendHealthStatus {
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  lastChecked: Date;
  adminApiDetails?: unknown;
  coreApiDetails?: unknown;
  coreApiMessage?: string;
  coreApiChecks?: {
    name: string;
    status: string;
    description?: string;
    data?: unknown;
  }[];
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
        const health = await response.json();
        
        // Determine status based on the health response
        const isHealthy = health.status === 'healthy' || health.overallStatus === 'healthy';
        
        setHealthStatus({
          adminApi: isHealthy ? 'healthy' : 'degraded',
          coreApi: isHealthy ? 'healthy' : 'degraded',
          lastChecked: new Date(),
          adminApiDetails: health.adminApiDetails,
          coreApiDetails: health.coreApiDetails,
          coreApiMessage: health.coreApiMessage,
          coreApiChecks: health.coreApiChecks,
        });
      } else {
        setHealthStatus({
          adminApi: 'degraded',
          coreApi: 'degraded',
          lastChecked: new Date(),
        });
        setError('Health check returned non-OK status');
      }
    } catch (err) {
      setHealthStatus({
        adminApi: 'unavailable',
        coreApi: 'unavailable',
        lastChecked: new Date(),
      });
      setError(err instanceof Error ? err.message : 'Health check failed');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (autoRefresh) {
      // Initial check
      checkHealth();

      // Set up interval
      const interval = setInterval(checkHealth, refreshInterval);

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