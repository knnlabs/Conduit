import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  ModelDto,
  ModelDetailsDto,
  ModelCapabilities,
  ModelSearchFilters,
  ModelSearchResult,
  ModelListResponseDto,
  RefreshModelsResponse,
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
    return this.client['get']<ModelDto[]>(
      ENDPOINTS.PROVIDER_MODELS.BY_PROVIDER(providerName),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cached models for a specific provider (faster, may be stale)
   */
  async getCachedProviderModels(
    providerName: string,
    config?: RequestConfig
  ): Promise<ModelDto[]> {
    return this.client['get']<ModelDto[]>(
      ENDPOINTS.PROVIDER_MODELS.CACHED(providerName),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Refresh model list from provider
   */
  async refreshProviderModels(
    providerName: string,
    config?: RequestConfig
  ): Promise<RefreshModelsResponse> {
    return this.client['post']<RefreshModelsResponse>(
      ENDPOINTS.PROVIDER_MODELS.REFRESH(providerName),
      { providerName, forceRefresh: true },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get detailed model information
   */
  async getModelDetails(
    provider: string,
    model: string,
    config?: RequestConfig
  ): Promise<ModelDetailsDto> {
    return this.client['get']<ModelDetailsDto>(
      ENDPOINTS.PROVIDER_MODELS.DETAILS(provider, model),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get model capabilities
   */
  async getModelCapabilities(
    provider: string,
    model: string,
    config?: RequestConfig
  ): Promise<ModelCapabilities> {
    return this.client['get']<ModelCapabilities>(
      ENDPOINTS.PROVIDER_MODELS.CAPABILITIES(provider, model),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Search models across all providers
   */
  async searchModels(
    query: string,
    filters?: ModelSearchFilters,
    config?: RequestConfig
  ): Promise<ModelSearchResult> {
    return this.client['post']<ModelSearchResult>(
      ENDPOINTS.PROVIDER_MODELS.SEARCH,
      {
        query,
        filters,
      },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get summary of all provider models
   */
  async getModelsSummary(config?: RequestConfig): Promise<Record<string, number>> {
    return this.client['get']<Record<string, number>>(
      ENDPOINTS.PROVIDER_MODELS.SUMMARY,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Test provider connection
   */
  async testProviderConnection(
    providerName: string,
    config?: RequestConfig
  ): Promise<{ success: boolean; message: string; responseTimeMs?: number }> {
    return this.client['post']<{ success: boolean; message: string; responseTimeMs?: number }>(
      ENDPOINTS.PROVIDER_MODELS.TEST_CONNECTION,
      { providerName },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
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