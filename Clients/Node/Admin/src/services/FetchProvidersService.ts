import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import type { ProviderSettings, HealthCheckDetails } from '../models/common-types';
import { ENDPOINTS } from '../constants';

// Type aliases for better readability
type ProviderDto = components['schemas']['ProviderCredentialDto'];
type CreateProviderDto = components['schemas']['CreateProviderCredentialDto'];
type UpdateProviderDto = components['schemas']['UpdateProviderCredentialDto'];
type TestConnectionResult = components['schemas']['ProviderConnectionTestResultDto'];

// Define inline types for responses that aren't in the generated schemas
interface ProviderListResponseDto {
  items: ProviderDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface ProviderConfig {
  providerName: string;
  apiKey: string;
  baseUrl?: string;
  organizationId?: string;
  additionalConfig?: ProviderSettings;
}

interface HealthStatusParams {
  includeHistory?: boolean;
  historyDays?: number;
}

interface ExportParams {
  format: 'json' | 'csv' | 'excel';
  startDate?: string;
  endDate?: string;
  providers?: string[];
}

interface ExportResult {
  fileUrl: string;
  fileName: string;
  expiresAt: string;
  size: number;
}

interface ProviderHealthStatus {
  providerId: string;
  providerName: string;
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  lastCheck?: string;
  responseTime?: number;
  details?: HealthCheckDetails;
}

/**
 * Type-safe Providers service using native fetch
 */
export class FetchProvidersService {
  constructor(private readonly client: FetchBaseApiClient) {}

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

    return this.client['get']<ProviderListResponseDto>(
      `${ENDPOINTS.PROVIDERS.BASE}?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific provider by ID
   */
  async getById(id: number, config?: RequestConfig): Promise<ProviderDto> {
    return this.client['get']<ProviderDto>(
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
    return this.client['post']<ProviderDto, CreateProviderDto>(
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
    return this.client['put']<ProviderDto, UpdateProviderDto>(
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
  ): Promise<TestConnectionResult> {
    return this.client['post']<TestConnectionResult>(
      ENDPOINTS.PROVIDERS.TEST_BY_ID(id),
      undefined,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Test a provider configuration without creating it
   */
  async testConfig(
    providerConfig: ProviderConfig,
    config?: RequestConfig
  ): Promise<TestConnectionResult> {
    return this.client['post']<TestConnectionResult, ProviderConfig>(
      `${ENDPOINTS.PROVIDERS.BASE}/test`,
      providerConfig,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get health status for all providers
   */
  async getHealthStatus(
    params?: HealthStatusParams,
    config?: RequestConfig
  ): Promise<ProviderHealthStatus[]> {
    const searchParams = new URLSearchParams();
    if (params?.includeHistory) {
      searchParams.set('includeHistory', 'true');
    }
    if (params?.historyDays) {
      searchParams.set('historyDays', params.historyDays.toString());
    }

    return this.client['get']<ProviderHealthStatus[]>(
      `${ENDPOINTS.PROVIDERS.BASE}/health${searchParams.toString() ? `?${searchParams}` : ''}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export provider health data
   */
  async exportHealthData(
    params: ExportParams,
    config?: RequestConfig
  ): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      `${ENDPOINTS.PROVIDERS.BASE}/health/export`,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to check if provider is enabled
   */
  isProviderEnabled(provider: ProviderDto): boolean {
    return provider.isEnabled === true;
  }

  /**
   * Helper method to check if provider has API key configured
   */
  hasApiKey(provider: ProviderDto): boolean {
    return provider.apiKey !== null && provider.apiKey !== undefined && provider.apiKey !== '';
  }

  /**
   * Helper method to format provider display name
   */
  formatProviderName(provider: ProviderDto): string {
    return provider.providerName;
  }

  /**
   * Helper method to get provider status
   */
  getProviderStatus(provider: ProviderDto): 'active' | 'inactive' | 'unconfigured' {
    if (!this.hasApiKey(provider)) {
      return 'unconfigured';
    }
    return provider.isEnabled ? 'active' : 'inactive';
  }
}