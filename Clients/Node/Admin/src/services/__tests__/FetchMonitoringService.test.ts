import { FetchMonitoringService } from '../FetchMonitoringService';
import { FetchBaseApiClient } from '../../client/FetchBaseApiClient';
import type {
  MetricsQueryParams,
  MetricsResponse,
  AlertDto,
  CreateAlertDto,
  AlertHistoryEntry,
  DashboardDto,
  CreateDashboardDto,
  SystemResourceMetrics,
  TraceDto,
  TraceQueryParams,
  LogEntry,
  LogQueryParams,
  MonitoringHealthStatus,
  MetricExportParams,
  MetricExportResult,
} from '../../models/monitoring';
import type { FilterOptions, PagedResponse } from '../../models/common';

jest.mock('../../client/FetchBaseApiClient');

describe('FetchMonitoringService', () => {
  let service: FetchMonitoringService;
  let mockClient: jest.Mocked<FetchBaseApiClient>;

  beforeEach(() => {
    mockClient = new FetchBaseApiClient({
      baseUrl: 'https://api.test.com',
      masterKey: 'test-key'
    }) as jest.Mocked<FetchBaseApiClient>;
    service = new FetchMonitoringService(mockClient);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Real-time Metrics', () => {
    it('should query metrics', async () => {
      const params: MetricsQueryParams = {
        metrics: ['cpu.usage', 'memory.usage'],
        startTime: '2024-01-01T00:00:00Z',
        endTime: '2024-01-01T01:00:00Z',
        interval: '5m',
        aggregation: 'avg',
      };

      const mockResponse: MetricsResponse = {
        series: [
          {
            name: 'cpu.usage',
            displayName: 'CPU Usage',
            unit: 'percentage',
            aggregation: 'avg',
            dataPoints: [
              { timestamp: '2024-01-01T00:00:00Z', value: 45.5, unit: 'percentage' },
              { timestamp: '2024-01-01T00:05:00Z', value: 48.2, unit: 'percentage' },
            ],
          },
        ],
        query: params,
        executionTimeMs: 125,
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.queryMetrics(params);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/metrics/query',
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should export metrics', async () => {
      const params: MetricExportParams = {
        metrics: ['cpu.usage', 'memory.usage'],
        startTime: '2024-01-01T00:00:00Z',
        endTime: '2024-01-02T00:00:00Z',
        format: 'csv',
        aggregation: 'avg',
        interval: '1h',
      };

      const mockResult: MetricExportResult = {
        exportId: 'export-123',
        status: 'pending',
        format: 'csv',
        createdAt: '2024-01-01T00:00:00Z',
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.exportMetrics(params);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/metrics/export',
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });

    it('should get export status', async () => {
      const mockResult: MetricExportResult = {
        exportId: 'export-123',
        status: 'completed',
        format: 'csv',
        sizeBytes: 1024000,
        recordCount: 1440,
        downloadUrl: 'https://download.url/export-123.csv',
        createdAt: '2024-01-01T00:00:00Z',
        completedAt: '2024-01-01T00:05:00Z',
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockResult);

      const result = await service.getExportStatus('export-123');

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/monitoring/metrics/export/export-123',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });
  });

  describe('Alert Management', () => {
    it('should list alerts', async () => {
      const filters: FilterOptions & { severity?: string; status?: string } = {
        pageNumber: 1,
        pageSize: 20,
        severity: 'critical',
        status: 'active',
      };

      const mockResponse: PagedResponse<AlertDto> = {
        items: [
          {
            id: 'alert-1',
            name: 'High CPU Usage',
            severity: 'critical',
            status: 'active',
            metric: 'cpu.usage',
            condition: {
              type: 'threshold',
              operator: 'gt',
              threshold: 90,
              duration: '5m',
            },
            actions: [],
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
            triggeredCount: 5,
            enabled: true,
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 20,
        totalPages: 1,
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.listAlerts(filters);

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/monitoring/alerts?pageNumber=1&pageSize=20&severity=critical&status=active',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should create alert', async () => {
      const createData: CreateAlertDto = {
        name: 'Memory Alert',
        description: 'Alert when memory usage is high',
        severity: 'warning',
        metric: 'memory.usage',
        condition: {
          type: 'threshold',
          operator: 'gt',
          threshold: 80,
          duration: '10m',
        },
        actions: [
          { type: 'email', config: { to: 'team@company.com' } },
        ],
        enabled: true,
      };

      const mockAlert: AlertDto = {
        id: 'alert-2',
        ...createData,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
        triggeredCount: 0,
        status: 'active',
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockAlert);

      const result = await service.createAlert(createData);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/alerts',
        createData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockAlert);
    });

    it('should acknowledge alert', async () => {
      const mockAlert: AlertDto = {
        id: 'alert-1',
        name: 'High CPU Usage',
        severity: 'critical',
        status: 'acknowledged',
        metric: 'cpu.usage',
        condition: {
          type: 'threshold',
          operator: 'gt',
          threshold: 90,
        },
        actions: [],
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:10:00Z',
        triggeredCount: 5,
        enabled: true,
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockAlert);

      const result = await service.acknowledgeAlert('alert-1', 'Investigating high CPU usage');

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/alerts/alert-1/acknowledge',
        { notes: 'Investigating high CPU usage' },
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockAlert);
    });

    it('should get alert history', async () => {
      const mockHistory: PagedResponse<AlertHistoryEntry> = {
        items: [
          {
            alertId: 'alert-1',
            timestamp: '2024-01-01T00:05:00Z',
            status: 'active',
            value: 92.5,
            message: 'CPU usage exceeded threshold',
            actionsTaken: ['email'],
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 20,
        totalPages: 1,
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockHistory);

      const result = await service.getAlertHistory('alert-1');

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/monitoring/alerts/alert-1/history',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockHistory);
    });
  });

  describe('Dashboard Management', () => {
    it('should list dashboards', async () => {
      const mockResponse: PagedResponse<DashboardDto> = {
        items: [
          {
            id: 'dash-1',
            name: 'System Overview',
            layout: { type: 'grid', columns: 12, rows: 8 },
            widgets: [],
            isPublic: true,
            createdBy: 'admin',
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 20,
        totalPages: 1,
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.listDashboards();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/monitoring/dashboards',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should create dashboard', async () => {
      const createData: CreateDashboardDto = {
        name: 'Performance Dashboard',
        description: 'Monitor system performance',
        layout: { type: 'grid', columns: 12, rows: 8 },
        widgets: [
          {
            type: 'chart',
            title: 'CPU Usage',
            position: { x: 0, y: 0, width: 6, height: 4 },
            config: { chartType: 'line' },
            dataSource: { metrics: ['cpu.usage'] },
          },
        ],
        refreshInterval: 30,
        isPublic: false,
      };

      const mockDashboard: DashboardDto = {
        id: 'dash-2',
        ...createData,
        widgets: createData.widgets.map((w, i) => ({ ...w, id: `widget-${i}` })),
        createdBy: 'admin',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockDashboard);

      const result = await service.createDashboard(createData);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/dashboards',
        createData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockDashboard);
    });

    it('should clone dashboard', async () => {
      const mockDashboard: DashboardDto = {
        id: 'dash-3',
        name: 'Performance Dashboard (Copy)',
        layout: { type: 'grid', columns: 12, rows: 8 },
        widgets: [],
        isPublic: false,
        createdBy: 'admin',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockDashboard);

      const result = await service.cloneDashboard('dash-2', 'Performance Dashboard (Copy)');

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/dashboards/dash-2/clone',
        { name: 'Performance Dashboard (Copy)' },
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockDashboard);
    });
  });

  describe('System Monitoring', () => {
    it('should get system metrics', async () => {
      const mockMetrics: SystemResourceMetrics = {
        cpu: {
          usage: 45.5,
          userTime: 30.2,
          systemTime: 15.3,
          idleTime: 54.5,
          cores: [
            { coreId: 0, usage: 48.2, frequency: 3200, temperature: 65 },
            { coreId: 1, usage: 42.8, frequency: 3200, temperature: 63 },
          ],
        },
        memory: {
          total: 16000000000,
          used: 8000000000,
          free: 8000000000,
          available: 7500000000,
          cached: 2000000000,
          buffers: 500000000,
          swapTotal: 8000000000,
          swapUsed: 0,
          swapFree: 8000000000,
        },
        disk: {
          devices: [
            {
              device: '/dev/sda1',
              mountPoint: '/',
              totalSpace: 500000000000,
              usedSpace: 150000000000,
              freeSpace: 350000000000,
              usagePercent: 30,
              readBytes: 1000000,
              writeBytes: 2000000,
              ioBusy: 5,
            },
          ],
          totalReadBytes: 1000000,
          totalWriteBytes: 2000000,
          readOpsPerSecond: 100,
          writeOpsPerSecond: 200,
        },
        network: {
          interfaces: [
            {
              name: 'eth0',
              bytesReceived: 10000000,
              bytesSent: 5000000,
              packetsReceived: 10000,
              packetsSent: 5000,
              errors: 0,
              dropped: 0,
              status: 'up',
            },
          ],
          totalBytesReceived: 10000000,
          totalBytesSent: 5000000,
          packetsReceived: 10000,
          packetsSent: 5000,
          errors: 0,
          dropped: 0,
        },
        processes: [],
        timestamp: '2024-01-01T00:00:00Z',
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockMetrics);

      const result = await service.getSystemMetrics();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/monitoring/system',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockMetrics);
    });
  });

  describe('Distributed Tracing', () => {
    it('should search traces', async () => {
      const params: TraceQueryParams = {
        service: 'api-gateway',
        minDuration: 100,
        status: 'error',
        startTime: '2024-01-01T00:00:00Z',
        endTime: '2024-01-01T01:00:00Z',
        limit: 50,
      };

      const mockResponse: PagedResponse<TraceDto> = {
        items: [
          {
            traceId: 'trace-123',
            spans: [],
            startTime: '2024-01-01T00:10:00Z',
            endTime: '2024-01-01T00:10:01Z',
            duration: 1000,
            serviceName: 'api-gateway',
            status: 'error',
            tags: { environment: 'production' },
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 50,
        totalPages: 1,
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.searchTraces(params);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/traces/search',
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should get trace by ID', async () => {
      const mockTrace: TraceDto = {
        traceId: 'trace-123',
        spans: [
          {
            spanId: 'span-1',
            operationName: 'HTTP GET /api/users',
            serviceName: 'api-gateway',
            startTime: '2024-01-01T00:10:00Z',
            endTime: '2024-01-01T00:10:01Z',
            duration: 1000,
            status: 'error',
            tags: { method: 'GET', path: '/api/users' },
            logs: [],
          },
        ],
        startTime: '2024-01-01T00:10:00Z',
        endTime: '2024-01-01T00:10:01Z',
        duration: 1000,
        serviceName: 'api-gateway',
        status: 'error',
        tags: { environment: 'production' },
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockTrace);

      const result = await service.getTrace('trace-123');

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/monitoring/traces/trace-123',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockTrace);
    });
  });

  describe('Log Management', () => {
    it('should search logs', async () => {
      const params: LogQueryParams = {
        query: 'error',
        level: 'error',
        service: 'api-gateway',
        startTime: '2024-01-01T00:00:00Z',
        endTime: '2024-01-01T01:00:00Z',
        limit: 100,
      };

      const mockResponse: PagedResponse<LogEntry> = {
        items: [
          {
            id: 'log-1',
            timestamp: '2024-01-01T00:15:00Z',
            level: 'error',
            message: 'Database connection failed',
            service: 'api-gateway',
            fields: { error: 'ECONNREFUSED' },
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 100,
        totalPages: 1,
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.searchLogs(params);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/monitoring/logs/search',
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('Health Status', () => {
    it('should get monitoring health status', async () => {
      const mockStatus: MonitoringHealthStatus = {
        healthy: true,
        services: [
          {
            name: 'Metrics Service',
            status: 'healthy',
            lastCheck: '2024-01-01T00:00:00Z',
          },
          {
            name: 'Alert Service',
            status: 'healthy',
            lastCheck: '2024-01-01T00:00:00Z',
          },
        ],
        lastCheck: '2024-01-01T00:00:00Z',
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockStatus);

      const result = await service.getHealthStatus();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/monitoring/health',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockStatus);
    });
  });

  describe('Helper methods', () => {
    describe('calculateMetricStats', () => {
      it('should calculate statistics correctly', () => {
        const series = {
          name: 'cpu.usage',
          displayName: 'CPU Usage',
          unit: 'percentage',
          aggregation: 'avg' as const,
          dataPoints: [
            { timestamp: '2024-01-01T00:00:00Z', value: 10, unit: 'percentage' },
            { timestamp: '2024-01-01T00:01:00Z', value: 20, unit: 'percentage' },
            { timestamp: '2024-01-01T00:02:00Z', value: 30, unit: 'percentage' },
            { timestamp: '2024-01-01T00:03:00Z', value: 40, unit: 'percentage' },
            { timestamp: '2024-01-01T00:04:00Z', value: 50, unit: 'percentage' },
          ],
        };

        const stats = service.calculateMetricStats(series);

        expect(stats.min).toBe(10);
        expect(stats.max).toBe(50);
        expect(stats.avg).toBe(30);
        expect(stats.sum).toBe(150);
        expect(stats.count).toBe(5);
        expect(stats.stdDev).toBeCloseTo(14.14, 2);
      });

      it('should handle empty data points', () => {
        const series = {
          name: 'cpu.usage',
          displayName: 'CPU Usage',
          unit: 'percentage',
          aggregation: 'avg' as const,
          dataPoints: [],
        };

        const stats = service.calculateMetricStats(series);

        expect(stats).toEqual({
          min: 0,
          max: 0,
          avg: 0,
          sum: 0,
          count: 0,
          stdDev: 0,
        });
      });
    });

    describe('formatMetricValue', () => {
      it('should format bytes correctly', () => {
        expect(service.formatMetricValue(1024, 'bytes')).toBe('1.00 KB');
        expect(service.formatMetricValue(1048576, 'bytes')).toBe('1.00 MB');
        expect(service.formatMetricValue(1073741824, 'bytes')).toBe('1.00 GB');
      });

      it('should format time units correctly', () => {
        expect(service.formatMetricValue(150.5, 'milliseconds')).toBe('150.50ms');
        expect(service.formatMetricValue(30.75, 'seconds')).toBe('30.75s');
      });

      it('should format percentage correctly', () => {
        expect(service.formatMetricValue(85.5, 'percentage')).toBe('85.50%');
      });
    });

    describe('parseLogQuery', () => {
      it('should parse log query parameters', () => {
        const query = 'level:error service:api-gateway trace:trace-123';
        const params = service.parseLogQuery(query);

        expect(params).toEqual({
          query,
          level: 'error',
          service: 'api-gateway',
          traceId: 'trace-123',
        });
      });
    });

    describe('generateAlertSummary', () => {
      it('should generate alert summary', () => {
        const alerts: AlertDto[] = [
          {
            id: '1',
            name: 'Alert 1',
            severity: 'critical',
            status: 'active',
            metric: 'cpu',
            condition: { type: 'threshold', operator: 'gt' },
            actions: [],
            createdAt: '',
            updatedAt: '',
            triggeredCount: 0,
            enabled: true,
          },
          {
            id: '2',
            name: 'Alert 2',
            severity: 'error',
            status: 'active',
            metric: 'memory',
            condition: { type: 'threshold', operator: 'gt' },
            actions: [],
            createdAt: '',
            updatedAt: '',
            triggeredCount: 0,
            enabled: true,
          },
          {
            id: '3',
            name: 'Alert 3',
            severity: 'warning',
            status: 'acknowledged',
            metric: 'disk',
            condition: { type: 'threshold', operator: 'gt' },
            actions: [],
            createdAt: '',
            updatedAt: '',
            triggeredCount: 0,
            enabled: true,
          },
        ];

        const summary = service.generateAlertSummary(alerts);
        expect(summary).toBe('Alerts: 2 active, 1 acknowledged (1 critical, 1 error, 1 warning)');
      });
    });

    describe('calculateSystemHealthScore', () => {
      it('should calculate system health score', () => {
        const metrics: SystemResourceMetrics = {
          cpu: {
            usage: 75,
            userTime: 0,
            systemTime: 0,
            idleTime: 0,
            cores: [],
          },
          memory: {
            total: 16000000000,
            used: 12000000000,
            free: 4000000000,
            available: 0,
            cached: 0,
            buffers: 0,
            swapTotal: 0,
            swapUsed: 0,
            swapFree: 0,
          },
          disk: {
            devices: [
              {
                device: '',
                mountPoint: '',
                totalSpace: 0,
                usedSpace: 0,
                freeSpace: 0,
                usagePercent: 85,
                readBytes: 0,
                writeBytes: 0,
                ioBusy: 0,
              },
            ],
            totalReadBytes: 0,
            totalWriteBytes: 0,
            readOpsPerSecond: 0,
            writeOpsPerSecond: 0,
          },
          network: {
            interfaces: [],
            totalBytesReceived: 0,
            totalBytesSent: 0,
            packetsReceived: 0,
            packetsSent: 0,
            errors: 50,
            dropped: 0,
          },
          processes: [],
          timestamp: '',
        };

        const score = service.calculateSystemHealthScore(metrics);
        // CPU: -10, Memory: -5, Disk: -10, Network: -5 = 70
        expect(score).toBe(70);
      });
    });

    describe('getRecommendedAlertActions', () => {
      it('should recommend actions based on severity', () => {
        const criticalActions = service.getRecommendedAlertActions('critical');
        expect(criticalActions).toContainEqual({ type: 'pagerduty', config: { urgency: 'high' } });
        expect(criticalActions).toHaveLength(3);

        const errorActions = service.getRecommendedAlertActions('error');
        expect(errorActions).toHaveLength(2);

        const warningActions = service.getRecommendedAlertActions('warning');
        expect(warningActions).toHaveLength(1);

        const infoActions = service.getRecommendedAlertActions('info');
        expect(infoActions).toContainEqual({ type: 'log', config: { level: 'info' } });
      });
    });
  });
});