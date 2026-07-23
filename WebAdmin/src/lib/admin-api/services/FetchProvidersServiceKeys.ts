import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import {
  type ProviderKeyCredentialDto,
  type CreateProviderKeyCredentialDto,
  type UpdateProviderKeyCredentialDto,
  type StandardApiKeyTestResponse,
  type ProviderDto,
  ApiKeyTestResult
} from '../models/provider';
import { classifyApiKeyTestError } from '../utils/error-classification';

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
 * Provider key credential management methods
 */
export class FetchProvidersServiceKeys {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all key credentials for a provider
   */
  async listKeys(
    providerId: number,
    config?: RequestConfig
  ): Promise<ProviderKeyCredentialDto[]> {
    const result = await this.client['executeContractRead'](`/v1/admin/providers/${providerId}/keys`,
      (contractClient, options) => contractClient.GET('/v1/admin/providers/{providerId}/keys', { ...options, params: { path: { providerId } } }), config);
    return result.data;
  }

  /**
   * Get a specific key credential
   */
  async getKeyById(
    providerId: number,
    keyId: number,
    config?: RequestConfig
  ): Promise<ProviderKeyCredentialDto> {
    return this.client['executeContractRead'](`/v1/admin/providers/${providerId}/keys/${keyId}`,
      (contractClient, options) => contractClient.GET('/v1/admin/providers/{providerId}/keys/{keyId}', { ...options, params: { path: { providerId, keyId } } }), config);
  }

  /**
   * Create a new key credential for a provider
   */
  async createKey(
    providerId: number,
    data: CreateProviderKeyCredentialDto,
    config?: RequestConfig
  ): Promise<ProviderKeyCredentialDto> {
    return this.client['executeContractOperation'](`/v1/admin/providers/${providerId}/keys`, HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/v1/admin/providers/{providerId}/keys', { ...options, params: { path: { providerId } }, body: data }), config, data);
  }

  /**
   * Update a key credential
   */
  async updateKey(
    providerId: number,
    keyId: number,
    data: UpdateProviderKeyCredentialDto,
    config?: RequestConfig
  ): Promise<ProviderKeyCredentialDto> {
    return this.client['executeContractOperation'](`/v1/admin/providers/${providerId}/keys/${keyId}`, HttpMethod.PATCH,
      (contractClient, options) => contractClient.PATCH('/v1/admin/providers/{providerId}/keys/{keyId}', { ...options, params: { path: { providerId, keyId } }, body: data }), config, data);
  }

  /**
   * Delete a key credential
   */
  async deleteKey(
    providerId: number,
    keyId: number,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['executeContractOperation'](`/v1/admin/providers/${providerId}/keys/${keyId}`, HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/v1/admin/providers/{providerId}/keys/{keyId}', { ...options, params: { path: { providerId, keyId } } }), config);
  }

  /**
   * Set a key as primary
   */
  async setPrimaryKey(
    providerId: number,
    keyId: number,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['executeContractOperation'](`/v1/admin/providers/${providerId}/keys/${keyId}/set-primary`, HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/v1/admin/providers/{providerId}/keys/{keyId}/set-primary', { ...options, params: { path: { providerId, keyId } } }), config);
  }

  /**
   * Get the primary key for a provider
   */
  async getPrimaryKey(
    providerId: number,
    config?: RequestConfig
  ): Promise<ProviderKeyCredentialDto> {
    // GET_PRIMARY endpoint was removed - fetch all keys and find primary
    const keys = await this.listKeys(providerId, config);
    const primaryKey = keys.find(key => key.isPrimary);
    if (!primaryKey) {
      throw new Error('No primary key found for provider');
    }
    return primaryKey;
  }

  /**
   * Test a key credential
   */
  async testKey(
    providerId: number,
    keyId: number,
    config?: RequestConfig
  ): Promise<StandardApiKeyTestResponse> {
    try {
      const result = await this.client['executeContractOperation']<RawApiKeyTestResponse>(`/v1/admin/providers/${providerId}/keys/${keyId}/test`, HttpMethod.POST,
        (contractClient, options) => contractClient.POST('/v1/admin/providers/{providerId}/keys/{keyId}/test', { ...options, params: { path: { providerId, keyId } } }), config);

      // Normalize the response to handle C# PascalCase and enum mismatches
      return normalizeApiKeyTestResponse(result);
    } catch (error) {
      // Get provider info to determine type for error classification
      try {
        const provider = await this.getProviderById(providerId, config);
        return classifyApiKeyTestError(error, provider?.providerType);
      } catch {
        // If we can't get provider info, classify without it
        return classifyApiKeyTestError(error);
      }
    }
  }

  /**
   * Helper method to get provider info (used by key testing)
   */
  private async getProviderById(id: number, config?: RequestConfig): Promise<ProviderDto | null> {
    try {
      return await this.client['executeContractRead'](`/v1/admin/providers/${id}`,
        (contractClient, options) => contractClient.GET('/v1/admin/providers/{id}', { ...options, params: { path: { id } } }), config);
    } catch {
      return null;
    }
  }
}
