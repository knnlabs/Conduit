import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
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
import { FetchMonitoringMetricsService } from './FetchMonitoringMetricsService';
import { FetchMonitoringAlertsService } from './FetchMonitoringAlertsService';
import { FetchMonitoringDashboardService } from './FetchMonitoringDashboardService';
import { FetchMonitoringTracingService } from './FetchMonitoringTracingService';
import { FetchMonitoringHelpers } from './FetchMonitoringHelpers';

/**
 * Type-safe Monitoring service using native fetch
 */
export class FetchMonitoringService {
  private metricsService: FetchMonitoringMetricsService;
  private alertsService: FetchMonitoringAlertsService;
  private dashboardService: FetchMonitoringDashboardService;
  private tracingService: FetchMonitoringTracingService;
  private helpers: FetchMonitoringHelpers;

  constructor(private readonly client: FetchBaseApiClient) {
    this.metricsService = new FetchMonitoringMetricsService(client);
    this.alertsService = new FetchMonitoringAlertsService(client);
    this.dashboardService = new FetchMonitoringDashboardService(client);
    this.tracingService = new FetchMonitoringTracingService(client);
    this.helpers = new FetchMonitoringHelpers();
  }

  // Real-time Metrics

  /**
   * Query real-time metrics
   */
  async queryMetrics(params: MetricsQueryParams, config?: RequestConfig): Promise<MetricsResponse> {
    return this.metricsService.queryMetrics(params, config);
  }

  /**
   * Stream real-time metrics
   */
  async *streamMetrics(
    params: MetricsQueryParams,
    config?: RequestConfig
  ): AsyncGenerator<MetricsResponse, void, unknown> {
    yield* this.metricsService.streamMetrics(params, config);
  }

  /**
   * Export metrics data
   */
  async exportMetrics(params: MetricExportParams, config?: RequestConfig): Promise<MetricExportResult> {
    return this.metricsService.exportMetrics(params, config);
  }

  /**
   * Get metric export status
   */
  async getExportStatus(exportId: string, config?: RequestConfig): Promise<MetricExportResult> {
    return this.metricsService.getExportStatus(exportId, config);
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
    return this.alertsService.listAlerts(filters, config);
  }

  /**
   * Get alert by ID
   */
  async getAlert(alertId: string, config?: RequestConfig): Promise<AlertDto> {
    return this.alertsService.getAlert(alertId, config);
  }

  /**
   * Create alert
   */
  async createAlert(alert: CreateAlertDto, config?: RequestConfig): Promise<AlertDto> {
    return this.alertsService.createAlert(alert, config);
  }

  /**
   * Update alert
   */
  async updateAlert(alertId: string, alert: UpdateAlertDto, config?: RequestConfig): Promise<AlertDto> {
    return this.alertsService.updateAlert(alertId, alert, config);
  }

  /**
   * Delete alert
   */
  async deleteAlert(alertId: string, config?: RequestConfig): Promise<void> {
    return this.alertsService.deleteAlert(alertId, config);
  }

  /**
   * Acknowledge alert
   */
  async acknowledgeAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto> {
    return this.alertsService.acknowledgeAlert(alertId, notes, config);
  }

  /**
   * Resolve alert
   */
  async resolveAlert(alertId: string, notes?: string, config?: RequestConfig): Promise<AlertDto> {
    return this.alertsService.resolveAlert(alertId, notes, config);
  }

  /**
   * Get alert history
   */
  async getAlertHistory(
    alertId: string,
    filters?: FilterOptions,
    config?: RequestConfig
  ): Promise<PagedResponse<AlertHistoryEntry>> {
    return this.alertsService.getAlertHistory(alertId, filters, config);
  }

  // Dashboard Management

  /**
   * List dashboards
   */
  async listDashboards(filters?: FilterOptions, config?: RequestConfig): Promise<PagedResponse<DashboardDto>> {
    return this.dashboardService.listDashboards(filters, config);
  }

  /**
   * Get dashboard by ID
   */
  async getDashboard(dashboardId: string, config?: RequestConfig): Promise<DashboardDto> {
    return this.dashboardService.getDashboard(dashboardId, config);
  }

  /**
   * Create dashboard
   */
  async createDashboard(dashboard: CreateDashboardDto, config?: RequestConfig): Promise<DashboardDto> {
    return this.dashboardService.createDashboard(dashboard, config);
  }

  /**
   * Update dashboard
   */
  async updateDashboard(
    dashboardId: string,
    dashboard: UpdateDashboardDto,
    config?: RequestConfig
  ): Promise<DashboardDto> {
    return this.dashboardService.updateDashboard(dashboardId, dashboard, config);
  }

  /**
   * Delete dashboard
   */
  async deleteDashboard(dashboardId: string, config?: RequestConfig): Promise<void> {
    return this.dashboardService.deleteDashboard(dashboardId, config);
  }

  /**
   * Clone dashboard
   */
  async cloneDashboard(
    dashboardId: string,
    name: string,
    config?: RequestConfig
  ): Promise<DashboardDto> {
    return this.dashboardService.cloneDashboard(dashboardId, name, config);
  }

  // System Monitoring

  /**
   * Get system resource metrics
   */
  async getSystemMetrics(config?: RequestConfig): Promise<SystemResourceMetrics> {
    return this.metricsService.getSystemMetrics(config);
  }

  /**
   * Stream system resource metrics
   */
  async *streamSystemMetrics(
    config?: RequestConfig
  ): AsyncGenerator<SystemResourceMetrics, void, unknown> {
    yield* this.metricsService.streamSystemMetrics(config);
  }

  // Distributed Tracing

  /**
   * Search traces
   */
  async searchTraces(params: TraceQueryParams, config?: RequestConfig): Promise<PagedResponse<TraceDto>> {
    return this.tracingService.searchTraces(params, config);
  }

  /**
   * Get trace by ID
   */
  async getTrace(traceId: string, config?: RequestConfig): Promise<TraceDto> {
    return this.tracingService.getTrace(traceId, config);
  }

  // Log Management

  /**
   * Search logs
   */
  async searchLogs(params: LogQueryParams, config?: RequestConfig): Promise<PagedResponse<LogEntry>> {
    return this.tracingService.searchLogs(params, config);
  }

  /**
   * Stream logs
   */
  async *streamLogs(
    options: LogStreamOptions,
    config?: RequestConfig
  ): AsyncGenerator<LogEntry, void, unknown> {
    yield* this.tracingService.streamLogs(options, config);
  }

  // Health Status

  /**
   * Get monitoring health status
   */
  async getHealthStatus(config?: RequestConfig): Promise<MonitoringHealthStatus> {
    return this.tracingService.getHealthStatus(config);
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
    return this.helpers.calculateMetricStats(series);
  }

  /**
   * Format metric value with unit
   */
  formatMetricValue(value: number, unit: string): string {
    return this.helpers.formatMetricValue(value, unit);
  }

  /**
   * Parse log query into structured format
   */
  parseLogQuery(query: string): LogQueryParams {
    return this.tracingService.parseLogQuery(query);
  }

  /**
   * Generate alert summary message
   */
  generateAlertSummary(alerts: AlertDto[]): string {
    return this.helpers.generateAlertSummary(alerts);
  }

  /**
   * Calculate system health score
   */
  calculateSystemHealthScore(metrics: SystemResourceMetrics): number {
    return this.helpers.calculateSystemHealthScore(metrics);
  }

  /**
   * Get recommended alert actions based on severity
   */
  getRecommendedAlertActions(severity: AlertSeverity): AlertAction[] {
    return this.helpers.getRecommendedAlertActions(severity);
  }
}