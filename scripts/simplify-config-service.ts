#!/usr/bin/env tsx

import * as fs from 'fs';
import * as path from 'path';

const SERVICE_FILE = path.join(__dirname, '../SDKs/Node/Admin/src/services/FetchConfigurationService.ts');

// Create a simplified version of the service
const simplifiedContent = `import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
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
  RoutingHealthStatus,
  RouteHealthDetails,
  RoutingHealthHistory,
  RoutingHealthOptions,
  RoutingHealthResponse,
  RoutePerformanceTestParams,
  RoutePerformanceTestResult,
  CircuitBreakerConfig,
  CircuitBreakerStatus,
  ErrorSummary,
} from '../models/configurationExtended';
import type {
  CircuitBreakerUpdateResponse
} from '../models/configurationResponses';
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
 * NOTE: Most endpoints in this service no longer exist in the Admin API.
 * Only basic routing and caching configuration endpoints are available.
 */
export class FetchConfigurationService {
  constructor(private readonly client: FetchBaseApiClient) {}

  // Working endpoints

  async getRoutingConfiguration(config?: RequestConfig): Promise<RoutingConfiguration> {
    return this.client['get']<RoutingConfiguration>(
      ENDPOINTS.CONFIG.ROUTING,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async updateRoutingConfiguration(
    data: UpdateRoutingConfigDto,
    config?: RequestConfig
  ): Promise<RoutingConfiguration> {
    return this.client['put']<RoutingConfiguration, UpdateRoutingConfigDto>(
      ENDPOINTS.CONFIG.ROUTING,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCachingConfiguration(config?: RequestConfig): Promise<CachingConfiguration> {
    return this.client['get']<CachingConfiguration>(
      ENDPOINTS.CONFIG.CACHING.BASE,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async updateCachingConfiguration(
    data: UpdateCacheConfigDto,
    config?: RequestConfig
  ): Promise<CachingConfiguration> {
    return this.client['put']<CachingConfiguration, UpdateCacheConfigDto>(
      ENDPOINTS.CONFIG.CACHING.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCacheStatistics(config?: RequestConfig): Promise<CacheStatistics> {
    return this.client['get']<CacheStatistics>(
      ENDPOINTS.CONFIG.CACHING.STATISTICS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCacheRegions(config?: RequestConfig): Promise<string[]> {
    return this.client['get']<string[]>(
      ENDPOINTS.CONFIG.CACHING.REGIONS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async getCacheEntries(regionId: string, config?: RequestConfig): Promise<unknown> {
    return this.client['get'](
      ENDPOINTS.CONFIG.CACHING.ENTRIES(regionId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async refreshCacheRegion(regionId: string, config?: RequestConfig): Promise<void> {
    return this.client['post']<void>(
      ENDPOINTS.CONFIG.CACHING.REFRESH(regionId),
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async updateCachePolicy(regionId: string, policy: unknown, config?: RequestConfig): Promise<void> {
    return this.client['put']<void>(
      ENDPOINTS.CONFIG.CACHING.POLICY(regionId),
      policy,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  async clearCacheRegion(cacheId: string, config?: RequestConfig): Promise<ClearCacheResult> {
    return this.client['post']<ClearCacheResult>(
      ENDPOINTS.CONFIG.CACHING.CLEAR(cacheId),
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // All other methods throw errors

  async getRoutingConfig(config?: RequestConfig): Promise<RoutingConfigDto> {
    throw new Error('Extended routing config endpoints no longer exist. Use getRoutingConfiguration instead.');
  }

  async updateRoutingConfig(
    data: ExtendedUpdateRoutingConfigDto,
    config?: RequestConfig
  ): Promise<RoutingConfigDto> {
    throw new Error('Extended routing config endpoints no longer exist. Use updateRoutingConfiguration instead.');
  }

  async testRoutingConfig(config?: RequestConfig): Promise<TestResult> {
    throw new Error('Routing test endpoint no longer exists in the API.');
  }

  async getRoutingRules(config?: RequestConfig): Promise<ExtendedRoutingRule[]> {
    throw new Error('Routing rules endpoint no longer exists in the API.');
  }

  async createRoutingRule(
    rule: CreateRoutingRuleDto,
    config?: RequestConfig
  ): Promise<ExtendedRoutingRule> {
    throw new Error('Routing rules endpoint no longer exists in the API.');
  }

  async updateRoutingRule(
    id: string,
    rule: UpdateRoutingRuleDto,
    config?: RequestConfig
  ): Promise<ExtendedRoutingRule> {
    throw new Error('Routing rules endpoint no longer exists in the API.');
  }

  async deleteRoutingRule(id: string, config?: RequestConfig): Promise<void> {
    throw new Error('Routing rules endpoint no longer exists in the API.');
  }

  async getCacheConfig(config?: RequestConfig): Promise<CacheConfigDto> {
    throw new Error('Extended cache config endpoints no longer exist. Use getCachingConfiguration instead.');
  }

  async updateCacheConfig(
    data: UpdateCacheConfigDto,
    config?: RequestConfig
  ): Promise<CacheConfigDto> {
    throw new Error('Extended cache config endpoints no longer exist. Use updateCachingConfiguration instead.');
  }

  async clearCache(params?: CacheClearParams, config?: RequestConfig): Promise<CacheClearResult> {
    throw new Error('Generic cache clear endpoint no longer exists. Use clearCacheRegion instead.');
  }

  async getCacheStats(config?: RequestConfig): Promise<CacheStatsDto> {
    throw new Error('Extended cache stats endpoint no longer exists. Use getCacheStatistics instead.');
  }

  async getCachePolicies(config?: RequestConfig): Promise<CachePolicy[]> {
    throw new Error('Cache policies endpoint no longer exists in the API.');
  }

  async createCachePolicy(
    policy: CreateCachePolicyDto,
    config?: RequestConfig
  ): Promise<CachePolicy> {
    throw new Error('Cache policies endpoint no longer exists in the API.');
  }

  async updateCachePolicy(
    id: string,
    policy: UpdateCachePolicyDto,
    config?: RequestConfig
  ): Promise<CachePolicy> {
    throw new Error('Cache policies endpoint no longer exists in the API. Use updateCachePolicy with regionId instead.');
  }

  async deleteCachePolicy(id: string, config?: RequestConfig): Promise<void> {
    throw new Error('Cache policies endpoint no longer exists in the API.');
  }

  async getLoadBalancerConfig(config?: RequestConfig): Promise<LoadBalancerConfigDto> {
    throw new Error('Load balancer configuration endpoint no longer exists in the API.');
  }

  async updateLoadBalancerConfig(
    data: UpdateLoadBalancerConfigDto,
    config?: RequestConfig
  ): Promise<LoadBalancerConfigDto> {
    throw new Error('Load balancer configuration endpoint no longer exists in the API.');
  }

  async getLoadBalancerHealth(config?: RequestConfig): Promise<LoadBalancerHealthDto> {
    throw new Error('Load balancer health endpoint no longer exists in the API.');
  }

  async getLoadBalancerHealthStatus(config?: RequestConfig): Promise<LoadBalancerHealth[]> {
    throw new Error('Load balancer health endpoint no longer exists in the API.');
  }

  async getPerformanceConfig(config?: RequestConfig): Promise<PerformanceConfigDto> {
    throw new Error('Performance configuration endpoint no longer exists in the API.');
  }

  async updatePerformanceConfig(
    data: UpdatePerformanceConfigDto,
    config?: RequestConfig
  ): Promise<PerformanceConfigDto> {
    throw new Error('Performance configuration endpoint no longer exists in the API.');
  }

  async runPerformanceTest(
    params: PerformanceTestParams,
    config?: RequestConfig
  ): Promise<PerformanceTestResult> {
    throw new Error('Performance test endpoint no longer exists in the API.');
  }

  async getFeatureFlags(config?: RequestConfig): Promise<FeatureFlag[]> {
    throw new Error('Feature flags endpoint no longer exists in the API.');
  }

  async updateFeatureFlag(
    key: string,
    data: UpdateFeatureFlagDto,
    config?: RequestConfig
  ): Promise<FeatureFlag> {
    throw new Error('Feature flags endpoint no longer exists in the API.');
  }

  async getRoutingHealthStatus(
    options: RoutingHealthOptions = {},
    config?: RequestConfig
  ): Promise<RoutingHealthResponse> {
    throw new Error('Routing health endpoints no longer exist in the API.');
  }

  async getRouteHealthStatus(
    routeId: string,
    config?: RequestConfig
  ): Promise<RouteHealthDetails> {
    throw new Error('Routing health endpoints no longer exist in the API.');
  }

  async getRoutingHealthHistory(
    timeRange: '1h' | '24h' | '7d' | '30d' = '24h',
    resolution: 'minute' | 'hour' | 'day' = 'hour',
    config?: RequestConfig
  ): Promise<RoutingHealthHistory> {
    throw new Error('Routing health history endpoint no longer exists in the API.');
  }

  async runRoutePerformanceTest(
    params: RoutePerformanceTestParams,
    config?: RequestConfig
  ): Promise<RoutePerformanceTestResult> {
    throw new Error('Route performance test endpoint no longer exists in the API.');
  }

  async getCircuitBreakerStatus(config?: RequestConfig): Promise<CircuitBreakerStatus[]> {
    throw new Error('Circuit breaker endpoints no longer exist in the API.');
  }

  async updateCircuitBreakerConfig(
    breakerId: string,
    config: Partial<CircuitBreakerConfig>,
    requestConfig?: RequestConfig
  ): Promise<CircuitBreakerStatus> {
    throw new Error('Circuit breaker endpoints no longer exist in the API.');
  }

  async subscribeToRoutingHealthEvents(
    eventTypes: string[] = ['route_health_change', 'circuit_breaker_state_change', 'performance_alert'],
    config?: RequestConfig
  ): Promise<{ connectionId: string; unsubscribe: () => void }> {
    throw new Error('Routing health events subscription endpoint no longer exists in the API.');
  }

  // Helper methods
  
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
    return errors;
  }

  calculateOptimalCacheSize(stats: CacheStatsDto): number {
    const hitRate = stats.hitRate;
    const currentSize = stats.currentSizeBytes;
    const maxSize = stats.maxSizeBytes;
    if (hitRate > 0.8 && currentSize > maxSize * 0.9) {
      return Math.min(maxSize * 1.5, maxSize * 2);
    }
    if (hitRate < 0.3 && currentSize < maxSize * 0.5) {
      return Math.max(maxSize * 0.5, currentSize * 1.2);
    }
    return maxSize;
  }

  formatCacheSize(bytes: number): string {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    return \`\${size.toFixed(2)} \${units[unitIndex]}\`;
  }
}
`;

// Write back
fs.writeFileSync(SERVICE_FILE, simplifiedContent);

console.log('✅ Simplified FetchConfigurationService.ts');