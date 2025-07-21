/**
 * Configuration-related models for routing and caching
 */

import type { BaseMetadata } from './metadata';
import type { ExtendedMetadata } from './common-types';

/**
 * Routing configuration for multi-provider setups
 */
export interface RoutingConfiguration {
  /** Enable automatic failover to backup providers */
  enableFailover: boolean;
  
  /** Enable load balancing across providers */
  enableLoadBalancing: boolean;
  
  /** Request timeout in seconds */
  requestTimeoutSeconds: number;
  
  /** Number of retry attempts on failure */
  retryAttempts: number;
  
  /** Delay between retries in milliseconds */
  retryDelayMs: number;
  
  /** Circuit breaker threshold (failures before circuit opens) */
  circuitBreakerThreshold: number;
  
  /** Health check interval in seconds */
  healthCheckIntervalSeconds: number;
  
  /** Load balancing strategy */
  loadBalancingStrategy: 'round-robin' | 'least-connections' | 'weighted' | 'random';
  
  /** Routing rules for conditional routing */
  routingRules: RoutingRule[];
  
  /** Provider priorities for failover */
  providerPriorities: ProviderPriority[];
}

/**
 * DTO for updating routing configuration
 */
export interface UpdateRoutingConfigDto {
  enableFailover?: boolean;
  enableLoadBalancing?: boolean;
  requestTimeoutSeconds?: number;
  retryAttempts?: number;
  retryDelayMs?: number;
  circuitBreakerThreshold?: number;
  healthCheckIntervalSeconds?: number;
  loadBalancingStrategy?: 'round-robin' | 'least-connections' | 'weighted' | 'random';
  routingRules?: RoutingRule[];
  providerPriorities?: ProviderPriority[];
}

/**
 * Routing rule for conditional provider selection
 */
export interface RoutingRule {
  /** Unique identifier for the rule */
  id: string;
  
  /** Name of the routing rule */
  name: string;
  
  /** Condition expression (e.g., "model.startsWith('gpt-4')") */
  condition: string;
  
  /** Target provider when condition matches */
  targetProvider: string;
  
  /** Priority order for rule evaluation */
  priority: number;
  
  /** Whether the rule is active */
  enabled: boolean;
}

/**
 * Provider priority for failover scenarios
 */
export interface ProviderPriority {
  /** Provider name */
  provider: string;
  
  /** Priority (lower number = higher priority) */
  priority: number;
  
  /** Weight for weighted load balancing */
  weight?: number;
}

/**
 * Test result for routing configuration
 */
export interface TestResult {
  /** Whether the test passed */
  success: boolean;
  
  /** Test execution time in milliseconds */
  executionTimeMs: number;
  
  /** Detailed test results */
  results: Array<{
    test: string;
    passed: boolean;
    message: string;
    details?: ExtendedMetadata;
  }>;
  
  /** Any errors encountered */
  errors: string[];
}

/**
 * Load balancer health status
 */
export interface LoadBalancerHealth {
  /** Provider name */
  provider: string;
  
  /** Health status */
  status: 'healthy' | 'degraded' | 'unhealthy';
  
  /** Last health check timestamp */
  lastCheckTime: string;
  
  /** Response time in milliseconds */
  responseTimeMs: number;
  
  /** Success rate percentage */
  successRate: number;
  
  /** Active connections */
  activeConnections: number;
  
  /** Error count in last interval */
  errorCount: number;
}

/**
 * Caching configuration
 */
export interface CachingConfiguration {
  /** Default TTL in seconds */
  defaultTTLSeconds: number;
  
  /** Maximum memory size in MB */
  maxMemorySizeMB: number;
  
  /** Cache eviction policy */
  evictionPolicy: 'lru' | 'lfu' | 'fifo';
  
  /** Whether compression is enabled */
  compressionEnabled: boolean;
  
  /** Whether distributed caching is enabled */
  distributedCacheEnabled: boolean;
  
  /** Redis connection string for distributed cache */
  redisConnectionString?: string;
  
  /** List of cacheable endpoints */
  cacheableEndpoints: string[];
  
  /** Patterns to exclude from caching */
  excludePatterns: string[];
}

/**
 * DTO for updating caching configuration
 */
