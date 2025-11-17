import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { ProviderSettings } from '../models/common-types';
import {
  type ProviderDto,
  type CreateProviderDto,
  type UpdateProviderDto,
  type StandardApiKeyTestResponse,
  ApiKeyTestResult
} from '../models/provider';
import { ENDPOINTS } from '../constants';
import { ProviderType } from '../models/providerType';
import { classifyApiKeyTestError } from '../utils/error-classification';
import { FetchProvidersServiceKeys } from './FetchProvidersServiceKeys';

// Type aliases for API compatibility - using existing DTO types since generated schemas are missing
type ApiProviderDto = ProviderDto;
type ApiCreateProviderDto = CreateProviderDto;
type ApiUpdateProviderDto = UpdateProviderDto;

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

// Type for raw API response (handles both PascalCase and camelCase)
interface RawApiKeyTestResponse {
  result?: string;
  Result?: string;
  message?: string;
  Message?: string;
  details?: RawApiKeyTestDetails;
  Details?: RawApiKeyTestDetails;
}

interface RawApiKeyTestDetails {
  responseTimeMs?: number;
  ResponseTimeMs?: number;
  modelsAvailable?: string[];
  ModelsAvailable?: string[];
  providerMessage?: string;
  ProviderMessage?: string;
  errorCode?: string;
  ErrorCode?: string;
  statusCode?: number;
  StatusCode?: number;
}

/**
 * Normalizes the API response to handle case mismatches between C# PascalCase and TypeScript camelCase
 */
function normalizeApiKeyTestResponse(response: RawApiKeyTestResponse): StandardApiKeyTestResponse {
  // Handle both PascalCase (from C#) and camelCase (expected by SDK)
  const result = response.result ?? response.Result ?? '';
  const message = response.message ?? response.Message ?? '';
  const details = response.details ?? response.Details;

  // Normalize the result enum value to lowercase with underscores
  const normalizedResult = normalizeEnumValue(result);

  return {
    result: normalizedResult,
    message: message,
    details: details ? {
      responseTimeMs: details.responseTimeMs ?? details.ResponseTimeMs,
      modelsAvailable: details.modelsAvailable ?? details.ModelsAvailable,
      providerMessage: details.providerMessage ?? details.ProviderMessage,
      errorCode: details.errorCode ?? details.ErrorCode,
      statusCode: details.statusCode ?? details.StatusCode,
    } : undefined,
  };
}

/**
 * Normalizes enum values from PascalCase to snake_case
 * Examples: "InvalidKey" -> "invalid_key", "Success" -> "success"
 */
function normalizeEnumValue(value: string): ApiKeyTestResult {
  if (!value) return ApiKeyTestResult.UNKNOWN_ERROR;

  // Convert PascalCase to snake_case
  const snakeCase = value
    .replace(/([A-Z])/g, '_$1')
    .toLowerCase()
    .replace(/^_/, '');

  // Map to the enum
  const enumMap: Record<string, ApiKeyTestResult> = {
    'success': ApiKeyTestResult.SUCCESS,
    'invalid_key': ApiKeyTestResult.INVALID_KEY,
    'ignored': ApiKeyTestResult.IGNORED,
    'provider_down': ApiKeyTestResult.PROVIDER_DOWN,
    'rate_limited': ApiKeyTestResult.RATE_LIMITED,
    'unknown_error': ApiKeyTestResult.UNKNOWN_ERROR,
  };

  return enumMap[snakeCase] ?? ApiKeyTestResult.UNKNOWN_ERROR;
}

/**
 * Type-safe Providers service using native fetch
 */
export class FetchProvidersService {
  private readonly keysService: FetchProvidersServiceKeys;

  constructor(private readonly client: FetchBaseApiClient) {
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
      const result = await this.client['post']<RawApiKeyTestResponse>(
        ENDPOINTS.PROVIDERS.TEST_BY_ID(id),
        undefined,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );

      // Normalize the response to handle C# PascalCase and enum mismatches
      return normalizeApiKeyTestResponse(result);
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
      const result = await this.client['post']<RawApiKeyTestResponse, ProviderConfig>(
        `${ENDPOINTS.PROVIDERS.BASE}/test`,
        providerConfig,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );

      // Normalize the response to handle C# PascalCase and enum mismatches
      return normalizeApiKeyTestResponse(result);
    } catch (error) {
      return classifyApiKeyTestError(error, providerConfig.providerType);
    }
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