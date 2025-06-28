/**
 * Health check response from the API
 */
export interface HealthCheckResponse {
  /** Overall health status */
  status: string;
  /** Total duration of all health checks in milliseconds */
  totalDuration: number;
  /** List of individual health check results */
  checks: HealthCheckItem[];
}

/**
 * Individual health check item
 */
export interface HealthCheckItem {
  /** Name of the health check */
  name: string;
  /** Status of this health check */
  status: string;
  /** Description of the health check result */
  description?: string;
  /** Duration of this health check in milliseconds */
  duration: number;
  /** Additional data associated with this health check */
  data?: Record<string, any>;
  /** Exception information if the health check failed */
  exception?: string;
  /** Tags associated with this health check */
  tags?: string[];
}

/**
 * Health check status enumeration
 */
export enum HealthStatus {
  /** The component is healthy */
  Healthy = 0,
  /** The component is degraded but still functioning */
  Degraded = 1,
  /** The component is unhealthy */
  Unhealthy = 2
}

/**
 * Health check options for customizing health check behavior
 */
export interface HealthCheckOptions {
  /** Timeout for health checks */
  timeout?: number;
  /** Whether to include exception details in the response */
  includeExceptionDetails?: boolean;
  /** Tags to filter health checks by */
  tags?: string[];
  /** Failure status to return if checks fail */
  failureStatus?: HealthStatus;
}

/**
 * Simplified health status for quick checks
 */
export interface SimpleHealthStatus {
  /** Whether the system is healthy */
  isHealthy: boolean;
  /** Brief status message */
  message: string;
  /** Timestamp of the health check */
  timestamp: Date;
  /** Response time in milliseconds */
  responseTimeMs: number;
}

/**
 * Health summary with key metrics
 */
export interface HealthSummary {
  /** Overall status */
  overallStatus: string;
  /** Total duration */
  totalDuration: number;
  /** Check counts breakdown */
  checkCounts: {
    total: number;
    healthy: number;
    degraded: number;
    unhealthy: number;
  };
  /** Health percentage */
  healthPercentage: number;
  /** Components summary */
  components: Array<{
    name: string;
    status: string;
    duration: number;
    hasData: boolean;
  }>;
}

/**
 * Options for waiting for health status
 */
export interface WaitForHealthOptions {
  /** Maximum time to wait */
  timeout: number;
  /** Interval between health check polls */
  pollingInterval?: number;
}