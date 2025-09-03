import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';
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
  private readonly clientAdapter: IFetchBasedClientAdapter;

  constructor(client: FetchBasedClient) {
    this.clientAdapter = createClientAdapter(client);
  }

  /**
   * Gets all discovered models and their capabilities.
   * @param options - Optional request options
   */
  async getModels(options?: RequestOptions): Promise<ModelsDiscoveryResponse> {
    const response = await this.clientAdapter.get<ModelsDiscoveryResponse>(
      `${this.baseEndpoint}/models`,
      options
    );
    return response;
  }

  /**
   * Gets discovered models filtered by a specific capability.
   * @param capability - The capability to filter by (e.g., 'image_generation', 'video_generation', 'chat')
   * @param options - Optional request options
   */
  async getModelsByCapability(capability: string, options?: RequestOptions): Promise<ModelsDiscoveryResponse> {
    if (!capability?.trim()) {
      throw new Error('Capability is required');
    }

    const response = await this.clientAdapter.get<ModelsDiscoveryResponse>(
      `${this.baseEndpoint}/models?capability=${encodeURIComponent(capability)}`,
      options
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

    const response = await this.clientAdapter.get<ProviderModelsDiscoveryResponse>(
      `${this.baseEndpoint}/providers/${encodeURIComponent(provider)}/models`,
      options
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

    const response = await this.clientAdapter.get<CapabilityTestResponse>(
      `${this.baseEndpoint}/models/${encodeURIComponent(model)}/capabilities/${capability}`,
      options
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
    
    const response = await this.clientAdapter.post<BulkCapabilityTestResponse, BulkCapabilityTestRequest>(
      `${this.baseEndpoint}/bulk/capabilities`,
      request,
      options
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
    
    const response = await this.clientAdapter.post<BulkModelDiscoveryResponse, BulkModelDiscoveryRequest>(
      `${this.baseEndpoint}/bulk/models`,
      request,
      options
    );
    return response;
  }

  /**
   * Refreshes the capability cache for all providers.
   * Requires admin/master key access.
   */
  async refreshCapabilities(options?: RequestOptions): Promise<void> {
    await this.clientAdapter.post(
      `${this.baseEndpoint}/refresh`,
      undefined,
      options
    );
  }

  /**
   * Static helper to filter models by capability in memory.
   * Note: The backend currently returns capabilities as flat properties (e.g., supports_image_generation)
   * rather than nested under a capabilities object.
   * 
   * @param models - Array of discovered models
   * @param capability - The capability to filter by (e.g., 'image_generation', 'video_generation')
   * @returns Filtered array of models that have the specified capability
   */
  static filterByCapability(models: any[], capability: string): any[] {
    if (!models || !Array.isArray(models)) {
      return [];
    }
    if (!capability?.trim()) {
      return models;
    }

    // Convert capability to the flat property name used by the backend
    const capabilityKey = `supports_${capability.replace(/-/g, '_').toLowerCase()}`;
    
    return models.filter(model => {
      // Check for flat property format (current backend format)
      if (capabilityKey in model && model[capabilityKey] === true) {
        return true;
      }
      
      // Also check for nested capabilities object (future-proof)
      if (model.capabilities && typeof model.capabilities === 'object') {
        const nestedKey = capability.replace(/-/g, '_').toLowerCase();
        return model.capabilities[nestedKey] === true;
      }
      
      return false;
    });
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