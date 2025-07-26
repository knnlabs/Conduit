'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { ProviderPriority, RoutingConfiguration, LoadBalancerHealth } from '../types/routing';

export function useProviderPriorities() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getProviderPriorities = useCallback(async (): Promise<ProviderPriority[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing/providers');
      
      if (!response.ok) {
        throw new Error('Failed to fetch provider priorities');
      }
      
      const providers = await response.json() as ProviderPriority[];
      return providers;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch provider priorities';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateProviderPriorities = useCallback(async (providers: ProviderPriority[]): Promise<ProviderPriority[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing/providers', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(providers),
      });

      if (!response.ok) {
        const result = await response.json() as { error?: string };
        throw new Error(result.error ?? 'Failed to update provider priorities');
      }

      const updatedProviders = await response.json() as ProviderPriority[];

      notifications.show({
        title: 'Success',
        message: 'Provider priorities updated successfully',
        color: 'green',
      });

      return updatedProviders;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update provider priorities';
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

  const getRoutingConfiguration = useCallback(async (): Promise<RoutingConfiguration> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing');
      
      if (!response.ok) {
        throw new Error('Failed to fetch routing configuration');
      }
      
      const config = await response.json() as RoutingConfiguration;
      return config;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch routing configuration';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateRoutingConfiguration = useCallback(async (config: Partial<RoutingConfiguration>): Promise<RoutingConfiguration> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing', {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(config),
      });

      const result = await response.json() as RoutingConfiguration & { error?: string };

      if (!response.ok) {
        throw new Error(result.error ?? 'Failed to update routing configuration');
      }

      notifications.show({
        title: 'Success',
        message: 'Routing configuration updated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update routing configuration';
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

  const getLoadBalancerHealth = useCallback(async (): Promise<LoadBalancerHealth> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing/health');
      
      if (!response.ok) {
        throw new Error('Failed to fetch load balancer health');
      }
      
      const health = await response.json() as LoadBalancerHealth;
      return health;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch load balancer health';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    getProviderPriorities,
    updateProviderPriorities,
    getRoutingConfiguration,
    updateRoutingConfiguration,
    getLoadBalancerHealth,
    isLoading,
    error,
  };
}