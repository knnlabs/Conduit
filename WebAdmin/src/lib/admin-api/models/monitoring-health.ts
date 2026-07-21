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