'use client';

import { useState, useEffect, useCallback } from 'react';
import type { HealthStatusDto } from '@knn_labs/conduit-admin-client';

export interface BackendHealthStatus {
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  lastChecked: Date;
  adminApiDetails?: Record<string, unknown>; // Health check details - flexible structure
  coreApiDetails?: Record<string, unknown>; // Health check details - flexible structure
  coreApiMessage?: string;
  coreApiChecks?: {
    name: string;
    status: string;
    description?: string;
    data?: Record<string, unknown>; // Health check data - flexible structure
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
        const health = await response.json() as HealthStatusDto;
        // The SDK response contains the standard health data
        
        // Determine status based on the health response
        const isHealthy = health.status === 'healthy';
        
        setHealthStatus({
          adminApi: isHealthy ? 'healthy' : 'degraded',
          coreApi: isHealthy ? 'healthy' : 'degraded',
          lastChecked: new Date(),
          adminApiDetails: health.checks ? Object.fromEntries(Object.entries(health.checks)) : undefined,
          coreApiDetails: health.checks ? Object.fromEntries(Object.entries(health.checks)) : undefined,
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