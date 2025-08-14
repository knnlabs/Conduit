import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchSecurityService } from '../FetchSecurityService';
import { ENDPOINTS } from '../../constants';
import type {
  ThreatSummaryDto,
  ActiveThreat,
} from '../../models/securityExtended';
import type {
  ThreatAnalytics,
} from '../../models/security';

describe('FetchSecurityService - Threat Detection', () => {
  let service: FetchSecurityService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchSecurityService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      mockClient.get = jest.fn().mockResolvedValue(mockSummary);

      const result = await service.getThreatSummary();

      expect(mockClient.get).toHaveBeenCalledWith(
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

      mockClient.get = jest.fn().mockResolvedValue(mockThreats);

      const result = await service.getActiveThreats();

      expect(mockClient.get).toHaveBeenCalledWith(
        '/api/security/threats/active',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockThreats);
    });

    it('should update threat status', async () => {
      mockClient.put = jest.fn().mockResolvedValue(undefined);

      await service.updateThreatStatus('threat-1', 'acknowledge');

      expect(mockClient.put).toHaveBeenCalledWith(
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

      mockClient.get = jest.fn().mockResolvedValue(mockAnalytics);

      const result = await service.getThreatAnalytics();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SECURITY.THREAT_ANALYTICS,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockAnalytics);
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

  describe('formatThreatLevel', () => {
    it('should format threat level correctly', () => {
      expect(service.formatThreatLevel('low')).toBe('Low Risk');
      expect(service.formatThreatLevel('high')).toBe('High Risk');
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