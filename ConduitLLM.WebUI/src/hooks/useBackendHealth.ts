'use client';

import { useState, useEffect } from 'react';
import { useSystemHealth } from '@/hooks/useConduitAdmin';
import { BackendErrorHandler, BackendErrorType } from '@/lib/errors/BackendErrorHandler';

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
    adminApi: 'unavailable',
    coreApi: 'unavailable',
    lastChecked: new Date(),
  });

  // Use SDK's system health hook
  const { data: systemHealth, error: healthError, isLoading: healthLoading, refetch } = useSystemHealth();

  // For now, we'll assume Admin API health is the same as system health
  // and Core API health is based on provider checks
  const adminHealth = systemHealth;
  const adminError = healthError;
  const adminLoading = healthLoading;

  // Extract Core API status from provider checks in system health
  const coreHealth = systemHealth?.checks?.providers ? {
    status: systemHealth.checks.providers.status === 'healthy' ? 'Healthy' : 
            systemHealth.checks.providers.status === 'degraded' ? 'Degraded' : 'Unhealthy',
    checks: [systemHealth.checks.providers]
  } : null;
  const coreError = systemHealth?.checks?.providers?.error ? new Error(systemHealth.checks.providers.error) : null;
  const coreLoading = healthLoading;

  useEffect(() => {
    // Extract message and checks from Core API health response
    let coreApiMessage: string | undefined;
    let coreApiChecks: BackendHealthStatus['coreApiChecks'] | undefined;
    
    if (coreHealth && typeof coreHealth === 'object' && 'checks' in coreHealth && Array.isArray(coreHealth.checks)) {
      coreApiChecks = coreHealth.checks;
      // Look for provider check message
      const providerCheck = coreHealth.checks.find((check: { name: string; description?: string }) => check.name === 'providers');
      if (providerCheck?.description) {
        coreApiMessage = providerCheck.description;
      }
    }
    
    setHealthStatus({
      adminApi: getServiceStatus(adminHealth, adminError, adminLoading),
      coreApi: getServiceStatus(coreHealth, coreError, coreLoading),
      lastChecked: new Date(),
      adminApiDetails: adminHealth,
      coreApiDetails: coreHealth,
      coreApiMessage,
      coreApiChecks,
    });
  }, [adminHealth, adminError, adminLoading, coreHealth, coreError, coreLoading]);

  function getServiceStatus(
    data: unknown, 
    error: unknown, 
    loading: boolean
  ): 'healthy' | 'degraded' | 'unavailable' {
    if (loading) {
      return 'unavailable';
    }

    if (error) {
      const classifiedError = BackendErrorHandler.classifyError(error);
      
      switch (classifiedError.type) {
        case BackendErrorType.CONNECTION_FAILED:
        case BackendErrorType.SERVICE_UNAVAILABLE:
        case BackendErrorType.TIMEOUT:
          return 'unavailable';
        
        case BackendErrorType.RATE_LIMITED:
        case BackendErrorType.INTERNAL_ERROR:
          return 'degraded';
        
        default:
          return 'unavailable';
      }
    }

    if (data && typeof data === 'object' && 'status' in data) {
      // Check the health status from the response
      if (data.status === 'Healthy') {
        return 'healthy';
      } else if (data.status === 'Degraded') {
        return 'degraded';
      } else {
        return 'unavailable';
      }
    }

    return 'unavailable';
  }

  const isHealthy = healthStatus.adminApi === 'healthy' && healthStatus.coreApi === 'healthy';
  const isDegraded = healthStatus.adminApi === 'degraded' || healthStatus.coreApi === 'degraded';
  const isUnavailable = healthStatus.adminApi === 'unavailable' || healthStatus.coreApi === 'unavailable';

  return {
    healthStatus,
    isHealthy,
    isDegraded,
    isUnavailable,
    adminError,
    coreError,
    refetch,
  };
}