'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import type { ErrorResponse } from '@knn_labs/conduit-common';

interface RoutingSettings {
  loadBalancingStrategy: 'round-robin' | 'least-latency' | 'weighted';
  fallbackEnabled: boolean;
  maxRetries: number;
  timeoutMs: number;
  healthCheckIntervalSeconds: number;
}


export function useConfigurationApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getRoutingSettings = useCallback(async (): Promise<RoutingSettings> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing', {
        method: 'GET',
      });

      if (!response.ok) {
        const errorData = await response.json() as ErrorResponse;
        throw new Error(errorData.error ?? errorData.message ?? 'Failed to fetch routing settings');
      }

      const result = await response.json() as RoutingSettings;
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch routing settings';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateRoutingSettings = useCallback(async (settings: Partial<RoutingSettings>): Promise<RoutingSettings> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing', {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(settings),
      });

      if (!response.ok) {
        const errorData = await response.json() as ErrorResponse;
        throw new Error(errorData.error ?? errorData.message ?? 'Failed to update routing settings');
      }

      const result = await response.json() as RoutingSettings;

      notifications.show({
        title: 'Success',
        message: 'Routing settings updated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update routing settings';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);


  return {
    getRoutingSettings,
    updateRoutingSettings,
    isLoading,
    error,
  };
}