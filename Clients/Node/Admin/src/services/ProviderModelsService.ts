import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';

export interface ProviderModel {
  id: string;
  object: 'model';
  created: number;
  owned_by: string;
}

export interface ProviderModelsResponse {
  object: 'list';
  data: ProviderModel[];
}

export interface ProviderConnectionTestRequest {
  providerId: string;
  virtualKey?: string;
  testModel?: string;
}

export interface ProviderConnectionTestResponse {
  success: boolean;
  providerId: string;
  message: string;
  modelsCount?: number;
  testResults?: {
    modelsRetrieved: boolean;
    authenticationValid: boolean;
    responseTime: number;
  };
  error?: string;
}

/**
 * Service for managing provider models and testing provider connections
 */
export class ProviderModelsService extends BaseApiClient {

  /**
   * Get available models for a specific provider
   * @param providerId The provider identifier (e.g., 'openai', 'anthropic')
   * @param options Optional parameters
   * @returns Promise resolving to provider models response
   */
  async getProviderModels(
    providerId: string,
    options?: {
      forceRefresh?: boolean;
      virtualKey?: string;
    }
  ): Promise<ProviderModel[]> {
    const params: Record<string, any> = {};
    
    if (options?.forceRefresh) {
      params.forceRefresh = true;
    }
    
    if (options?.virtualKey) {
      params.virtualKey = options.virtualKey;
    }

    const cacheKey = this.getCacheKey('provider-models', providerId, params);
    const response = await this.withCache(
      cacheKey,
      () => super.get<ProviderModelsResponse>(ENDPOINTS.PROVIDER_MODELS.BY_PROVIDER(providerId), params),
      CACHE_TTL.MEDIUM
    );
    return response.data;
  }

  /**
   * Test connection to a provider and retrieve available models
   * @param request Provider connection test request
   * @returns Promise resolving to connection test response
   */
  async testProviderConnection(
    request: ProviderConnectionTestRequest
  ): Promise<ProviderConnectionTestResponse> {
    return this.post<ProviderConnectionTestResponse>(
      ENDPOINTS.PROVIDER_MODELS.TEST_CONNECTION,
      request
    );
  }

  /**
   * Get cached provider models without making external API calls
   * @param providerId The provider identifier
   * @returns Promise resolving to cached provider models response
   */
  async getCachedProviderModels(providerId: string): Promise<ProviderModel[]> {
    const cacheKey = this.getCacheKey('cached-provider-models', providerId);
    const response = await this.withCache(
      cacheKey,
      () => super.get<ProviderModelsResponse>(ENDPOINTS.PROVIDER_MODELS.CACHED(providerId)),
      CACHE_TTL.LONG
    );
    return response.data;
  }

  /**
   * Refresh provider models cache for a specific provider
   * @param providerId The provider identifier
   * @param virtualKey Optional virtual key for authentication
   * @returns Promise resolving to refreshed provider models
   */
  async refreshProviderModels(
    providerId: string,
    virtualKey?: string
  ): Promise<ProviderModel[]> {
    const params: Record<string, any> = {};
    if (virtualKey) {
      params.virtualKey = virtualKey;
    }

    const response = await this.post<ProviderModelsResponse>(
      ENDPOINTS.PROVIDER_MODELS.REFRESH(providerId),
      params
    );
    
    // Invalidate cache after refresh
    await this.invalidateCache();
    return response.data;
  }

  /**
   * Get all supported providers with their available model counts
   * @returns Promise resolving to provider summary information
   */
  async getProviderSummary(): Promise<{
    providers: Array<{
      providerId: string;
      providerName: string;
      isAvailable: boolean;
      modelCount: number;
      lastUpdated?: string;
      capabilities: string[];
    }>;
  }> {
    const cacheKey = 'provider-summary';
    return this.withCache(
      cacheKey,
      () => super.get(ENDPOINTS.PROVIDER_MODELS.SUMMARY),
      CACHE_TTL.MEDIUM
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}