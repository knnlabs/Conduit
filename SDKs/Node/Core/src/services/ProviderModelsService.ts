import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';
import type { RequestOptions } from '../client/types';

/**
 * Service for retrieving provider model information.
 * NOTE: Provider ID is now the canonical identifier, not provider name
 */
export class ProviderModelsService {
  private readonly baseEndpoint = '/api/provider-models';
  private readonly clientAdapter: IFetchBasedClientAdapter;

  constructor(client: FetchBasedClient) {
    this.clientAdapter = createClientAdapter(client);
  }

  /**
   * Gets available models for a specified provider.
   * @param providerId - ID of the provider (numeric)
   * @param forceRefresh - Whether to bypass cache and force refresh
   * @returns List of available model IDs
   */
  async getProviderModels(
    providerId: number, 
    forceRefresh = false,
    options?: RequestOptions
  ): Promise<string[]> {
    if (!providerId || providerId <= 0) {
      throw new Error('Valid provider ID is required');
    }

    const queryParams = forceRefresh ? '?forceRefresh=true' : '';
    
    const response = await this.clientAdapter.get<string[]>(
      `${this.baseEndpoint}/${providerId}${queryParams}`,
      options
    );

    return response;
  }

  // getProviderModelsByName removed - use getProviderModels with provider ID

  /**
   * Static validation helper to validate provider ID.
   */
  static validateProviderId(providerId: number): void {
    if (!providerId || providerId <= 0) {
      throw new Error('Valid provider ID is required');
    }
  }

  // validateProviderName removed - use validateProviderId
}