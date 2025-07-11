'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';

interface RoutingSettings {
  loadBalancingStrategy: 'round-robin' | 'least-latency' | 'weighted';
  fallbackEnabled: boolean;
  maxRetries: number;
  timeoutMs: number;
  healthCheckIntervalSeconds: number;
}

interface CachingSettings {
  enabled: boolean;
  ttlSeconds: number;
  maxSizeMb: number;
  strategy: 'lru' | 'lfu' | 'fifo';
  cacheModels: string[];
  cacheEndpoints: string[];
}

interface CacheStats {
  hits: number;
  misses: number;
  size: number;
  itemCount: number;
  hitRate: number;
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

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to fetch routing settings');
      }

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

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to update routing settings');
      }

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

  const getCachingSettings = useCallback(async (): Promise<CachingSettings> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/caching', {
        method: 'GET',
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to fetch caching settings');
      }

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch caching settings';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateCachingSettings = useCallback(async (settings: Partial<CachingSettings>): Promise<CachingSettings> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/caching', {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(settings),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to update caching settings');
      }

      notifications.show({
        title: 'Success',
        message: 'Caching settings updated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update caching settings';
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

  const getCacheStats = useCallback(async (cacheId?: string): Promise<CacheStats> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const url = cacheId 
        ? `/api/config/caching/${cacheId}/stats`
        : '/api/config/caching/stats';
        
      const response = await fetch(url, {
        method: 'GET',
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to fetch cache stats');
      }

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch cache stats';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const clearCache = useCallback(async (cacheId?: string): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const url = cacheId 
        ? `/api/config/caching/${cacheId}/clear`
        : '/api/config/caching/clear';
        
      const response = await fetch(url, {
        method: 'POST',
      });

      if (!response.ok) {
        const result = await response.json();
        throw new Error(result.error || 'Failed to clear cache');
      }

      notifications.show({
        title: 'Success',
        message: cacheId ? `Cache ${cacheId} cleared successfully` : 'All caches cleared successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to clear cache';
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
    getCachingSettings,
    updateCachingSettings,
    getCacheStats,
    clearCache,
    isLoading,
    error,
  };
}