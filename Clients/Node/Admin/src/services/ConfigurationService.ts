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
    ];

    for (const key of keysToInvalidate) {
      await this.cache.delete(key);
    }
  }
}