import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchConfigurationService } from '../FetchConfigurationService';
import { ENDPOINTS } from '../../constants';
import type {
  PerformanceConfigDto,
  PerformanceTestParams,
  PerformanceTestResult,
} from '../../models/configurationExtended';

describe('FetchConfigurationService - Performance Configuration', () => {
  let service: FetchConfigurationService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchConfigurationService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      mockClient.get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getPerformanceConfig();

      expect(mockClient.get).toHaveBeenCalledWith(
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

      mockClient.post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.runPerformanceTest(params);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.PERFORMANCE_TEST,
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
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
        timeline: [
          {
            timestamp: '2025-01-11T10:00:00Z',
            requestsPerSecond: 100,
            avgLatency: 1500,
            errorRate: 0.1,
            activeConnections: 50
          }
        ],
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