import { AlertMetadata } from './metadata';

/**
 * Real-time monitoring metric
 */
export interface MetricDataPoint {
  timestamp: string;
  value: number;
  unit: string;
  tags?: Record<string, string>;
}

/**
 * Metric time series data
 */
export interface MetricTimeSeries {
  name: string;
  displayName: string;
  unit: string;
  aggregation: 'avg' | 'sum' | 'max' | 'min' | 'count';
  dataPoints: MetricDataPoint[];
  metadata?: AlertMetadata;
}

/**
 * Real-time metrics parameters
 */
export interface MetricsQueryParams {
  metrics: string[];
  startTime?: string;
  endTime?: string;
  interval?: string;
  aggregation?: 'avg' | 'sum' | 'max' | 'min' | 'count';
  groupBy?: string[];
  filters?: Record<string, string>;
}

/**
 * Real-time metrics response
 */
export interface MetricsResponse {
  series: MetricTimeSeries[];
  query: MetricsQueryParams;
  executionTimeMs: number;
}

/**
 * Alert severity levels
 */
export type AlertSeverity = 'critical' | 'error' | 'warning' | 'info';

/**
 * Alert status
 */
export type AlertStatus = 'active' | 'acknowledged' | 'resolved' | 'suppressed';

/**
 * Alert trigger type
 */
export type AlertTriggerType = 'threshold' | 'anomaly' | 'pattern' | 'availability';

/**
 * Alert definition
 */
export interface AlertDto {
  id: string;
  name: string;
  description?: string;
  severity: AlertSeverity;
  status: AlertStatus;
  metric: string;
  condition: AlertCondition;
  actions: AlertAction[];
  metadata?: AlertMetadata;
  createdAt: string;
  updatedAt: string;
  lastTriggered?: string;
  triggeredCount: number;
  enabled: boolean;
}

/**
 * Alert condition definition
 */
export interface AlertCondition {
  type: AlertTriggerType;
  operator: 'gt' | 'gte' | 'lt' | 'lte' | 'eq' | 'neq' | 'contains' | 'not_contains';
  threshold?: number;
  duration?: string;
  evaluationWindow?: string;
  anomalyConfidence?: number;
  pattern?: string;
}

/**
 * Alert action definition
 */
export interface AlertAction {
  type: 'email' | 'webhook' | 'slack' | 'teams' | 'pagerduty' | 'log';
  config: {
    recipients?: string[];
    url?: string;
    channel?: string;
    apiKey?: string;
    priority?: string;
    template?: string;
    [key: string]: string | string[] | undefined;
  };
  cooldownMinutes?: number;
}

/**
 * Create alert request
 */
export interface CreateAlertDto {
  name: string;
  description?: string;
  severity: AlertSeverity;
  metric: string;
  condition: AlertCondition;
  actions: AlertAction[];
  metadata?: AlertMetadata;
  enabled?: boolean;
}

/**
 * Update alert request
 */
export interface UpdateAlertDto {
  name?: string;
  description?: string;
  severity?: AlertSeverity;
  condition?: AlertCondition;
  actions?: AlertAction[];
  metadata?: AlertMetadata;
  enabled?: boolean;
}

/**
 * Alert history entry
 */
export interface AlertHistoryEntry {
  alertId: string;
  timestamp: string;
  status: AlertStatus;
  value: number;
  message: string;
  actionsTaken: string[];
  acknowledgedBy?: string;
  resolvedBy?: string;
  notes?: string;
}

/**
 * Dashboard definition
 */
export interface DashboardDto {
  id: string;
  name: string;
  description?: string;
  layout: DashboardLayout;
  widgets: DashboardWidget[];
  refreshInterval?: number;
  metadata?: AlertMetadata;
  isPublic: boolean;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

/**
 * Dashboard layout configuration
 */
export interface DashboardLayout {
  type: 'grid' | 'flex' | 'fixed';
  columns?: number;
  rows?: number;
  breakpoints?: Record<string, number>;
}

/**
 * Dashboard widget definition
 */
export interface DashboardWidget {
  id: string;
  type: 'metric' | 'chart' | 'table' | 'gauge' | 'heatmap' | 'logs' | 'alerts';
  title: string;
  position: WidgetPosition;
  config: WidgetConfig;
  dataSource: WidgetDataSource;
}

/**
 * Widget position in dashboard
 */
export interface WidgetPosition {
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * Widget configuration
 */
export interface WidgetConfig {
  chartType?: 'line' | 'bar' | 'area' | 'pie' | 'scatter';
  colors?: string[];
  showLegend?: boolean;
  showGrid?: boolean;
  yAxisRange?: [number, number];
  thresholds?: Array<{ value: number; color: string; label?: string }>;
  displayFormat?: string;
  // Additional chart-specific configuration
  [key: string]: string | number | boolean | string[] | [number, number] | Array<{ value: number; color: string; label?: string }> | undefined;
}

/**
 * Widget data source configuration
 */
export interface WidgetDataSource {
  metrics?: string[];
  query?: string;
  interval?: string;
  aggregation?: 'avg' | 'sum' | 'max' | 'min' | 'count';
  filters?: Record<string, string>;
}

/**
 * Create dashboard request
 */
export interface CreateDashboardDto {
  name: string;
  description?: string;
  layout: DashboardLayout;
  widgets: Omit<DashboardWidget, 'id'>[];
  refreshInterval?: number;
  metadata?: AlertMetadata;
  isPublic?: boolean;
}

/**
 * Update dashboard request
 */
export interface UpdateDashboardDto {
  name?: string;
  description?: string;
  layout?: DashboardLayout;
  widgets?: DashboardWidget[];
  refreshInterval?: number;
  metadata?: AlertMetadata;
  isPublic?: boolean;
}