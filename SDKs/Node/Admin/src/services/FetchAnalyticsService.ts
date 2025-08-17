import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
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
 * Type-safe Analytics service using native fetch
 */
export class FetchAnalyticsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get paginated request logs
   */
  async getRequestLogs(params?: RequestLogParams, config?: RequestConfig): Promise<RequestLogPage> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.page) queryParams.append('page', params.page.toString());
      if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.virtualKeyId) queryParams.append('virtualKeyId', params.virtualKeyId);
      if (params.provider) queryParams.append('provider', params.provider);
      if (params.model) queryParams.append('model', params.model);
      if (params.statusCode) queryParams.append('statusCode', params.statusCode.toString());
      if (params.minLatency) queryParams.append('minLatency', params.minLatency.toString());
      if (params.maxLatency) queryParams.append('maxLatency', params.maxLatency.toString());
      if (params.sortBy) queryParams.append('sortBy', params.sortBy);
      if (params.sortOrder) queryParams.append('sortOrder', params.sortOrder);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.REQUEST_LOGS}?${queryString}` : ENDPOINTS.ANALYTICS.REQUEST_LOGS;

    return this.client['get']<RequestLogPage>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific request log by ID
   */
  async getRequestLogById(id: string, config?: RequestConfig): Promise<RequestLogDto> {
    return this.client['get']<RequestLogDto>(
      ENDPOINTS.ANALYTICS.REQUEST_LOG_BY_ID(id),
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
    const queryParams = new URLSearchParams();
    queryParams.append('timeframe', timeframe);
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    const queryString = queryParams.toString();
    const url = `${ENDPOINTS.ANALYTICS.COST_SUMMARY}?${queryString}`;

    return this.client['get']<CostDashboardDto>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
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
    const queryParams = new URLSearchParams();
    queryParams.append('period', period);
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    const queryString = queryParams.toString();
    const url = `${ENDPOINTS.ANALYTICS.COST_TRENDS}?${queryString}`;

    return this.client['get']<CostTrendDto>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
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
    const queryParams = new URLSearchParams();
    queryParams.append('format', format);
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);
    if (model) queryParams.append('model', model);
    if (virtualKeyId) queryParams.append('virtualKeyId', virtualKeyId.toString());

    const queryString = queryParams.toString();
    const url = `${ENDPOINTS.ANALYTICS.EXPORT}?${queryString}`;

    // Make a direct fetch request for binary data
    const baseUrl = (this.client as any).baseUrl || '';
    const masterKey = (this.client as any).masterKey || '';
    
    const response = await fetch(`${baseUrl}${url}`, {
      method: 'GET',
      headers: {
        'X-API-Key': masterKey,
        'Accept': format === 'csv' ? 'text/csv' : 'application/json',
        ...config?.headers,
      },
      signal: config?.signal,
    });

    if (!response.ok) {
      throw new Error(`Export failed: ${response.statusText}`);
    }

    const buffer = await response.arrayBuffer();
    return new Uint8Array(buffer);
  }
}