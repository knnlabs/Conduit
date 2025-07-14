import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  ModelDto,
  ModelDetailsDto,
  ModelCapabilities,
  RefreshModelsResponse,
  DiscoveredModel,
} from '../models/providerModels';

/**
 * Type-safe Provider Models service using native fetch
 */
export class FetchProviderModelsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get models for a specific provider
   */
  async getProviderModels(
    providerName: string,
    config?: RequestConfig
  ): Promise<ModelDto[]> {
    // The Admin API returns DiscoveredModel[], we need to map to ModelDto[]
    const discoveredModels = await this.client['get']<DiscoveredModel[]>(
      ENDPOINTS.MODEL_MAPPINGS.DISCOVER_PROVIDER(providerName),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    // Map DiscoveredModel to ModelDto
    return discoveredModels.map(dm => ({
      id: dm.modelId,
      name: dm.modelId,
      displayName: dm.displayName || dm.modelId,
      provider: dm.provider,
      description: dm.metadata?.description as string | undefined,
      contextWindow: dm.capabilities?.maxTokens || 0,
      maxTokens: dm.capabilities?.maxOutputTokens || 0,
      inputCost: 0, // Admin API doesn't provide cost information
      outputCost: 0, // Admin API doesn't provide cost information
      capabilities: {
        chat: dm.capabilities?.chat || false,
        completion: false, // Not in DiscoveredModel
        embedding: dm.capabilities?.embeddings || false,
        vision: dm.capabilities?.vision || false,
        functionCalling: dm.capabilities?.functionCalling || false,
        streaming: dm.capabilities?.chatStream || false,
        fineTuning: false, // Not in DiscoveredModel
        plugins: false, // Not in DiscoveredModel
      },
      status: 'active', // Default since Admin API doesn't provide status
    }));
  }

  /**
   * Get cached models for a specific provider (faster, may be stale)
   * @deprecated This endpoint doesn't exist in Admin API. Use getProviderModels instead.
   */
  async getCachedProviderModels(
    providerName: string,
    config?: RequestConfig
  ): Promise<ModelDto[]> {
    // Fallback to regular getProviderModels since cached endpoint doesn't exist
    console.warn('getCachedProviderModels: This endpoint does not exist in Admin API. Using getProviderModels instead.');
    return this.getProviderModels(providerName, config);
  }

  /**
   * Refresh model list from provider
   * @deprecated This endpoint doesn't exist in Admin API. Model discovery happens in real-time.
   */
  async refreshProviderModels(
    providerName: string,
    config?: RequestConfig
  ): Promise<RefreshModelsResponse> {
    // Admin API discovers models in real-time, no refresh needed
    console.warn('refreshProviderModels: This endpoint does not exist in Admin API. Model discovery happens in real-time.');
    const models = await this.getProviderModels(providerName, config);
    return {
      provider: providerName,
      modelsCount: models.length,
      success: true,
      message: `Discovered ${models.length} models for ${providerName}`,
    };
  }

  /**
   * Get detailed model information
   */
  async getModelDetails(
    providerName: string,
    modelId: string,
    config?: RequestConfig
  ): Promise<ModelDetailsDto> {
    // Use the discover endpoint to get model details
    const discoveredModel = await this.client['get']<DiscoveredModel>(
      ENDPOINTS.MODEL_MAPPINGS.DISCOVER_MODEL(providerName, modelId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    // Map to ModelDetailsDto
    return {
      id: discoveredModel.modelId,
      name: discoveredModel.modelId,
      displayName: discoveredModel.displayName || discoveredModel.modelId,
      provider: discoveredModel.provider,
      description: discoveredModel.metadata?.description as string | undefined,
      contextWindow: discoveredModel.capabilities?.maxTokens || 0,
      maxTokens: discoveredModel.capabilities?.maxOutputTokens || 0,
      inputCost: 0,
      outputCost: 0,
      capabilities: {
        chat: discoveredModel.capabilities?.chat || false,
        completion: false,
        embedding: discoveredModel.capabilities?.embeddings || false,
        vision: discoveredModel.capabilities?.vision || false,
        functionCalling: discoveredModel.capabilities?.functionCalling || false,
        streaming: discoveredModel.capabilities?.chatStream || false,
        fineTuning: false,
        plugins: false,
      },
      status: 'active',
      version: discoveredModel.metadata?.version as string || 'unknown',
    };
  }

  /**
   * Get model capabilities
   */
  async getModelCapabilities(
    providerName: string,
    modelId: string,
    config?: RequestConfig
  ): Promise<ModelCapabilities> {
    // Get model details which includes capabilities
    const modelDetails = await this.getModelDetails(providerName, modelId, config);
    return modelDetails.capabilities;
  }


  /**
   * Helper method to check if a model supports a specific capability
   */
  modelSupportsCapability(model: ModelDto, capability: keyof ModelCapabilities): boolean {
    return model.capabilities[capability] === true;
  }

  /**
   * Helper method to filter models by capabilities
   */
  filterModelsByCapabilities(
    models: ModelDto[],
    requiredCapabilities: Partial<ModelCapabilities>
  ): ModelDto[] {
    return models.filter(model => {
      for (const [capability, required] of Object.entries(requiredCapabilities)) {
        if (required && !model.capabilities[capability as keyof ModelCapabilities]) {
          return false;
        }
      }
      return true;
    });
  }

  /**
   * Helper method to get active models only
   */
  getActiveModels(models: ModelDto[]): ModelDto[] {
    return models.filter(model => model.status === 'active');
  }

  /**
   * Helper method to group models by provider
   */
  groupModelsByProvider(models: ModelDto[]): Record<string, ModelDto[]> {
    return models.reduce((acc, model) => {
      if (!acc[model.provider]) {
        acc[model.provider] = [];
      }
      acc[model.provider].push(model);
      return acc;
    }, {} as Record<string, ModelDto[]>);
  }

  /**
   * Helper method to calculate total cost for tokens
   */
  calculateCost(model: ModelDto, inputTokens: number, outputTokens: number): number {
    const inputCost = (inputTokens / 1000) * model.inputCost;
    const outputCost = (outputTokens / 1000) * model.outputCost;
    return inputCost + outputCost;
  }

  /**
   * Helper method to find cheapest model with specific capabilities
   */
  findCheapestModel(
    models: ModelDto[],
    requiredCapabilities: Partial<ModelCapabilities>
  ): ModelDto | undefined {
    const filteredModels = this.filterModelsByCapabilities(models, requiredCapabilities);
    const activeModels = this.getActiveModels(filteredModels);
    
    if (activeModels.length === 0) {
      return undefined;
    }

    return activeModels.reduce((cheapest, model) => {
      const cheapestAvgCost = (cheapest.inputCost + cheapest.outputCost) / 2;
      const modelAvgCost = (model.inputCost + model.outputCost) / 2;
      return modelAvgCost < cheapestAvgCost ? model : cheapest;
    });
  }

  /**
   * Helper method to sort models by context window size
   */
  sortByContextWindow(models: ModelDto[], descending: boolean = true): ModelDto[] {
    return [...models].sort((a, b) => {
      const diff = a.contextWindow - b.contextWindow;
      return descending ? -diff : diff;
    });
  }

  /**
   * Helper method to format model display name with provider
   */
  formatModelName(model: ModelDto): string {
    return `${model.provider}/${model.name}`;
  }

  /**
   * Helper method to check if model is deprecated or will be soon
   */
  isModelDeprecated(model: ModelDto): boolean {
    if (model.status === 'deprecated') {
      return true;
    }
    
    if (model.deprecationDate) {
      const deprecationDate = new Date(model.deprecationDate);
      return deprecationDate <= new Date();
    }
    
    return false;
  }

  /**
   * Helper method to get model status label
   */
  getModelStatusLabel(model: ModelDto): string {
    switch (model.status) {
      case 'active':
        return 'Active';
      case 'deprecated':
        return 'Deprecated';
      case 'beta':
        return 'Beta';
      case 'preview':
        return 'Preview';
      default:
        return 'Unknown';
    }
  }
}