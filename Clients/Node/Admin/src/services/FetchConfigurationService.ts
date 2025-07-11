import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  RoutingConfigDto,
  UpdateRoutingConfigDto as ExtendedUpdateRoutingConfigDto,
  RoutingRule as ExtendedRoutingRule,
  CreateRoutingRuleDto,
  UpdateRoutingRuleDto,
  CacheConfigDto,
  UpdateCacheConfigDto,
  CacheClearParams,
  CacheClearResult,
  CacheStatsDto,
  LoadBalancerConfigDto,
  UpdateLoadBalancerConfigDto,
  LoadBalancerHealthDto,
  PerformanceConfigDto,
  UpdatePerformanceConfigDto,
  PerformanceTestParams,
  PerformanceTestResult,
  FeatureFlag,
  UpdateFeatureFlagDto,
} from '../models/configurationExtended';
import type {
  RoutingConfiguration,
  UpdateRoutingConfigDto,
  CachingConfiguration,
  CachePolicy,
  CreateCachePolicyDto,
  UpdateCachePolicyDto,
  CacheStatistics,
  TestResult,
  LoadBalancerHealth,
  ClearCacheResult,
} from '../models/configuration';

/**
 * Type-safe Configuration service using native fetch
 */
export class FetchConfigurationService {
  constructor(private readonly client: FetchBaseApiClient) {}

  // Routing Configuration

