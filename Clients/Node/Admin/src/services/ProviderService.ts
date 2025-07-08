import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL, DEFAULT_PAGE_SIZE } from '../constants';
import {
  ProviderCredentialDto,
  CreateProviderCredentialDto,
  UpdateProviderCredentialDto,
  ProviderConnectionTestRequest,
  ProviderConnectionTestResultDto,
  ProviderDataDto,
  ProviderHealthConfigurationDto,
  UpdateProviderHealthConfigurationDto,
  ProviderHealthRecordDto,
  ProviderHealthStatusDto,
  ProviderHealthSummaryDto,
  ProviderFilters,
  ProviderHealthFilters,
  ProviderUsageStatistics,
} from '../models/provider';
import { PaginatedResponse } from '../models/common';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const createProviderSchema = z.object({
  providerName: z.string().min(1),
  apiKey: z.string().optional(),
  apiEndpoint: z.string().url().optional(),
  organizationId: z.string().optional(),
  additionalConfig: z.string().optional(),
  isEnabled: z.boolean().optional(),
});

const updateHealthConfigSchema = z.object({
  isEnabled: z.boolean().optional(),
  checkIntervalSeconds: z.number().min(30).max(3600).optional(),
  timeoutSeconds: z.number().min(5).max(300).optional(),
  unhealthyThreshold: z.number().min(1).max(10).optional(),
  healthyThreshold: z.number().min(1).max(10).optional(),
  testModel: z.string().optional(),
});

export class ProviderService extends BaseApiClient {
  async create(request: CreateProviderCredentialDto): Promise<ProviderCredentialDto> {
    try {
      createProviderSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid provider credential request', { validationError: error });
    }

    const response = await this.post<ProviderCredentialDto>(
      ENDPOINTS.PROVIDERS.BASE,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async list(filters?: ProviderFilters): Promise<ProviderCredentialDto[]> {
    const cacheKey = this.getCacheKey('providers', filters);
    return this.withCache(
      cacheKey,
      () => super.get<ProviderCredentialDto[]>(ENDPOINTS.PROVIDERS.BASE),
      CACHE_TTL.MEDIUM
    );
  }

  async getById(id: number): Promise<ProviderCredentialDto> {
    const cacheKey = this.getCacheKey('provider', id);
    return this.withCache(
      cacheKey,
      () => super.get<ProviderCredentialDto>(ENDPOINTS.PROVIDERS.BY_ID(id)),
      CACHE_TTL.MEDIUM
    );
  }

  async getByName(providerName: string): Promise<ProviderCredentialDto> {
    const cacheKey = this.getCacheKey('provider-name', providerName);
    return this.withCache(
      cacheKey,
      () => super.get<ProviderCredentialDto>(ENDPOINTS.PROVIDERS.BY_NAME(providerName)),
      CACHE_TTL.MEDIUM
    );
  }

  async getProviderNames(): Promise<string[]> {
    const cacheKey = 'provider-names';
    return this.withCache(
      cacheKey,
      () => super.get<string[]>(ENDPOINTS.PROVIDERS.NAMES),
      CACHE_TTL.LONG
    );
  }

  async update(id: number, request: UpdateProviderCredentialDto): Promise<void> {
    await this.put(ENDPOINTS.PROVIDERS.BY_ID(id), request);
    await this.invalidateCache();
  }

  async deleteById(id: number): Promise<void> {
    await super.delete(ENDPOINTS.PROVIDERS.BY_ID(id));
    await this.invalidateCache();
  }

  async testConnectionById(id: number): Promise<ProviderConnectionTestResultDto> {
    return this.post<ProviderConnectionTestResultDto>(
      ENDPOINTS.PROVIDERS.TEST_BY_ID(id)
    );
  }

  async testConnection(
    request: ProviderConnectionTestRequest
  ): Promise<ProviderConnectionTestResultDto> {
    return this.post<ProviderConnectionTestResultDto>(
      ENDPOINTS.PROVIDERS.TEST,
      request
    );
  }

  // Health monitoring methods
  async getHealthConfigurations(): Promise<ProviderHealthConfigurationDto[]> {
    const cacheKey = 'provider-health-configs';
    return this.withCache(
      cacheKey,
      () => super.get<ProviderHealthConfigurationDto[]>(ENDPOINTS.HEALTH.CONFIGURATIONS),
      CACHE_TTL.SHORT
    );
  }

  async getHealthConfiguration(providerName: string): Promise<ProviderHealthConfigurationDto> {
    const cacheKey = this.getCacheKey('provider-health-config', providerName);
    return this.withCache(
      cacheKey,
      () =>
        this.get<ProviderHealthConfigurationDto>(
          ENDPOINTS.HEALTH.CONFIG_BY_PROVIDER(providerName)
        ),
      CACHE_TTL.SHORT
    );
  }

  async updateHealthConfiguration(
    providerName: string,
    request: UpdateProviderHealthConfigurationDto
  ): Promise<void> {
    try {
      updateHealthConfigSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid health configuration request', { validationError: error });
    }

    await this.put(ENDPOINTS.HEALTH.CONFIG_BY_PROVIDER(providerName), request);
    await this.invalidateCachePattern('provider-health');
  }

  async getHealthStatus(): Promise<ProviderHealthSummaryDto> {
    return super.get<ProviderHealthSummaryDto>(ENDPOINTS.HEALTH.STATUS);
  }

  async getProviderHealthStatus(providerName: string): Promise<ProviderHealthStatusDto> {
    return super.get<ProviderHealthStatusDto>(
      ENDPOINTS.HEALTH.STATUS_BY_PROVIDER(providerName)
    );
  }

  async getHealthHistory(
    filters?: ProviderHealthFilters
  ): Promise<PaginatedResponse<ProviderHealthRecordDto>> {
    const params = {
      pageNumber: filters?.pageNumber || 1,
      pageSize: filters?.pageSize || DEFAULT_PAGE_SIZE,
      providerName: filters?.providerName,
      isHealthy: filters?.isHealthy,
      startDate: filters?.startDate,
      endDate: filters?.endDate,
      minResponseTime: filters?.minResponseTime,
      maxResponseTime: filters?.maxResponseTime,
      sortBy: filters?.sortBy?.field,
      sortDirection: filters?.sortBy?.direction,
    };

    return super.get<PaginatedResponse<ProviderHealthRecordDto>>(
      ENDPOINTS.HEALTH.HISTORY,
      params
    );
  }

  async checkHealth(providerName: string): Promise<ProviderConnectionTestResultDto> {
    return this.post<ProviderConnectionTestResultDto>(
      ENDPOINTS.HEALTH.CHECK(providerName)
    );
  }

  // Stub methods
  async getUsageStatistics(
    _providerName: string,
    _startDate?: string,
    _endDate?: string
  ): Promise<ProviderUsageStatistics> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getUsageStatistics requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/providercredentials/{name}/statistics'
    );
  }

  async bulkTest(_providerNames: string[]): Promise<ProviderConnectionTestResultDto[]> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'bulkTest requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/providercredentials/test/bulk'
    );
  }

  async getAvailableProviders(): Promise<ProviderDataDto[]> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getAvailableProviders requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/providercredentials/available'
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }

  private async invalidateCachePattern(_pattern: string): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}