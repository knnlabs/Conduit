import { FilterOptions } from './common';

// Extended Routing types from issue #380
export interface RoutingConfigDto {
  defaultStrategy: 'round_robin' | 'least_latency' | 'cost_optimized' | 'priority';
  fallbackEnabled: boolean;
  retryPolicy: RetryPolicy;
  timeoutMs: number;
  maxConcurrentRequests: number;
}

export interface RetryPolicy {
  maxAttempts: number;
  initialDelayMs: number;
  maxDelayMs: number;
  backoffMultiplier: number;
  retryableStatuses: number[];
}

export interface UpdateRoutingConfigDto {
  defaultStrategy?: 'round_robin' | 'least_latency' | 'cost_optimized' | 'priority';
  fallbackEnabled?: boolean;
  retryPolicy?: Partial<RetryPolicy>;
  timeoutMs?: number;
  maxConcurrentRequests?: number;
}

export interface RoutingRule {
  id: string;
  name: string;
  priority: number;
  conditions: RuleCondition[];
  actions: RuleAction[];
  enabled: boolean;
  stats?: {
    matchCount: number;
    lastMatched?: string;
  };
}

export interface RuleCondition {
  type: 'model' | 'header' | 'body' | 'time' | 'load';
  field?: string;
  operator: 'equals' | 'contains' | 'regex' | 'gt' | 'lt' | 'between';
  value: any;
}

export interface RuleAction {
  type: 'route' | 'transform' | 'cache' | 'rate_limit' | 'log';
  target?: string;
  parameters?: Record<string, any>;
}

export interface CreateRoutingRuleDto {
  name: string;
  priority?: number;
  conditions: RuleCondition[];
  actions: RuleAction[];
  enabled?: boolean;
}

export interface UpdateRoutingRuleDto {
  name?: string;
  priority?: number;
  conditions?: RuleCondition[];
  actions?: RuleAction[];
  enabled?: boolean;
}

// Extended Cache types
export interface CacheConfigDto {
  enabled: boolean;
  strategy: 'lru' | 'lfu' | 'ttl' | 'adaptive';
  maxSizeBytes: number;
  defaultTtlSeconds: number;
  rules: CacheRule[];
  redis?: {
    enabled: boolean;
    endpoint: string;
    cluster: boolean;
  };
}

export interface UpdateCacheConfigDto {
  enabled?: boolean;
  strategy?: 'lru' | 'lfu' | 'ttl' | 'adaptive';
  maxSizeBytes?: number;
  defaultTtlSeconds?: number;
  rules?: CacheRule[];
  redis?: {
    enabled?: boolean;
    endpoint?: string;
    cluster?: boolean;
  };
}

export interface CacheRule {
  id: string;
  pattern: string;
  ttlSeconds: number;
  maxSizeBytes?: number;
  conditions?: CacheCondition[];
}

export interface CacheCondition {
  type: 'header' | 'query' | 'body' | 'time';
  field: string;
  operator: 'equals' | 'contains' | 'regex' | 'exists';
  value?: any;
}

export interface CacheClearParams {
  pattern?: string;
  region?: string;
  type?: 'all' | 'expired' | 'pattern';
  force?: boolean;
}

export interface CacheClearResult {
  success: boolean;
  clearedCount: number;
  clearedSizeBytes: number;
  errors?: string[];
}

export interface CacheStatsDto {
  hitRate: number;
  missRate: number;
  evictionRate: number;
  totalRequests: number;
  totalHits: number;
  totalMisses: number;
  currentSizeBytes: number;
  maxSizeBytes: number;
  itemCount: number;
  topKeys: CacheKeyStats[];
}

export interface CacheKeyStats {
  key: string;
  hits: number;
  misses: number;
  sizeBytes: number;
  ttlSeconds: number;
  lastAccessed: string;
}

// Load Balancing types
export interface LoadBalancerConfigDto {
  algorithm: 'round_robin' | 'weighted_round_robin' | 'least_connections' | 'ip_hash' | 'random';
  healthCheck: {
    enabled: boolean;
    intervalSeconds: number;
    timeoutSeconds: number;
    unhealthyThreshold: number;
    healthyThreshold: number;
  };
  weights?: Record<string, number>;
  stickySession?: {
    enabled: boolean;
    cookieName: string;
    ttlSeconds: number;
  };
}

export interface UpdateLoadBalancerConfigDto {
  algorithm?: 'round_robin' | 'weighted_round_robin' | 'least_connections' | 'ip_hash' | 'random';
  healthCheck?: Partial<LoadBalancerConfigDto['healthCheck']>;
  weights?: Record<string, number>;
  stickySession?: Partial<LoadBalancerConfigDto['stickySession']>;
}

export interface LoadBalancerHealthDto {
  status: 'healthy' | 'degraded' | 'unhealthy';
  nodes: LoadBalancerNode[];
  lastCheck: string;
  distribution: Record<string, number>;
}

export interface LoadBalancerNode {
  id: string;
  endpoint: string;
  status: 'healthy' | 'unhealthy' | 'draining';
  weight: number;
  activeConnections: number;
  totalRequests: number;
  avgResponseTime: number;
  lastHealthCheck: string;
}

// Performance Configuration types
export interface PerformanceConfigDto {
  connectionPool: {
    minSize: number;
    maxSize: number;
    acquireTimeoutMs: number;
    idleTimeoutMs: number;
  };
  requestQueue: {
    maxSize: number;
    timeout: number;
    priorityLevels: number;
  };
  circuitBreaker: {
    enabled: boolean;
    failureThreshold: number;
    resetTimeoutMs: number;
    halfOpenRequests: number;
  };
  rateLimiter: {
    enabled: boolean;
    requestsPerSecond: number;
    burstSize: number;
  };
}

export interface UpdatePerformanceConfigDto {
  connectionPool?: Partial<PerformanceConfigDto['connectionPool']>;
  requestQueue?: Partial<PerformanceConfigDto['requestQueue']>;
  circuitBreaker?: Partial<PerformanceConfigDto['circuitBreaker']>;
  rateLimiter?: Partial<PerformanceConfigDto['rateLimiter']>;
}

export interface PerformanceTestParams {
  duration: number;
  concurrentUsers: number;
  requestsPerSecond: number;
  models: string[];
  payloadSize: 'small' | 'medium' | 'large';
}

export interface PerformanceTestResult {
  summary: {
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    avgLatency: number;
    p50Latency: number;
    p95Latency: number;
    p99Latency: number;
    throughput: number;
  };
  timeline: PerformanceDataPoint[];
  errors: ErrorSummary[];
  recommendations: string[];
}

export interface PerformanceDataPoint {
  timestamp: string;
  requestsPerSecond: number;
  avgLatency: number;
  errorRate: number;
  activeConnections: number;
}

export interface ErrorSummary {
  type: string;
  count: number;
  message: string;
  firstOccurred: string;
  lastOccurred: string;
}

// Feature Flag types
export interface FeatureFlag {
  key: string;
  name: string;
  description?: string;
  enabled: boolean;
  rolloutPercentage?: number;
  conditions?: FeatureFlagCondition[];
  metadata?: Record<string, any>;
  lastModified: string;
}

export interface FeatureFlagCondition {
  type: 'user' | 'key' | 'environment' | 'custom';
  field: string;
  operator: 'in' | 'not_in' | 'equals' | 'regex';
  values: any[];
}

export interface UpdateFeatureFlagDto {
  name?: string;
  description?: string;
  enabled?: boolean;
  rolloutPercentage?: number;
  conditions?: FeatureFlagCondition[];
  metadata?: Record<string, any>;
}