import { FetchModelMappingsService } from '../services/FetchModelMappingsService';
import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ENDPOINTS } from '../constants';

describe('FetchModelMappingsService', () => {
  let mockClient: FetchBaseApiClient;
  let service: FetchModelMappingsService;

  beforeEach(() => {
    mockClient = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
      request: jest.fn(),
    } as any;

    service = new FetchModelMappingsService(mockClient);
  });

  describe('list', () => {
    it('should list model mappings with default pagination', async () => {
      const mockResponse = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 10,
        totalPages: 0,
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.list();

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.MODEL_MAPPINGS.BASE}?page=1&pageSize=10`,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should list model mappings with custom pagination', async () => {
      const mockResponse = {
        items: [],
        totalCount: 0,
        page: 2,
        pageSize: 20,
        totalPages: 0,
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.list(2, 20);

      expect(mockClient.get).toHaveBeenCalledWith(
        `${ENDPOINTS.MODEL_MAPPINGS.BASE}?page=2&pageSize=20`,
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
    it('should get model mapping by ID', async () => {
      const mockMapping = {
        id: 1,
        modelId: 'gpt-4',
        providerId: 'openai',
        providerModelId: 'gpt-4',
        isEnabled: true,
        priority: 1,
        supportsVision: true,
        supportsStreaming: true,
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockMapping);

      const result = await service.getById(1);

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.BY_ID(1),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockMapping);
    });
  });

  describe('create', () => {
    it('should create a new model mapping', async () => {
      const createData = {
        modelId: 'gpt-4',
        providerId: 'openai',
        providerModelId: 'gpt-4',
        isEnabled: true,
        priority: 1,
      };
      const mockResponse = {
        id: 1,
        ...createData,
        createdAt: '2025-01-11T10:00:00Z',
        updatedAt: '2025-01-11T10:00:00Z',
      };
      (mockClient.post as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.create(createData);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.BASE,
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
    it('should update an existing model mapping', async () => {
      const updateData = {
        id: 1,
        modelId: 'gpt-4',
        isEnabled: false,
        priority: 2,
      };
      (mockClient.put as jest.Mock).mockResolvedValue(undefined);

      await service.update(1, updateData);

      expect(mockClient.put).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.BY_ID(1),
        updateData,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
    });
  });

  describe('deleteById', () => {
    it('should delete a model mapping', async () => {
      (mockClient.delete as jest.Mock).mockResolvedValue(undefined);

      await service.deleteById(1);

      expect(mockClient.delete).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.BY_ID(1),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
    });
  });

  describe('discoverModels', () => {
    it('should discover all available models', async () => {
      const mockModels = [
        {
          provider: 'openai',
          model: 'gpt-4',
          displayName: 'GPT-4',
          capabilities: {
            chat: true,
            chatStream: true,
            vision: true,
            functionCalling: true,
          },
        },
      ];
      (mockClient.get as jest.Mock).mockResolvedValue(mockModels);

      const result = await service.discoverModels();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.DISCOVER_ALL,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockModels);
    });
  });

  describe('testCapability', () => {
    it('should test model capability', async () => {
      const mockMapping = {
        id: 1,
        modelId: 'gpt-4',
      };
      const mockResult = {
        modelAlias: 'gpt-4',
        capability: 'vision',
        isSupported: true,
        confidence: 0.95,
        testDurationMs: 150,
        testedAt: '2025-01-11T10:00:00Z',
      };
      
      (mockClient.get as jest.Mock).mockResolvedValueOnce(mockMapping);
      (mockClient.post as jest.Mock).mockResolvedValue(mockResult);

      const result = await service.testCapability(1, 'vision');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.BY_ID(1),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.TEST_CAPABILITY('gpt-4', 'vision'),
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

  describe('helper methods', () => {
    const mockMapping = {
      id: 1,
      modelId: 'gpt-4',
      providerId: 'openai',
      providerModelId: 'gpt-4',
      isEnabled: true,
      priority: 1,
      supportsVision: true,
      supportsImageGeneration: false,
      supportsAudioTranscription: false,
      supportsTextToSpeech: false,
      supportsRealtimeAudio: false,
      supportsFunctionCalling: true,
      supportsStreaming: true,
      isDefault: false,
      createdAt: '2025-01-11T10:00:00Z',
      updatedAt: '2025-01-11T10:00:00Z',
    };

    it('isMappingEnabled should check enabled status', () => {
      expect(service.isMappingEnabled(mockMapping)).toBe(true);
      expect(service.isMappingEnabled({ ...mockMapping, isEnabled: false })).toBe(false);
    });

    it('getMappingCapabilities should return list of capabilities', () => {
      const capabilities = service.getMappingCapabilities(mockMapping);
      expect(capabilities).toEqual(['vision', 'function-calling', 'streaming']);
    });

    it('formatMappingName should format display name', () => {
      expect(service.formatMappingName(mockMapping)).toBe('gpt-4 → openai:gpt-4');
    });

    it('supportsCapability should check specific capability', () => {
      expect(service.supportsCapability(mockMapping, 'vision')).toBe(true);
      expect(service.supportsCapability(mockMapping, 'image-generation')).toBe(false);
      expect(service.supportsCapability(mockMapping, 'function-calling')).toBe(true);
      expect(service.supportsCapability(mockMapping, 'streaming')).toBe(true);
      expect(service.supportsCapability(mockMapping, 'unknown')).toBe(false);
    });
  });

  describe('bulkCreate', () => {
    it('should bulk create model mappings', async () => {
      const mappings = [
        {
          modelId: 'gpt-4',
          providerId: 'openai',
          providerModelId: 'gpt-4',
        },
        {
          modelId: 'claude-3',
          providerId: 'anthropic',
          providerModelId: 'claude-3-opus',
        },
      ];
      const mockResponse = {
        created: mappings.map((m, i) => ({ ...m, id: i + 1 })),
        updated: [],
        failed: [],
      };
      (mockClient.post as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.bulkCreate(mappings);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.BULK,
        {
          mappings: mappings,
          replaceExisting: false,
          validateProviderModels: true,
        },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });
});