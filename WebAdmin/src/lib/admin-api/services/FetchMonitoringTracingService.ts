import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type {
  TraceDto,
  TraceQueryParams,
  LogEntry,
  LogQueryParams,
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
