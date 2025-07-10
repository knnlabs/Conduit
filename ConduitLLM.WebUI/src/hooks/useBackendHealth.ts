'use client';

import { useState, useEffect } from 'react';

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

export function useBackendHealth() {
  const [healthStatus, setHealthStatus] = useState<BackendHealthStatus>({
    adminApi: 'healthy',
    coreApi: 'healthy',
    lastChecked: new Date(),
  });

  useEffect(() => {
    // Check health status
    const checkHealth = async () => {
      try {
        const response = await fetch('/api/health');
        if (response.ok) {
          setHealthStatus({
            adminApi: 'healthy',
            coreApi: 'healthy',
            lastChecked: new Date(),
          });
        } else {
          setHealthStatus({
            adminApi: 'degraded',
            coreApi: 'degraded',
            lastChecked: new Date(),
          });
        }
      } catch (error) {
        setHealthStatus({
          adminApi: 'unavailable',
          coreApi: 'unavailable',
          lastChecked: new Date(),
        });
      }
    };

    checkHealth();
    const interval = setInterval(checkHealth, 30000); // Check every 30 seconds

    return () => clearInterval(interval);
  }, []);

  const isHealthy = healthStatus.adminApi === 'healthy' && healthStatus.coreApi === 'healthy';
  const isDegraded = healthStatus.adminApi === 'degraded' || healthStatus.coreApi === 'degraded';
  const isUnavailable = healthStatus.adminApi === 'unavailable' || healthStatus.coreApi === 'unavailable';

  return {
    healthStatus,
    isHealthy,
    isDegraded,
    isUnavailable,
    adminError: undefined,
    coreError: undefined,
    refetch: () => {},
  };
}