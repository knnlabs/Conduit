import { useState, useEffect, useCallback } from 'react';
import { processHealthStatus, createErrorHealthStatus, type ProcessedHealthStatus } from '@/lib/utils/health-status';
import { getClientCoreClient } from '@/lib/client/coreClient';

export interface BackendHealthState {
  health: ProcessedHealthStatus | null;
  isLoading: boolean;
  error: string | null;
  lastChecked: Date | null;
}

export interface UseBackendHealthOptions {
  /** Polling interval in milliseconds (default: 30000 = 30 seconds) */
  pollInterval?: number;
  /** Whether to start polling immediately (default: true) */
  autoStart?: boolean;
  /** Whether to include SignalR health check (default: true) */
  includeSignalR?: boolean;
}

export function useBackendHealth(options: UseBackendHealthOptions = {}) {
  const {
    pollInterval = 30000,
    autoStart = true,
    includeSignalR = true
  } = options;

  const [state, setState] = useState<BackendHealthState>({
    health: null,
    isLoading: false,
    error: null,
    lastChecked: null
  });

  const checkHealth = useCallback(async () => {
    setState(prev => ({ ...prev, isLoading: true, error: null }));

    try {
      // For client-side health checking, we'll use a simplified approach
      // Get basic health status from a public endpoint
      const healthResponse = await fetch('/api/health/status');
      if (!healthResponse.ok) {
        throw new Error('Failed to fetch health status');
      }
      
      // For now we don't need the response data as we use a basic health status
      await healthResponse.json();
      
      // Try to get SignalR status from Core API if enabled
      let signalrStatus: 'healthy' | 'degraded' | 'unavailable' = 'unavailable';
      
      if (includeSignalR) {
        try {
          const coreClient = await getClientCoreClient();
          const coreHealth = await coreClient.health.checkReady();
          
          // Find SignalR health check in the response
          const signalrCheck = coreHealth.checks?.find(
            (check) => check.name?.toLowerCase().includes('signalr')
          );
          
          if (signalrCheck) {
            // Normalize SignalR status to match our type
            const checkStatus = signalrCheck.status;
            if (checkStatus === 'healthy') signalrStatus = 'healthy';
            else if (checkStatus === 'degraded') signalrStatus = 'degraded';
            else signalrStatus = 'unavailable';
          }
        } catch (error) {
          console.warn('Failed to get SignalR health status:', error);
          // Keep signalrStatus as 'unavailable'
        }
      }

      // Create a basic health status object that matches HealthStatusDto structure
      const basicHealthStatus: import('@knn_labs/conduit-admin-client').HealthStatusDto = {
        status: 'healthy',
        timestamp: new Date().toISOString(),
        checks: {},
        totalDuration: 0
      };

      // Process the health data
      const processedHealth = processHealthStatus(basicHealthStatus, signalrStatus);

      setState({
        health: processedHealth,
        isLoading: false,
        error: null,
        lastChecked: new Date()
      });

    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      const errorHealth = createErrorHealthStatus(errorMessage);
      
      setState({
        health: errorHealth,
        isLoading: false,
        error: errorMessage,
        lastChecked: new Date()
      });
    }
  }, [includeSignalR]);

  // Manual refresh function
  const refresh = useCallback(() => {
    void checkHealth();
  }, [checkHealth]);

  // Polling effect
  useEffect(() => {
    if (!autoStart) return;

    // Initial check
    void checkHealth();

    // Set up polling
    const interval = setInterval(() => {
      void checkHealth();
    }, pollInterval);

    return () => clearInterval(interval);
  }, [checkHealth, autoStart, pollInterval]);

  return {
    ...state,
    refresh,
    checkHealth
  };
}

/**
 * Hook for backend health with default polling
 */
export function useBackendHealthPolling() {
  return useBackendHealth({
    pollInterval: 30000,
    autoStart: true,
    includeSignalR: true
  });
}

/**
 * Hook for backend health without polling (manual only)
 */
export function useBackendHealthManual() {
  return useBackendHealth({
    autoStart: false
  });
}