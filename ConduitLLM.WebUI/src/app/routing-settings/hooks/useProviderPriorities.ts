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
      // TODO: Replace with actual API call when backend is implemented
      // Simulating API call with mock data
      await new Promise(resolve => setTimeout(resolve, 300));
      
      const mockProviders: ProviderPriority[] = [
        {
          providerId: 'openai-1',
          providerName: 'OpenAI Premium',
          priority: 1,
          weight: 80,
          isEnabled: true
        },
        {
          providerId: 'anthropic-1',
          providerName: 'Anthropic Claude',
          priority: 2,
          weight: 60,
          isEnabled: true
        },
        {
          providerId: 'openai-2',
          providerName: 'OpenAI Standard',
          priority: 3,
          weight: 40,
          isEnabled: false
        }
      ];

      return mockProviders;
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
      // TODO: Replace with actual API call when backend is implemented
      await new Promise(resolve => setTimeout(resolve, 600));

      notifications.show({
        title: 'Success',
        message: 'Provider priorities updated successfully (mock)',
        color: 'green',
      });

      return providers;
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
      // TODO: Replace with actual API call when backend is implemented
      // Simulating API call with mock data
      await new Promise(resolve => setTimeout(resolve, 400));
      
      const mockConfig: RoutingConfiguration = {
        defaultStrategy: 'round_robin',
        fallbackEnabled: true,
        timeoutMs: 30000,
        maxConcurrentRequests: 100,
        retryPolicy: {
          maxAttempts: 3,
          initialDelayMs: 1000,
          maxDelayMs: 5000,
          backoffMultiplier: 2,
          retryableStatuses: [500, 502, 503, 504]
        }
      };

      return mockConfig;
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

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to update routing configuration');
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
      // TODO: Replace with actual API call when backend is implemented
      // Simulating API call with mock data
      await new Promise(resolve => setTimeout(resolve, 600));
      
      const mockHealth: LoadBalancerHealth = {
        status: 'healthy',
        lastCheck: new Date().toISOString(),
        nodes: [
          {
            id: 'openai-1',
            endpoint: 'api.openai.com',
            status: 'healthy',
            weight: 80,
            totalRequests: 1250,
            avgResponseTime: 850,
            activeConnections: 15,
            lastHealthCheck: new Date().toISOString()
          },
          {
            id: 'anthropic-1',
            endpoint: 'api.anthropic.com',
            status: 'healthy',
            weight: 60,
            totalRequests: 890,
            avgResponseTime: 920,
            activeConnections: 8,
            lastHealthCheck: new Date().toISOString()
          },
          {
            id: 'openai-2',
            endpoint: 'backup.openai.com',
            status: 'draining',
            weight: 40,
            totalRequests: 340,
            avgResponseTime: 1100,
            activeConnections: 2,
            lastHealthCheck: new Date().toISOString()
          }
        ],
        distribution: {
          'openai-1': 45,
          'anthropic-1': 35,
          'openai-2': 20
        }
      };

      return mockHealth;
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