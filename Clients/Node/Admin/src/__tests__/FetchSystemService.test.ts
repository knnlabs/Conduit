import { createMockClient, type MockClient } from './helpers/mockClient.helper';
import { FetchSystemService } from '../services/FetchSystemService';

import { ENDPOINTS } from '../constants';

describe('FetchSystemService', () => {
  let mockClient: MockClient;
  let service: FetchSystemService;

  beforeEach(() => {
    mockClient = createMockClient();

    service = new FetchSystemService(mockClient as any);
  });

  describe('getSystemInfo', () => {
    it('should get system information', async () => {
      const mockSystemInfo = {
        version: '1.0.0',
        buildDate: '2025-01-11',
        environment: 'production',
        uptime: 86400,
        systemTime: '2025-01-11T10:00:00Z',
        features: {
          ipFiltering: true,
          providerHealth: true,
          costTracking: true,
          audioSupport: false,
        },
        runtime: {
          dotnetVersion: '8.0',
          os: 'Linux',
          architecture: 'x64',
          memoryUsage: 512,
        },
        database: {
          provider: 'PostgreSQL',
          isConnected: true,
        },
      };
      mockClient.get.mockResolvedValue(mockSystemInfo);

      const result = await service.getSystemInfo();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SYSTEM.INFO,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockSystemInfo);
    });
  });

  describe('getHealth', () => {
    it('should get system health status', async () => {
      const mockHealth = {
        status: 'healthy',
        timestamp: '2025-01-11T10:00:00Z',
        checks: {
          database: {
            status: 'healthy',
            duration: 5,
          },
          redis: {
            status: 'healthy',
            duration: 2,
          },
        },
        totalDuration: 7,
      };
      mockClient.get.mockResolvedValue(mockHealth);

      const result = await service.getHealth();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SYSTEM.HEALTH,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockHealth);
    });
  });

  describe('getWebUIVirtualKey', () => {
    it('should return existing WebUI virtual key from settings', async () => {
      const mockSetting = {
        id: 1,
        key: 'WebUI_VirtualKey',
        value: 'vk_webui_existing',
        description: 'Virtual key for WebUI Core API access',
      };
      mockClient.get.mockResolvedValue(mockSetting);

      const result = await service.getWebUIVirtualKey();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('WebUI_VirtualKey'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toBe('vk_webui_existing');
    });

    it('should create new WebUI virtual key if not exists', async () => {
      // First call to get setting fails (not found)
      mockClient.get.mockRejectedValueOnce(new Error('Not found'));
      
      // Create virtual key response
      const mockCreateResponse = {
        virtualKey: 'vk_webui_new_123456',
        keyInfo: {
          id: 100,
          keyName: 'WebUI Internal Key',
          isEnabled: true,
        },
      };
      mockClient.post.mockResolvedValueOnce(mockCreateResponse);
      
      // Create global setting response
      const mockSettingResponse = {
        id: 2,
        key: 'WebUI_VirtualKey',
        value: 'vk_webui_new_123456',
      };
      mockClient.post.mockResolvedValueOnce(mockSettingResponse);

      const result = await service.getWebUIVirtualKey();

      // Should try to get existing first
      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('WebUI_VirtualKey'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );

      // Should create virtual key
      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.VIRTUAL_KEYS.BASE,
        {
          keyName: 'WebUI Internal Key',
          metadata: expect.stringContaining('"originator":"Admin SDK"'),
        },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );

      // Should store in global settings
      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL,
        {
          key: 'WebUI_VirtualKey',
          value: 'vk_webui_new_123456',
          description: 'Virtual key for WebUI Core API access',
        },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );

      expect(result).toBe('vk_webui_new_123456');
    });
  });

  describe('helper methods', () => {
    const mockHealth = {
      status: 'degraded' as const,
      timestamp: '2025-01-11T10:00:00Z',
      checks: {
        database: {
          status: 'healthy' as const,
          duration: 5,
        },
        redis: {
          status: 'unhealthy' as const,
          duration: 2,
          error: 'Connection timeout',
        },
        api: {
          status: 'degraded' as const,
          duration: 10,
        },
      },
      totalDuration: 17,
    };

    const mockSystemInfo = {
      version: '1.0.0',
      buildDate: '2025-01-11',
      environment: 'production',
      uptime: 90061, // 1 day, 1 hour, 1 minute, 1 second
      systemTime: '2025-01-11T10:00:00Z',
      features: {
        ipFiltering: true,
        providerHealth: false,
        costTracking: true,
        audioSupport: false,
      },
      runtime: {
        dotnetVersion: '8.0',
        os: 'Linux',
        architecture: 'x64',
        memoryUsage: 512,
      },
      database: {
        provider: 'PostgreSQL',
        isConnected: true,
      },
    };

    it('isSystemHealthy should check health status', () => {
      expect(service.isSystemHealthy(mockHealth)).toBe(false);
      expect(service.isSystemHealthy({ ...mockHealth, status: 'healthy' })).toBe(true);
    });

    it('getUnhealthyServices should return list of unhealthy services', () => {
      const unhealthy = service.getUnhealthyServices(mockHealth);
      expect(unhealthy).toEqual(['redis', 'api']);
    });

    it('formatUptime should format seconds to human readable', () => {
      expect(service.formatUptime(90061)).toBe('1d 1h 1m');
      expect(service.formatUptime(3661)).toBe('1h 1m');
      expect(service.formatUptime(61)).toBe('1m');
    });

    it('isFeatureEnabled should check feature flags', () => {
      expect(service.isFeatureEnabled(mockSystemInfo, 'ipFiltering')).toBe(true);
      expect(service.isFeatureEnabled(mockSystemInfo, 'providerHealth')).toBe(false);
      expect(service.isFeatureEnabled(mockSystemInfo, 'costTracking')).toBe(true);
      expect(service.isFeatureEnabled(mockSystemInfo, 'audioSupport')).toBe(false);
    });
  });
});