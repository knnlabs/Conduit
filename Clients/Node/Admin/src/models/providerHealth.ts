import { FilterOptions } from './common';
import type { 
  ProviderHealthStatusDto,
  ProviderHealthSummaryDto,
  ProviderHealthConfigurationDto,
  UpdateProviderHealthConfigurationDto,
  ProviderHealthRecordDto,
  CreateProviderHealthConfigurationDto,
  ProviderHealthStatisticsDto,
  ProviderHealthFilters
} from './provider';

// Core health types
export interface HealthSummaryDto {
  overall: 'healthy' | 'degraded' | 'unhealthy';
  providers: ProviderHealthSummary[];
  lastUpdated: string;
  alerts: number;
  degradedCount: number;
  unhealthyCount: number;
}

export interface ProviderHealthSummary {
  providerId: string;
  providerName: string;
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  uptime: number; // percentage
  avgLatency: number; // milliseconds
  errorRate: number; // percentage
  lastChecked: string;
}

export interface ProviderHealthDto {
  providerId: string;
  providerName: string;
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  details: {
    connectivity: HealthCheck;
    performance: HealthCheck;
    errorRate: HealthCheck;
    quotaUsage: HealthCheck;
  };
  metrics: {
    uptime: UptimeMetric;
    latency: LatencyMetric;
    throughput: ThroughputMetric;
    errors: ErrorMetric;
  };
  lastIncident?: Incident;
  maintenanceWindows?: MaintenanceWindow[];
}

export interface HealthCheck {
  status: 'ok' | 'warning' | 'critical';
  message: string;
  lastChecked: string;
  details?: Record<string, any>;
}

export interface UptimeMetric {
  percentage: number;
  totalUptime: number; // seconds
  totalDowntime: number; // seconds
  since: string;
}

export interface LatencyMetric {
  current: number;
  avg: number;
  min: number;
  max: number;
  p50: number;
  p95: number;
  p99: number;
}

export interface ThroughputMetric {
  requestsPerMinute: number;
  tokensPerMinute: number;
  bytesPerMinute: number;
}

export interface ErrorMetric {
  rate: number;
  count: number;
  types: Record<string, number>;
}

// History types
export interface HistoryParams {
  startDate?: string;
  endDate?: string;
  resolution?: 'minute' | 'hour' | 'day';
  includeIncidents?: boolean;
}

export interface HealthHistory {
  providerId: string;
  dataPoints: HealthDataPoint[];
  incidents: Incident[];
  summary: {
    avgUptime: number;
    totalIncidents: number;
    avgRecoveryTime: number;
  };
}

export interface HealthDataPoint {
  timestamp: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: number;
  latency: number;
  errorRate: number;
  throughput: number;
}

// Alert types
export interface AlertParams extends FilterOptions {
  severity?: ('info' | 'warning' | 'critical')[];
  type?: ('connectivity' | 'performance' | 'quota' | 'error_rate')[];
  providerId?: string;
  acknowledged?: boolean;
  resolved?: boolean;
  startDate?: string;
  endDate?: string;
}

export interface HealthAlert {
  id: string;
  providerId: string;
  providerName: string;
  severity: 'info' | 'warning' | 'critical';
  type: 'connectivity' | 'performance' | 'quota' | 'error_rate';
  message: string;
  createdAt: string;
  acknowledgedAt?: string;
  resolvedAt?: string;
}

// Connection test types
export interface ConnectionTestResult {
  success: boolean;
  latency: number;
  statusCode?: number;
  error?: string;
  details: {
    dnsResolution: number;
    tcpConnection: number;
    tlsHandshake: number;
    httpResponse: number;
  };
}

// Performance types
export interface PerformanceParams {
  startDate?: string;
  endDate?: string;
  resolution?: 'minute' | 'hour' | 'day';
}

export interface PerformanceMetrics {
  latency: {
    p50: number;
    p95: number;
    p99: number;
    avg: number;
  };
  throughput: {
    requestsPerMinute: number;
    tokensPerMinute: number;
  };
  availability: {
    uptime: number;
    downtime: number;
    mtbf: number; // mean time between failures
    mttr: number; // mean time to recover
  };
  errors: {
    rate: number;
    types: ErrorTypeCount[];
  };
}

export interface ErrorTypeCount {
  type: string;
  count: number;
  percentage: number;
}

// Incident types
export interface Incident {
  id: string;
  startTime: string;
  endTime?: string;
  severity: 'minor' | 'major' | 'critical';
  type: string;
  description: string;
  impact: string;
  resolution?: string;
}

export interface MaintenanceWindow {
  id: string;
  startTime: string;
  endTime: string;
  description: string;
  impact: 'none' | 'degraded' | 'outage';
}

// List response types
export interface HealthAlertListResponseDto {
  items: HealthAlert[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ProviderHealthListResponseDto {
  items: ProviderHealthStatusDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Re-export types from provider.ts for convenience
export type {
  ProviderHealthConfigurationDto,
  UpdateProviderHealthConfigurationDto,
  ProviderHealthRecordDto,
  ProviderHealthStatusDto,
  ProviderHealthSummaryDto,
  CreateProviderHealthConfigurationDto,
  ProviderHealthStatisticsDto,
  ProviderHealthFilters,
} from './provider';