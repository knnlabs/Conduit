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
  metadata?: Record<string, any>;
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
  metadata?: Record<string, any>;
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
  config: Record<string, any>;
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
  metadata?: Record<string, any>;
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
  metadata?: Record<string, any>;
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
  metadata?: Record<string, any>;
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
  [key: string]: any;
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
  metadata?: Record<string, any>;
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
  metadata?: Record<string, any>;
  isPublic?: boolean;
}

/**
 * System resource metrics
 */
export interface SystemResourceMetrics {
  cpu: MonitoringCpuMetrics;
  memory: MonitoringMemoryMetrics;
  disk: DiskMetrics;
  network: NetworkMetrics;
  processes: ProcessMetrics[];
  timestamp: string;
}

/**
 * Extended CPU metrics
 */
export interface MonitoringCpuMetrics {
  usage: number;
  userTime: number;
  systemTime: number;
  idleTime: number;
  cores: CpuCoreMetrics[];
}

/**
 * CPU core metrics
 */
export interface CpuCoreMetrics {
  coreId: number;
  usage: number;
  frequency: number;
  temperature?: number;
}

/**
 * Extended memory metrics
 */
export interface MonitoringMemoryMetrics {
  total: number;
  used: number;
  free: number;
  available: number;
  cached: number;
  buffers: number;
  swapTotal: number;
  swapUsed: number;
  swapFree: number;
}

/**
 * Disk metrics
 */
export interface DiskMetrics {
  devices: DiskDeviceMetrics[];
  totalReadBytes: number;
  totalWriteBytes: number;
  readOpsPerSecond: number;
  writeOpsPerSecond: number;
}

/**
 * Disk device metrics
 */
export interface DiskDeviceMetrics {
  device: string;
  mountPoint: string;
  totalSpace: number;
  usedSpace: number;
  freeSpace: number;
  usagePercent: number;
  readBytes: number;
  writeBytes: number;
  ioBusy: number;
}

/**
 * Network metrics
 */
export interface NetworkMetrics {
  interfaces: NetworkInterfaceMetrics[];
  totalBytesReceived: number;
  totalBytesSent: number;
  packetsReceived: number;
  packetsSent: number;
  errors: number;
  dropped: number;
}

/**
 * Network interface metrics
 */
export interface NetworkInterfaceMetrics {
  name: string;
  bytesReceived: number;
  bytesSent: number;
  packetsReceived: number;
  packetsSent: number;
  errors: number;
  dropped: number;
  status: 'up' | 'down';
}

/**
 * Process metrics
 */
export interface ProcessMetrics {
  pid: number;
  name: string;
  cpuUsage: number;
  memoryUsage: number;
  threads: number;
  handles: number;
  startTime: string;
}

/**
 * Distributed trace
 */
export interface TraceDto {
  traceId: string;
  spans: SpanDto[];
  startTime: string;
  endTime: string;
  duration: number;
  serviceName: string;
  status: 'ok' | 'error' | 'timeout';
  tags: Record<string, string>;
}

/**
 * Trace span
 */
export interface SpanDto {
  spanId: string;
  parentSpanId?: string;
  operationName: string;
  serviceName: string;
  startTime: string;
  endTime: string;
  duration: number;
  status: 'ok' | 'error' | 'timeout';
  tags: Record<string, string>;
  logs: SpanLog[];
}

/**
 * Span log entry
 */
export interface SpanLog {
  timestamp: string;
  level: 'debug' | 'info' | 'warn' | 'error';
  message: string;
  fields?: Record<string, any>;
}

/**
 * Trace query parameters
 */
export interface TraceQueryParams {
  service?: string;
  operation?: string;
  minDuration?: number;
  maxDuration?: number;
  status?: 'ok' | 'error' | 'timeout';
  startTime?: string;
  endTime?: string;
  tags?: Record<string, string>;
  limit?: number;
}

/**
 * Log entry
 */
export interface LogEntry {
  id: string;
  timestamp: string;
  level: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
  message: string;
  service: string;
  traceId?: string;
  spanId?: string;
  fields: Record<string, any>;
  stackTrace?: string;
}

/**
 * Log query parameters
 */
export interface LogQueryParams {
  query?: string;
  level?: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
  service?: string;
  startTime?: string;
  endTime?: string;
  traceId?: string;
  fields?: Record<string, string>;
  limit?: number;
  offset?: number;
}

/**
 * Log stream options
 */
export interface LogStreamOptions {
  query?: string;
  level?: 'debug' | 'info' | 'warn' | 'error' | 'fatal';
  service?: string;
  follow?: boolean;
  tail?: number;
}

/**
 * Monitoring health status
 */
export interface MonitoringHealthStatus {
  healthy: boolean;
  services: ServiceHealthStatus[];
  lastCheck: string;
  message?: string;
}

/**
 * Service health status
 */
export interface ServiceHealthStatus {
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  lastCheck: string;
  message?: string;
  metrics?: Record<string, number>;
}

/**
 * Metric export parameters
 */
export interface MetricExportParams {
  metrics: string[];
  startTime: string;
  endTime: string;
  format: 'csv' | 'json' | 'prometheus';
  aggregation?: 'raw' | 'avg' | 'sum' | 'max' | 'min';
  interval?: string;
}

/**
 * Metric export result
 */
export interface MetricExportResult {
  exportId: string;
  status: 'pending' | 'processing' | 'completed' | 'failed';
  format: 'csv' | 'json' | 'prometheus';
  sizeBytes?: number;
  recordCount?: number;
  downloadUrl?: string;
  error?: string;
  createdAt: string;
  completedAt?: string;
}