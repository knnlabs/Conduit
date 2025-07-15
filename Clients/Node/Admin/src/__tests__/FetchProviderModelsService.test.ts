import { createMockClient, type MockClient } from './helpers/mockClient.helper';
import { FetchProviderModelsService } from '../services/FetchProviderModelsService';

import { ENDPOINTS } from '../constants';
import type { 
  ModelDto
} from '../models/providerModels';

describe('FetchProviderModelsService', () => {
  let mockClient: MockClient;
  let service: FetchProviderModelsService;

  const mockModel: ModelDto = {
    id: 'gpt-4',
    name: 'gpt-4',
    displayName: 'GPT-4',
    provider: 'openai',
    description: 'OpenAI GPT-4 model',
    contextWindow: 8192,
    maxTokens: 4096,
    inputCost: 0, // Admin API doesn't provide cost
    outputCost: 0, // Admin API doesn't provide cost
    capabilities: {
      chat: true,
      completion: false, // Not in DiscoveredModel
      embedding: false,
      vision: false,
      functionCalling: true,
      streaming: true,
      fineTuning: false, // Not in DiscoveredModel
      plugins: false, // Not in DiscoveredModel
    },
    status: 'active',
    // releaseDate is not returned by the service
  };

  beforeEach(() => {
    mockClient = createMockClient();

    service = new FetchProviderModelsService(mockClient as any);
  });

  describe('getProviderModels', () => {
    it('should get models for a provider', async () => {
      const mockDiscoveredModels = [{
        modelId: mockModel.id,
        displayName: mockModel.displayName,
        provider: mockModel.provider,
        capabilities: {
          chat: mockModel.capabilities.chat,
          embeddings: mockModel.capabilities.embedding,
          vision: mockModel.capabilities.vision,
          functionCalling: mockModel.capabilities.functionCalling,
          chatStream: mockModel.capabilities.streaming,
          maxTokens: mockModel.contextWindow,
          maxOutputTokens: mockModel.maxTokens,
        },
        metadata: { description: mockModel.description }
      }];
      mockClient.get.mockResolvedValue(mockDiscoveredModels);

      const result = await service.getProviderModels('openai');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.DISCOVER_PROVIDER('openai'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      // The service transforms DiscoveredModel to ModelDto
      expect(result).toEqual([mockModel]);
    });
  });

  describe('getCachedProviderModels', () => {
    it('should get cached models for a provider', async () => {
      const mockDiscoveredModels = [{
        modelId: mockModel.id,
        displayName: mockModel.displayName,
        provider: mockModel.provider,
        capabilities: {
          chat: mockModel.capabilities.chat,
          embeddings: mockModel.capabilities.embedding,
          vision: mockModel.capabilities.vision,
          functionCalling: mockModel.capabilities.functionCalling,
          chatStream: mockModel.capabilities.streaming,
          maxTokens: mockModel.contextWindow,
          maxOutputTokens: mockModel.maxTokens,
        },
        metadata: { description: mockModel.description }
      }];
      mockClient.get.mockResolvedValue(mockDiscoveredModels);

      const result = await service.getCachedProviderModels('openai');

      // getCachedProviderModels falls back to getProviderModels
      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.DISCOVER_PROVIDER('openai'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual([mockModel]);
    });
  });

  describe('refreshProviderModels', () => {
    it('should refresh models from provider', async () => {
      const mockModels = [{
        modelId: mockModel.id,
        displayName: mockModel.displayName,
        provider: mockModel.provider,
        capabilities: {
          chat: mockModel.capabilities.chat,
          embeddings: mockModel.capabilities.embedding,
          vision: mockModel.capabilities.vision,
          functionCalling: mockModel.capabilities.functionCalling,
          chatStream: mockModel.capabilities.streaming,
          maxTokens: mockModel.contextWindow,
          maxOutputTokens: mockModel.maxTokens,
        },
        metadata: { description: mockModel.description }
      }];
      mockClient.get.mockResolvedValue(mockModels);

      const result = await service.refreshProviderModels('openai');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.DISCOVER_PROVIDER('openai'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual({
        provider: 'openai',
        modelsCount: 1,
        success: true,
        message: 'Discovered 1 models for openai',
      });
    });
  });

  describe('getModelDetails', () => {
    it('should get detailed model information', async () => {
      const mockDiscoveredModel = {
        modelId: mockModel.id,
        displayName: mockModel.displayName,
        provider: mockModel.provider,
        capabilities: {
          chat: mockModel.capabilities.chat,
          embeddings: mockModel.capabilities.embedding,
          vision: mockModel.capabilities.vision,
          functionCalling: mockModel.capabilities.functionCalling,
          chatStream: mockModel.capabilities.streaming,
          maxTokens: mockModel.contextWindow,
          maxOutputTokens: mockModel.maxTokens,
        },
        metadata: { 
          description: mockModel.description,
          version: '1.0',
          trainingData: 'Web data up to 2021'
        }
      };
      mockClient.get.mockResolvedValue(mockDiscoveredModel);

      const result = await service.getModelDetails('openai', 'gpt-4');

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.DISCOVER_MODEL('openai', 'gpt-4'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      // The service transforms DiscoveredModel to ModelDetailsDto
      expect(result.id).toBe(mockModel.id);
      expect(result.displayName).toBe(mockModel.displayName);
      expect(result.capabilities).toEqual(mockModel.capabilities);
    });
  });

  describe('getModelCapabilities', () => {
    it('should get model capabilities', async () => {
      const mockDiscoveredModel = {
        modelId: mockModel.id,
        displayName: mockModel.displayName,
        provider: mockModel.provider,
        capabilities: {
          chat: mockModel.capabilities.chat,
          embeddings: mockModel.capabilities.embedding,
          vision: mockModel.capabilities.vision,
          functionCalling: mockModel.capabilities.functionCalling,
          chatStream: mockModel.capabilities.streaming,
          maxTokens: mockModel.contextWindow,
          maxOutputTokens: mockModel.maxTokens,
        },
        metadata: { description: mockModel.description }
      };
      mockClient.get.mockResolvedValue(mockDiscoveredModel);

      const result = await service.getModelCapabilities('openai', 'gpt-4');

      // getModelCapabilities uses getModelDetails internally
      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.MODEL_MAPPINGS.DISCOVER_MODEL('openai', 'gpt-4'),
        {
          signal: undefined,
          timeout: undefined,
          headers: undefined,
        }
      );
      expect(result).toEqual(mockModel.capabilities);
    });
  });

  // Note: searchModels, getModelsSummary, and testProviderConnection methods
  // do not exist in the FetchProviderModelsService implementation

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
      // Since Admin API doesn't provide costs, they are 0
      expect(cost).toBeCloseTo(0); 
    });

    it('findCheapestModel should find model with lowest cost', () => {
      const cheapest = service.findCheapestModel(models, { chat: true });
      // Since all costs are 0, it returns the first matching model
      expect(cheapest?.name).toBe('gpt-4');
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