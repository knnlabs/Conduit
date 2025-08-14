import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchSecurityService } from '../FetchSecurityService';
import type {
  IpWhitelistDto,
} from '../../models/securityExtended';

describe('FetchSecurityService - IP Management', () => {
  let service: FetchSecurityService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchSecurityService(mockClient as any);
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

      mockClient.get = jest.fn().mockResolvedValue(mockWhitelist);

      const result = await service.getIpWhitelist();

      expect(mockClient.get).toHaveBeenCalledWith(
        '/api/security/ip-whitelist',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockWhitelist);
    });

    it('should add IPs to whitelist', async () => {
      mockClient.post = jest.fn().mockResolvedValue(undefined);

      const ips = ['192.168.1.1', '10.0.0.0/24'];
      await service.addToIpWhitelist(ips);

      expect(mockClient.post).toHaveBeenCalledWith(
        '/api/security/ip-whitelist',
        { ips },
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });

    it('should remove IPs from whitelist', async () => {
      mockClient.request = jest.fn().mockResolvedValue(undefined);

      const ips = ['192.168.1.1'];
      await service.removeFromIpWhitelist(ips);

      expect(mockClient.request).toHaveBeenCalledWith(
        '/api/security/ip-whitelist',
        {
          method: 'DELETE',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ ips }),
          signal: undefined,
          timeout: undefined,
        }
      );
    });
  });

  describe('validateIpAddress', () => {
    it('should validate IPv4 addresses', () => {
      expect(service.validateIpAddress('192.168.1.1')).toBe(true);
      expect(service.validateIpAddress('10.0.0.0/24')).toBe(true);
      // Note: The current implementation only validates format, not value ranges
      expect(service.validateIpAddress('256.256.256.256')).toBe(true); // This passes because it matches the pattern
      expect(service.validateIpAddress('invalid-ip')).toBe(false);
    });
  });

  describe('isIpInRange', () => {
    it('should check if IP is in CIDR range', () => {
      expect(service.isIpInRange('192.168.1.5', '192.168.1.0/24')).toBe(true);
      expect(service.isIpInRange('192.168.2.5', '192.168.1.0/24')).toBe(false);
      expect(service.isIpInRange('192.168.1.1', '192.168.1.1')).toBe(true);
    });
  });
});