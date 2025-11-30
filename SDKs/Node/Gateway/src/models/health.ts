/**
 * Health check types for the Core API
 */

export interface HealthCheckResponse {
  status: 'healthy' | 'degraded' | 'unhealthy';
  checks?: HealthCheckItem[];
  lastChecked: string;
  totalDuration: number;
  message?: string;
}

export interface HealthCheckItem {
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  duration: number;
  data?: Record<string, unknown>;
  description?: string;
  exception?: string;
  tags?: string[];
}

export interface HealthSummary {
  status: 'healthy' | 'degraded' | 'unhealthy';
  totalChecks: number;
  healthyChecks: number;
  degradedChecks: number;
  unhealthyChecks: number;
  totalDuration: number;
}

export type HealthStatus = 'healthy' | 'degraded' | 'unhealthy';

export interface HealthCheckOptions {
  tags?: string[];
  timeout?: number;
  includeDetails?: boolean;
}

export interface SimpleHealthStatus {
  status: HealthStatus;
}

export interface WaitForHealthOptions {
  timeout?: number;
  interval?: number;
  requiredStatus?: HealthStatus;
  maxRetries?: number;
}