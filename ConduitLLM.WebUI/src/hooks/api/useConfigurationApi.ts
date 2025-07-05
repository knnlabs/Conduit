'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';

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
        const client = getAdminClient();
        const [config, health] = await Promise.all([
          client.configuration.routing.get(),
          client.configuration.routing.getLoadBalancerHealth()
        ]);
        
        // TODO: Replace with actual statistics when SDK method is available
        const stats = {
          totalRequests: 0,
          providerMetrics: [] as Array<{
            provider: string;
            requestCount: number;
            successRate: number;
            avgLatency: number;
          }>,
          failoverCount: 0,
        };
        
        // Transform SDK response to match expected format
        const transformedData: RoutingData = {
          timestamp: new Date().toISOString(),
          routingRules: config.routingRules?.map(rule => ({
            id: parseInt(rule.id),
            modelAlias: rule.name,
            providerModelName: rule.targetProvider,
            isEnabled: rule.enabled,
            provider: {
              name: rule.targetProvider,
              isEnabled: true,
            },
          })) || [],
          loadBalancers: health.map(lb => ({
            id: lb.provider,
            name: lb.provider,
            algorithm: config.loadBalancingStrategy || 'round-robin',
            healthCheckInterval: config.healthCheckIntervalSeconds || 30,
            failoverThreshold: config.circuitBreakerThreshold || 3,
            endpoints: [{
              name: lb.provider,
              url: lb.provider,
              weight: 1,
              healthStatus: lb.status,
              responseTime: lb.responseTimeMs,
            }],
          })),
          retryPolicies: [{
            id: '1',
            name: 'Default Retry Policy',
            maxRetries: config.retryAttempts || 3,
            initialDelay: config.retryDelayMs || 1000,
            maxDelay: 10000,
            backoffMultiplier: 2,
            retryableStatusCodes: [429, 500, 502, 503, 504],
          }],
          statistics: {
            totalRequests: stats.totalRequests,
            providerDistribution: stats.providerMetrics.map(pm => ({
              provider: pm.provider,
              requestCount: pm.requestCount,
              successRate: pm.successRate,
              avgLatency: pm.avgLatency,
            })),
            failoverEvents: stats.failoverCount,
            loadBalancerHealth: health.filter(h => h.status === 'healthy').length / health.length * 100,
          },
          configuration: {
            enableFailover: config.enableFailover,
            enableLoadBalancing: config.enableLoadBalancing,
            requestTimeoutSeconds: config.requestTimeoutSeconds,
            circuitBreakerThreshold: config.circuitBreakerThreshold,
          },
        };
        
        return transformedData;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch routing config');
        throw backendError;
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
        const client = getAdminClient();
        const [config, policies, regions, stats] = await Promise.all([
          client.configuration.caching.get(),
          client.configuration.caching.getPolicies(),
          client.configuration.caching.getRegions(),
          client.configuration.caching.getCacheStatistics()
        ]);
        
        // Transform SDK response to match expected format
        const transformedData: CachingData = {
          timestamp: new Date().toISOString(),
          cachePolicies: policies.map(policy => ({
            id: policy.id,
            name: policy.name,
            type: policy.type,
            ttl: policy.ttlSeconds,
            maxSize: policy.maxSizeMB || 100,
            strategy: policy.strategy,
            enabled: policy.enabled,
            description: policy.pattern,
          })),
          cacheRegions: regions.map(region => ({
            id: region.id,
            name: region.name,
            type: region.type,
            status: region.status,
            nodes: region.nodes.length,
            metrics: {
              size: `${region.statistics.memoryUsageMB} MB`,
              items: region.statistics.itemCount,
              hitRate: region.statistics.hitRate,
              missRate: region.statistics.missRate,
              evictionRate: region.statistics.evictionRate,
            },
          })),
          statistics: {
            totalHits: stats.global.totalHits,
            totalMisses: stats.global.totalMisses,
            hitRate: stats.global.hitRate,
            avgResponseTime: {
              withCache: 10, // TODO: Get from actual data when available
              withoutCache: 100, // TODO: Get from actual data when available
            },
            memoryUsage: {
              current: `${stats.global.memoryUsageMB} MB`,
              peak: `${stats.global.memoryUsageMB} MB`, // TODO: Get peak from actual data
              limit: `${config.maxMemorySizeMB} MB`,
            },
            topCachedItems: [], // TODO: Get from actual data when available
          },
          configuration: {
            defaultTTL: config.defaultTTLSeconds,
            maxMemorySize: `${config.maxMemorySizeMB} MB`,
            evictionPolicy: config.evictionPolicy,
            compressionEnabled: config.compressionEnabled,
            redisConnectionString: config.redisConnectionString || null,
          },
        };
        
        return transformedData;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch caching config');
        throw backendError;
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
        const client = getAdminClient();
        const result = await client.configuration.routing.update({
          enableFailover: data.enableFailover,
          enableLoadBalancing: data.enableLoadBalancing,
          requestTimeoutSeconds: data.requestTimeoutSeconds,
          circuitBreakerThreshold: data.circuitBreakerThreshold,
        });
        return result;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to update routing config');
        throw backendError;
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
        const client = getAdminClient();
        const result = await client.configuration.caching.update({
          defaultTTLSeconds: data.defaultTTLSeconds,
          maxMemorySizeMB: parseInt(data.maxMemorySize.replace(/[^0-9]/g, '')),
          evictionPolicy: data.evictionPolicy as 'lru' | 'lfu' | 'fifo',
          compressionEnabled: data.enableCompression,
        });
        
        // TODO: Implement clearAll when SDK method is available
        if (data.clearAllCaches) {
          console.warn('Clear all caches not yet implemented in SDK');
        }
        
        return result;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to update caching config');
        throw backendError;
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
        const client = getAdminClient();
        const result = await client.configuration.caching.clearCache(cacheId);
        return result;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to clear cache');
        throw backendError;
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