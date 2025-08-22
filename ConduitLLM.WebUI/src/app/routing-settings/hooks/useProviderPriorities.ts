'use client';

import { useState, useCallback } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';
import { RoutingConfiguration } from '../types/routing';

export function useProviderPriorities() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);


  const getRoutingConfiguration = useCallback(async (): Promise<RoutingConfiguration> => {
    setIsLoading(true);
    setError(null);
    
    try {
      return withAdminClient(client => 
        client.configuration.getRoutingConfiguration()
      ) as Promise<RoutingConfiguration>;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch routing configuration';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);


  return {
    getRoutingConfiguration,
    isLoading,
    error,
  };
}