  /**
   * Get routing configuration
   */
  async getRoutingConfig(config?: RequestConfig): Promise<RoutingConfigDto> {
    return this.client['get']<RoutingConfigDto>(
      '/api/config/routing',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get routing configuration (using existing endpoint)
   */
  async getRoutingConfiguration(config?: RequestConfig): Promise<RoutingConfiguration> {
    return this.client['get']<RoutingConfiguration>(
      ENDPOINTS.CONFIGURATION.ROUTING,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update routing configuration
   */
  async updateRoutingConfig(
    data: ExtendedUpdateRoutingConfigDto,
    config?: RequestConfig
  ): Promise<RoutingConfigDto> {
    return this.client['put']<RoutingConfigDto, ExtendedUpdateRoutingConfigDto>(
      '/api/config/routing',
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update routing configuration (using existing endpoint)
   */
  async updateRoutingConfiguration(
    data: UpdateRoutingConfigDto,
    config?: RequestConfig
  ): Promise<RoutingConfiguration> {
    return this.client['put']<RoutingConfiguration, UpdateRoutingConfigDto>(
      ENDPOINTS.CONFIGURATION.ROUTING,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Test routing configuration
   */
  async testRoutingConfig(config?: RequestConfig): Promise<TestResult> {
    return this.client['post']<TestResult>(
      ENDPOINTS.CONFIGURATION.ROUTING_TEST,
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get routing rules
   */
  async getRoutingRules(config?: RequestConfig): Promise<ExtendedRoutingRule[]> {
    return this.client['get']<ExtendedRoutingRule[]>(
      ENDPOINTS.CONFIGURATION.ROUTING_RULES,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create routing rule
   */
  async createRoutingRule(
    rule: CreateRoutingRuleDto,
    config?: RequestConfig
  ): Promise<ExtendedRoutingRule> {
    return this.client['post']<ExtendedRoutingRule, CreateRoutingRuleDto>(
      ENDPOINTS.CONFIGURATION.ROUTING_RULES,
      rule,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update routing rule
   */
  async updateRoutingRule(
    id: string,
    rule: UpdateRoutingRuleDto,
    config?: RequestConfig
  ): Promise<ExtendedRoutingRule> {
    return this.client['put']<ExtendedRoutingRule, UpdateRoutingRuleDto>(
      ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID(id),
      rule,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete routing rule
   */
  async deleteRoutingRule(id: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Caching Configuration

  /**
   * Get cache configuration
   */
  async getCacheConfig(config?: RequestConfig): Promise<CacheConfigDto> {
    return this.client['get']<CacheConfigDto>(
      ENDPOINTS.CONFIGURATION.CACHE_CONFIG,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get caching configuration (using existing endpoint)
   */
  async getCachingConfiguration(config?: RequestConfig): Promise<CachingConfiguration> {
    return this.client['get']<CachingConfiguration>(
      ENDPOINTS.CONFIGURATION.CACHING,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update cache configuration
   */
  async updateCacheConfig(
    data: UpdateCacheConfigDto,
    config?: RequestConfig
  ): Promise<CacheConfigDto> {
    return this.client['put']<CacheConfigDto, UpdateCacheConfigDto>(
      ENDPOINTS.CONFIGURATION.CACHE_CONFIG,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update caching configuration (using existing endpoint)
   */
  async updateCachingConfiguration(
    data: UpdateCacheConfigDto,
    config?: RequestConfig
  ): Promise<CachingConfiguration> {
    return this.client['put']<CachingConfiguration, UpdateCacheConfigDto>(
      ENDPOINTS.CONFIGURATION.CACHING,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Clear cache
   */
  async clearCache(params?: CacheClearParams, config?: RequestConfig): Promise<CacheClearResult> {
    return this.client['post']<CacheClearResult, CacheClearParams>(
      '/api/config/cache/clear',
      params || {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Clear cache by region (using existing endpoint)
   */
  async clearCacheByRegion(regionId: string, config?: RequestConfig): Promise<ClearCacheResult> {
    return this.client['post']<ClearCacheResult>(
      ENDPOINTS.CONFIGURATION.CACHE_CLEAR(regionId),
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cache statistics
   */
  async getCacheStats(config?: RequestConfig): Promise<CacheStatsDto> {
    return this.client['get']<CacheStatsDto>(
      ENDPOINTS.CONFIGURATION.CACHE_STATS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cache statistics (using existing endpoint)
   */
  async getCacheStatistics(config?: RequestConfig): Promise<CacheStatistics> {
    return this.client['get']<CacheStatistics>(
      ENDPOINTS.CONFIGURATION.CACHE_STATISTICS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cache policies
   */
  async getCachePolicies(config?: RequestConfig): Promise<CachePolicy[]> {
    return this.client['get']<CachePolicy[]>(
      ENDPOINTS.CONFIGURATION.CACHE_POLICIES,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create cache policy
   */
  async createCachePolicy(
    policy: CreateCachePolicyDto,
    config?: RequestConfig
  ): Promise<CachePolicy> {
    return this.client['post']<CachePolicy, CreateCachePolicyDto>(
      ENDPOINTS.CONFIGURATION.CACHE_POLICIES,
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update cache policy
   */
  async updateCachePolicy(
    id: string,
    policy: UpdateCachePolicyDto,
    config?: RequestConfig
  ): Promise<CachePolicy> {
    return this.client['put']<CachePolicy, UpdateCachePolicyDto>(
      ENDPOINTS.CONFIGURATION.CACHE_POLICY_BY_ID(id),
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete cache policy
   */
  async deleteCachePolicy(id: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.CONFIGURATION.CACHE_POLICY_BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Load Balancing

  /**
   * Get load balancer configuration
   */
  async getLoadBalancerConfig(config?: RequestConfig): Promise<LoadBalancerConfigDto> {
    return this.client['get']<LoadBalancerConfigDto>(
      ENDPOINTS.CONFIGURATION.LOAD_BALANCER,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update load balancer configuration
   */
  async updateLoadBalancerConfig(
    data: UpdateLoadBalancerConfigDto,
    config?: RequestConfig
  ): Promise<LoadBalancerConfigDto> {
    return this.client['put']<LoadBalancerConfigDto, UpdateLoadBalancerConfigDto>(
      ENDPOINTS.CONFIGURATION.LOAD_BALANCER,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get load balancer health
   */
  async getLoadBalancerHealth(config?: RequestConfig): Promise<LoadBalancerHealthDto> {
    return this.client['get']<LoadBalancerHealthDto>(
      '/api/config/loadbalancer/health',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get load balancer health (using existing endpoint)
   */
  async getLoadBalancerHealthStatus(config?: RequestConfig): Promise<LoadBalancerHealth[]> {
    return this.client['get']<LoadBalancerHealth[]>(
      ENDPOINTS.CONFIGURATION.LOAD_BALANCER_HEALTH,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Performance Tuning

  /**
   * Get performance configuration
   */
  async getPerformanceConfig(config?: RequestConfig): Promise<PerformanceConfigDto> {
    return this.client['get']<PerformanceConfigDto>(
      ENDPOINTS.CONFIGURATION.PERFORMANCE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update performance configuration
   */
  async updatePerformanceConfig(
    data: UpdatePerformanceConfigDto,
    config?: RequestConfig
  ): Promise<PerformanceConfigDto> {
    return this.client['put']<PerformanceConfigDto, UpdatePerformanceConfigDto>(
      ENDPOINTS.CONFIGURATION.PERFORMANCE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Run performance test
   */
  async runPerformanceTest(
    params: PerformanceTestParams,
    config?: RequestConfig
  ): Promise<PerformanceTestResult> {
    return this.client['post']<PerformanceTestResult, PerformanceTestParams>(
      ENDPOINTS.CONFIGURATION.PERFORMANCE_TEST,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Feature Flags

  /**
   * Get feature flags
   */
  async getFeatureFlags(config?: RequestConfig): Promise<FeatureFlag[]> {
    return this.client['get']<FeatureFlag[]>(
      ENDPOINTS.CONFIGURATION.FEATURES,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update feature flag
   */
  async updateFeatureFlag(
    key: string,
    data: UpdateFeatureFlagDto,
    config?: RequestConfig
  ): Promise<FeatureFlag> {
    return this.client['put']<FeatureFlag, UpdateFeatureFlagDto>(
      ENDPOINTS.CONFIGURATION.FEATURE_BY_KEY(key),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Helper methods

  /**
   * Validate routing rule conditions
   */
  validateRoutingRule(rule: CreateRoutingRuleDto): string[] {
    const errors: string[] = [];

    if (!rule.name || rule.name.trim() === '') {
      errors.push('Rule name is required');
    }

    if (!rule.conditions || rule.conditions.length === 0) {
      errors.push('At least one condition is required');
    }

    if (!rule.actions || rule.actions.length === 0) {
      errors.push('At least one action is required');
    }

    rule.conditions?.forEach((condition, index) => {
      if (!condition.type) {
        errors.push(`Condition ${index + 1}: type is required`);
      }
      if (!condition.operator) {
        errors.push(`Condition ${index + 1}: operator is required`);
      }
      if (condition.value === undefined || condition.value === null) {
        errors.push(`Condition ${index + 1}: value is required`);
      }
    });

    rule.actions?.forEach((action, index) => {
      if (!action.type) {
        errors.push(`Action ${index + 1}: type is required`);
      }
      if (action.type === 'route' && !action.target) {
        errors.push(`Action ${index + 1}: target is required for route action`);
      }
    });

    return errors;
  }

  /**
   * Calculate optimal cache size based on usage patterns
   */
  calculateOptimalCacheSize(stats: CacheStatsDto): number {
    const hitRate = stats.hitRate;
    const currentSize = stats.currentSizeBytes;
    const maxSize = stats.maxSizeBytes;

    // If hit rate is high and we're near capacity, increase size
    if (hitRate > 0.8 && currentSize > maxSize * 0.9) {
      return Math.min(maxSize * 1.5, maxSize * 2);
    }

    // If hit rate is low and we're using less than half, decrease size
    if (hitRate < 0.3 && currentSize < maxSize * 0.5) {
      return Math.max(maxSize * 0.5, currentSize * 1.2);
    }

    return maxSize;
  }

  /**
   * Get load balancer algorithm recommendation
   */
  recommendLoadBalancerAlgorithm(
    nodes: LoadBalancerHealthDto['nodes']
  ): 'round_robin' | 'weighted_round_robin' | 'least_connections' {
    // Check if all nodes have similar performance
    const avgResponseTimes = nodes.map(n => n.avgResponseTime);
    const avgTime = avgResponseTimes.reduce((a, b) => a + b, 0) / avgResponseTimes.length;
    const variance = avgResponseTimes.reduce((sum, time) => sum + Math.pow(time - avgTime, 2), 0) / avgResponseTimes.length;
    const stdDev = Math.sqrt(variance);

    // If performance is similar, use round robin
    if (stdDev < avgTime * 0.1) {
      return 'round_robin';
    }

    // If performance varies significantly, use weighted or least connections
    const hasHighLoad = nodes.some(n => n.activeConnections > 100);
    
    if (hasHighLoad) {
      return 'least_connections';
    }

    return 'weighted_round_robin';
  }

  /**
   * Calculate circuit breaker settings based on performance metrics
   */
  calculateCircuitBreakerSettings(metrics: PerformanceTestResult): {
    failureThreshold: number;
    resetTimeoutMs: number;
    halfOpenRequests: number;
  } {
    const errorRate = metrics.summary.failedRequests / metrics.summary.totalRequests;
    const avgLatency = metrics.summary.avgLatency;

    return {
      failureThreshold: errorRate < 0.01 ? 10 : errorRate < 0.05 ? 5 : 3,
      resetTimeoutMs: avgLatency < 100 ? 5000 : avgLatency < 500 ? 10000 : 30000,
      halfOpenRequests: Math.max(1, Math.floor(metrics.summary.throughput / 10)),
    };
  }

  /**
   * Check if feature flag should be enabled for a given context
   */
  evaluateFeatureFlag(flag: FeatureFlag, context: Record<string, any>): boolean {
    if (!flag.enabled) {
      return false;
    }

    // Check rollout percentage
    if (flag.rolloutPercentage !== undefined && flag.rolloutPercentage < 100) {
      // Simple hash-based rollout
      const hash = this.hashString(context.userId || context.key || '');
      const bucket = (hash % 100) + 1;
      if (bucket > flag.rolloutPercentage) {
        return false;
      }
    }

    // Check conditions
    if (flag.conditions && flag.conditions.length > 0) {
      return flag.conditions.every(condition => {
        const value = context[condition.field];
        
        switch (condition.operator) {
          case 'equals':
            return value === condition.values[0];
          case 'in':
            return condition.values.includes(value);
          case 'not_in':
            return !condition.values.includes(value);
          case 'regex':
            return new RegExp(condition.values[0]).test(value);
          default:
            return false;
        }
      });
    }

    return true;
  }

  /**
   * Simple string hash function for consistent bucketing
   */
  private hashString(str: string): number {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash; // Convert to 32-bit integer
    }
    return Math.abs(hash);
  }

  /**
   * Format cache size for display
   */
  formatCacheSize(bytes: number): string {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;

    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }

    return `${size.toFixed(2)} ${units[unitIndex]}`;
  }

  /**
   * Generate performance test recommendations
   */
  generatePerformanceRecommendations(result: PerformanceTestResult): string[] {
    const recommendations: string[] = [];
    const { summary } = result;

    // Latency recommendations
    if (summary.p99Latency > summary.avgLatency * 3) {
      recommendations.push('High latency variance detected. Consider implementing request timeout and retry logic.');
    }

    if (summary.avgLatency > 1000) {
      recommendations.push('Average latency exceeds 1 second. Consider optimizing model selection or implementing caching.');
    }

    // Error rate recommendations
    const errorRate = summary.failedRequests / summary.totalRequests;
    if (errorRate > 0.05) {
      recommendations.push(`Error rate is ${(errorRate * 100).toFixed(2)}%. Investigate error patterns and implement circuit breakers.`);
    }

    // Throughput recommendations
    if (summary.throughput < result.timeline[0].requestsPerSecond * 0.8) {
      recommendations.push('Throughput degradation detected. Consider increasing connection pool size or implementing load balancing.');
    }

    // Error pattern recommendations
    const errorTypes = result.errors.map(e => e.type);
    if (errorTypes.includes('timeout')) {
      recommendations.push('Timeout errors detected. Consider increasing timeout values or optimizing slow endpoints.');
    }

    if (errorTypes.includes('rate_limit')) {
      recommendations.push('Rate limiting detected. Implement request queuing or distribute load across multiple API keys.');
    }

    return recommendations;
  }
}