import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type {
  RequestLogParams,
  RequestLogPage,
  RequestLogDto,
} from '../models/analytics';

// Cost-related types
export interface CostDashboardDto {
  timeFrame: string;
  startDate: string;
  endDate: string;
  last24HoursCost: number;
  last7DaysCost: number;
  last30DaysCost: number;
  totalCost: number;
  topModelsBySpend: DetailedCostDataDto[];
  topProvidersBySpend: DetailedCostDataDto[];
  topVirtualKeysBySpend: DetailedCostDataDto[];
}

export interface DetailedCostDataDto {
  name: string;
  cost: number;
  percentage: number;
  requestCount: number;
}

export interface CostTrendDto {
  period: string;
  startDate: string;
  endDate: string;
  data: CostTrendDataDto[];
}

export interface CostTrendDataDto {
  date: string;
  cost: number;
  requestCount: number;
}

/**
 * Type-safe Analytics service using the Admin contract transport.
 */
export class FetchAnalyticsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get paginated request logs
   */
  async getRequestLogs(params?: RequestLogParams, config?: RequestConfig): Promise<RequestLogPage> {
    const query = {
      page: params?.page,
      pageSize: params?.pageSize,
      startDate: params?.startDate,
      endDate: params?.endDate,
      model: params?.model,
      virtualKeyId: params?.virtualKeyId ? Number(params.virtualKeyId) : undefined,
      status: params?.statusCode,
    };

    return this.client['executeContractRead'](
      '/api/Analytics/logs',
      (contractClient, options) => contractClient.GET('/api/Analytics/logs', {
        ...options,
        params: { query },
      }),
      config,
    ) as unknown as Promise<RequestLogPage>;
  }

  /**
   * Get a specific request log by ID
   */
  async getRequestLogById(id: string, config?: RequestConfig): Promise<RequestLogDto> {
    const numericId = Number(id);
    return this.client['executeContractRead'](
      `/api/Analytics/logs/${numericId}`,
      (contractClient, options) => contractClient.GET('/api/Analytics/logs/{id}', {
        ...options,
        params: { path: { id: numericId } },
      }),
      config,
    ) as unknown as Promise<RequestLogDto>;
  }


  /**
   * Helper method to format date range
   */
  formatDateRange(days: number): { startDate: string; endDate: string } {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - days);

    return {
      startDate: startDate.toISOString().split('T')[0],
      endDate: endDate.toISOString().split('T')[0],
    };
  }

  /**
   * Helper method to calculate growth rate
   */
  calculateGrowthRate(current: number, previous: number): number {
    if (previous === 0) return current > 0 ? 100 : 0;
    return ((current - previous) / previous) * 100;
  }

  /**
   * Helper method to get top items from analytics
   */
  getTopItems<T extends { value: number }>(items: T[], limit: number = 10): T[] {
    return [...items].sort((a, b) => b.value - a.value).slice(0, limit);
  }

  /**
   * Helper method to aggregate time series data
   */
  aggregateTimeSeries(
    data: Array<{ timestamp: string; value: number }>,
    groupBy: 'hour' | 'day' | 'week' | 'month'
  ): Array<{ period: string; value: number }> {
    const grouped = new Map<string, number>();

    data.forEach(item => {
      const date = new Date(item.timestamp);
      let period: string;

      switch (groupBy) {
        case 'hour':
          period = `${date.toISOString().slice(0, 13)}:00`;
          break;
        case 'day':
          period = date.toISOString().slice(0, 10);
          break;
        case 'week': {
          const weekStart = new Date(date);
          weekStart.setDate(date.getDate() - date.getDay());
          period = weekStart.toISOString().slice(0, 10);
          break;
        }
        case 'month':
          period = date.toISOString().slice(0, 7);
          break;
      }

      grouped.set(period, (grouped.get(period) ?? 0) + item.value);
    });

    return Array.from(grouped.entries())
      .map(([period, value]) => ({ period, value }))
      .sort((a, b) => a.period.localeCompare(b.period));
  }

  /**
   * Helper method to validate date range
   */
  validateDateRange(startDate?: string, endDate?: string): boolean {
    if (!startDate || !endDate) return true;

    const start = new Date(startDate);
    const end = new Date(endDate);

    return start <= end && end <= new Date();
  }

  /**
   * Get cost dashboard summary
   */
  async getCostSummary(
    timeframe: string = 'daily',
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<CostDashboardDto> {
    return this.client['executeContractRead'](
      '/api/Analytics/costs/summary',
      (contractClient, options) => contractClient.GET('/api/Analytics/costs/summary', {
        ...options,
        params: { query: { timeframe, startDate, endDate } },
      }),
      config,
    ) as Promise<CostDashboardDto>;
  }

  /**
   * Get cost trends over time
   */
  async getCostTrends(
    period: string = 'daily',
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<CostTrendDto> {
    return this.client['executeContractRead'](
      '/api/Analytics/costs/trends',
      (contractClient, options) => contractClient.GET('/api/Analytics/costs/trends', {
        ...options,
        params: { query: { period, startDate, endDate } },
      }),
      config,
    ) as Promise<CostTrendDto>;
  }

  /**
   * Export analytics data in specified format
   * Returns the data as a Uint8Array for binary compatibility
   */
  async exportAnalyticsAsync(
    format: string = 'csv',
    startDate?: string,
    endDate?: string,
    model?: string,
    virtualKeyId?: number,
    config?: RequestConfig
  ): Promise<Uint8Array> {
    const buffer = await this.client['executeContractRead']<ArrayBuffer>(
      '/api/Analytics/export',
      (contractClient, options) => contractClient.GET('/api/Analytics/export', {
        ...options,
        headers: {
          Accept: format === 'csv' ? 'text/csv' : 'application/json',
          ...options.headers,
        },
        params: { query: { format, startDate, endDate, model, virtualKeyId } },
        parseAs: 'arrayBuffer',
      }),
      config,
    );
    return new Uint8Array(buffer);
  }
}
