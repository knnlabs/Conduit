import { FetchAnalyticsService } from '../services/FetchAnalyticsService';
import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ENDPOINTS } from '../constants';
import type {
  RequestLogDto,
  RequestLogPage,
  UsageAnalytics,
  VirtualKeyAnalytics,
  ModelUsageAnalytics,
  CostAnalytics,
  ExportResult,
} from '../models/analytics';

describe('FetchAnalyticsService', () => {
  let mockClient: FetchBaseApiClient;
  let service: FetchAnalyticsService;

  const mockRequestLog: RequestLogDto = {
    id: 'log-123',
    timestamp: '2025-01-11T10:00:00Z',
    virtualKeyId: 1,
    virtualKeyName: 'test-key',
    model: 'gpt-4',
    provider: 'openai',
    inputTokens: 100,
    outputTokens: 200,
    cost: 0.015,
    currency: 'USD',
    duration: 1500,
    status: 'success',
  };

  beforeEach(() => {
    mockClient = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
      request: jest.fn(),
    } as any;

    service = new FetchAnalyticsService(mockClient);
  });

  describe('getRequestLogs', () => {
    it('should get paginated request logs', async () => {
      const mockResponse: RequestLogPage = {
        items: [mockRequestLog],
        totalCount: 100,
        page: 1,
        pageSize: 20,
        totalPages: 5,
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.getRequestLogs({
        page: 1,
        pageSize: 20,
        startDate: '2025-01-01',
        provider: 'openai',
      });

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.ANALYTICS.REQUEST_LOGS}?page=1&pageSize=20&startDate=2025-01-01&provider=openai`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should handle request logs without params', async () => {
      const mockResponse: RequestLogPage = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockResponse);

      await service.getRequestLogs();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.ANALYTICS.REQUEST_LOGS,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
    });
  });

  describe('getRequestLogById', () => {
    it('should get a specific request log', async () => {
      (mockClient.get as jest.Mock).mockResolvedValue(mockRequestLog);

      const result = await service.getRequestLogById('log-123');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.ANALYTICS.REQUEST_LOG_BY_ID('log-123'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockRequestLog);
    });
  });

  describe('exportRequestLogs', () => {
    it('should export request logs', async () => {
      const mockExportResult: ExportResult = {
        url: 'https://example.com/export/123',
        expiresAt: '2025-01-11T11:00:00Z',
        size: 1024000,
        recordCount: 1000,
      };
      (mockClient.post as jest.Mock).mockResolvedValue(mockExportResult);

      const result = await service.exportRequestLogs({
        format: 'csv',
        startDate: '2025-01-01',
        endDate: '2025-01-31',
      });

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.ANALYTICS.EXPORT_REQUEST_LOGS,
        {
          format: 'csv',
          startDate: '2025-01-01',
          endDate: '2025-01-31',
        },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockExportResult);
    });
  });

  describe('getUsageAnalytics', () => {
    it('should get usage analytics', async () => {
      const mockUsageAnalytics: UsageAnalytics = {
        summary: {
          totalRequests: 10000,
          totalTokens: 5000000,
          totalCost: 150.50,
          averageLatency: 1200,
          successRate: 98.5,
        },
        byProvider: {
          openai: {
            provider: 'openai',
            requests: 7000,
            tokens: 3500000,
            cost: 100.00,
            averageLatency: 1100,
            successRate: 99.0,
          },
        },
        byVirtualKey: {},
        byModel: {},
        timeSeries: [],
        timeRange: {
          start: '2025-01-01',
          end: '2025-01-31',
        },
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockUsageAnalytics);

      const result = await service.getUsageAnalytics({
        startDate: '2025-01-01',
        endDate: '2025-01-31',
        groupBy: 'day',
      });

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.ANALYTICS.USAGE_ANALYTICS}?startDate=2025-01-01&endDate=2025-01-31&groupBy=day`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockUsageAnalytics);
    });

    it('should handle array parameters correctly', async () => {
      (mockClient.get as jest.Mock).mockResolvedValue({});

      await service.getUsageAnalytics({
        virtualKeyIds: ['key1', 'key2'],
        providers: ['openai', 'anthropic'],
        models: ['gpt-4', 'claude-3'],
      });

      const expectedUrl = `${ENDPOINTS.ANALYTICS.USAGE_ANALYTICS}?virtualKeyIds=key1&virtualKeyIds=key2&providers=openai&providers=anthropic&models=gpt-4&models=claude-3`;
      expect(mockClient.get).toHaveBeenCalledWith(
        expectedUrl,
        expect.any(Object)
      );
    });
  });

  describe('getVirtualKeyAnalytics', () => {
    it('should get virtual key analytics', async () => {
      const mockAnalytics: VirtualKeyAnalytics = {
        virtualKeys: [],
        topUsers: {
          byRequests: [],
          byCost: [],
          byTokens: [],
        },
        trends: {
          daily: [],
          weekly: [],
          monthly: [],
        },
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockAnalytics);

      const result = await service.getVirtualKeyAnalytics({
        startDate: '2025-01-01',
        endDate: '2025-01-31',
      });

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.ANALYTICS.VIRTUAL_KEY_ANALYTICS}?startDate=2025-01-01&endDate=2025-01-31`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockAnalytics);
    });
  });

  describe('getModelUsageAnalytics', () => {
    it('should get model usage analytics', async () => {
      const mockAnalytics: ModelUsageAnalytics = {
        models: [],
        capabilities: [],
        performance: [],
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockAnalytics);

      const result = await service.getModelUsageAnalytics({
        startDate: '2025-01-01',
        endDate: '2025-01-31',
        models: ['gpt-4'],
      });

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.ANALYTICS.MODEL_USAGE_ANALYTICS}?startDate=2025-01-01&endDate=2025-01-31&models=gpt-4`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockAnalytics);
    });
  });

  describe('getCostAnalytics', () => {
    it('should get cost analytics', async () => {
      const mockAnalytics: CostAnalytics = {
        totalCost: 1500.00,
        breakdown: {
          byProvider: [],
          byModel: [],
          byVirtualKey: [],
        },
        projections: {
          daily: 50.00,
          weekly: 350.00,
          monthly: 1500.00,
        },
        trends: [],
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockAnalytics);

      const result = await service.getCostAnalytics({
        startDate: '2025-01-01',
        endDate: '2025-01-31',
        groupBy: 'day',
      });

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.ANALYTICS.COST_ANALYTICS}?startDate=2025-01-01&endDate=2025-01-31&groupBy=day`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockAnalytics);
    });
  });

  describe('helper methods', () => {
    it('formatDateRange should format date range correctly', () => {
      const range = service.formatDateRange(7);
      const start = new Date(range.startDate);
      const end = new Date(range.endDate);
      const diff = Math.floor((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24));
      expect(diff).toBe(7);
    });

    it('calculateGrowthRate should calculate growth correctly', () => {
      expect(service.calculateGrowthRate(150, 100)).toBe(50);
      expect(service.calculateGrowthRate(50, 100)).toBe(-50);
      expect(service.calculateGrowthRate(100, 0)).toBe(100);
      expect(service.calculateGrowthRate(0, 0)).toBe(0);
    });

    it('getTopItems should return top items by value', () => {
      const items = [
        { name: 'A', value: 10 },
        { name: 'B', value: 30 },
        { name: 'C', value: 20 },
        { name: 'D', value: 40 },
      ];
      const top = service.getTopItems(items, 2);
      expect(top).toHaveLength(2);
      expect(top[0].name).toBe('D');
      expect(top[1].name).toBe('B');
    });

    it('validateDateRange should validate dates correctly', () => {
      const today = new Date().toISOString().split('T')[0];
      const yesterday = new Date(Date.now() - 86400000).toISOString().split('T')[0];
      const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];

      expect(service.validateDateRange(yesterday, today)).toBe(true);
      expect(service.validateDateRange(today, yesterday)).toBe(false);
      expect(service.validateDateRange(today, tomorrow)).toBe(false);
      expect(service.validateDateRange()).toBe(true);
    });

    it('aggregateTimeSeries should aggregate data correctly', () => {
      const data = [
        { timestamp: '2025-01-01T10:00:00Z', value: 10 },
        { timestamp: '2025-01-01T11:00:00Z', value: 20 },
        { timestamp: '2025-01-02T10:00:00Z', value: 30 },
      ];

      const daily = service.aggregateTimeSeries(data, 'day');
      expect(daily).toHaveLength(2);
      expect(daily[0]).toEqual({ period: '2025-01-01', value: 30 });
      expect(daily[1]).toEqual({ period: '2025-01-02', value: 30 });

      const hourly = service.aggregateTimeSeries(data, 'hour');
      expect(hourly).toHaveLength(3);
    });
  });
});