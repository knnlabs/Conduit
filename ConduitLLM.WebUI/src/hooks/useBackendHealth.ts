'use client';

import { useState, useEffect, useCallback } from 'react';
import type { HealthStatusDto } from '@knn_labs/conduit-admin-client';
import type { HealthCheckDetail } from '@/types/health';

export interface BackendHealthStatus {
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  lastChecked: Date;
  adminApiDetails?: HealthStatusDto;
  coreApiDetails?: HealthStatusDto;
  adminApiChecks?: Record<string, HealthCheckDetail>;
  coreApiChecks?: Record<string, HealthCheckDetail>;
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
        
        // Map status values
        const mapStatus = (status: string): 'healthy' | 'degraded' | 'unavailable' => {
          switch (status.toLowerCase()) {
            case 'healthy':
              return 'healthy';
            case 'degraded':
              return 'degraded';
            case 'unhealthy':
            case 'unavailable':
              return 'unavailable';
            default:
              return 'degraded';
          }
        };
        
        // Extract provider check for Core API status
        const providerCheck = health.checks?.providers;
        const coreApiStatus = providerCheck ? mapStatus(providerCheck.status) : 'unavailable';
        const coreApiMessage = providerCheck?.description;
        
        setHealthStatus({
          adminApi: mapStatus(health.status),
          coreApi: coreApiStatus,
          lastChecked: new Date(),
          adminApiDetails: health,
          coreApiDetails: health, // For now, using same data
          adminApiChecks: health.checks,
          coreApiChecks: health.checks,
          coreApiMessage,
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