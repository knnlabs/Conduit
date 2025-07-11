import { FetchSettingsService } from '../services/FetchSettingsService';
import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ENDPOINTS } from '../constants';
import type { GlobalSettingDto } from '../models/settings';

describe('FetchSettingsService', () => {
  let mockClient: FetchBaseApiClient;
  let service: FetchSettingsService;

  const mockSetting: GlobalSettingDto = {
    key: 'rate_limit',
    value: '100',
    description: 'API rate limit per minute',
    dataType: 'number',
    category: 'Performance',
    isSecret: false,
    createdAt: '2025-01-11T10:00:00Z',
    updatedAt: '2025-01-11T10:00:00Z',
  };

  const mockSettings: GlobalSettingDto[] = [
    mockSetting,
    {
      key: 'enable_logging',
      value: 'true',
      description: 'Enable application logging',
      dataType: 'boolean',
      category: 'Debug',
      isSecret: false,
      createdAt: '2025-01-11T10:00:00Z',
      updatedAt: '2025-01-11T10:00:00Z',
    },
    {
      key: 'api_key',
      value: 'secret123',
      description: 'Master API key',
      dataType: 'string',
      category: 'Security',
      isSecret: true,
      createdAt: '2025-01-11T10:00:00Z',
      updatedAt: '2025-01-11T10:00:00Z',
    },
  ];

  beforeEach(() => {
    mockClient = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
      request: jest.fn(),
    } as any;

    service = new FetchSettingsService(mockClient);
  });

  describe('getGlobalSettings', () => {
    it('should get all global settings', async () => {
      (mockClient.get as jest.Mock).mockResolvedValue(mockSettings);

      const result = await service.getGlobalSettings();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result.settings).toEqual(mockSettings);
      expect(result.categories).toEqual(['Performance', 'Debug', 'Security']);
      expect(result.lastModified).toBeDefined();
    });
  });

  describe('listGlobalSettings', () => {
    it('should list settings with pagination', async () => {
      const mockResponse = {
        items: mockSettings,
        totalCount: 3,
        page: 1,
        pageSize: 10,
        totalPages: 1,
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.listGlobalSettings(1, 10);

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.SETTINGS.GLOBAL}?page=1&pageSize=10`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('getGlobalSetting', () => {
    it('should get a specific setting by key', async () => {
      (mockClient.get as jest.Mock).mockResolvedValue(mockSetting);

      const result = await service.getGlobalSetting('rate_limit');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('rate_limit'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockSetting);
    });
  });

  describe('createGlobalSetting', () => {
    it('should create a new global setting', async () => {
      const createData = {
        key: 'new_setting',
        value: 'test',
        description: 'A new test setting',
        dataType: 'string' as const,
        category: 'Test',
      };
      const created = { ...createData, createdAt: '2025-01-11T10:00:00Z', updatedAt: '2025-01-11T10:00:00Z' };
      (mockClient.post as jest.Mock).mockResolvedValue(created);

      const result = await service.createGlobalSetting(createData);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL,
        createData,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(created);
    });
  });

  describe('updateGlobalSetting', () => {
    it('should update a setting', async () => {
      (mockClient.put as jest.Mock).mockResolvedValue(undefined);

      await service.updateGlobalSetting('rate_limit', {
        value: '200',
        description: 'Updated rate limit',
      });

      expect(mockClient.put).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('rate_limit'),
        {
          value: '200',
          description: 'Updated rate limit',
        },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
    });
  });

  describe('deleteGlobalSetting', () => {
    it('should delete a setting', async () => {
      (mockClient.delete as jest.Mock).mockResolvedValue(undefined);

      await service.deleteGlobalSetting('old_setting');

      expect(mockClient.delete).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('old_setting'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
    });
  });

  describe('batchUpdateSettings', () => {
    it('should batch update multiple settings', async () => {
      const updates = [
        { key: 'rate_limit', value: 200 },
        { key: 'enable_logging', value: false },
      ];
      (mockClient.post as jest.Mock).mockResolvedValue(undefined);

      await service.batchUpdateSettings(updates);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.BATCH_UPDATE,
        { settings: updates },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
    });
  });

  describe('getSettingsByCategory', () => {
    it('should get settings grouped by category', async () => {
      (mockClient.get as jest.Mock).mockResolvedValue(mockSettings);

      const result = await service.getSettingsByCategory();

      expect(result).toHaveLength(3);
      expect(result[0].name).toBe('Performance');
      expect(result[0].settings).toHaveLength(1);
      expect(result[1].name).toBe('Debug');
      expect(result[1].settings).toHaveLength(1);
      expect(result[2].name).toBe('Security');
      expect(result[2].settings).toHaveLength(1);
    });
  });

  describe('helper methods', () => {
    it('settingExists should check if setting exists', async () => {
      (mockClient.get as jest.Mock).mockResolvedValue(mockSetting);

      const exists = await service.settingExists('rate_limit');
      expect(exists).toBe(true);

      (mockClient.get as jest.Mock).mockRejectedValue({ statusCode: 404 });
      const notExists = await service.settingExists('non_existent');
      expect(notExists).toBe(false);
    });

    it('getTypedSettingValue should return typed values', async () => {
      (mockClient.get as jest.Mock).mockResolvedValueOnce(mockSetting);
      const numberValue = await service.getTypedSettingValue<number>('rate_limit');
      expect(numberValue).toBe(100);

      (mockClient.get as jest.Mock).mockResolvedValueOnce(mockSettings[1]);
      const boolValue = await service.getTypedSettingValue<boolean>('enable_logging');
      expect(boolValue).toBe(true);

      const jsonSetting = {
        ...mockSetting,
        dataType: 'json',
        value: '{"key": "value"}',
      };
      (mockClient.get as jest.Mock).mockResolvedValueOnce(jsonSetting);
      const jsonValue = await service.getTypedSettingValue<{ key: string }>('json_setting');
      expect(jsonValue).toEqual({ key: 'value' });
    });

    it('updateTypedSetting should update with type conversion', async () => {
      (mockClient.put as jest.Mock).mockResolvedValue(undefined);

      await service.updateTypedSetting('rate_limit', 150);
      expect(mockClient.put).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('rate_limit'),
        { value: '150', description: undefined },
        undefined
      );

      await service.updateTypedSetting('config', { foo: 'bar' }, 'JSON config');
      expect(mockClient.put).toHaveBeenCalledWith(
        ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('config'),
        { value: '{"foo":"bar"}', description: 'JSON config' },
        undefined
      );
    });

    it('getSecretSettings should return only secret settings', async () => {
      (mockClient.get as jest.Mock).mockResolvedValue(mockSettings);

      const secrets = await service.getSecretSettings();
      expect(secrets).toHaveLength(1);
      expect(secrets[0].key).toBe('api_key');
    });

    it('validateSettingValue should validate based on data type', () => {
      expect(service.validateSettingValue('100', 'number')).toBe(true);
      expect(service.validateSettingValue('abc', 'number')).toBe(false);
      expect(service.validateSettingValue('true', 'boolean')).toBe(true);
      expect(service.validateSettingValue('yes', 'boolean')).toBe(false);
      expect(service.validateSettingValue('{"valid": "json"}', 'json')).toBe(true);
      expect(service.validateSettingValue('invalid json', 'json')).toBe(false);
      expect(service.validateSettingValue('any string', 'string')).toBe(true);
    });

    it('formatSettingValue should format values for display', () => {
      expect(service.formatSettingValue(mockSettings[2])).toBe('********'); // Secret
      
      const jsonSetting = {
        ...mockSetting,
        dataType: 'json' as const,
        value: '{"key":"value"}',
      };
      expect(service.formatSettingValue(jsonSetting)).toBe('{\n  "key": "value"\n}');

      expect(service.formatSettingValue(mockSetting)).toBe('100');
    });
  });
});