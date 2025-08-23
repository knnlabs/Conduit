import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type {
  ProviderKeyCredentialDto,
  CreateProviderKeyCredentialDto,
  UpdateProviderKeyCredentialDto,
  StandardApiKeyTestResponse,
  ProviderDto
} from '../models/provider';
import { ENDPOINTS } from '../constants';
import { classifyApiKeyTestError, createSuccessResponse } from '../utils/error-classification';

type TestConnectionResult = {
  success: boolean;
  message?: string;
  details?: Record<string, unknown>;
};

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
    return this.client['get']<ProviderKeyCredentialDto[]>(
      ENDPOINTS.PROVIDER_KEYS.BASE(providerId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific key credential
   */
  async getKeyById(
    providerId: number,
    keyId: number,
    config?: RequestConfig
  ): Promise<ProviderKeyCredentialDto> {
    return this.client['get']<ProviderKeyCredentialDto>(
      ENDPOINTS.PROVIDER_KEYS.BY_ID(providerId, keyId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new key credential for a provider
   */
  async createKey(
    providerId: number,
    data: CreateProviderKeyCredentialDto,
    config?: RequestConfig
  ): Promise<ProviderKeyCredentialDto> {
    return this.client['post']<ProviderKeyCredentialDto, CreateProviderKeyCredentialDto>(
      ENDPOINTS.PROVIDER_KEYS.BASE(providerId),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
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
    return this.client['put']<ProviderKeyCredentialDto, UpdateProviderKeyCredentialDto>(
      ENDPOINTS.PROVIDER_KEYS.BY_ID(providerId, keyId),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a key credential
   */
  async deleteKey(
    providerId: number,
    keyId: number,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.PROVIDER_KEYS.BY_ID(providerId, keyId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Set a key as primary
   */
  async setPrimaryKey(
    providerId: number,
    keyId: number,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['post']<void>(
      ENDPOINTS.PROVIDER_KEYS.SET_PRIMARY(providerId, keyId),
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
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
      const startTime = Date.now();
      const result = await this.client['post']<TestConnectionResult>(
        ENDPOINTS.PROVIDER_KEYS.TEST(providerId, keyId),
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
        const provider = await this.getProviderById(providerId, config);
        return classifyApiKeyTestError(
          { message: result.message, status: 400 },
          provider?.providerType
        );
      }
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
      return await this.client['get']<ProviderDto>(
        ENDPOINTS.PROVIDERS.BY_ID(id),
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );
    } catch {
      return null;
    }
  }
}