import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { ProviderSettings } from '../models/common-types';
import type {
  ProviderDto,
  CreateProviderDto,
  UpdateProviderDto,
  StandardApiKeyTestResponse
} from '../models/provider';
import { ENDPOINTS } from '../constants';
import { ProviderType } from '../models/providerType';
import { classifyApiKeyTestError, createSuccessResponse } from '../utils/error-classification';
import { FetchProvidersServiceHealth } from './FetchProvidersServiceHealth';
import { FetchProvidersServiceKeys } from './FetchProvidersServiceKeys';

// Type aliases for API compatibility - using existing DTO types since generated schemas are missing
type ApiProviderDto = ProviderDto;
type ApiCreateProviderDto = CreateProviderDto;
type ApiUpdateProviderDto = UpdateProviderDto;
type TestConnectionResult = {
  success: boolean;
  message?: string;
  details?: Record<string, unknown>;
};

// Define inline types for responses that aren't in the generated schemas
interface ProviderListResponseDto {
  items: ProviderDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface ProviderConfig {
  providerType: ProviderType;
  apiKey: string;
  baseUrl?: string;
  organizationId?: string;
  additionalConfig?: ProviderSettings;
}

/**
 * Type-safe Providers service using native fetch
 */
export class FetchProvidersService {
  private readonly healthService: FetchProvidersServiceHealth;
  private readonly keysService: FetchProvidersServiceKeys;

  constructor(private readonly client: FetchBaseApiClient) {
    this.healthService = new FetchProvidersServiceHealth(client);
    this.keysService = new FetchProvidersServiceKeys(client);
  }

