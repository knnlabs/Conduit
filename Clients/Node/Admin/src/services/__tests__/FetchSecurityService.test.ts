import { FetchSecurityService } from '../FetchSecurityService';
import { FetchBaseApiClient } from '../../client/FetchBaseApiClient';
import { ENDPOINTS } from '../../constants';
import type { RequestConfig } from '../../client/types';
import type {
  IpWhitelistDto,
  SecurityEventParams,
  SecurityEventPage,
  SecurityEventExtended,
  ThreatSummaryDto,
  ActiveThreat,
  AccessPolicy,
  CreateAccessPolicyDto,
  UpdateAccessPolicyDto,
  AuditLogParams,
  AuditLogPage,
  ExportParams,
  ExportResult,
} from '../../models/securityExtended';
import type {
  SecurityEvent,
  CreateSecurityEventDto,
  ThreatDetection,
  ComplianceMetrics,
  PagedResult,
} from '../../models/security';

jest.mock('../../client/FetchBaseApiClient');

describe('FetchSecurityService', () => {
  let service: FetchSecurityService;
  let mockClient: jest.Mocked<FetchBaseApiClient>;

  beforeEach(() => {
    mockClient = new FetchBaseApiClient({
      baseUrl: 'https://api.test.com',
      masterKey: 'test-key'
    }) as jest.Mocked<FetchBaseApiClient>;
    service = new FetchSecurityService(mockClient);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('IP Management', () => {
    it('should get IP whitelist', async () => {
      const mockWhitelist: IpWhitelistDto = {
        enabled: true,
        ips: [],
        lastModified: '2024-01-01T00:00:00Z',
        totalBlocked: 0
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockWhitelist);

      const result = await service.getIpWhitelist();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/security/ip-whitelist',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockWhitelist);
    });

    it('should add IPs to whitelist', async () => {
      (mockClient as any).post = jest.fn().mockResolvedValue(undefined);

      const ips = ['192.168.1.1', '10.0.0.0/24'];
      await service.addToIpWhitelist(ips);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/security/ip-whitelist',
        { ips },
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });

    it('should remove IPs from whitelist', async () => {
      (mockClient as any).delete = jest.fn().mockResolvedValue(undefined);

      const ips = ['192.168.1.1'];
      await service.removeFromIpWhitelist(ips);

      expect((mockClient as any).delete).toHaveBeenCalledWith(
        '/api/security/ip-whitelist',
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
          body: JSON.stringify({ ips })
        }
      );
    });
  });

  describe('Security Events', () => {
    it('should get security events with filters', async () => {
      const mockPage: SecurityEventPage = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockPage);

      const params: SecurityEventParams = {
        pageNumber: 1,
        pageSize: 20,
        severity: 'high',
        status: 'active'
      };

      const result = await service.getSecurityEvents(params);

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/security/events?page=1&pageSize=20&severity=high&status=active',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPage);
    });

    it('should get security event by ID', async () => {
      const mockEvent: SecurityEventExtended = {
        id: 'event-1',
        type: 'suspicious_activity',
        severity: 'high',
        title: 'Suspicious Activity',
        description: 'Multiple failed attempts',
        source: { ip: '192.168.1.1' },
        timestamp: '2024-01-01T00:00:00Z',
        status: 'active'
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockEvent);

      const result = await service.getSecurityEventById('event-1');

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/security/events/event-1',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockEvent);
    });

    it('should acknowledge security event', async () => {
      (mockClient as any).post = jest.fn().mockResolvedValue(undefined);

      await service.acknowledgeSecurityEvent('event-1');

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/security/events/event-1/acknowledge',
        {},
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });

    it('should report security event', async () => {
      const createDto: CreateSecurityEventDto = {
        type: 'rate_limit_exceeded',
        severity: 'medium',
        source: 'api-gateway',
        ipAddress: '192.168.1.1',
        details: {}
      };

      const mockEvent: SecurityEvent = {
        id: 'event-1',
        timestamp: '2024-01-01T00:00:00Z',
        ...createDto
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockEvent);

      const result = await service.reportEvent(createDto);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        ENDPOINTS.SECURITY.REPORT_EVENT,
        createDto,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockEvent);
    });
  });

  describe('Threat Detection', () => {
    it('should get threat summary', async () => {
      const mockSummary: ThreatSummaryDto = {
        threatLevel: 'medium',
        activeThreats: 5,
        blockedAttempts24h: 100,
        suspiciousActivities24h: 20,
        topThreats: []
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockSummary);

      const result = await service.getThreatSummary();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/security/threats',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockSummary);
    });

    it('should get active threats', async () => {
      const mockThreats: ActiveThreat[] = [
        {
          id: 'threat-1',
          type: 'brute_force',
          severity: 'high',
          source: '192.168.1.1',
          firstDetected: '2024-01-01T00:00:00Z',
          lastActivity: '2024-01-01T00:10:00Z',
          attemptCount: 50,
          status: 'blocking'
        }
      ];

      (mockClient as any).get = jest.fn().mockResolvedValue(mockThreats);

      const result = await service.getActiveThreats();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/security/threats/active',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockThreats);
    });

    it('should update threat status', async () => {
      (mockClient as any).put = jest.fn().mockResolvedValue(undefined);

      await service.updateThreatStatus('threat-1', 'acknowledge');

      expect((mockClient as any).put).toHaveBeenCalledWith(
        ENDPOINTS.SECURITY.THREAT_BY_ID('threat-1'),
        { action: 'acknowledge' },
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });

    it('should get threat analytics', async () => {
      const mockAnalytics: ThreatAnalytics = {
        threatLevel: 'medium',
        metrics: {
          blockedRequests: 100,
          suspiciousActivity: 20,
          rateLimitHits: 50,
          failedAuthentications: 10,
          activeThreats: 5
        },
        topThreats: [],
        threatTrend: []
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockAnalytics);

      const result = await service.getThreatAnalytics();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.SECURITY.THREAT_ANALYTICS,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockAnalytics);
    });
  });

  describe('Access Control', () => {
    it('should get access policies', async () => {
      const mockPolicies: AccessPolicy[] = [
        {
          id: 'policy-1',
          name: 'IP Whitelist',
          type: 'ip_based',
          rules: [],
          enabled: true,
          priority: 1,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z'
        }
      ];

      (mockClient as any).get = jest.fn().mockResolvedValue(mockPolicies);

      const result = await service.getAccessPolicies();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/security/policies',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPolicies);
    });

    it('should create access policy', async () => {
      const createDto: CreateAccessPolicyDto = {
        name: 'Rate Limit',
        type: 'rate_limit',
        rules: [{
          condition: { field: 'requests_per_minute', operator: 'gt', value: 100 },
          action: 'limit'
        }],
        enabled: true
      };

      const mockPolicy: AccessPolicy = {
        id: 'policy-1',
        ...createDto,
        priority: 1,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z'
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockPolicy);

      const result = await service.createAccessPolicy(createDto);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/security/policies',
        createDto,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPolicy);
    });

    it('should update access policy', async () => {
      const updateDto: UpdateAccessPolicyDto = {
        enabled: false
      };

      const mockPolicy: AccessPolicy = {
        id: 'policy-1',
        name: 'Test Policy',
        type: 'custom',
        rules: [],
        enabled: false,
        priority: 1,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z'
      };

      (mockClient as any).put = jest.fn().mockResolvedValue(mockPolicy);

      const result = await service.updateAccessPolicy('policy-1', updateDto);

      expect((mockClient as any).put).toHaveBeenCalledWith(
        '/api/security/policies/policy-1',
        updateDto,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPolicy);
    });

    it('should delete access policy', async () => {
      (mockClient as any).delete = jest.fn().mockResolvedValue(undefined);

      await service.deleteAccessPolicy('policy-1');

      expect((mockClient as any).delete).toHaveBeenCalledWith(
        '/api/security/policies/policy-1',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });
  });

  describe('Audit Logs', () => {
    it('should get audit logs with filters', async () => {
      const mockPage: AuditLogPage = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockPage);

      const params: AuditLogParams = {
        pageNumber: 1,
        pageSize: 20,
        action: 'update',
        userId: 'user-1'
      };

      const result = await service.getAuditLogs(params);

      expect((mockClient as any).get).toHaveBeenCalledWith(
        '/api/security/audit-logs?page=1&pageSize=20&action=update&userId=user-1',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPage);
    });

    it('should export audit logs', async () => {
      const params: ExportParams = {
        format: 'csv',
        startDate: '2024-01-01',
        endDate: '2024-01-31'
      };

      const mockResult: ExportResult = {
        exportId: 'export-1',
        status: 'processing'
      };

      (mockClient as any).post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.exportAuditLogs(params);

      expect((mockClient as any).post).toHaveBeenCalledWith(
        '/api/security/audit-logs/export',
        params,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });
  });

  describe('Compliance', () => {
    it('should get compliance metrics', async () => {
      const mockMetrics: ComplianceMetrics = {
        overallScore: 85,
        categories: {
          dataProtection: 90,
          accessControl: 85,
          auditLogging: 80,
          incidentResponse: 85,
          monitoring: 85
        },
        lastAssessment: '2024-01-01T00:00:00Z',
        issues: []
      };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockMetrics);

      const result = await service.getComplianceMetrics();

      expect((mockClient as any).get).toHaveBeenCalledWith(
        ENDPOINTS.SECURITY.COMPLIANCE_METRICS,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockMetrics);
    });

    it('should get compliance report', async () => {
      const mockReport = { report: 'data' };

      (mockClient as any).get = jest.fn().mockResolvedValue(mockReport);

      const result = await service.getComplianceReport('2024-01-01', '2024-01-31');

      expect((mockClient as any).get).toHaveBeenCalledWith(
        `${ENDPOINTS.SECURITY.COMPLIANCE_REPORT}?startDate=2024-01-01&endDate=2024-01-31`,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockReport);
    });
  });

  describe('Helper methods', () => {
    describe('validateIpAddress', () => {
      it('should validate IPv4 addresses', () => {
        expect(service.validateIpAddress('192.168.1.1')).toBe(true);
        expect(service.validateIpAddress('10.0.0.0/24')).toBe(true);
        expect(service.validateIpAddress('256.256.256.256')).toBe(false);
      });
    });

    describe('calculateSecurityScore', () => {
      it('should calculate security score correctly', () => {
        const score = service.calculateSecurityScore({
          blockedAttempts: 10,
          suspiciousActivities: 5,
          activeThreats: 2,
          failedAuthentications: 50
        });

        expect(score).toBeGreaterThan(0);
        expect(score).toBeLessThanOrEqual(100);
      });
    });

    describe('groupEventsByType', () => {
      it('should group events by type', () => {
        const events: SecurityEventExtended[] = [
          { id: '1', type: 'suspicious_activity', severity: 'high', title: 'Test', description: 'Test', source: {}, timestamp: '2024-01-01', status: 'active' },
          { id: '2', type: 'rate_limit_exceeded', severity: 'medium', title: 'Test', description: 'Test', source: {}, timestamp: '2024-01-01', status: 'active' },
          { id: '3', type: 'suspicious_activity', severity: 'high', title: 'Test', description: 'Test', source: {}, timestamp: '2024-01-01', status: 'active' }
        ];

        const grouped = service.groupEventsByType(events);

        expect(grouped).toEqual({
          suspicious_activity: [events[0], events[2]],
          rate_limit_exceeded: [events[1]]
        });
      });
    });

    describe('getSeverityColor', () => {
      it('should return correct colors for severity levels', () => {
        expect(service.getSeverityColor('low')).toBe('#10B981');
        expect(service.getSeverityColor('medium')).toBe('#F59E0B');
        expect(service.getSeverityColor('high')).toBe('#EF4444');
        expect(service.getSeverityColor('critical')).toBe('#7C3AED');
      });
    });

    describe('formatThreatLevel', () => {
      it('should format threat level correctly', () => {
        expect(service.formatThreatLevel('low')).toBe('Low Risk');
        expect(service.formatThreatLevel('high')).toBe('High Risk');
      });
    });

    describe('isIpInRange', () => {
      it('should check if IP is in CIDR range', () => {
        expect(service.isIpInRange('192.168.1.5', '192.168.1.0/24')).toBe(true);
        expect(service.isIpInRange('192.168.2.5', '192.168.1.0/24')).toBe(false);
        expect(service.isIpInRange('192.168.1.1', '192.168.1.1')).toBe(true);
      });
    });

    describe('generatePolicyRecommendation', () => {
      it('should generate policy recommendations based on threats', () => {
        const threats: ActiveThreat[] = [
          { id: '1', type: 'brute_force', severity: 'high', source: '192.168.1.1', firstDetected: '2024-01-01', lastActivity: '2024-01-01', attemptCount: 10, status: 'monitoring' },
          { id: '2', type: 'brute_force', severity: 'high', source: '192.168.1.1', firstDetected: '2024-01-01', lastActivity: '2024-01-01', attemptCount: 10, status: 'monitoring' },
          { id: '3', type: 'brute_force', severity: 'high', source: '192.168.1.1', firstDetected: '2024-01-01', lastActivity: '2024-01-01', attemptCount: 10, status: 'monitoring' }
        ];

        const recommendations = service.generatePolicyRecommendation(threats);

        expect(recommendations).toHaveLength(1);
        expect(recommendations[0].action).toBe('deny');
        expect(recommendations[0].condition.value).toBe('192.168.1.1');
      });
    });
  });
});