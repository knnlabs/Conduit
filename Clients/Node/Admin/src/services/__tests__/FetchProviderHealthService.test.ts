import { FetchProviderHealthService } from '../FetchProviderHealthService';
import { FetchBaseApiClient } from '../../client/FetchBaseApiClient';
import { ENDPOINTS } from '../../constants';
import type { RequestConfig } from '../../client/types';
import type { 
  HealthSummaryDto,
  ProviderHealthDto,
  HealthHistory,
  HealthAlert,
  ConnectionTestResult,
  PerformanceMetrics,
  ProviderHealthConfigurationDto,
  CreateProviderHealthConfigurationDto,
  UpdateProviderHealthConfigurationDto
} from '../../models/providerHealth';

jest.mock('../../client/FetchBaseApiClient');

describe('FetchProviderHealthService', () => {
  let service: FetchProviderHealthService;
  let mockClient: jest.Mocked<FetchBaseApiClient>;

  beforeEach(() => {
    mockClient = new FetchBaseApiClient({
      baseUrl: 'https://api.test.com',
      masterKey: 'test-key'
    }) as jest.Mocked<FetchBaseApiClient>;
    service = new FetchProviderHealthService(mockClient);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('getHealthSummary', () => {
    it('should get health summary', async () => {
      const mockSummary: HealthSummaryDto = {
        overall: 'healthy',
        providers: [],
        lastUpdated: '2024-01-01T00:00:00Z',
        alerts: 0,
        degradedCount: 0,
        unhealthyCount: 0
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockSummary);

      const result = await service.getHealthSummary();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.HEALTH.SUMMARY,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockSummary);
    });
  });

  describe('getProviderHealth', () => {
    it('should get provider health details', async () => {
      const mockHealth: ProviderHealthDto = {
        providerId: 'openai',
        providerName: 'OpenAI',
        status: 'healthy',
        details: {
          connectivity: { status: 'ok', message: 'Connected', lastChecked: '2024-01-01T00:00:00Z' },
          performance: { status: 'ok', message: 'Normal', lastChecked: '2024-01-01T00:00:00Z' },
          errorRate: { status: 'ok', message: 'Low', lastChecked: '2024-01-01T00:00:00Z' },
          quotaUsage: { status: 'ok', message: 'Within limits', lastChecked: '2024-01-01T00:00:00Z' }
        },
        metrics: {
          uptime: { percentage: 99.9, totalUptime: 86400, totalDowntime: 86.4, since: '2024-01-01T00:00:00Z' },
          latency: { current: 200, avg: 250, min: 100, max: 500, p50: 230, p95: 450, p99: 490 },
          throughput: { requestsPerMinute: 100, tokensPerMinute: 50000, bytesPerMinute: 100000 },
          errors: { rate: 0.1, count: 10, types: {} }
        }
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockHealth);

      const result = await service.getProviderHealth('openai');

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.HEALTH.STATUS_BY_PROVIDER('openai'),
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockHealth);
    });
  });

  describe('getHealthHistory', () => {
    it('should get health history with parameters', async () => {
      const mockHistory: HealthHistory = {
        providerId: 'openai',
        dataPoints: [],
        incidents: [],
        summary: {
          avgUptime: 99.9,
          totalIncidents: 0,
          avgRecoveryTime: 0
        }
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockHistory);

      const params = {
        startDate: '2024-01-01',
        endDate: '2024-01-31',
        resolution: 'hour' as const,
        includeIncidents: true
      };

      const result = await service.getHealthHistory('openai', params);

      expect((mockClient as any).get).toHaveBeenCalledWith(
        `${ENDPOINTS.HEALTH.HISTORY_BY_PROVIDER('openai')}?startDate=2024-01-01&endDate=2024-01-31&resolution=hour&includeIncidents=true`,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockHistory);
    });
  });

  describe('getHealthAlerts', () => {
    it('should get health alerts with filters', async () => {
      const mockAlerts: HealthAlert[] = [
        {
          id: '1',
          providerId: 'openai',
          providerName: 'OpenAI',
          severity: 'warning',
          type: 'performance',
          message: 'High latency detected',
          createdAt: '2024-01-01T00:00:00Z'
        }
      ];

      (mockClient as any).get = jest.fn().mockResolvedValue(mockAlerts);

      const params = {
        severity: ['warning', 'critical'] as const,
        type: ['performance'] as const,
        providerId: 'openai',
        acknowledged: false,
        pageNumber: 1,
        pageSize: 20
      };

      const result = await service.getHealthAlerts(params);

      expect((mockClient as any).get).toHaveBeenCalledWith(
        `${ENDPOINTS.HEALTH.ALERTS}?page=1&pageSize=20&severity=warning&severity=critical&type=performance&providerId=openai&acknowledged=false`,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockAlerts);
    });
  });

  describe('testProviderConnection', () => {
    it('should test provider connection', async () => {
      const mockResult: ConnectionTestResult = {
        success: true,
        latency: 200,
        statusCode: 200,
        details: {
          dnsResolution: 50,
          tcpConnection: 30,
          tlsHandshake: 70,
          httpResponse: 50
        }
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.testProviderConnection('openai');

      expect((mockClient as any).post).toHaveBeenCalledWith(
        ENDPOINTS.HEALTH.CHECK('openai'),
        {},
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });
  });

  describe('getProviderPerformance', () => {
    it('should get provider performance metrics', async () => {
      const mockMetrics: PerformanceMetrics = {
        latency: { p50: 200, p95: 400, p99: 500, avg: 250 },
        throughput: { requestsPerMinute: 100, tokensPerMinute: 50000 },
        availability: { uptime: 99.9, downtime: 86.4, mtbf: 86400, mttr: 300 },
        errors: { rate: 0.1, types: [] }
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockMetrics);

      const params = {
        startDate: '2024-01-01',
        endDate: '2024-01-31',
        resolution: 'day' as const
      };

      const result = await service.getProviderPerformance('openai', params);

      expect((mockClient as any).get).toHaveBeenCalledWith(
        `${ENDPOINTS.HEALTH.PERFORMANCE('openai')}?startDate=2024-01-01&endDate=2024-01-31&resolution=day`,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockMetrics);
    });
  });

  describe('createHealthConfiguration', () => {
    it('should create health configuration', async () => {
      const createData: CreateProviderHealthConfigurationDto = {
        providerName: 'openai',
        monitoringEnabled: true,
        checkIntervalMinutes: 5
      };

      const mockResponse: ProviderHealthConfigurationDto = {
        providerName: 'openai',
        isEnabled: true,
        checkIntervalSeconds: 300,
        timeoutSeconds: 30,
        unhealthyThreshold: 3,
        healthyThreshold: 2
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.createHealthConfiguration(createData);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        ENDPOINTS.HEALTH.CONFIGURATIONS,
        createData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('updateHealthConfiguration', () => {
    it('should update health configuration', async () => {
      const updateData: UpdateProviderHealthConfigurationDto = {
        isEnabled: false,
        checkIntervalSeconds: 600
      };

      (mockClient as any).put = jest.fn().mockResolvedValue(undefined);

      await service.updateHealthConfiguration('openai', updateData);

      expect((mockClient as any).put).toHaveBeenCalledWith(
        ENDPOINTS.HEALTH.CONFIG_BY_PROVIDER('openai'),
        updateData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });
  });

  describe('acknowledgeAlert', () => {
    it('should acknowledge alert', async () => {
      (mockClient as any).post = jest.fn().mockResolvedValue(undefined);

      await service.acknowledgeAlert('alert-1');

      expect((mockClient as any).post).toHaveBeenCalledWith(
        `${ENDPOINTS.HEALTH.ALERTS}/alert-1/acknowledge`,
        {},
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });
  });

  describe('resolveAlert', () => {
    it('should resolve alert with resolution message', async () => {
      (mockClient as any).post = jest.fn().mockResolvedValue(undefined);

      await service.resolveAlert('alert-1', 'Fixed by restarting service');

      expect((mockClient as any).post).toHaveBeenCalledWith(
        `${ENDPOINTS.HEALTH.ALERTS}/alert-1/resolve`,
        { resolution: 'Fixed by restarting service' },
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });
  });

  describe('helper methods', () => {
    describe('calculateHealthScore', () => {
      it('should calculate health score correctly', () => {
        const score = service.calculateHealthScore({
          uptime: 99.9,
          errorRate: 0.5,
          avgLatency: 250,
          expectedLatency: 200
        });

        // (99.9 * 0.4) + (99.5 * 0.4) + (75 * 0.2) = 39.96 + 39.8 + 15 = 94.76
        expect(score).toBeCloseTo(94.76, 1);
      });
    });

    describe('getHealthStatus', () => {
      it('should return healthy for score >= 90', () => {
        expect(service.getHealthStatus(95)).toBe('healthy');
      });

      it('should return degraded for score >= 70', () => {
        expect(service.getHealthStatus(75)).toBe('degraded');
      });

      it('should return unhealthy for score < 70', () => {
        expect(service.getHealthStatus(65)).toBe('unhealthy');
      });
    });

    describe('formatUptime', () => {
      it('should format high uptime correctly', () => {
        expect(service.formatUptime(99.999)).toBe('99.99%');
        expect(service.formatUptime(99.95)).toBe('99.95%');
        expect(service.formatUptime(99.5)).toBe('99.5%');
      });
    });

    describe('getSeverityColor', () => {
      it('should return correct colors for severity levels', () => {
        expect(service.getSeverityColor('info')).toBe('#3B82F6');
        expect(service.getSeverityColor('warning')).toBe('#F59E0B');
        expect(service.getSeverityColor('critical')).toBe('#EF4444');
      });
    });

    describe('groupAlertsByProvider', () => {
      it('should group alerts by provider', () => {
        const alerts: HealthAlert[] = [
          { id: '1', providerId: 'openai', providerName: 'OpenAI', severity: 'info', type: 'connectivity', message: 'Test', createdAt: '2024-01-01' },
          { id: '2', providerId: 'anthropic', providerName: 'Anthropic', severity: 'warning', type: 'performance', message: 'Test', createdAt: '2024-01-01' },
          { id: '3', providerId: 'openai', providerName: 'OpenAI', severity: 'critical', type: 'error_rate', message: 'Test', createdAt: '2024-01-01' }
        ];

        const grouped = service.groupAlertsByProvider(alerts);

        expect(grouped).toEqual({
          openai: [alerts[0], alerts[2]],
          anthropic: [alerts[1]]
        });
      });
    });

    describe('calculateMTBF', () => {
      it('should calculate MTBF correctly', () => {
        const incidents = [
          { startTime: '2024-01-01T00:00:00Z', endTime: '2024-01-01T01:00:00Z' },
          { startTime: '2024-01-01T12:00:00Z', endTime: '2024-01-01T12:30:00Z' }
        ];

        const mtbf = service.calculateMTBF(incidents, 24);

        // Total time: 24 hours = 86400 seconds
        // Total downtime: 1.5 hours = 5400 seconds
        // Total uptime: 81000 seconds
        // MTBF = 81000 / 2 = 40500 seconds
        expect(mtbf).toBe(40500);
      });

      it('should return total time if no incidents', () => {
        const mtbf = service.calculateMTBF([], 24);
        expect(mtbf).toBe(86400);
      });
    });

    describe('calculateMTTR', () => {
      it('should calculate MTTR correctly', () => {
        const incidents = [
          { startTime: '2024-01-01T00:00:00Z', endTime: '2024-01-01T01:00:00Z' },
          { startTime: '2024-01-01T12:00:00Z', endTime: '2024-01-01T12:30:00Z' }
        ];

        const mttr = service.calculateMTTR(incidents);

        // Recovery times: 3600 seconds + 1800 seconds = 5400 seconds
        // MTTR = 5400 / 2 = 2700 seconds
        expect(mttr).toBe(2700);
      });

      it('should return 0 if no resolved incidents', () => {
        const incidents = [{ startTime: '2024-01-01T00:00:00Z' }];
        const mttr = service.calculateMTTR(incidents);
        expect(mttr).toBe(0);
      });
    });
  });
});