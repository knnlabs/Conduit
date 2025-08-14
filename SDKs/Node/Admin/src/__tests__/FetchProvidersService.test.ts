import { FetchProvidersService } from '../services/FetchProvidersService';
import { ENDPOINTS } from '../constants';
import { ProviderType } from '../models/providerType';
import { createMockClient, type MockClient } from './helpers/mockClient.helper';

describe('FetchProvidersService', () => {
  let mockClient: MockClient;
  let service: FetchProvidersService;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchProvidersService(mockClient as any);
  });

  describe('list', () => {
    it('should list providers with default pagination', async () => {
      const mockResponse = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 10,
        totalPages: 0,
      };
      mockClient.get.mockResolvedValue(mockResponse);

      const result = await service.list();

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.PROVIDERS.BASE}?page=1&pageSize=10`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should list providers with custom pagination', async () => {
      const mockResponse = {
        items: [],
        totalCount: 0,
        page: 2,
        pageSize: 20,
        totalPages: 0,
      };
      mockClient.get.mockResolvedValue(mockResponse);

      const result = await service.list(2, 20);

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.PROVIDERS.BASE}?page=2&pageSize=20`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('getById', () => {
    it('should get provider by ID', async () => {
      const mockProvider = {
        id: 1,
        providerType: ProviderType.OpenAI,
        apiKey: 'test-key',
        isEnabled: true,
      };
      mockClient.get.mockResolvedValue(mockProvider);

      const result = await service.getById(1);

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDERS.BY_ID(1),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockProvider);
    });
  });

  describe('create', () => {
    it('should create a new provider', async () => {
      const createData = {
        providerType: ProviderType.OpenAI,
        apiKey: 'test-key',
        isEnabled: true,
      };
      const mockResponse = {
        id: 1,
        ...createData,
        createdAt: '2025-01-11T10:00:00Z',
        updatedAt: '2025-01-11T10:00:00Z',
      };
      mockClient.post.mockResolvedValue(mockResponse);

      const result = await service.create(createData);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDERS.BASE,
        createData,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('update', () => {
    it('should update an existing provider', async () => {
      const updateData = {
        id: 1,
        apiKey: 'new-key',
        isEnabled: false,
      };
      const mockResponse = {
        id: 1,
        providerType: ProviderType.OpenAI,
        apiKey: 'new-key',
        isEnabled: false,
      };
      mockClient.put.mockResolvedValue(mockResponse);

      const result = await service.update(1, updateData);

      expect(mockClient.put).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDERS.BY_ID(1),
        updateData,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('deleteById', () => {
    it('should delete a provider', async () => {
      mockClient.delete.mockResolvedValue(undefined);

      await service.deleteById(1);

      expect(mockClient.delete).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDERS.BY_ID(1),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
    });
  });

  describe('testConnectionById', () => {
    it('should test connection by provider ID', async () => {
      const mockResult = {
        success: true,
        message: 'Connection successful',
        latency: 150,
      };
      mockClient.post.mockResolvedValue(mockResult);

      const result = await service.testConnectionById(1);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDERS.TEST_BY_ID(1),
        undefined,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResult);
    });
  });

  describe('testConfig', () => {
    it('should test provider configuration', async () => {
      const config = {
        providerType: ProviderType.OpenAI,
        apiKey: 'test-key',
        baseUrl: 'https://api.openai.com',
      };
      const mockResult = {
        success: true,
        message: 'Configuration is valid',
      };
      mockClient.post.mockResolvedValue(mockResult);

      const result = await service.testConfig(config);

      expect(mockClient.post).toHaveBeenCalledWith(
        `${ENDPOINTS.PROVIDERS.BASE}/test`,
        config,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResult);
    });
  });

  describe('helper methods', () => {
    const mockProvider = {
      id: 1,
      providerType: ProviderType.OpenAI,
      apiKey: 'test-key',
      apiBase: 'https://api.openai.com',
      isEnabled: true,
      createdAt: '2025-01-11T10:00:00Z',
      updatedAt: '2025-01-11T10:00:00Z',
    };

    it('isProviderEnabled should check enabled status', () => {
      expect(service.isProviderEnabled(mockProvider)).toBe(true);
      expect(service.isProviderEnabled({ ...mockProvider, isEnabled: false })).toBe(false);
    });

    it('hasApiKey should check for API key', () => {
      expect(service.hasApiKey(mockProvider)).toBe(true);
      expect(service.hasApiKey({ ...mockProvider, apiKey: '' })).toBe(false);
      expect(service.hasApiKey({ ...mockProvider, apiKey: null as any })).toBe(false);
    });

    it('formatProviderName should return provider name', () => {
      expect(service.formatProviderName(mockProvider)).toBe('openai');
    });

    it('getProviderStatus should return correct status', () => {
      expect(service.getProviderStatus(mockProvider)).toBe('active');
      expect(service.getProviderStatus({ ...mockProvider, isEnabled: false })).toBe('inactive');
      expect(service.getProviderStatus({ ...mockProvider, apiKey: '' })).toBe('unconfigured');
    });
  });
});