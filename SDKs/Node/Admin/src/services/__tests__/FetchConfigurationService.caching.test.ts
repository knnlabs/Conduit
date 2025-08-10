import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchConfigurationService } from '../FetchConfigurationService';
import { ENDPOINTS } from '../../constants';
import type {
  CacheConfigDto,
  CacheClearParams,
  CacheClearResult,
  CacheStatsDto,
} from '../../models/configurationExtended';
import type {
  CachePolicy,
  CreateCachePolicyDto,
} from '../../models/configuration';

describe('FetchConfigurationService - Caching Configuration', () => {
  let service: FetchConfigurationService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchConfigurationService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      mockClient.get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getCacheConfig();

      expect(mockClient.get).toHaveBeenCalledWith(
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

      mockClient.post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.clearCache(params);

      expect(mockClient.post).toHaveBeenCalledWith(
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

      mockClient.get = jest.fn().mockResolvedValue(mockStats);

      const result = await service.getCacheStats();

      expect(mockClient.get).toHaveBeenCalledWith(
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
        ...createData,
        enabled: createData.enabled ?? true // Ensure enabled is always set
      };

      mockClient.post = jest.fn().mockResolvedValue(mockPolicy);

      const result = await service.createCachePolicy(createData);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.CACHE_POLICIES,
        createData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPolicy);
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

  describe('formatCacheSize', () => {
    it('should format cache sizes correctly', () => {
      expect(service.formatCacheSize(1024)).toBe('1.00 KB');
      expect(service.formatCacheSize(1048576)).toBe('1.00 MB');
      expect(service.formatCacheSize(1073741824)).toBe('1.00 GB');
    });
  });
});