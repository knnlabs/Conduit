import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchSecurityService } from '../FetchSecurityService';
import type {
  AccessPolicy,
  CreateAccessPolicyDto,
  UpdateAccessPolicyDto,
} from '../../models/securityExtended';

describe('FetchSecurityService - Access Control', () => {
  let service: FetchSecurityService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchSecurityService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      mockClient.get = jest.fn().mockResolvedValue(mockPolicies);

      const result = await service.getAccessPolicies();

      expect(mockClient.get).toHaveBeenCalledWith(
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
        name: createDto.name,
        type: createDto.type,
        rules: createDto.rules,
        enabled: createDto.enabled ?? true,
        description: createDto.description,
        priority: 1,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z'
      };

      mockClient.post = jest.fn().mockResolvedValue(mockPolicy);

      const result = await service.createAccessPolicy(createDto);

      expect(mockClient.post).toHaveBeenCalledWith(
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

      mockClient.put = jest.fn().mockResolvedValue(mockPolicy);

      const result = await service.updateAccessPolicy('policy-1', updateDto);

      expect(mockClient.put).toHaveBeenCalledWith(
        '/api/security/policies/policy-1',
        updateDto,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockPolicy);
    });

    it('should delete access policy', async () => {
      mockClient.delete = jest.fn().mockResolvedValue(undefined);

      await service.deleteAccessPolicy('policy-1');

      expect(mockClient.delete).toHaveBeenCalledWith(
        '/api/security/policies/policy-1',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });
  });
});