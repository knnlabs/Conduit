import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { HttpMethod } from '../client/HttpMethod';
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
   * Stream real-time metrics
   */
  async *streamMetrics(
    params: MetricsQueryParams,
    config?: RequestConfig
  ): AsyncGenerator<MetricsResponse, void, unknown> {
    const response = await this.client['request']<ReadableStream<Uint8Array>>(
      '/api/monitoring/metrics/stream',
      {
        method: HttpMethod.POST,
        headers: {
          ...config?.headers,
          'Accept': 'text/event-stream',
        },
        body: JSON.stringify(params),
        signal: config?.signal,
        timeout: config?.timeout,
      }
    );

    if (!(response instanceof ReadableStream)) {
      throw new Error('Expected ReadableStream response');
    }

    const reader = response.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() ?? '';

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6);
            if (data === '[DONE]') continue;
            try {
              yield JSON.parse(data) as MetricsResponse;
            } catch {
              // Skip invalid JSON
            }
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
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

  /**
   * Stream system resource metrics
   */
  async *streamSystemMetrics(
    config?: RequestConfig
  ): AsyncGenerator<SystemResourceMetrics, void, unknown> {
    const response = await this.client['request']<ReadableStream<Uint8Array>>(
      '/api/monitoring/system/stream',
      {
        method: HttpMethod.GET,
        headers: {
          ...config?.headers,
          'Accept': 'text/event-stream',
        },
        signal: config?.signal,
        timeout: config?.timeout,
      }
    );

    if (!(response instanceof ReadableStream)) {
      throw new Error('Expected ReadableStream response');
    }

    const reader = response.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() ?? '';

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6);
            if (data === '[DONE]') continue;
            try {
              yield JSON.parse(data) as SystemResourceMetrics;
            } catch {
              // Skip invalid JSON
            }
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
  }
}