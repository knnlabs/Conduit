import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';

// Response types based on the C# DTOs
export interface CostDashboardDto {
  totalCost: number;
  costChange: number;
  costChangePercentage: number;
  topModels: ModelCostDto[];
  topProviders: ProviderCostDto[];
  costByDay: DailyCostDto[];
}

export interface ModelCostDto {
  model: string;
  provider: string;
  cost: number;
  requestCount: number;
  tokenCount: number;
}

export interface ProviderCostDto {
  provider: string;
  cost: number;
  requestCount: number;
  tokenCount: number;
}

export interface DailyCostDto {
  date: string;
  cost: number;
  requestCount: number;
  tokenCount: number;
}

export interface CostTrendDto {
  period: string;
  cost: number;
  requestCount: number;
  tokenCount: number;
  avgCostPerRequest: number;
  avgTokensPerRequest: number;
}

export interface ModelCostDataDto {
  model: string;
  provider: string;
  cost: number;
  requestCount: number;
  averageCostPerRequest: number;
}

export interface VirtualKeyCostDataDto {
  virtualKeyId: number;
  keyName: string;
  cost: number;
  requestCount: number;
  averageCostPerRequest: number;
  budgetUsed?: number;
  budgetRemaining?: number;
}

/**
 * Type-safe Cost Dashboard service using native fetch
 * Provides access to actual /api/costs endpoints
 */
export class FetchCostDashboardService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get cost dashboard summary data
   * @param timeframe - The timeframe for the summary (daily, weekly, monthly)
   * @param startDate - Optional start date
   * @param endDate - Optional end date
   */
  async getCostSummary(
    timeframe: 'daily' | 'weekly' | 'monthly' = 'daily',
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<CostDashboardDto> {
    const queryParams = new URLSearchParams({ timeframe });
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    return this.client['get']<CostDashboardDto>(
      `${ENDPOINTS.COSTS.SUMMARY}?${queryParams.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cost trend data
   * @param period - The period for the trend (daily, weekly, monthly)
   * @param startDate - Optional start date
   * @param endDate - Optional end date
   */
  async getCostTrends(
    period: 'daily' | 'weekly' | 'monthly' = 'daily',
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<CostTrendDto[]> {
    const queryParams = new URLSearchParams({ period });
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    return this.client['get']<CostTrendDto[]>(
      `${ENDPOINTS.COSTS.TRENDS}?${queryParams.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get model costs data
   * @param startDate - Optional start date
   * @param endDate - Optional end date
   */
  async getModelCosts(
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<ModelCostDataDto[]> {
    const queryParams = new URLSearchParams();
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.COSTS.MODELS}?${queryString}` : ENDPOINTS.COSTS.MODELS;

    return this.client['get']<ModelCostDataDto[]>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get virtual key costs data
   * @param startDate - Optional start date
   * @param endDate - Optional end date
   */
  async getVirtualKeyCosts(
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<VirtualKeyCostDataDto[]> {
    const queryParams = new URLSearchParams();
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.COSTS.VIRTUAL_KEYS}?${queryString}` : ENDPOINTS.COSTS.VIRTUAL_KEYS;

    return this.client['get']<VirtualKeyCostDataDto[]>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to format date range
   */
  formatDateRange(days: number): { startDate: string; endDate: string } {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - days);

    return {
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString(),
    };
  }

  /**
   * Helper method to calculate growth rate
   */
  calculateGrowthRate(current: number, previous: number): number {
    if (previous === 0) return current > 0 ? 100 : 0;
    return ((current - previous) / previous) * 100;
  }
}