  /**
   * Get all providers with optional pagination
   */
  async list(
    page: number = 1,
    pageSize: number = 10,
    config?: RequestConfig
  ): Promise<ProviderListResponseDto> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });

    // The backend returns an array directly, not a paginated response
    const response = await this.client['get']<ApiProviderDto[]>(
      `${ENDPOINTS.PROVIDERS.BASE}?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );

    // Convert array response to expected paginated format
    return {
      items: response,
      totalCount: response.length,
      page: page,
      pageSize: pageSize,
      totalPages: Math.ceil(response.length / pageSize)
    };
  }

  /**
   * Get a specific provider by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ProviderDto> {
    return this.client['get']<ApiProviderDto>(
      ENDPOINTS.PROVIDERS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new provider
   */
  async create(
    data: CreateProviderDto,
    config?: RequestConfig
  ): Promise<ProviderDto> {
    return this.client['post']<ApiProviderDto, ApiCreateProviderDto>(
      ENDPOINTS.PROVIDERS.BASE,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update an existing provider
   */
  async update(
    id: number,
    data: UpdateProviderDto,
    config?: RequestConfig
  ): Promise<ProviderDto> {
    return this.client['put']<ApiProviderDto, ApiUpdateProviderDto>(
      ENDPOINTS.PROVIDERS.BY_ID(id),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a provider
   */
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.PROVIDERS.BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Test connection for a specific provider
   */
  async testConnectionById(
    id: number,
    config?: RequestConfig
  ): Promise<StandardApiKeyTestResponse> {
    try {
      const startTime = Date.now();
      const result = await this.client['post']<TestConnectionResult>(
        ENDPOINTS.PROVIDERS.TEST_BY_ID(id),
        undefined,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );
      
      const responseTimeMs = Date.now() - startTime;
      
      // Convert old response format to new standardized format
      if (result.success) {
        return createSuccessResponse(
          responseTimeMs,
          (result.details as Record<string, unknown>)?.modelsAvailable as string[] | undefined
        );
      } else {
        // Get provider info to determine type
        const provider = await this.getById(id, config);
        return classifyApiKeyTestError(
          { message: result.message, status: 400 },
          provider.providerType
        );
      }
    } catch (error) {
      // Get provider info to determine type for error classification
      try {
        const provider = await this.getById(id, config);
        return classifyApiKeyTestError(error, provider.providerType);
      } catch {
        // If we can't get provider info, classify without it
        return classifyApiKeyTestError(error);
      }
    }
  }

  /**
   * Test a provider configuration without creating it
   */
  async testConfig(
    providerConfig: ProviderConfig,
    config?: RequestConfig
  ): Promise<StandardApiKeyTestResponse> {
    try {
      const startTime = Date.now();
      const result = await this.client['post']<TestConnectionResult, ProviderConfig>(
        `${ENDPOINTS.PROVIDERS.BASE}/test`,
        providerConfig,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );
      
      const responseTimeMs = Date.now() - startTime;
      
      // Convert old response format to new standardized format
      if (result.success) {
        return createSuccessResponse(
          responseTimeMs,
          (result.details as Record<string, unknown>)?.modelsAvailable as string[] | undefined
        );
      } else {
        return classifyApiKeyTestError(
          { message: result.message, status: 400 },
          providerConfig.providerType
        );
      }
    } catch (error) {
      return classifyApiKeyTestError(error, providerConfig.providerType);
    }
  }

  // Health-related methods are delegated to the health service
  async getHealthStatus(...args: Parameters<FetchProvidersServiceHealth['getHealthStatus']>) {
    return this.healthService.getHealthStatus(...args);
  }

  async exportHealthData(...args: Parameters<FetchProvidersServiceHealth['exportHealthData']>) {
    return this.healthService.exportHealthData(...args);
  }

  async getHealth(...args: Parameters<FetchProvidersServiceHealth['getHealth']>) {
    return this.healthService.getHealth(...args);
  }

  async listWithHealth(...args: Parameters<FetchProvidersServiceHealth['listWithHealth']>) {
    return this.healthService.listWithHealth(...args);
  }

  async getHealthMetrics(...args: Parameters<FetchProvidersServiceHealth['getHealthMetrics']>) {
    return this.healthService.getHealthMetrics(...args);
  }

  /**
   * Helper method to check if provider is enabled
   */
  isProviderEnabled(provider: ProviderDto): boolean {
    return provider.isEnabled === true;
  }


  /**
   * Helper method to format provider display name
   */
  formatProviderName(provider: ProviderDto): string {
    // Use the user-friendly provider name instead of type
    return provider.providerName || provider.providerType?.toString() || 'Unknown';
  }

  /**
   * Helper method to get provider status
   */
  getProviderStatus(provider: ProviderDto): 'active' | 'inactive' | 'unconfigured' {
    // Check if provider is enabled instead of checking for API key
    if (!provider.isEnabled) {
      return 'inactive';
    }
    return 'active';
  }


  // Key credential methods are delegated to the keys service
  async listKeys(...args: Parameters<FetchProvidersServiceKeys['listKeys']>) {
    return this.keysService.listKeys(...args);
  }

  async getKeyById(...args: Parameters<FetchProvidersServiceKeys['getKeyById']>) {
    return this.keysService.getKeyById(...args);
  }

  async createKey(...args: Parameters<FetchProvidersServiceKeys['createKey']>) {
    return this.keysService.createKey(...args);
  }

  async updateKey(...args: Parameters<FetchProvidersServiceKeys['updateKey']>) {
    return this.keysService.updateKey(...args);
  }

  async deleteKey(...args: Parameters<FetchProvidersServiceKeys['deleteKey']>) {
    return this.keysService.deleteKey(...args);
  }

  async setPrimaryKey(...args: Parameters<FetchProvidersServiceKeys['setPrimaryKey']>) {
    return this.keysService.setPrimaryKey(...args);
  }

  async getPrimaryKey(...args: Parameters<FetchProvidersServiceKeys['getPrimaryKey']>) {
    return this.keysService.getPrimaryKey(...args);
  }

  async testKey(...args: Parameters<FetchProvidersServiceKeys['testKey']>) {
    return this.keysService.testKey(...args);
  }

  /**
   * Get all available provider types.
   * This method returns all provider types, allowing multiple providers of the same type
   * (e.g., "Production OpenAI", "Dev OpenAI").
   * 
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<ProviderType[]> - Array of all available provider types
   * @throws {Error} When provider types cannot be retrieved
   */
  async getAvailableProviderTypes(): Promise<ProviderType[]> {
    // Get all provider types from the enum
    const allProviderTypes = Object.values(ProviderType)
      .filter((value): value is ProviderType => typeof value === 'number');

    // Return all provider types (allowing multiple instances of same type)
    return allProviderTypes;
  }
}