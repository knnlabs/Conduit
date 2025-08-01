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

  /**
   * @deprecated Use getProviderModels with provider ID instead
   * Gets available models for a specified provider by name.
   * @param providerName - Name of the provider
   * @param forceRefresh - Whether to bypass cache and force refresh
   * @returns List of available model IDs
   */
  async getProviderModelsByName(): Promise<string[]> {
    throw new Error('Provider names are no longer unique identifiers. Use getProviderModels with provider ID instead.');
  }

  /**
   * Static validation helper to validate provider ID.
   */
  static validateProviderId(providerId: number): void {
    if (!providerId || providerId <= 0) {
      throw new Error('Valid provider ID is required');
    }
  }

  /**
   * @deprecated Use validateProviderId instead
   * Static validation helper to validate provider name.
   */
  static validateProviderName(): void {
    throw new Error('Provider names are no longer unique identifiers. Use validateProviderId instead.');
  }
}