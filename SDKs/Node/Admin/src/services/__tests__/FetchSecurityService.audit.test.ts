import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchSecurityService } from '../FetchSecurityService';
import { ENDPOINTS } from '../../constants';
import type {
  AuditLogParams,
  AuditLogPage,
  ExportParams,
  ExportResult,
} from '../../models/securityExtended';
import type {
  ComplianceMetrics,
} from '../../models/security';

describe('FetchSecurityService - Audit and Compliance', () => {
  let service: FetchSecurityService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchSecurityService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      mockClient.get = jest.fn().mockResolvedValue(mockPage);

      const params: AuditLogParams = {
        pageNumber: 1,
        pageSize: 20,
        action: 'update',
        userId: 'user-1'
      };

      const result = await service.getAuditLogs(params);

      expect(mockClient.get).toHaveBeenCalledWith(
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

      mockClient.post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.exportAuditLogs(params);

      expect(mockClient.post).toHaveBeenCalledWith(
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

      mockClient.get = jest.fn().mockResolvedValue(mockMetrics);

      const result = await service.getComplianceMetrics();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SECURITY.COMPLIANCE_METRICS,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockMetrics);
    });

    it('should get compliance report', async () => {
      const mockReport = { report: 'data' };

      mockClient.get = jest.fn().mockResolvedValue(mockReport);

      const result = await service.getComplianceReport('2024-01-01', '2024-01-31');

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.SECURITY.COMPLIANCE_REPORT}?startDate=2024-01-01&endDate=2024-01-31`,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockReport);
    });
  });
});