export interface UpdateCachingConfigDto {
  defaultTTLSeconds?: number;
  maxMemorySizeMB?: number;
  evictionPolicy?: 'lru' | 'lfu' | 'fifo';
  compressionEnabled?: boolean;
  distributedCacheEnabled?: boolean;
  redisConnectionString?: string;
  cacheableEndpoints?: string[];
  excludePatterns?: string[];
}

/**
 * Cache policy for fine-grained control
 */
export interface CachePolicy {
  /** Unique identifier */
  id: string;
  
  /** Policy name */
  name: string;
  
  /** Policy type */
  type: 'endpoint' | 'model' | 'global';
  
  /** Pattern to match (regex or glob) */
  pattern: string;
  
  /** TTL in seconds */
  ttlSeconds: number;
  
  /** Maximum size in MB */
  maxSizeMB?: number;
  
  /** Caching strategy */
  strategy: 'memory' | 'redis' | 'hybrid';
  
  /** Whether the policy is enabled */
  enabled: boolean;
  
  /** Additional metadata */
  metadata?: BaseMetadata;
}

/**
 * DTO for creating a cache policy
 */
export interface CreateCachePolicyDto {
  name: string;
  type: 'endpoint' | 'model' | 'global';
  pattern: string;
  ttlSeconds: number;
  maxSizeMB?: number;
  strategy: 'memory' | 'redis' | 'hybrid';
  enabled?: boolean;
  metadata?: BaseMetadata;
}

/**
 * DTO for updating a cache policy
 */
export interface UpdateCachePolicyDto {
  name?: string;
  pattern?: string;
  ttlSeconds?: number;
  maxSizeMB?: number;
  strategy?: 'memory' | 'redis' | 'hybrid';
  enabled?: boolean;
  metadata?: BaseMetadata;
}

/**
 * Cache region information
 */
export interface CacheRegion {
  /** Region identifier */
  id: string;
  
  /** Region name */
  name: string;
  
  /** Region type */
  type: 'memory' | 'redis' | 'distributed';
  
  /** Region health status */
  status: 'healthy' | 'degraded' | 'offline';
  
  /** Nodes in this region */
  nodes: CacheNode[];
  
  /** Region statistics */
  statistics: {
    hitRate: number;
    missRate: number;
    evictionRate: number;
    memoryUsageMB: number;
    itemCount: number;
  };
}

/**
 * Cache node information
 */
export interface CacheNode {
  /** Node identifier */
  id: string;
  
  /** Node hostname */
  hostname: string;
  
  /** Node status */
  status: 'online' | 'offline' | 'maintenance';
  
  /** Memory usage in MB */
  memoryUsageMB: number;
  
  /** Number of cached items */
  itemCount: number;
}

/**
 * Result of cache clearing operation
 */
export interface ClearCacheResult {
  /** Whether the operation was successful */
  success: boolean;
  
  /** Number of items cleared */
  itemsCleared: number;
  
  /** Memory freed in MB */
  memoryFreedMB: number;
  
  /** Time taken in milliseconds */
  executionTimeMs: number;
  
  /** Any errors encountered */
  errors?: string[];
}

/**
 * Cache statistics
 */
export interface CacheStatistics {
  /** Global cache statistics */
  global: {
    totalHits: number;
    totalMisses: number;
    hitRate: number;
    memoryUsageMB: number;
    itemCount: number;
  };
  
  /** Statistics by region */
  byRegion: Record<string, RegionStatistics>;
  
  /** Statistics by endpoint */
  byEndpoint: Record<string, EndpointStatistics>;
  
  /** Trend data */
  trends: {
    hourly: StatisticPoint[];
    daily: StatisticPoint[];
  };
}

/**
 * Region-specific statistics
 */
export interface RegionStatistics {
  hits: number;
  misses: number;
  hitRate: number;
  memoryUsageMB: number;
  itemCount: number;
  evictions: number;
}

/**
 * Endpoint-specific statistics
 */
export interface EndpointStatistics {
  hits: number;
  misses: number;
  hitRate: number;
  averageResponseTimeMs: number;
  cachedResponseSizeKB: number;
}

/**
 * Point in time statistic
 */
export interface StatisticPoint {
  timestamp: string;
  hitRate: number;
  requestCount: number;
  memoryUsageMB: number;
}