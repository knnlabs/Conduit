import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ApiClientConfig } from '../client/types';
import type {
  ProviderHealthConfigurationDto,
  CreateProviderHealthConfigurationDto,
  UpdateProviderHealthConfigurationDto,
  ProviderHealthRecordDto,
  ProviderHealthStatusDto,
  ProviderHealthSummaryDto,
  ProviderHealthStatisticsDto,
  ProviderStatus,
  ProviderHealthFilters
} from '../models/provider';
import type { PagedResponse } from '../models/common';

/**
 * Service for managing provider health monitoring and status
 */
export class ProviderHealthService extends FetchBaseApiClient {
  constructor(config: ApiClientConfig) {
    super(config);
  }

  /**
   * Gets the health configuration for a specific provider
   * 
   * @param providerName - The name of the provider
   * @returns Promise<ProviderHealthConfigurationDto> The provider health configuration
   */
  async getProviderHealthConfiguration(providerName: string): Promise<ProviderHealthConfigurationDto> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }

    return this.get<ProviderHealthConfigurationDto>(
      `/api/ProviderHealth/configuration/${encodeURIComponent(providerName)}`
    );
  }

  /**
   * Creates a new provider health configuration
   * 
   * @param request - The configuration request
   * @returns Promise<ProviderHealthConfigurationDto> The created configuration
   */
  async createProviderHealthConfiguration(
    request: CreateProviderHealthConfigurationDto
  ): Promise<ProviderHealthConfigurationDto> {
    if (!request) {
      throw new Error('Request cannot be null');
    }

    return this.post<ProviderHealthConfigurationDto>(
      '/api/ProviderHealth/configuration',
      request
    );
  }

  /**
   * Updates an existing provider health configuration
   * 
   * @param providerName - The name of the provider
   * @param request - The update request
   * @returns Promise<ProviderHealthConfigurationDto> The updated configuration
   */
  async updateProviderHealthConfiguration(
    providerName: string,
    request: UpdateProviderHealthConfigurationDto
  ): Promise<ProviderHealthConfigurationDto> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }
    if (!request) {
      throw new Error('Request cannot be null');
    }

    return this.put<ProviderHealthConfigurationDto>(
      `/api/ProviderHealth/configuration/${encodeURIComponent(providerName)}`,
      request
    );
  }

  /**
   * Gets health records for a specific provider
   * 
   * @param providerName - The name of the provider
   * @param filters - Optional filters for the query
   * @returns Promise<PagedResponse<ProviderHealthRecordDto>> A paginated list of health records
   */
  async getProviderHealthRecords(
    providerName: string,
    filters?: ProviderHealthFilters
  ): Promise<PagedResponse<ProviderHealthRecordDto>> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }

    const params = filters ? new URLSearchParams(
      Object.entries(filters)
        .filter(([, value]) => value !== undefined && value !== null)
        .reduce((acc, [key, value]) => ({ ...acc, [key]: String(value) }), {} as Record<string, string>)
    ).toString() : '';

    const url = `/api/ProviderHealth/records/${encodeURIComponent(providerName)}${params ? `?${params}` : ''}`;

    return this.get<PagedResponse<ProviderHealthRecordDto>>(url);
  }

  /**
   * Gets all health records across all providers
   * 
   * @param filters - Optional filters for the query
   * @returns Promise<PagedResponse<ProviderHealthRecordDto>> A paginated list of health records
   */
  async getAllHealthRecords(
    filters?: ProviderHealthFilters
  ): Promise<PagedResponse<ProviderHealthRecordDto>> {
    const params = filters ? new URLSearchParams(
      Object.entries(filters)
        .filter(([, value]) => value !== undefined && value !== null)
        .reduce((acc, [key, value]) => ({ ...acc, [key]: String(value) }), {} as Record<string, string>)
    ).toString() : '';

    const url = `/api/ProviderHealth/records${params ? `?${params}` : ''}`;

    return this.get<PagedResponse<ProviderHealthRecordDto>>(url);
  }

  /**
   * Gets the current health status for all providers
   * 
   * @returns Promise<ProviderHealthSummaryDto> A summary of all provider health statuses
   */
  async getHealthSummary(): Promise<ProviderHealthSummaryDto> {
    return this.get<ProviderHealthSummaryDto>('/api/ProviderHealth/summary');
  }

  /**
   * Gets the health status for a specific provider
   * 
   * @param providerName - The name of the provider
   * @returns Promise<ProviderHealthStatusDto> The provider health status
   */
  async getProviderHealthStatus(providerName: string): Promise<ProviderHealthStatusDto> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }

    return this.get<ProviderHealthStatusDto>(
      `/api/ProviderHealth/status/${encodeURIComponent(providerName)}`
    );
  }

  /**
   * Gets health statistics for all providers
   * 
   * @param periodHours - The time period in hours for statistics calculation
   * @returns Promise<ProviderHealthStatisticsDto> Overall provider health statistics
   */
  async getHealthStatistics(periodHours: number = 24): Promise<ProviderHealthStatisticsDto> {
    const params = new URLSearchParams({ periodHours: String(periodHours) });
    return this.get<ProviderHealthStatisticsDto>(`/api/ProviderHealth/statistics?${params}`);
  }

  /**
   * Gets simple status information for a provider
   * 
   * @param providerName - The name of the provider
   * @returns Promise<ProviderStatus> Simple provider status
   */
  async getProviderStatus(providerName: string): Promise<ProviderStatus> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }

    return this.get<ProviderStatus>(
      `/api/ProviderHealth/simple-status/${encodeURIComponent(providerName)}`
    );
  }

  /**
   * Triggers a manual health check for a specific provider
   * 
   * @param providerName - The name of the provider
   * @returns Promise<ProviderHealthRecordDto> The health check result
   */
  async triggerHealthCheck(providerName: string): Promise<ProviderHealthRecordDto> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }

    return this.post<ProviderHealthRecordDto>(
      `/api/ProviderHealth/check/${encodeURIComponent(providerName)}`
    );
  }

  /**
   * Deletes a provider health configuration
   * 
   * @param providerName - The name of the provider
   */
  async deleteProviderHealthConfiguration(providerName: string): Promise<void> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }

    await this.delete(`/api/ProviderHealth/configuration/${encodeURIComponent(providerName)}`);
  }

  /**
   * Checks if a provider is currently healthy
   * 
   * @param providerName - The name of the provider
   * @returns Promise<boolean> True if the provider is healthy, false otherwise
   */
  async isProviderHealthy(providerName: string): Promise<boolean> {
    try {
      const status = await this.getProviderHealthStatus(providerName);
      return status.isHealthy;
    } catch {
      return false;
    }
  }

  /**
   * Gets all unhealthy providers
   * 
   * @returns Promise<ProviderHealthStatusDto[]> List of unhealthy providers
   */
  async getUnhealthyProviders(): Promise<ProviderHealthStatusDto[]> {
    const summary = await this.getHealthSummary();
    return summary.providers.filter(p => !p.isHealthy);
  }

  /**
   * Gets the overall system health percentage
   * 
   * @returns Promise<number> The percentage of healthy providers (0-100)
   */
  async getOverallHealthPercentage(): Promise<number> {
    const summary = await this.getHealthSummary();
    
    if (summary.totalProviders === 0) {
      return 100; // No providers means 100% healthy
    }
    
    return (summary.healthyProviders / summary.totalProviders) * 100;
  }

  /**
   * Gets providers by health status
   * 
   * @param isHealthy - Whether to get healthy or unhealthy providers
   * @returns Promise<ProviderHealthStatusDto[]> List of providers with the specified health status
   */
  async getProvidersByHealthStatus(isHealthy: boolean): Promise<ProviderHealthStatusDto[]> {
    const summary = await this.getHealthSummary();
    return summary.providers.filter(p => p.isHealthy === isHealthy);
  }

  /**
   * Gets health trend for a provider over time
   * 
   * @param providerName - The name of the provider
   * @param startDate - Start date for the trend analysis
   * @param endDate - End date for the trend analysis
   * @returns Promise<ProviderHealthRecordDto[]> Health records for trend analysis
   */
  async getProviderHealthTrend(
    providerName: string,
    startDate: Date,
    endDate: Date
  ): Promise<ProviderHealthRecordDto[]> {
    if (!providerName?.trim()) {
      throw new Error('Provider name cannot be null or empty');
    }
    if (startDate > endDate) {
      throw new Error('Start date cannot be greater than end date');
    }

    const filters: ProviderHealthFilters = {
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString(),
      pageSize: 1000 // Get a large number of records for trend analysis
    };

    const response = await this.getProviderHealthRecords(providerName, filters);
    return response.data || [];
  }

  /**
   * Gets health statistics summary for multiple providers
   * 
   * @param providerNames - List of provider names to get statistics for
   * @returns Promise<Record<string, ProviderHealthStatusDto>> Statistics by provider name
   */
  async getMultipleProviderStatistics(
    providerNames: string[]
  ): Promise<Record<string, ProviderHealthStatusDto>> {
    if (!providerNames || providerNames.length === 0) {
      return {};
    }

    const results: Record<string, ProviderHealthStatusDto> = {};
    
    // Get all statuses in parallel
    const promises = providerNames.map(async (providerName) => {
      try {
        const status = await this.getProviderHealthStatus(providerName);
        results[providerName] = status;
      } catch {
        // Skip providers that fail to load
        console.warn(`Failed to get health status for provider ${providerName}:`, error);
      }
    });

    await Promise.allSettled(promises);
    return results;
  }
}