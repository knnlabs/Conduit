import { BaseApiClient } from '../client/BaseApiClient';
import { ApiClientConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import { 
  BulkCapabilityTestRequest, 
  BulkCapabilityTestResponse,
  BulkModelDiscoveryRequest,
  BulkModelDiscoveryResponse,
  DiscoveryModelsResponse,
  DiscoveryProviderModelsResponse,
  CapabilityTestResponse,
  CapabilityTest,
  ModelDiscoveryRequest
} from '../models/discovery';

/**
 * Service for discovery operations including bulk model discovery and capability testing
 */
export class DiscoveryService extends BaseApiClient {
  constructor(config: ApiClientConfig) {
    super(config);
  }

  /**
   * Test multiple model capabilities in a single request
   * @param request The bulk capability test request containing model-capability pairs
   * @returns Promise resolving to bulk capability test results
   */
  async testBulkCapabilities(request: BulkCapabilityTestRequest): Promise<BulkCapabilityTestResponse> {
    this.validateRequest(request, 'BulkCapabilityTestRequest');
    
    // Discovery endpoints don't use /api prefix, so we need to make a direct request
    // Create a custom axios instance for discovery endpoints
    const discoveryAxios = this.axios.create({
      baseURL: this.axios.defaults.baseURL?.replace('/api', ''),
      timeout: 30000
    });
    
    const response = await discoveryAxios.post<BulkCapabilityTestResponse>(
      ENDPOINTS.DISCOVERY.BULK_CAPABILITIES,
      request
    );
    
    return response.data;
  }

  /**
   * Discover multiple models and their capabilities in a single request
   * @param request The bulk model discovery request containing model names
   * @returns Promise resolving to bulk model discovery results
   */
  async getBulkModels(request: BulkModelDiscoveryRequest): Promise<BulkModelDiscoveryResponse> {
    this.validateRequest(request, 'BulkModelDiscoveryRequest');
    
    // Discovery endpoints don't use /api prefix, so we need to make a direct request
    const discoveryAxios = this.axios.create({
      baseURL: this.axios.defaults.baseURL?.replace('/api', ''),
      timeout: 30000
    });
    
    const response = await discoveryAxios.post<BulkModelDiscoveryResponse>(
      ENDPOINTS.DISCOVERY.BULK_MODELS,
      request
    );
    
    return response.data;
  }

  /**
   * Get all available models with their capabilities
   * @returns Promise resolving to all discovered models
   */
  async getAllModels(): Promise<DiscoveryModelsResponse> {
    const discoveryAxios = this.axios.create({
      baseURL: this.axios.defaults.baseURL?.replace('/api', ''),
      timeout: 30000
    });
    
    const response = await discoveryAxios.get<DiscoveryModelsResponse>(
      ENDPOINTS.DISCOVERY.MODELS
    );
    
    return response.data;
  }

  /**
   * Get models available from a specific provider
   * @param provider The provider name (e.g., "openai", "anthropic")
   * @returns Promise resolving to provider-specific models
   */
  async getProviderModels(provider: string): Promise<DiscoveryProviderModelsResponse> {
    if (!provider?.trim()) {
      throw new Error('Provider name is required');
    }

    const discoveryAxios = this.axios.create({
      baseURL: this.axios.defaults.baseURL?.replace('/api', ''),
      timeout: 30000
    });
    
    const response = await discoveryAxios.get<DiscoveryProviderModelsResponse>(
      ENDPOINTS.DISCOVERY.PROVIDER_MODELS(provider)
    );
    
    return response.data;
  }

  /**
   * Test a single model's capability
   * @param model The model name
   * @param capability The capability to test (e.g., "Chat", "ImageGeneration")
   * @returns Promise resolving to capability test result
   */
  async testModelCapability(model: string, capability: string): Promise<CapabilityTestResponse> {
    if (!model?.trim()) {
      throw new Error('Model name is required');
    }
    if (!capability?.trim()) {
      throw new Error('Capability name is required');
    }

    const discoveryAxios = this.axios.create({
      baseURL: this.axios.defaults.baseURL?.replace('/api', ''),
      timeout: 30000
    });
    
    const response = await discoveryAxios.get<CapabilityTestResponse>(
      ENDPOINTS.DISCOVERY.MODEL_CAPABILITY(model, capability)
    );
    
    return response.data;
  }

  /**
   * Refresh the discovery capabilities cache (admin operation)
   * @returns Promise that resolves when cache refresh is complete
   */
  async refreshCapabilities(): Promise<void> {
    const discoveryAxios = this.axios.create({
      baseURL: this.axios.defaults.baseURL?.replace('/api', ''),
      timeout: 60000
    });
    
    await discoveryAxios.post<void>(ENDPOINTS.DISCOVERY.REFRESH, {});
  }

  /**
   * Convenience method to test multiple capabilities for a single model
   * @param model The model name
   * @param capabilities Array of capability names to test
   * @returns Promise resolving to bulk capability test results
   */
  async testModelCapabilities(model: string, capabilities: string[]): Promise<BulkCapabilityTestResponse> {
    if (!model?.trim()) {
      throw new Error('Model name is required');
    }
    if (!capabilities?.length) {
      throw new Error('At least one capability is required');
    }

    const tests: CapabilityTest[] = capabilities.map(capability => ({
      model,
      capability
    }));

    return this.testBulkCapabilities({ tests });
  }

  /**
   * Convenience method to discover models and test their capabilities
   * @param request Model discovery request with optional capability filtering
   * @returns Promise resolving to models with capability information
   */
  async discoverModelsWithCapabilities(request: ModelDiscoveryRequest): Promise<BulkModelDiscoveryResponse> {
    this.validateRequest(request, 'ModelDiscoveryRequest');
    
    return this.getBulkModels({
      models: request.models,
      includeCapabilities: true,
      filterByCapabilities: request.requiredCapabilities
    });
  }

  private validateRequest(request: any, requestType: string): void {
    if (!request) {
      throw new Error(`${requestType} is required`);
    }
    
    // Basic validation - in a production system you might use Zod schemas
    if (requestType === 'BulkCapabilityTestRequest') {
      if (!request.tests?.length) {
        throw new Error('At least one capability test is required');
      }
      for (const test of request.tests) {
        if (!test.model?.trim()) {
          throw new Error('Model name is required for each test');
        }
        if (!test.capability?.trim()) {
          throw new Error('Capability name is required for each test');
        }
      }
    }
    
    if (requestType === 'BulkModelDiscoveryRequest') {
      if (!request.models?.length) {
        throw new Error('At least one model name is required');
      }
      for (const model of request.models) {
        if (!model?.trim()) {
          throw new Error('All model names must be non-empty strings');
        }
      }
    }
    
    if (requestType === 'ModelDiscoveryRequest') {
      if (!request.models?.length) {
        throw new Error('At least one model name is required');
      }
    }
  }
}