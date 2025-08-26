import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { components } from '../generated/admin-api';

type ProviderErrorDto = components['schemas']['ConduitLLM.Admin.DTOs.ProviderErrorDto'];
type ProviderErrorSummaryDto = components['schemas']['ConduitLLM.Admin.DTOs.ProviderErrorSummaryDto'];
type ErrorStatisticsDto = components['schemas']['ConduitLLM.Admin.DTOs.ErrorStatisticsDto'];
type KeyErrorDetailsDto = components['schemas']['ConduitLLM.Admin.DTOs.KeyErrorDetailsDto'];

export class FetchProviderErrorsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get recent provider errors
   */
  async getRecentErrors(
    params?: {
      providerId?: number;
      keyId?: number;
      limit?: number;
    },
    config?: RequestConfig
  ): Promise<ProviderErrorDto[]> {
    const queryParams = new URLSearchParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined) {
          queryParams.append(key, String(value));
        }
      });
    }
    
    const url = queryParams.toString()
      ? `/api/provider-errors/recent?${queryParams.toString()}`
      : '/api/provider-errors/recent';
    
    return this.client['get']<ProviderErrorDto[]>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Get error summary for all providers
   */
  async getSummary(config?: RequestConfig): Promise<ProviderErrorSummaryDto[]> {
    return this.client['get']<ProviderErrorSummaryDto[]>(
      '/api/provider-errors/summary',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get error statistics
   */
  async getStatistics(hours: number = 24, config?: RequestConfig): Promise<ErrorStatisticsDto> {
    const url = `/api/provider-errors/stats?hours=${hours}`;
    return this.client['get']<ErrorStatisticsDto>(url, {
      signal: config?.signal,
      timeout: config?.timeout,
      headers: config?.headers,
    });
  }

  /**
   * Get detailed error information for a specific key
   */
  async getKeyErrors(keyId: number, config?: RequestConfig): Promise<KeyErrorDetailsDto> {
    return this.client['get']<KeyErrorDetailsDto>(
      `/api/provider-errors/keys/${keyId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Clear errors and optionally re-enable a key
   */
  async clearKeyErrors(
    keyId: number,
    options: {
      reEnableKey?: boolean;
      confirmReenable?: boolean;
      reason?: string;
    },
    config?: RequestConfig
  ): Promise<void> {
    await this.client['post'](
      `/api/provider-errors/keys/${keyId}/clear`,
      options,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }
}