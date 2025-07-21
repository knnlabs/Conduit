import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';
import type { RequestOptions } from '../client/types';

/**
 * Service for retrieving provider model information.
 */
export class ProviderModelsService {
  private readonly baseEndpoint = '/api/provider-models';
  private readonly clientAdapter: IFetchBasedClientAdapter;

  constructor(client: FetchBasedClient) {
    this.clientAdapter = createClientAdapter(client);
  }

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
    
    const response = await this.clientAdapter.get<string[]>(
      `${this.baseEndpoint}/${encodeURIComponent(providerName)}${queryParams}`,
      options
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