import { SettingsService } from './SettingsService';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  RoutingConfiguration,
  UpdateRoutingConfigDto,
  TestResult,
  LoadBalancerHealth,
  CachingConfiguration,
  UpdateCachingConfigDto,
  CachePolicy,
  CreateCachePolicyDto,
  UpdateCachePolicyDto,
  CacheRegion,
  ClearCacheResult,
  CacheStatistics,
} from '../models/configuration';
import {
  RoutingConfigDto,
  UpdateRoutingConfigDto as UpdateExtendedRoutingConfigDto,
  RoutingRule,
  CreateRoutingRuleDto,
  UpdateRoutingRuleDto,
  LoadBalancerHealthDto,
} from '../models/configurationExtended';
import { ValidationError } from '../utils/errors';
import { z } from 'zod';

/**
 * Schema for routing configuration updates
 */
const updateRoutingSchema = z.object({
  enableFailover: z.boolean().optional(),
  enableLoadBalancing: z.boolean().optional(),
  requestTimeoutSeconds: z.number().positive().optional(),
  retryAttempts: z.number().nonnegative().optional(),
  retryDelayMs: z.number().nonnegative().optional(),
  circuitBreakerThreshold: z.number().positive().optional(),
  healthCheckIntervalSeconds: z.number().positive().optional(),
  loadBalancingStrategy: z.enum(['round-robin', 'least-connections', 'weighted', 'random']).optional(),
  routingRules: z.array(z.object({
    id: z.string(),
    name: z.string(),
    condition: z.string(),
    targetProvider: z.string(),
    priority: z.number(),
    enabled: z.boolean(),
  })).optional(),
  providerPriorities: z.array(z.object({
    provider: z.string(),
    priority: z.number(),
    weight: z.number().optional(),
  })).optional(),
});

/**
 * Schema for caching configuration updates
 */
const updateCachingSchema = z.object({
  defaultTTLSeconds: z.number().positive().optional(),
  maxMemorySizeMB: z.number().positive().optional(),
  evictionPolicy: z.enum(['lru', 'lfu', 'fifo']).optional(),
  compressionEnabled: z.boolean().optional(),
  distributedCacheEnabled: z.boolean().optional(),
  redisConnectionString: z.string().optional(),
  cacheableEndpoints: z.array(z.string()).optional(),
  excludePatterns: z.array(z.string()).optional(),
});

/**
 * Schema for creating cache policies
 */
const createCachePolicySchema = z.object({
  name: z.string().min(1),
  type: z.enum(['endpoint', 'model', 'global']),
  pattern: z.string().min(1),
  ttlSeconds: z.number().positive(),
  maxSizeMB: z.number().positive().optional(),
  strategy: z.enum(['memory', 'redis', 'hybrid']),
  enabled: z.boolean().optional(),
  metadata: z.record(z.any()).optional(),
});

/**
 * Schema for extended routing configuration updates
 */
const updateExtendedRoutingSchema = z.object({
  defaultStrategy: z.enum(['round_robin', 'least_latency', 'cost_optimized', 'priority']).optional(),
  fallbackEnabled: z.boolean().optional(),
  retryPolicy: z.object({
    maxAttempts: z.number().min(0).optional(),
    initialDelayMs: z.number().min(0).optional(),
    maxDelayMs: z.number().min(0).optional(),
    backoffMultiplier: z.number().positive().optional(),
    retryableStatuses: z.array(z.number()).optional(),
  }).optional(),
  timeoutMs: z.number().positive().optional(),
  maxConcurrentRequests: z.number().positive().optional(),
});

/**
 * Schema for creating routing rules
 */
const createRoutingRuleSchema = z.object({
  name: z.string().min(1),
  priority: z.number().optional(),
  conditions: z.array(z.object({
    type: z.enum(['model', 'header', 'body', 'time', 'load']),
    field: z.string().optional(),
    operator: z.enum(['equals', 'contains', 'regex', 'gt', 'lt', 'between']),
    value: z.any(),
  })),
  actions: z.array(z.object({
    type: z.enum(['route', 'transform', 'cache', 'rate_limit', 'log']),
    target: z.string().optional(),
    parameters: z.record(z.any()).optional(),
  })),
  enabled: z.boolean().optional(),
});

/**
 * Enhanced configuration service with routing and caching management
 */
