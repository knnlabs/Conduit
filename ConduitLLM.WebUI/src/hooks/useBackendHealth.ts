'use client';

import { useQuery } from '@tanstack/react-query';
import { useState, useEffect } from 'react';
import { BackendErrorHandler, BackendErrorType } from '@/lib/errors/BackendErrorHandler';

export interface BackendHealthStatus {
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  lastChecked: Date;
  adminApiDetails?: any;
  coreApiDetails?: any;
}

export function useBackendHealth() {
  const [healthStatus, setHealthStatus] = useState<BackendHealthStatus>({
    adminApi: 'unavailable',
    coreApi: 'unavailable',
    lastChecked: new Date(),
  });

  const { data: adminHealth, error: adminError, isLoading: adminLoading } = useQuery({
    queryKey: ['backend-health', 'admin'],
    queryFn: async () => {
      try {
        const response = await fetch('/api/admin/system/health', {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
          credentials: 'include', // Include cookies for authentication
        });

        if (!response.ok) {
          throw new Error(`Admin API health check failed: ${response.status}`);
        }

        return await response.json();
      } catch (error: any) {
        const classifiedError = BackendErrorHandler.classifyError(error);
        throw classifiedError;
      }
    },
    refetchInterval: 30000, // Check every 30 seconds
    retry: (failureCount, error: any) => {
      // Don't retry if it's an authentication error
      if (error?.type === BackendErrorType.AUTHENTICATION_FAILED) {
        return false;
      }
      return failureCount < 3;
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });

  const { data: coreHealth, error: coreError, isLoading: coreLoading } = useQuery({
    queryKey: ['backend-health', 'core'],
    queryFn: async () => {
      try {
        // For Core API, we'll check through a simple proxy endpoint
        const response = await fetch('/api/core/health', {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
          credentials: 'include', // Include cookies for authentication
        });

        if (!response.ok) {
          throw new Error(`Core API health check failed: ${response.status}`);
        }

        return await response.json();
      } catch (error: any) {
        const classifiedError = BackendErrorHandler.classifyError(error);
        throw classifiedError;
      }
    },
    refetchInterval: 30000, // Check every 30 seconds
    retry: (failureCount, error: any) => {
      // Don't retry if it's an authentication error
      if (error?.type === BackendErrorType.AUTHENTICATION_FAILED) {
        return false;
      }
      return failureCount < 3;
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });

  useEffect(() => {
    setHealthStatus({
      adminApi: getServiceStatus(adminHealth, adminError, adminLoading),
      coreApi: getServiceStatus(coreHealth, coreError, coreLoading),
      lastChecked: new Date(),
      adminApiDetails: adminHealth,
      coreApiDetails: coreHealth,
    });
  }, [adminHealth, adminError, adminLoading, coreHealth, coreError, coreLoading]);

  function getServiceStatus(
    data: any, 
    error: any, 
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

    if (data) {
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
    refetch: () => {
      // Force refetch both health checks
    },
  };
}