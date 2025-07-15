import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  RequestLogParams,
  RequestLogPage,
  RequestLogDto,
  CostSummaryDto,
  CostByPeriodDto,
  ExportParams,
  ExportResult,
} from '../models/analytics';

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
   * Export request logs
   */
  async exportRequestLogs(params: ExportParams, config?: RequestConfig): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      ENDPOINTS.ANALYTICS.EXPORT_REQUEST_LOGS,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }








  /**
   * Get cost summary (legacy endpoint)
   */
  async getCostSummary(startDate?: string, endDate?: string, config?: RequestConfig): Promise<CostSummaryDto> {
    const queryParams = new URLSearchParams();
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.COST_SUMMARY}?${queryString}` : ENDPOINTS.ANALYTICS.COST_SUMMARY;

    return this.client['get']<CostSummaryDto>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cost by period (legacy endpoint)
   */
  async getCostByPeriod(
    period: 'hour' | 'day' | 'week' | 'month',
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<CostByPeriodDto> {
    const queryParams = new URLSearchParams({ period });
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    return this.client['get']<CostByPeriodDto>(
      `${ENDPOINTS.ANALYTICS.COST_BY_PERIOD}?${queryParams.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to get export status
   */
  async getExportStatus(exportId: string, config?: RequestConfig): Promise<ExportResult> {
    return this.client['get']<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_STATUS(exportId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to download export
   */
  async downloadExport(exportId: string, config?: RequestConfig): Promise<Blob> {
    const response = await this.client['get']<Response>(
      ENDPOINTS.ANALYTICS.EXPORT_DOWNLOAD(exportId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
        responseType: 'raw',
      }
    );

    return response.blob();
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

      grouped.set(period, (grouped.get(period) || 0) + item.value);
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




}