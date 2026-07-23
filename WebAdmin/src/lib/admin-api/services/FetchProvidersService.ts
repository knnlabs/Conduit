import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { components } from '../generated/admin-api';
import { HttpMethod } from '../client/HttpMethod';
import type { ProviderSettings } from '../models/common-types';
import {
  type ProviderDto,
  type CreateProviderDto,
  type UpdateProviderDto,
  type StandardApiKeyTestResponse,
  ApiKeyTestResult
} from '../models/provider';
import { ProviderType } from '../models/providerType';
import { classifyApiKeyTestError } from '../utils/error-classification';
import { FetchProvidersServiceKeys } from './FetchProvidersServiceKeys';

type ProviderListResponseDto = components['schemas']['PagedResultOfProviderDto'];

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
  details?: RawApiKeyTestDetails | null;
  Details?: RawApiKeyTestDetails | null;
}

interface RawApiKeyTestDetails {
  responseTimeMs?: number | null;
  ResponseTimeMs?: number;
  modelsAvailable?: string[] | null;
  ModelsAvailable?: string[];
  providerMessage?: string | null;
  ProviderMessage?: string;
  errorCode?: string | null;
  ErrorCode?: string;
  statusCode?: number | null;
  StatusCode?: number;
}

/**
 * Normalizes the API response to handle case mismatches between C# PascalCase and TypeScript camelCase
 */
function normalizeApiKeyTestResponse(response: RawApiKeyTestResponse): StandardApiKeyTestResponse {
  // Handle both PascalCase wire data and the camelCase local model.
  const result = response.result ?? response.Result ?? '';
  const message = response.message ?? response.Message ?? '';
  const details = response.details ?? response.Details;

  // Normalize the result enum value to lowercase with underscores
  const normalizedResult = normalizeEnumValue(result);

  return {
    result: normalizedResult,
    message: message,
    details: details ? {
      responseTimeMs: details.responseTimeMs ?? details.ResponseTimeMs ?? undefined,
      modelsAvailable: details.modelsAvailable ?? details.ModelsAvailable ?? undefined,
      providerMessage: details.providerMessage ?? details.ProviderMessage ?? undefined,
      errorCode: details.errorCode ?? details.ErrorCode ?? undefined,
      statusCode: details.statusCode ?? details.StatusCode ?? undefined,
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
   * Get all providers with pagination
   */
  async list(
    page: number = 1,
    pageSize: number = 50,
    config?: RequestConfig
  ): Promise<ProviderListResponseDto> {
    const query = { page, pageSize };
    return this.client['executeContractRead'](`/api/ProviderCredentials?page=${page}&pageSize=${pageSize}`,
      (contractClient, options) => contractClient.GET('/api/ProviderCredentials', { ...options, params: { query } }), config);
  }

  /**
   * Get a specific provider by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ProviderDto> {
    return this.client['executeContractRead'](`/api/ProviderCredentials/${id}`,
      (contractClient, options) => contractClient.GET('/api/ProviderCredentials/{id}', { ...options, params: { path: { id } } }), config);
  }

  /**
   * Create a new provider
   */
  async create(
    data: CreateProviderDto,
    config?: RequestConfig
  ): Promise<ProviderDto> {
    return this.client['executeContractOperation']('/api/ProviderCredentials', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/ProviderCredentials', { ...options, body: data }), config, data);
  }

  /**
   * Update an existing provider
   */
  async update(
    id: number,
    data: UpdateProviderDto,
    config?: RequestConfig
  ): Promise<ProviderDto> {
    return this.client['executeContractOperation'](`/api/ProviderCredentials/${id}`, HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/api/ProviderCredentials/{id}', { ...options, params: { path: { id } }, body: data }), config, data);
  }

  /**
   * Delete a provider
   */
  async deleteById(id: number, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation'](`/api/ProviderCredentials/${id}`, HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/api/ProviderCredentials/{id}', { ...options, params: { path: { id } } }), config);
  }

  /**
   * Test connection for a specific provider
   */
  async testConnectionById(
    id: number,
    config?: RequestConfig
  ): Promise<StandardApiKeyTestResponse> {
    try {
      const result = await this.client['executeContractOperation']<RawApiKeyTestResponse>(`/api/ProviderCredentials/${id}/test`, HttpMethod.POST,
        (contractClient, options) => contractClient.POST('/api/ProviderCredentials/{id}/test', { ...options, params: { path: { id } } }), config);

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
      const result = await this.client['executeContractOperation']<RawApiKeyTestResponse, ProviderConfig>('/api/ProviderCredentials/test', HttpMethod.POST,
        (contractClient, options) => contractClient.POST('/api/ProviderCredentials/test', { ...options, body: providerConfig }), config, providerConfig);

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
