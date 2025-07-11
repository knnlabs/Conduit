import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { RequestOptions } from '../client/types';

/**
 * Service for retrieving provider model information.
 */
export class ProviderModelsService {
  private readonly baseEndpoint = '/api/provider-models';

  constructor(private readonly client: FetchBasedClient) {}

  /**
   * Gets available models for a specified provider.
   * @param providerName - Name of the provider
   * @param forceRefresh - Whether to bypass cache and force refresh
   * @returns List of available model IDs
   */
  async getProviderModels(
    providerName: string, 
    forceRefresh = false,
    options?: RequestOptions
  ): Promise<string[]> {
    if (!providerName?.trim()) {
      throw new Error('Provider name is required');
    }

    const queryParams = forceRefresh ? '?forceRefresh=true' : '';
    
    const response = await this.client['request']<string[]>(
      `${this.baseEndpoint}/${encodeURIComponent(providerName)}${queryParams}`,
      {
        method: 'GET',
        ...options
      }
    );

    return response;
  }

  /**
   * Static validation helper to validate provider name.
   */
  static validateProviderName(providerName: string): void {
    if (!providerName?.trim()) {
      throw new Error('Provider name is required');
    }
  }
}