export class ConfigurationService extends SettingsService {
  /**
   * Routing configuration management
   */
  routing = {
    /**
     * Get current routing configuration
     */
    get: async (): Promise<RoutingConfiguration> => {
      return this.withCache(
        ENDPOINTS.CONFIGURATION.ROUTING,
        () => this.get<RoutingConfiguration>(ENDPOINTS.CONFIGURATION.ROUTING),
        CACHE_TTL.SHORT
      );
    },

    /**
     * Update routing configuration
     */
    update: async (config: UpdateRoutingConfigDto): Promise<RoutingConfiguration> => {
      const parsed = updateRoutingSchema.safeParse(config);
      if (!parsed.success) {
        throw new ValidationError('Invalid routing configuration', {
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }

      const response = await this.put<RoutingConfiguration>(
        ENDPOINTS.CONFIGURATION.ROUTING,
        parsed.data
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Test routing configuration
     */
    testConfiguration: async (config: RoutingConfiguration): Promise<TestResult> => {
      const response = await this.post<TestResult>(
        ENDPOINTS.CONFIGURATION.ROUTING_TEST,
        config
      );
      return response;
    },

    /**
     * Get load balancer health status
     */
    getLoadBalancerHealth: async (): Promise<LoadBalancerHealth[]> => {
      return this.withCache(
        ENDPOINTS.CONFIGURATION.LOAD_BALANCER_HEALTH,
        () => this.get<LoadBalancerHealth[]>(ENDPOINTS.CONFIGURATION.LOAD_BALANCER_HEALTH),
        CACHE_TTL.SHORT
      );
    },
  };

  /**
   * Caching configuration management
   */
  caching = {
    /**
     * Get current caching configuration
     */
    get: async (): Promise<CachingConfiguration> => {
      return this.withCache(
        ENDPOINTS.CONFIGURATION.CACHING,
        () => this.get<CachingConfiguration>(ENDPOINTS.CONFIGURATION.CACHING),
        CACHE_TTL.MEDIUM
      );
    },

    /**
     * Update caching configuration
     */
    update: async (config: UpdateCachingConfigDto): Promise<CachingConfiguration> => {
      const parsed = updateCachingSchema.safeParse(config);
      if (!parsed.success) {
        throw new ValidationError('Invalid caching configuration', {
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }

      const response = await this.put<CachingConfiguration>(
        ENDPOINTS.CONFIGURATION.CACHING,
        parsed.data
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Get all cache policies
     */
    getPolicies: async (): Promise<CachePolicy[]> => {
      return this.withCache(
        ENDPOINTS.CONFIGURATION.CACHE_POLICIES,
        () => this.get<CachePolicy[]>(ENDPOINTS.CONFIGURATION.CACHE_POLICIES),
        CACHE_TTL.MEDIUM
      );
    },

    /**
     * Create a new cache policy
     */
    createPolicy: async (policy: CreateCachePolicyDto): Promise<CachePolicy> => {
      const parsed = createCachePolicySchema.safeParse(policy);
      if (!parsed.success) {
        throw new ValidationError('Invalid cache policy', {
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }

      const response = await this.post<CachePolicy>(
        ENDPOINTS.CONFIGURATION.CACHE_POLICIES,
        parsed.data
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Update an existing cache policy
     */
    updatePolicy: async (id: string, policy: UpdateCachePolicyDto): Promise<CachePolicy> => {
      if (!id || id.trim() === '') {
        throw new ValidationError('Policy ID is required');
      }

      const response = await this.put<CachePolicy>(
        ENDPOINTS.CONFIGURATION.CACHE_POLICY_BY_ID(id),
        policy
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Delete a cache policy
     */
    deletePolicy: async (id: string): Promise<void> => {
      if (!id || id.trim() === '') {
        throw new ValidationError('Policy ID is required');
      }

      await this.delete(ENDPOINTS.CONFIGURATION.CACHE_POLICY_BY_ID(id));
      await this.invalidateConfigurationCache();
    },

    /**
     * Get all cache regions
     */
    getRegions: async (): Promise<CacheRegion[]> => {
      return this.withCache(
        ENDPOINTS.CONFIGURATION.CACHE_REGIONS,
        () => this.get<CacheRegion[]>(ENDPOINTS.CONFIGURATION.CACHE_REGIONS),
        CACHE_TTL.SHORT
      );
    },

    /**
     * Clear cache for a specific region
     */
    clearCache: async (regionId: string): Promise<ClearCacheResult> => {
      if (!regionId || regionId.trim() === '') {
        throw new ValidationError('Region ID is required');
      }

      const response = await this.post<ClearCacheResult>(
        ENDPOINTS.CONFIGURATION.CACHE_CLEAR(regionId),
        {}
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Get cache statistics
     */
    getCacheStatistics: async (): Promise<CacheStatistics> => {
      return this.withCache(
        ENDPOINTS.CONFIGURATION.CACHE_STATISTICS,
        () => this.get<CacheStatistics>(ENDPOINTS.CONFIGURATION.CACHE_STATISTICS),
        CACHE_TTL.SHORT
      );
    },
  };

  /**
   * Extended routing configuration management
   */
  extendedRouting = {
    /**
     * Get extended routing configuration
     */
    get: async (): Promise<RoutingConfigDto> => {
      return this.withCache(
        'extended-routing-config',
        () => this.get<RoutingConfigDto>('/api/config/routing'),
        CACHE_TTL.SHORT
      );
    },

    /**
     * Update extended routing configuration
     */
    update: async (config: UpdateExtendedRoutingConfigDto): Promise<RoutingConfigDto> => {
      const parsed = updateExtendedRoutingSchema.safeParse(config);
      if (!parsed.success) {
        throw new ValidationError('Invalid extended routing configuration', {
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }

      const response = await this.put<RoutingConfigDto>(
        '/api/config/routing',
        parsed.data
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Get all routing rules
     */
    getRules: async (): Promise<RoutingRule[]> => {
      return this.withCache(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        () => this.get<RoutingRule[]>(ENDPOINTS.CONFIGURATION.ROUTING_RULES),
        CACHE_TTL.SHORT
      );
    },

    /**
     * Create a new routing rule
     */
    createRule: async (rule: CreateRoutingRuleDto): Promise<RoutingRule> => {
      const parsed = createRoutingRuleSchema.safeParse(rule);
      if (!parsed.success) {
        throw new ValidationError('Invalid routing rule', {
          validationErrors: parsed.error.errors,
          issues: parsed.error.format()
        });
      }

      const response = await this.post<RoutingRule>(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        parsed.data
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Update an existing routing rule
     */
    updateRule: async (id: string, rule: UpdateRoutingRuleDto): Promise<RoutingRule> => {
      if (!id || id.trim() === '') {
        throw new ValidationError('Rule ID is required');
      }

      const response = await this.put<RoutingRule>(
        ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID(id),
        rule
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Delete a routing rule
     */
    deleteRule: async (id: string): Promise<void> => {
      if (!id || id.trim() === '') {
        throw new ValidationError('Rule ID is required');
      }

      await this.delete(ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID(id));
      await this.invalidateConfigurationCache();
    },

    /**
     * Bulk update routing rules
     */
    bulkUpdateRules: async (rules: RoutingRule[]): Promise<RoutingRule[]> => {
      const response = await this.put<RoutingRule[]>(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        { rules }
      );
      
      await this.invalidateConfigurationCache();
      return response;
    },

    /**
     * Get extended load balancer health
     */
    getLoadBalancerHealthExtended: async (): Promise<LoadBalancerHealthDto> => {
      return this.withCache(
        'extended-load-balancer-health',
        () => this.get<LoadBalancerHealthDto>('/api/config/loadbalancer/health'),
        CACHE_TTL.SHORT
      );
    },
  };

  /**
   * Real-time subscription support (to be implemented with SignalR)
   */
  subscriptions = {
    /**
     * Subscribe to routing status updates
     * @param callback Function to call when status updates
     * @returns Unsubscribe function
     */
    subscribeToRoutingStatus: (callback: (status: any) => void): (() => void) => {
      // TODO: Implement with SignalR
      console.warn('Real-time subscriptions not yet implemented');
      return () => {};
    },

    /**
     * Subscribe to health status updates
     * @param callback Function to call when health updates
     * @returns Unsubscribe function
     */
    subscribeToHealthStatus: (callback: (health: LoadBalancerHealthDto) => void): (() => void) => {
      // TODO: Implement with SignalR
      console.warn('Real-time subscriptions not yet implemented');
      return () => {};
    },
  };

  /**
   * Invalidate configuration-related cache entries
   */
  private async invalidateConfigurationCache(): Promise<void> {
    if (!this.cache) return;
    
    // Clear configuration-specific cache entries
    const keysToInvalidate = [
      ENDPOINTS.CONFIGURATION.ROUTING,
      ENDPOINTS.CONFIGURATION.LOAD_BALANCER_HEALTH,
      ENDPOINTS.CONFIGURATION.CACHING,
      ENDPOINTS.CONFIGURATION.CACHE_POLICIES,
      ENDPOINTS.CONFIGURATION.CACHE_REGIONS,
      ENDPOINTS.CONFIGURATION.CACHE_STATISTICS,
      ENDPOINTS.CONFIGURATION.ROUTING_RULES,
      '/api/config/routing',
      'extended-routing-config',
      'extended-load-balancer-health',
    ];

    for (const key of keysToInvalidate) {
      await this.cache.delete(key);
    }
  }
}