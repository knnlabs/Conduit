import { FetchProviderModelsService } from '../services/FetchProviderModelsService';
import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ENDPOINTS } from '../constants';
import type { 
  ModelDto, 
  ModelDetailsDto, 
  ModelSearchFilters, 
  ModelCapabilities 
} from '../models/providerModels';

describe('FetchProviderModelsService', () => {
  let mockClient: FetchBaseApiClient;
  let service: FetchProviderModelsService;

  const mockModel: ModelDto = {
    id: 'gpt-4',
    name: 'gpt-4',
    displayName: 'GPT-4',
    provider: 'openai',
    description: 'OpenAI GPT-4 model',
    contextWindow: 8192,
    maxTokens: 4096,
    inputCost: 0.03,
    outputCost: 0.06,
    capabilities: {
      chat: true,
      completion: true,
      embedding: false,
      vision: false,
      functionCalling: true,
      streaming: true,
      fineTuning: false,
      plugins: true,
    },
    status: 'active',
    releaseDate: '2023-03-14',
  };

  beforeEach(() => {
    mockClient = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
      request: jest.fn(),
    } as any;

    service = new FetchProviderModelsService(mockClient);
  });

  describe('getProviderModels', () => {
    it('should get models for a provider', async () => {
      const mockModels: ModelDto[] = [mockModel];
      (mockClient.get as jest.Mock).mockResolvedValue(mockModels);

      const result = await service.getProviderModels('openai');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.BY_PROVIDER('openai'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockModels);
    });
  });

  describe('getCachedProviderModels', () => {
    it('should get cached models for a provider', async () => {
      const mockModels: ModelDto[] = [mockModel];
      (mockClient.get as jest.Mock).mockResolvedValue(mockModels);

      const result = await service.getCachedProviderModels('openai');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.CACHED('openai'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockModels);
    });
  });

  describe('refreshProviderModels', () => {
    it('should refresh models from provider', async () => {
      const mockResponse = {
        providerName: 'openai',
        modelsUpdated: 5,
        modelsAdded: 2,
        modelsRemoved: 0,
        refreshedAt: '2025-01-11T10:00:00Z',
      };
      (mockClient.post as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.refreshProviderModels('openai');

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.REFRESH('openai'),
        { providerName: 'openai', forceRefresh: true },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('getModelDetails', () => {
    it('should get detailed model information', async () => {
      const mockDetails: ModelDetailsDto = {
        ...mockModel,
        version: '1.0',
        trainingData: 'Web data up to 2021',
        benchmarks: { 'MMLU': 86.4, 'HumanEval': 67.0 },
        limitations: ['Context window limited to 8k tokens'],
        bestPractices: ['Use system prompts for consistent behavior'],
        examples: [
          {
            title: 'Code Generation',
            description: 'Generate Python code',
            input: 'Write a function to calculate fibonacci',
            output: 'def fibonacci(n): ...',
          },
        ],
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockDetails);

      const result = await service.getModelDetails('openai', 'gpt-4');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.DETAILS('openai', 'gpt-4'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockDetails);
    });
  });

  describe('getModelCapabilities', () => {
    it('should get model capabilities', async () => {
      const capabilities: ModelCapabilities = mockModel.capabilities;
      (mockClient.get as jest.Mock).mockResolvedValue(capabilities);

      const result = await service.getModelCapabilities('openai', 'gpt-4');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.CAPABILITIES('openai', 'gpt-4'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(capabilities);
    });
  });

  describe('searchModels', () => {
    it('should search models with filters', async () => {
      const filters: ModelSearchFilters = {
        capabilities: { vision: true },
        status: ['active'],
        minContextWindow: 4096,
      };
      const mockSearchResult = {
        models: [mockModel],
        totalCount: 1,
        facets: {
          providers: { openai: 1 },
          capabilities: { chat: 1, vision: 0 },
          status: { active: 1 },
        },
      };
      (mockClient.post as jest.Mock).mockResolvedValue(mockSearchResult);

      const result = await service.searchModels('gpt', filters);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.SEARCH,
        {
          query: 'gpt',
          filters,
        },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockSearchResult);
    });
  });

  describe('getModelsSummary', () => {
    it('should get models summary', async () => {
      const mockSummary = {
        openai: 10,
        anthropic: 5,
        google: 8,
      };
      (mockClient.get as jest.Mock).mockResolvedValue(mockSummary);

      const result = await service.getModelsSummary();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.SUMMARY,
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockSummary);
    });
  });

  describe('testProviderConnection', () => {
    it('should test provider connection', async () => {
      const mockResponse = {
        success: true,
        message: 'Connection successful',
        responseTimeMs: 250,
      };
      (mockClient.post as jest.Mock).mockResolvedValue(mockResponse);

      const result = await service.testProviderConnection('openai');

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.PROVIDER_MODELS.TEST_CONNECTION,
        { providerName: 'openai' },
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('helper methods', () => {
    const models: ModelDto[] = [
      mockModel,
      {
        ...mockModel,
        id: 'gpt-3.5-turbo',
        name: 'gpt-3.5-turbo',
        displayName: 'GPT-3.5 Turbo',
        contextWindow: 4096,
        inputCost: 0.001,
        outputCost: 0.002,
        capabilities: {
          ...mockModel.capabilities,
          vision: false,
        },
        status: 'active',
      },
      {
        ...mockModel,
        id: 'text-davinci-003',
        name: 'text-davinci-003',
        displayName: 'Text Davinci 003',
        status: 'deprecated',
        deprecationDate: '2024-01-04',
      },
    ];

    it('modelSupportsCapability should check specific capability', () => {
      expect(service.modelSupportsCapability(mockModel, 'chat')).toBe(true);
      expect(service.modelSupportsCapability(mockModel, 'vision')).toBe(false);
      expect(service.modelSupportsCapability(mockModel, 'functionCalling')).toBe(true);
    });

    it('filterModelsByCapabilities should filter by required capabilities', () => {
      const filtered = service.filterModelsByCapabilities(models, {
        chat: true,
        functionCalling: true,
      });
      expect(filtered).toHaveLength(3);

      const visionFiltered = service.filterModelsByCapabilities(models, {
        vision: true,
      });
      expect(visionFiltered).toHaveLength(0);
    });

    it('getActiveModels should return only active models', () => {
      const active = service.getActiveModels(models);
      expect(active).toHaveLength(2);
      expect(active.every(m => m.status === 'active')).toBe(true);
    });

    it('groupModelsByProvider should group models', () => {
      const grouped = service.groupModelsByProvider(models);
      expect(grouped).toHaveProperty('openai');
      expect(grouped.openai).toHaveLength(3);
    });

    it('calculateCost should calculate token costs', () => {
      const cost = service.calculateCost(mockModel, 1000, 500);
      expect(cost).toBeCloseTo(0.06); // (1000/1000 * 0.03) + (500/1000 * 0.06)
    });

    it('findCheapestModel should find model with lowest cost', () => {
      const cheapest = service.findCheapestModel(models, { chat: true });
      expect(cheapest?.name).toBe('gpt-3.5-turbo');
    });

    it('sortByContextWindow should sort models', () => {
      const sorted = service.sortByContextWindow(models);
      expect(sorted[0].contextWindow).toBe(8192);
      expect(sorted[sorted.length - 1].contextWindow).toBe(4096);

      const ascending = service.sortByContextWindow(models, false);
      expect(ascending[0].contextWindow).toBe(4096);
    });

    it('formatModelName should format with provider', () => {
      expect(service.formatModelName(mockModel)).toBe('openai/gpt-4');
    });

    it('isModelDeprecated should check deprecation status', () => {
      expect(service.isModelDeprecated(models[0])).toBe(false);
      expect(service.isModelDeprecated(models[2])).toBe(true);
    });

    it('getModelStatusLabel should return status label', () => {
      expect(service.getModelStatusLabel(mockModel)).toBe('Active');
      expect(service.getModelStatusLabel({ ...mockModel, status: 'beta' })).toBe('Beta');
      expect(service.getModelStatusLabel({ ...mockModel, status: 'deprecated' })).toBe('Deprecated');
    });
  });
});