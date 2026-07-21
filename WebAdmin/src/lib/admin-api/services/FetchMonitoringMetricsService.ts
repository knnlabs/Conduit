import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type {
  MetricsQueryParams,
  MetricsResponse,
  MetricExportParams,
  MetricExportResult,
  SystemResourceMetrics,
} from '../models/monitoring';

/**
 * Type-safe Monitoring metrics service using native fetch
 */
export class FetchMonitoringMetricsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Query real-time metrics
   */
  async queryMetrics(params: MetricsQueryParams, config?: RequestConfig): Promise<MetricsResponse> {
    return this.client['post']<MetricsResponse, MetricsQueryParams>(
      '/api/monitoring/metrics/query',
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export metrics data
   */
  async exportMetrics(params: MetricExportParams, config?: RequestConfig): Promise<MetricExportResult> {
    return this.client['post']<MetricExportResult, MetricExportParams>(
      '/api/monitoring/metrics/export',
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get metric export status
   */
  async getExportStatus(exportId: string, config?: RequestConfig): Promise<MetricExportResult> {
    return this.client['get']<MetricExportResult>(
      `/api/monitoring/metrics/export/${exportId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get system resource metrics
   */
  async getSystemMetrics(config?: RequestConfig): Promise<SystemResourceMetrics> {
    return this.client['get']<SystemResourceMetrics>(
      '/api/monitoring/system',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

}
