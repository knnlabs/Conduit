import type { FetchBasedClient } from '../client/FetchBasedClient';
import type { RequestOptions } from '../client/types';
import type { 
  ModelsDiscoveryResponse, 
  ProviderModelsDiscoveryResponse, 
  CapabilityTestResponse,
  BulkCapabilityTestRequest,
  BulkCapabilityTestResponse,
  BulkModelDiscoveryRequest,
  BulkModelDiscoveryResponse,
  ModelCapability,
  CapabilityTest
} from '../models/discovery';


/**
 * Service for discovering model capabilities and provider features.
 */
export class DiscoveryService {
  private readonly baseEndpoint = '/v1/discovery';

  constructor(private readonly client: FetchBasedClient) {}

  /**
   * Gets all discovered models and their capabilities.
   */
  async getModels(options?: RequestOptions): Promise<ModelsDiscoveryResponse> {
    const response = await this.client['request']<ModelsDiscoveryResponse>(
      `${this.baseEndpoint}/models`,
      {
        method: 'GET',
        ...options
      }
    );
    return response;
  }

  /**
   * Gets models for a specific provider.
   */
  async getProviderModels(provider: string, options?: RequestOptions): Promise<ProviderModelsDiscoveryResponse> {
    if (!provider?.trim()) {
      throw new Error('Provider name is required');
    }

    const response = await this.client['request']<ProviderModelsDiscoveryResponse>(
      `${this.baseEndpoint}/providers/${encodeURIComponent(provider)}/models`,
      {
        method: 'GET',
        ...options
      }
    );
    return response;
  }

  /**
   * Tests if a model supports a specific capability.
   */
  async testModelCapability(
    model: string, 
    capability: ModelCapability | string,
    options?: RequestOptions
  ): Promise<CapabilityTestResponse> {
    if (!model?.trim()) {
      throw new Error('Model name is required');
    }

    const response = await this.client['request']<CapabilityTestResponse>(
      `${this.baseEndpoint}/models/${encodeURIComponent(model)}/capabilities/${capability}`,
      {
        method: 'GET',
        ...options
      }
    );
    return response;
  }

  /**
   * Tests multiple model capabilities in a single request.
   */
  async testBulkCapabilities(
    request: BulkCapabilityTestRequest,
    options?: RequestOptions
  ): Promise<BulkCapabilityTestResponse> {
    // Validate request
    if (!request.tests || request.tests.length === 0) {
      throw new Error('At least one test is required');
    }
    
    const response = await this.client['request']<BulkCapabilityTestResponse>(
      `${this.baseEndpoint}/bulk/capabilities`,
      {
        method: 'POST',
        body: request,
        ...options
      }
    );
    return response;
  }

  /**
   * Gets discovery information for multiple models in a single request.
   */
  async getBulkModels(
    request: BulkModelDiscoveryRequest,
    options?: RequestOptions
  ): Promise<BulkModelDiscoveryResponse> {
    // Validate request
    if (!request.models || request.models.length === 0) {
      throw new Error('At least one model is required');
    }
    
    const response = await this.client['request']<BulkModelDiscoveryResponse>(
      `${this.baseEndpoint}/bulk/models`,
      {
        method: 'POST',
        body: request,
        ...options
      }
    );
    return response;
  }

  /**
   * Refreshes the capability cache for all providers.
   * Requires admin/master key access.
   */
  async refreshCapabilities(options?: RequestOptions): Promise<void> {
    await this.client['request'](
      `${this.baseEndpoint}/refresh`,
      {
        method: 'POST',
        ...options
      }
    );
  }

  /**
   * Static validation helper to test capabilities without making API calls.
   */
  static validateCapabilityTest(test: CapabilityTest): void {
    if (!test.model?.trim()) {
      throw new Error('Model name is required');
    }
    if (!test.capability?.trim()) {
      throw new Error('Capability name is required');
    }
  }

  /**
   * Static validation helper for bulk requests.
   */
  static validateBulkCapabilityRequest(request: BulkCapabilityTestRequest): void {
    if (!request.tests || request.tests.length === 0) {
      throw new Error('At least one test is required');
    }
    request.tests.forEach((test, index) => {
      try {
        DiscoveryService.validateCapabilityTest(test);
      } catch (error) {
        throw new Error(`Invalid test at index ${index}: ${error instanceof Error ? error.message : String(error)}`);
      }
    });
  }

  /**
   * Static validation helper for bulk model discovery requests.
   */
  static validateBulkModelRequest(request: BulkModelDiscoveryRequest): void {
    if (!request.models || request.models.length === 0) {
      throw new Error('At least one model is required');
    }
    request.models.forEach((model, index) => {
      if (!model?.trim()) {
        throw new Error(`Invalid model at index ${index}: Model name is required`);
      }
    });
  }
}