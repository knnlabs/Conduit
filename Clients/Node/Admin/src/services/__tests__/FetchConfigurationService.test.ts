import { FetchConfigurationService } from '../FetchConfigurationService';
import { FetchBaseApiClient } from '../../client/FetchBaseApiClient';
import { ENDPOINTS } from '../../constants';
import type { RequestConfig } from '../../client/types';
import type {
  RoutingConfigDto,
  UpdateRoutingConfigDto as ExtendedUpdateRoutingConfigDto,
  RoutingRule as ExtendedRoutingRule,
  CreateRoutingRuleDto,
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
} from '../../models/configurationExtended';
import type {
  RoutingConfiguration,
  CachingConfiguration,
  CachePolicy,
  CreateCachePolicyDto,
  CacheStatistics,
  TestResult,
} from '../../models/configuration';

jest.mock('../../client/FetchBaseApiClient');

describe('FetchConfigurationService', () => {
  let service: FetchConfigurationService;
  let mockClient: jest.Mocked<FetchBaseApiClient>;

  beforeEach(() => {
    mockClient = new FetchBaseApiClient({
      baseUrl: 'https://api.test.com',
      masterKey: 'test-key'
    }) as jest.Mocked<FetchBaseApiClient>;
    service = new FetchConfigurationService(mockClient);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Routing Configuration', () => {
    it('should get routing config', async () => {
      const mockConfig: RoutingConfigDto = {
        defaultStrategy: 'round_robin',
        fallbackEnabled: true,
        retryPolicy: {
          maxAttempts: 3,
          initialDelayMs: 100,
          maxDelayMs: 5000,
          backoffMultiplier: 2,
          retryableStatuses: [502, 503, 504]
        },
        timeoutMs: 30000,
        maxConcurrentRequests: 100
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getRoutingConfig();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/config/routing',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockConfig);
    });

    it('should update routing config', async () => {
      const updateData: ExtendedUpdateRoutingConfigDto = {
        defaultStrategy: 'least_latency',
        timeoutMs: 60000
      };

      const mockResponse: RoutingConfigDto = {
        defaultStrategy: 'least_latency',
        fallbackEnabled: true,
        retryPolicy: {
          maxAttempts: 3,
          initialDelayMs: 100,
          maxDelayMs: 5000,
          backoffMultiplier: 2,
          retryableStatuses: [502, 503, 504]
        },
        timeoutMs: 60000,
        maxConcurrentRequests: 100
      };

      (mockClient as any).put = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.updateRoutingConfig(updateData);

      expect((mockClient as any).put).toHaveBeenCalledWith(
        '/api/config/routing',
        updateData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should test routing config', async () => {
      const mockResult: TestResult = {
        success: true,
        executionTimeMs: 150,
        results: [
          { test: 'connectivity', passed: true, message: 'All providers reachable' }
        ],
        errors: []
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.testRoutingConfig();

      expect((mockClient as any).post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_TEST,
        {},
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });

    it('should get routing rules', async () => {
      const mockRules: ExtendedRoutingRule[] = [
        {
          id: 'rule-1',
          name: 'GPT-4 Priority',
          priority: 1,
          conditions: [{ type: 'model', operator: 'equals', value: 'gpt-4' }],
          actions: [{ type: 'route', target: 'openai' }],
          enabled: true
        }
      ];

      (mockClient as any).get = jest.fn().mockResolvedValue(mockRules);

      const result = await service.getRoutingRules();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockRules);
    });

    it('should create routing rule', async () => {
      const createData: CreateRoutingRuleDto = {
        name: 'High Load Rule',
        priority: 2,
        conditions: [{ type: 'load', operator: 'gt', value: 80 }],
        actions: [{ type: 'route', target: 'backup-provider' }],
        enabled: true
      };

      const mockRule: ExtendedRoutingRule = {
        id: 'rule-2',
        ...createData,
        stats: { matchCount: 0 }
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockRule);

      const result = await service.createRoutingRule(createData);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        createData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockRule);
    });

    it('should delete routing rule', async () => {
      (mockClient as any).delete = jest.fn().mockResolvedValue(undefined);

      await service.deleteRoutingRule('rule-1');

      expect((mockClient as any).delete).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID('rule-1'),
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });
  });

  describe('Caching Configuration', () => {
    it('should get cache config', async () => {
      const mockConfig: CacheConfigDto = {
        enabled: true,
        strategy: 'lru',
        maxSizeBytes: 1073741824, // 1GB
        defaultTtlSeconds: 3600,
        rules: [],
        redis: {
          enabled: false,
          endpoint: '',
          cluster: false
        }
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getCacheConfig();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.CACHE_CONFIG,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockConfig);
    });

    it('should clear cache', async () => {
      const params: CacheClearParams = {
        pattern: 'model:gpt-4*',
        type: 'pattern'
      };

      const mockResult: CacheClearResult = {
        success: true,
        clearedCount: 150,
        clearedSizeBytes: 52428800, // 50MB
        errors: []
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.clearCache(params);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/config/cache/clear',
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });

    it('should get cache stats', async () => {
      const mockStats: CacheStatsDto = {
        hitRate: 0.85,
        missRate: 0.15,
        evictionRate: 0.02,
        totalRequests: 10000,
        totalHits: 8500,
        totalMisses: 1500,
        currentSizeBytes: 536870912, // 512MB
        maxSizeBytes: 1073741824, // 1GB
        itemCount: 2500,
        topKeys: []
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockStats);

      const result = await service.getCacheStats();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.CACHE_STATS,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockStats);
    });

    it('should create cache policy', async () => {
      const createData: CreateCachePolicyDto = {
        name: 'GPT-4 Cache',
        type: 'model',
        pattern: 'gpt-4*',
        ttlSeconds: 7200,
        strategy: 'memory',
        enabled: true
      };

      const mockPolicy: CachePolicy = {
        id: 'policy-1',
        ...createData
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockPolicy);

      const result = await service.createCachePolicy(createData);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.CACHE_POLICIES,
        createData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPolicy);
    });
  });

  describe('Load Balancing', () => {
    it('should get load balancer config', async () => {
      const mockConfig: LoadBalancerConfigDto = {
        algorithm: 'weighted_round_robin',
        healthCheck: {
          enabled: true,
          intervalSeconds: 30,
          timeoutSeconds: 5,
          unhealthyThreshold: 3,
          healthyThreshold: 2
        },
        weights: {
          'openai': 70,
          'anthropic': 30
        }
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getLoadBalancerConfig();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.LOAD_BALANCER,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockConfig);
    });

    it('should get load balancer health', async () => {
      const mockHealth: LoadBalancerHealthDto = {
        status: 'healthy',
        nodes: [
          {
            id: 'node-1',
            endpoint: 'https://api.openai.com',
            status: 'healthy',
            weight: 70,
            activeConnections: 45,
            totalRequests: 10000,
            avgResponseTime: 250,
            lastHealthCheck: '2024-01-01T00:00:00Z'
          }
        ],
        lastCheck: '2024-01-01T00:00:00Z',
        distribution: {
          'openai': 7000,
          'anthropic': 3000
        }
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockHealth);

      const result = await service.getLoadBalancerHealth();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/config/loadbalancer/health',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockHealth);
    });
  });

  describe('Performance Configuration', () => {
    it('should get performance config', async () => {
      const mockConfig: PerformanceConfigDto = {
        connectionPool: {
          minSize: 10,
          maxSize: 100,
          acquireTimeoutMs: 5000,
          idleTimeoutMs: 300000
        },
        requestQueue: {
          maxSize: 1000,
          timeout: 30000,
          priorityLevels: 3
        },
        circuitBreaker: {
          enabled: true,
          failureThreshold: 5,
          resetTimeoutMs: 60000,
          halfOpenRequests: 3
        },
        rateLimiter: {
          enabled: true,
          requestsPerSecond: 100,
          burstSize: 200
        }
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getPerformanceConfig();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.PERFORMANCE,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockConfig);
    });

    it('should run performance test', async () => {
      const params: PerformanceTestParams = {
        duration: 300,
        concurrentUsers: 50,
        requestsPerSecond: 100,
        models: ['gpt-4', 'claude-2'],
        payloadSize: 'medium'
      };

      const mockResult: PerformanceTestResult = {
        summary: {
          totalRequests: 30000,
          successfulRequests: 29700,
          failedRequests: 300,
          avgLatency: 250,
          p50Latency: 200,
          p95Latency: 450,
          p99Latency: 800,
          throughput: 99
        },
        timeline: [],
        errors: [],
        recommendations: ['Consider increasing connection pool size']
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.runPerformanceTest(params);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.PERFORMANCE_TEST,
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });
  });

  describe('Feature Flags', () => {
    it('should get feature flags', async () => {
      const mockFlags: FeatureFlag[] = [
        {
          key: 'new-ui',
          name: 'New UI Experience',
          description: 'Enable new dashboard UI',
          enabled: true,
          rolloutPercentage: 50,
          lastModified: '2024-01-01T00:00:00Z'
        }
      ];

      (mockClient as any).get = jest.fn().mockResolvedValue(mockFlags);

      const result = await service.getFeatureFlags();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.FEATURES,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockFlags);
    });

    it('should update feature flag', async () => {
      const updateData: UpdateFeatureFlagDto = {
        enabled: true,
        rolloutPercentage: 75
      };

      const mockFlag: FeatureFlag = {
        key: 'new-ui',
        name: 'New UI Experience',
        enabled: true,
        rolloutPercentage: 75,
        lastModified: '2024-01-01T00:00:00Z'
      };

      (mockClient as any).put = jest.fn().mockResolvedValue(mockFlag);

      const result = await service.updateFeatureFlag('new-ui', updateData);

      expect((mockClient as any).put).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.FEATURE_BY_KEY('new-ui'),
        updateData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockFlag);
    });
  });

  describe('Helper methods', () => {
    describe('validateRoutingRule', () => {
      it('should validate valid routing rule', () => {
        const rule: CreateRoutingRuleDto = {
          name: 'Valid Rule',
          conditions: [{ type: 'model', operator: 'equals', value: 'gpt-4' }],
          actions: [{ type: 'route', target: 'openai' }]
        };

        const errors = service.validateRoutingRule(rule);
        expect(errors).toHaveLength(0);
      });

      it('should catch validation errors', () => {
        const rule: CreateRoutingRuleDto = {
          name: '',
          conditions: [],
          actions: []
        };

        const errors = service.validateRoutingRule(rule);
        expect(errors).toContain('Rule name is required');
        expect(errors).toContain('At least one condition is required');
        expect(errors).toContain('At least one action is required');
      });
    });

    describe('calculateOptimalCacheSize', () => {
      it('should recommend increased size for high hit rate near capacity', () => {
        const stats: CacheStatsDto = {
          hitRate: 0.9,
          missRate: 0.1,
          evictionRate: 0.05,
          totalRequests: 10000,
          totalHits: 9000,
          totalMisses: 1000,
          currentSizeBytes: 950 * 1024 * 1024, // 950MB
          maxSizeBytes: 1024 * 1024 * 1024, // 1GB
          itemCount: 5000,
          topKeys: []
        };

        const optimal = service.calculateOptimalCacheSize(stats);
        expect(optimal).toBeGreaterThan(stats.maxSizeBytes);
      });
    });

    describe('recommendLoadBalancerAlgorithm', () => {
      it('should recommend round robin for similar performance', () => {
        const nodes: LoadBalancerHealthDto['nodes'] = [
          { id: '1', endpoint: 'url1', status: 'healthy', weight: 50, activeConnections: 10, totalRequests: 1000, avgResponseTime: 100, lastHealthCheck: '' },
          { id: '2', endpoint: 'url2', status: 'healthy', weight: 50, activeConnections: 12, totalRequests: 1000, avgResponseTime: 105, lastHealthCheck: '' }
        ];

        const recommendation = service.recommendLoadBalancerAlgorithm(nodes);
        expect(recommendation).toBe('round_robin');
      });

      it('should recommend least connections for high load', () => {
        const nodes: LoadBalancerHealthDto['nodes'] = [
          { id: '1', endpoint: 'url1', status: 'healthy', weight: 50, activeConnections: 150, totalRequests: 10000, avgResponseTime: 200, lastHealthCheck: '' },
          { id: '2', endpoint: 'url2', status: 'healthy', weight: 50, activeConnections: 50, totalRequests: 10000, avgResponseTime: 100, lastHealthCheck: '' }
        ];

        const recommendation = service.recommendLoadBalancerAlgorithm(nodes);
        expect(recommendation).toBe('least_connections');
      });
    });

    describe('evaluateFeatureFlag', () => {
      it('should evaluate feature flag with rollout percentage', () => {
        const flag: FeatureFlag = {
          key: 'test-feature',
          name: 'Test Feature',
          enabled: true,
          rolloutPercentage: 50,
          lastModified: '2024-01-01'
        };

        // Test multiple times to check distribution
        let enabledCount = 0;
        for (let i = 0; i < 100; i++) {
          const result = service.evaluateFeatureFlag(flag, { userId: `user-${i}` });
          if (result) enabledCount++;
        }

        // Should be roughly 50% enabled
        expect(enabledCount).toBeGreaterThan(30);
        expect(enabledCount).toBeLessThan(70);
      });

      it('should evaluate feature flag with conditions', () => {
        const flag: FeatureFlag = {
          key: 'premium-feature',
          name: 'Premium Feature',
          enabled: true,
          conditions: [
            { type: 'user', field: 'plan', operator: 'equals', values: ['premium'] }
          ],
          lastModified: '2024-01-01'
        };

        expect(service.evaluateFeatureFlag(flag, { plan: 'premium' })).toBe(true);
        expect(service.evaluateFeatureFlag(flag, { plan: 'free' })).toBe(false);
      });
    });

    describe('formatCacheSize', () => {
      it('should format cache sizes correctly', () => {
        expect(service.formatCacheSize(1024)).toBe('1.00 KB');
        expect(service.formatCacheSize(1048576)).toBe('1.00 MB');
        expect(service.formatCacheSize(1073741824)).toBe('1.00 GB');
      });
    });

    describe('generatePerformanceRecommendations', () => {
      it('should generate recommendations based on test results', () => {
        const result: PerformanceTestResult = {
          summary: {
            totalRequests: 10000,
            successfulRequests: 9000,
            failedRequests: 1000,
            avgLatency: 1500,
            p50Latency: 1000,
            p95Latency: 3000,
            p99Latency: 5000,
            throughput: 90
          },
          timeline: [],
          errors: [
            { type: 'timeout', count: 500, message: 'Request timeout', firstOccurred: '', lastOccurred: '' }
          ],
          recommendations: []
        };

        const recommendations = service.generatePerformanceRecommendations(result);
        
        expect(recommendations).toContain('High latency variance detected. Consider implementing request timeout and retry logic.');
        expect(recommendations).toContain('Average latency exceeds 1 second. Consider optimizing model selection or implementing caching.');
        expect(recommendations.some(r => r.includes('Error rate is 10.00%'))).toBe(true);
        expect(recommendations).toContain('Timeout errors detected. Consider increasing timeout values or optimizing slow endpoints.');
      });
    });
  });
});