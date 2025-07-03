'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { apiFetch } from '@/lib/utils/fetch-wrapper';

// Query key factory for Configuration API
export const configApiKeys = {
  all: ['config-api'] as const,
  routing: () => [...configApiKeys.all, 'routing'] as const,
  caching: () => [...configApiKeys.all, 'caching'] as const,
} as const;

export interface RoutingRule {
  id: number;
  modelAlias: string;
  providerModelName: string;
  isEnabled: boolean;
  provider: {
    name: string;
    isEnabled: boolean;
  };
}

export interface LoadBalancer {
  id: string;
  name: string;
  algorithm: string;
  healthCheckInterval: number;
  failoverThreshold: number;
  endpoints: Array<{
    name: string;
    url: string;
    weight: number;
    healthStatus: string;
    responseTime: number;
  }>;
}

export interface RetryPolicy {
  id: string;
  name: string;
  maxRetries: number;
  initialDelay: number;
  maxDelay: number;
  backoffMultiplier: number;
  retryableStatusCodes: number[];
}

export interface RoutingStatistics {
  totalRequests: number;
  providerDistribution: Array<{
    provider: string;
    requestCount: number;
    successRate: number;
    avgLatency: number;
  }>;
  failoverEvents: number;
  loadBalancerHealth: number;
}

export interface RoutingConfiguration {
  enableFailover: boolean;
  enableLoadBalancing: boolean;
  requestTimeoutSeconds: number;
  circuitBreakerThreshold: number;
}

export interface RoutingData {
  timestamp: string;
  routingRules: RoutingRule[];
  loadBalancers: LoadBalancer[];
  retryPolicies: RetryPolicy[];
  statistics: RoutingStatistics;
  configuration: RoutingConfiguration;
}

export interface CachePolicy {
  id: string;
  name: string;
  type: string;
  ttl: number;
  maxSize: number;
  strategy: string;
  enabled: boolean;
  description: string;
}

export interface CacheRegion {
  id: string;
  name: string;
  type: string;
  status: string;
  nodes: number;
  metrics: {
    size: string;
    items: number;
    hitRate: number;
    missRate: number;
    evictionRate: number;
  };
}

export interface CacheStatistics {
  totalHits: number;
  totalMisses: number;
  hitRate: number;
  avgResponseTime: {
    withCache: number;
    withoutCache: number;
  };
  memoryUsage: {
    current: string;
    peak: string;
    limit: string;
  };
  topCachedItems: Array<{
    key: string;
    hits: number;
    size: string;
  }>;
}

export interface CachingConfiguration {
  defaultTTL: number;
  maxMemorySize: string;
  evictionPolicy: string;
  compressionEnabled: boolean;
  redisConnectionString: string | null;
}

export interface CachingData {
  timestamp: string;
  cachePolicies: CachePolicy[];
  cacheRegions: CacheRegion[];
  statistics: CacheStatistics;
  configuration: CachingConfiguration;
}

/**
 * Hook to fetch routing configuration
 */
export function useRoutingConfig() {
  return useQuery({
    queryKey: configApiKeys.routing(),
    queryFn: async () => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch('/api/config/routing', {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch routing config: ${response.statusText}`);
        }

        return response.json() as Promise<RoutingData>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch routing config');
        throw error;
      }
    },
    staleTime: 60000, // 1 minute
  });
}

/**
 * Hook to fetch caching configuration
 */
export function useCachingConfig() {
  return useQuery({
    queryKey: configApiKeys.caching(),
    queryFn: async () => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch('/api/config/caching', {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch caching config: ${response.statusText}`);
        }

        return response.json() as Promise<CachingData>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch caching config');
        throw error;
      }
    },
    staleTime: 60000, // 1 minute
  });
}

export interface UpdateRoutingConfigData {
  enableFailover: boolean;
  enableLoadBalancing: boolean;
  requestTimeoutSeconds: number;
  circuitBreakerThreshold: number;
}

/**
 * Hook to update routing configuration
 */
export function useUpdateRoutingConfig() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: UpdateRoutingConfigData) => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch('/api/config/routing', {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(data),
        });

        if (!response.ok) {
          throw new Error(`Failed to update routing config: ${response.statusText}`);
        }

        return response.json();
      } catch (error) {
        reportError(error as Error, 'Failed to update routing config');
        throw error;
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: configApiKeys.routing() });
    },
  });
}

export interface UpdateCachingConfigData {
  defaultTTLSeconds: number;
  maxMemorySize: string;
  evictionPolicy: string;
  enableCompression: boolean;
  clearAllCaches: boolean;
}

/**
 * Hook to update caching configuration
 */
export function useUpdateCachingConfig() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: UpdateCachingConfigData) => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch('/api/config/caching', {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(data),
        });

        if (!response.ok) {
          throw new Error(`Failed to update caching config: ${response.statusText}`);
        }

        return response.json();
      } catch (error) {
        reportError(error as Error, 'Failed to update caching config');
        throw error;
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: configApiKeys.caching() });
    },
  });
}

/**
 * Hook to clear a specific cache
 */
export function useClearCache() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (cacheId: string) => {
      try {
        const client = await getAdminClient();
        const response = await apiFetch(`/api/config/caching/${cacheId}/clear`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
        });

        if (!response.ok) {
          throw new Error(`Failed to clear cache: ${response.statusText}`);
        }

        return response.json();
      } catch (error) {
        reportError(error as Error, 'Failed to clear cache');
        throw error;
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: configApiKeys.caching() });
    },
  });
}

/**
 * Hook to invalidate configuration queries
 */
export function useInvalidateConfig() {
  const queryClient = useQueryClient();
  
  return {
    invalidateAll: () => queryClient.invalidateQueries({ queryKey: configApiKeys.all }),
    invalidateRouting: () => queryClient.invalidateQueries({ queryKey: configApiKeys.routing() }),
    invalidateCaching: () => queryClient.invalidateQueries({ queryKey: configApiKeys.caching() }),
  };
}