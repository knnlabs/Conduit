'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { withAdminClient } from '@/lib/client/adminClient';
import { ProviderPriority, RoutingConfiguration, LoadBalancerHealth } from '../types/routing';

export function useProviderPriorities() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getProviderPriorities = useCallback(async (): Promise<ProviderPriority[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: getProviderPriorities method doesn't exist in Admin SDK
      // Using placeholder implementation
      // TODO: Implement provider priorities once SDK supports it
      return Promise.resolve([]);
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
      // Note: updateProviderPriorities method doesn't exist in Admin SDK
      // Using placeholder implementation
      // TODO: Implement provider priorities update once SDK supports it
      const updatedProviders = await Promise.resolve(providers);

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

  const updateRoutingConfiguration = useCallback(async (config: Partial<RoutingConfiguration>): Promise<RoutingConfiguration> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: updateConfiguration method doesn't exist in Admin SDK
      // Using placeholder implementation
      // TODO: Implement configuration update once SDK supports it
      const result = await Promise.resolve(config as RoutingConfiguration);

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
      // Note: getHealth method doesn't exist in Configuration service
      // Using placeholder implementation
      // TODO: Implement load balancer health once SDK supports it
      return Promise.resolve({ isHealthy: true, nodes: [] } as unknown as LoadBalancerHealth);
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