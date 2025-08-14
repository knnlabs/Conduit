import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchSecurityService } from '../FetchSecurityService';
import { ENDPOINTS } from '../../constants';
import type {
  SecurityEventParams,
  SecurityEventPage,
  SecurityEventExtended,
} from '../../models/securityExtended';
import type {
  SecurityEvent,
  CreateSecurityEventDto,
} from '../../models/security';

describe('FetchSecurityService - Security Events', () => {
  let service: FetchSecurityService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchSecurityService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      mockClient.get = jest.fn().mockResolvedValue(mockPage);

      const params: SecurityEventParams = {
        pageNumber: 1,
        pageSize: 20,
        severity: 'high',
        status: 'active'
      };

      const result = await service.getSecurityEvents(params);

      expect(mockClient.get).toHaveBeenCalledWith(
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

      mockClient.get = jest.fn().mockResolvedValue(mockEvent);

      const result = await service.getSecurityEventById('event-1');

      expect(mockClient.get).toHaveBeenCalledWith(
        '/api/security/events/event-1',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockEvent);
    });

    it('should acknowledge security event', async () => {
      mockClient.post = jest.fn().mockResolvedValue(undefined);

      await service.acknowledgeSecurityEvent('event-1');

      expect(mockClient.post).toHaveBeenCalledWith(
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

      mockClient.post = jest.fn().mockResolvedValue(mockEvent);

      const result = await service.reportEvent(createDto);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.SECURITY.REPORT_EVENT,
        createDto,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockEvent);
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
});