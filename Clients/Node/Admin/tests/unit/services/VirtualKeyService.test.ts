import { VirtualKeyService } from '../../../src/services/VirtualKeyService';
import { ValidationError, NotImplementedError } from '../../../src/utils/errors';
import { CreateVirtualKeyRequest } from '../../../src/models/virtualKey';
import axios from 'axios';

// Mock axios
jest.mock('axios');
const mockedAxios = axios as jest.Mocked<typeof axios>;

// Mock axios instance
const mockAxiosInstance = {
  interceptors: {
    request: {
      use: jest.fn(),
    },
    response: {
      use: jest.fn(),
    },
  },
  request: jest.fn(),
  get: jest.fn(),
  post: jest.fn(),
  put: jest.fn(),
  delete: jest.fn(),
  patch: jest.fn(),
};

// Setup axios.create mock
mockedAxios.create = jest.fn(() => mockAxiosInstance as any);

describe('VirtualKeyService', () => {
  let service: VirtualKeyService;
  const mockConfig = {
    baseUrl: 'http://localhost:5002',
    masterKey: 'test-master-key',
  };

  beforeEach(() => {
    jest.clearAllMocks();
    // Reset axios mock
    mockedAxios.create.mockReturnValue(mockAxiosInstance as any);
    service = new VirtualKeyService(mockConfig);
  });

  describe('create', () => {
    it('should create a virtual key with valid request', async () => {
      const request: CreateVirtualKeyRequest = {
        keyName: 'Test Key',
        allowedModels: 'gpt-4,gpt-3.5-turbo',
        maxBudget: 100,
        budgetDuration: 'Monthly',
      };

      const mockResponse = {
        virtualKey: 'ck_test123',
        keyInfo: {
          id: 1,
          keyName: 'Test Key',
          allowedModels: 'gpt-4,gpt-3.5-turbo',
          maxBudget: 100,
          currentSpend: 0,
          budgetDuration: 'Monthly',
          budgetStartDate: '2024-01-01',
          isEnabled: true,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      };

      // Mock the axios request directly
      mockAxiosInstance.request.mockResolvedValue({ data: mockResponse });

      const result = await service.create(request);

      expect(result).toEqual(mockResponse);
      expect(mockAxiosInstance.request).toHaveBeenCalledWith(
        expect.objectContaining({
          method: 'POST',
          url: '/virtualkeys',
          data: request
        })
      );
    });

    it('should throw ValidationError for invalid key name', async () => {
      const request: CreateVirtualKeyRequest = {
        keyName: '', // Empty key name
      };

      await expect(service.create(request)).rejects.toThrow(ValidationError);
    });

    it('should throw ValidationError for invalid budget', async () => {
      const request: CreateVirtualKeyRequest = {
        keyName: 'Test Key',
        maxBudget: -10, // Negative budget
      };

      await expect(service.create(request)).rejects.toThrow(ValidationError);
    });

    it('should throw ValidationError for budget exceeding maximum', async () => {
      const request: CreateVirtualKeyRequest = {
        keyName: 'Test Key',
        maxBudget: 2000000, // Exceeds max of 1,000,000
      };

      await expect(service.create(request)).rejects.toThrow(ValidationError);
    });
  });

  describe('list', () => {
    it('should list virtual keys with default pagination', async () => {
      const mockResponse = {
        items: [
          {
            id: 1,
            keyName: 'Key 1',
            allowedModels: 'gpt-4',
            maxBudget: 100,
            currentSpend: 50,
            budgetDuration: 'Monthly',
            budgetStartDate: '2024-01-01',
            isEnabled: true,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
          },
        ],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 20,
        totalPages: 1,
      };

      // Mock the axios request directly
      mockAxiosInstance.request.mockResolvedValue({ data: mockResponse });
      
      jest.spyOn(service as any, 'withCache').mockImplementation(
        async (_key: any, fetcher: any) => fetcher()
      );

      const result = await service.list();

      expect(result).toEqual(mockResponse);
      expect(mockAxiosInstance.request).toHaveBeenCalledWith(
        expect.objectContaining({
          method: 'GET',
          url: '/virtualkeys',
          params: expect.objectContaining({
            pageNumber: 1,
            pageSize: 20,
          })
        })
      );
    });

    it('should list virtual keys with custom filters', async () => {
      const filters = {
        pageSize: 50,
        isEnabled: true,
        budgetDuration: 'Monthly' as const,
        search: 'production',
      };

      const mockResponse = {
        items: [],
        totalCount: 0,
        pageNumber: 1,
        pageSize: 50,
        totalPages: 0,
      };

      // Mock the axios request directly
      mockAxiosInstance.request.mockResolvedValue({ data: mockResponse });
      
      jest.spyOn(service as any, 'withCache').mockImplementation(
        async (_key: any, fetcher: any) => fetcher()
      );

      const result = await service.list(filters);

      expect(result).toEqual(mockResponse);
      expect(mockAxiosInstance.request).toHaveBeenCalledWith(
        expect.objectContaining({
          method: 'GET',
          url: '/virtualkeys',
          params: expect.objectContaining({
            pageSize: 50,
            isEnabled: true,
            budgetDuration: 'Monthly',
            search: 'production',
          })
        })
      );
    });
  });

  describe('validate', () => {
    it('should validate a key successfully', async () => {
      const key = 'ck_test123';
      const mockResponse = {
        isValid: true,
        virtualKeyId: 1,
        keyName: 'Test Key',
        allowedModels: ['gpt-4', 'gpt-3.5-turbo'],
        maxBudget: 100,
        currentSpend: 50,
        budgetRemaining: 50,
      };

      mockAxiosInstance.request.mockResolvedValue({ data: mockResponse });

      const result = await service.validate(key);

      expect(result).toEqual(mockResponse);
      expect(mockAxiosInstance.request).toHaveBeenCalledWith(
        expect.objectContaining({
          method: 'POST',
          url: '/virtualkeys/validate',
          data: { key }
        })
      );
    });

    it('should return invalid key response', async () => {
      const key = 'ck_invalid';
      const mockResponse = {
        isValid: false,
        reason: 'Key not found',
      };

      mockAxiosInstance.request.mockResolvedValue({ data: mockResponse });

      const result = await service.validate(key);

      expect(result).toEqual(mockResponse);
      expect(result.isValid).toBe(false);
    });
  });

  describe('stub functions', () => {
    it('should throw NotImplementedError for getStatistics', async () => {
      await expect(service.getStatistics()).rejects.toThrow(NotImplementedError);
    });

    it('should throw NotImplementedError for bulkCreate', async () => {
      const requests = [
        { keyName: 'Key 1' },
        { keyName: 'Key 2' },
      ];

      await expect(service.bulkCreate(requests)).rejects.toThrow(NotImplementedError);
    });

    it('should throw NotImplementedError for exportKeys', async () => {
      await expect(service.exportKeys('csv')).rejects.toThrow(NotImplementedError);
      await expect(service.exportKeys('json')).rejects.toThrow(NotImplementedError);
    });
  });

  describe('cache invalidation', () => {
    it('should invalidate cache after create', async () => {
      const mockCache = {
        get: jest.fn(),
        set: jest.fn(),
        delete: jest.fn(),
        clear: jest.fn(),
      };

      const serviceWithCache = new VirtualKeyService({
        ...mockConfig,
        cache: mockCache,
      });

      mockAxiosInstance.request.mockResolvedValue({ 
        data: {
          virtualKey: 'ck_test',
          keyInfo: { id: 1 },
        }
      });

      await serviceWithCache.create({ keyName: 'Test' });

      expect(mockCache.clear).toHaveBeenCalled();
    });

    it('should invalidate cache after update', async () => {
      const mockCache = {
        get: jest.fn(),
        set: jest.fn(),
        delete: jest.fn(),
        clear: jest.fn(),
      };

      const serviceWithCache = new VirtualKeyService({
        ...mockConfig,
        cache: mockCache,
      });

      mockAxiosInstance.request.mockResolvedValue({ data: undefined });

      await serviceWithCache.update(1, { keyName: 'Updated' });

      expect(mockCache.clear).toHaveBeenCalled();
    });
  });
});