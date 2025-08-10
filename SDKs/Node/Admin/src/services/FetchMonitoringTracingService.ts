import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { HttpMethod } from '../client/HttpMethod';
import type { RequestConfig } from '../client/types';
import type {
  TraceDto,
  TraceQueryParams,
  LogEntry,
  LogQueryParams,
  LogStreamOptions,
  MonitoringHealthStatus,
} from '../models/monitoring';
import type { PagedResponse } from '../models/common';

/**
 * Type-safe Monitoring tracing and logs service using native fetch
 */
export class FetchMonitoringTracingService {
  constructor(private readonly client: FetchBaseApiClient) {}

  // Distributed Tracing

  /**
   * Search traces
   */
  async searchTraces(params: TraceQueryParams, config?: RequestConfig): Promise<PagedResponse<TraceDto>> {
    return this.client['post']<PagedResponse<TraceDto>, TraceQueryParams>(
      '/api/monitoring/traces/search',
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get trace by ID
   */
  async getTrace(traceId: string, config?: RequestConfig): Promise<TraceDto> {
    return this.client['get']<TraceDto>(
      `/api/monitoring/traces/${traceId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Log Management

  /**
   * Search logs
   */
  async searchLogs(params: LogQueryParams, config?: RequestConfig): Promise<PagedResponse<LogEntry>> {
    return this.client['post']<PagedResponse<LogEntry>, LogQueryParams>(
      '/api/monitoring/logs/search',
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Stream logs
   */
  async *streamLogs(
    options: LogStreamOptions,
    config?: RequestConfig
  ): AsyncGenerator<LogEntry, void, unknown> {
    const response = await this.client['request']<ReadableStream<Uint8Array>>(
      '/api/monitoring/logs/stream',
      {
        method: HttpMethod.POST,
        headers: {
          ...config?.headers,
          'Accept': 'text/event-stream',
        },
        body: JSON.stringify(options),
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
              yield JSON.parse(data) as LogEntry;
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

  // Health Status

  /**
   * Get monitoring health status
   */
  async getHealthStatus(config?: RequestConfig): Promise<MonitoringHealthStatus> {
    return this.client['get']<MonitoringHealthStatus>(
      '/api/monitoring/health',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Parse log query into structured format
   */
  parseLogQuery(query: string): LogQueryParams {
    const params: LogQueryParams = { query };
    
    // Extract common patterns
    const levelMatch = query.match(/level:(debug|info|warn|error|fatal)/i);
    if (levelMatch) {
      params.level = levelMatch[1].toLowerCase() as LogQueryParams['level'];
    }

    const serviceMatch = query.match(/service:(\S+)/);
    if (serviceMatch) {
      params.service = serviceMatch[1];
    }

    const traceMatch = query.match(/trace:(\S+)/);
    if (traceMatch) {
      params.traceId = traceMatch[1];
    }

    return params;
  }
}