'use client';

import { useState, useCallback } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';

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
      return withAdminClient(client => 
        client.configuration.getRoutingConfiguration()
      ) as Promise<RoutingSettings>;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch routing settings';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);



  return {
    getRoutingSettings,
    isLoading,
    error,
  };
}