import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { HttpMethod } from '../client/HttpMethod';
import type { RequestConfig } from '../client/types';
import type {
  MetricsQueryParams,
  MetricsResponse,
  AlertDto,
  CreateAlertDto,
  UpdateAlertDto,
  AlertHistoryEntry,
  AlertAction,
  DashboardDto,
  CreateDashboardDto,
  UpdateDashboardDto,
  SystemResourceMetrics,
  TraceDto,
  TraceQueryParams,
  LogEntry,
  LogQueryParams,
  LogStreamOptions,
  MonitoringHealthStatus,
  MetricExportParams,
  MetricExportResult,
  AlertSeverity,
  AlertStatus,
} from '../models/monitoring';
import type { FilterOptions, PagedResponse } from '../models/common';

/**
 * Type-safe Monitoring service using native fetch
 */
export class FetchMonitoringService {
  constructor(private readonly client: FetchBaseApiClient) {}

  // Real-time Metrics

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

  // Alert Management

  /**
   * List alerts
   */
  async listAlerts(
    filters?: FilterOptions & {
      severity?: AlertSeverity;
      status?: AlertStatus;
      metric?: string;
    },
    config?: RequestConfig
  ): Promise<PagedResponse<AlertDto>> {
    const queryParams = new URLSearchParams();
    
    if (filters?.search) queryParams.append('search', filters.search);
    if (filters?.pageNumber) queryParams.append('pageNumber', filters.pageNumber.toString());
    if (filters?.pageSize) queryParams.append('pageSize', filters.pageSize.toString());
    if (filters?.severity) queryParams.append('severity', filters.severity);
    if (filters?.status) queryParams.append('status', filters.status);
    if (filters?.metric) queryParams.append('metric', filters.metric);

    const url = `/api/monitoring/alerts${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;

    return this.client['get']<PagedResponse<AlertDto>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get alert by ID
   */
  async getAlert(alertId: string, config?: RequestConfig): Promise<AlertDto> {
    return this.client['get']<AlertDto>(
      `/api/monitoring/alerts/${alertId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create alert
   */
  async createAlert(alert: CreateAlertDto, config?: RequestConfig): Promise<AlertDto> {
    return this.client['post']<AlertDto, CreateAlertDto>(
      '/api/monitoring/alerts',
      alert,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update alert
   */
  async updateAlert(alertId: string, alert: UpdateAlertDto, config?: RequestConfig): Promise<AlertDto> {
    return this.client['put']<AlertDto, UpdateAlertDto>(
      `/api/monitoring/alerts/${alertId}`,
      alert,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete alert
   */
  async deleteAlert(alertId: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      `/api/monitoring/alerts/${alertId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Acknowledge alert
   */
  async acknowledgeAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto> {
    return this.client['post']<AlertDto, { notes?: string }>(
      `/api/monitoring/alerts/${alertId}/acknowledge`,
      { notes },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Resolve alert
   */
  async resolveAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto> {
    return this.client['post']<AlertDto, { notes?: string }>(
      `/api/monitoring/alerts/${alertId}/resolve`,
      { notes },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get alert history
   */
  async getAlertHistory(
    alertId: string,
    filters?: FilterOptions,
    config?: RequestConfig
  ): Promise<PagedResponse<AlertHistoryEntry>> {
    const queryParams = new URLSearchParams();
    
    if (filters?.search) queryParams.append('search', filters.search);
    if (filters?.pageNumber) queryParams.append('pageNumber', filters.pageNumber.toString());
    if (filters?.pageSize) queryParams.append('pageSize', filters.pageSize.toString());

    const url = `/api/monitoring/alerts/${alertId}/history${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;

    return this.client['get']<PagedResponse<AlertHistoryEntry>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // Dashboard Management

  /**
   * List dashboards
   */
  async listDashboards(filters?: FilterOptions, config?: RequestConfig): Promise<PagedResponse<DashboardDto>> {
    const queryParams = new URLSearchParams();
    
    if (filters?.search) queryParams.append('search', filters.search);
    if (filters?.pageNumber) queryParams.append('pageNumber', filters.pageNumber.toString());
    if (filters?.pageSize) queryParams.append('pageSize', filters.pageSize.toString());

    const url = `/api/monitoring/dashboards${queryParams.toString() ? `?${queryParams.toString()}` : ''}`;

    return this.client['get']<PagedResponse<DashboardDto>>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get dashboard by ID
   */
  async getDashboard(dashboardId: string, config?: RequestConfig): Promise<DashboardDto> {
    return this.client['get']<DashboardDto>(
      `/api/monitoring/dashboards/${dashboardId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create dashboard
   */
  async createDashboard(dashboard: CreateDashboardDto, config?: RequestConfig): Promise<DashboardDto> {
    return this.client['post']<DashboardDto, CreateDashboardDto>(
      '/api/monitoring/dashboards',
      dashboard,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update dashboard
   */
  async updateDashboard(
    dashboardId: string,
    dashboard: UpdateDashboardDto,
    config?: RequestConfig
  ): Promise<DashboardDto> {
    return this.client['put']<DashboardDto, UpdateDashboardDto>(
      `/api/monitoring/dashboards/${dashboardId}`,
      dashboard,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete dashboard
   */
  async deleteDashboard(dashboardId: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      `/api/monitoring/dashboards/${dashboardId}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Clone dashboard
   */
  async cloneDashboard(
    dashboardId: string,
    name: string,
    config?: RequestConfig
  ): Promise<DashboardDto> {
    return this.client['post']<DashboardDto, { name: string }>(
      `/api/monitoring/dashboards/${dashboardId}/clone`,
      { name },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  // System Monitoring

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

  // Helper methods

  /**
   * Calculate metric statistics
   */
  calculateMetricStats(series: MetricsResponse['series'][0]): {
    min: number;
    max: number;
    avg: number;
    sum: number;
    count: number;
    stdDev: number;
  } {
    const values = series.dataPoints.map(p => p.value);
    
    if (values.length === 0) {
      return { min: 0, max: 0, avg: 0, sum: 0, count: 0, stdDev: 0 };
    }

    const min = Math.min(...values);
    const max = Math.max(...values);
    const sum = values.reduce((a, b) => a + b, 0);
    const count = values.length;
    const avg = sum / count;
    
    const variance = values.reduce((acc, val) => acc + Math.pow(val - avg, 2), 0) / count;
    const stdDev = Math.sqrt(variance);

    return { min, max, avg, sum, count, stdDev };
  }

  /**
   * Format metric value with unit
   */
  formatMetricValue(value: number, unit: string): string {
    switch (unit) {
      case 'bytes':
        return this.formatBytes(value);
      case 'milliseconds':
        return `${value.toFixed(2)}ms`;
      case 'seconds':
        return `${value.toFixed(2)}s`;
      case 'percentage':
        return `${value.toFixed(2)}%`;
      case 'count':
        return value.toLocaleString();
      default:
        return `${value.toFixed(2)} ${unit}`;
    }
  }

  /**
   * Format bytes to human readable format
   */
  private formatBytes(bytes: number): string {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;

    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }

    return `${size.toFixed(2)} ${units[unitIndex]}`;
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

  /**
   * Generate alert summary message
   */
  generateAlertSummary(alerts: AlertDto[]): string {
    const byStatus = alerts.reduce((acc, alert) => {
      acc[alert.status] = (acc[alert.status] ?? 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    const bySeverity = alerts.reduce((acc, alert) => {
      acc[alert.severity] = (acc[alert.severity] ?? 0) + 1;
      return acc;
    }, {} as Record<string, number>);

    const parts = [];
    
    if (byStatus.active > 0) {
      parts.push(`${byStatus.active} active`);
    }
    if (byStatus.acknowledged > 0) {
      parts.push(`${byStatus.acknowledged} acknowledged`);
    }
    
    const severityParts = [];
    if (bySeverity.critical > 0) {
      severityParts.push(`${bySeverity.critical} critical`);
    }
    if (bySeverity.error > 0) {
      severityParts.push(`${bySeverity.error} error`);
    }
    if (bySeverity.warning > 0) {
      severityParts.push(`${bySeverity.warning} warning`);
    }

    return `Alerts: ${parts.join(', ')}${severityParts.length > 0 ? ` (${severityParts.join(', ')})` : ''}`;
  }

  /**
   * Calculate system health score
   */
  calculateSystemHealthScore(metrics: SystemResourceMetrics): number {
    let score = 100;

    // CPU usage impact
    if (metrics.cpu.usage > 90) score -= 30;
    else if (metrics.cpu.usage > 80) score -= 20;
    else if (metrics.cpu.usage > 70) score -= 10;

    // Memory usage impact
    const memoryUsagePercent = (metrics.memory.used / metrics.memory.total) * 100;
    if (memoryUsagePercent > 90) score -= 25;
    else if (memoryUsagePercent > 80) score -= 15;
    else if (memoryUsagePercent > 70) score -= 5;

    // Disk usage impact
    const maxDiskUsage = Math.max(...metrics.disk.devices.map(d => d.usagePercent));
    if (maxDiskUsage > 90) score -= 20;
    else if (maxDiskUsage > 80) score -= 10;
    else if (maxDiskUsage > 70) score -= 5;

    // Network errors impact
    if (metrics.network.errors > 1000) score -= 15;
    else if (metrics.network.errors > 100) score -= 10;
    else if (metrics.network.errors > 10) score -= 5;

    return Math.max(0, score);
  }

  /**
   * Get recommended alert actions based on severity
   */
  getRecommendedAlertActions(severity: AlertSeverity): AlertAction[] {
    switch (severity) {
      case 'critical':
        return [
          { type: 'pagerduty', config: { urgency: 'high' } },
          { type: 'email', config: { to: 'oncall@company.com' } },
          { type: 'slack', config: { channel: '#alerts-critical' } },
        ];
      case 'error':
        return [
          { type: 'email', config: { to: 'team@company.com' } },
          { type: 'slack', config: { channel: '#alerts' } },
        ];
      case 'warning':
        return [
          { type: 'slack', config: { channel: '#alerts' } },
        ];
      case 'info':
        return [
          { type: 'log', config: { level: 'info' } },
        ];
    }
  }
  
}