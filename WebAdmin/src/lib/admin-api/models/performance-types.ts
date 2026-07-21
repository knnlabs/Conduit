// Performance Configuration types
export interface PerformanceConfigDto {
  connectionPool: {
    minSize: number;
    maxSize: number;
    acquireTimeoutMs: number;
    idleTimeoutMs: number;
  };
  requestQueue: {
    maxSize: number;
    timeout: number;
    priorityLevels: number;
  };
  circuitBreaker: {
    enabled: boolean;
    failureThreshold: number;
    resetTimeoutMs: number;
    halfOpenRequests: number;
  };
  rateLimiter: {
    enabled: boolean;
    requestsPerSecond: number;
    burstSize: number;
  };
}

export interface UpdatePerformanceConfigDto {
  connectionPool?: Partial<PerformanceConfigDto['connectionPool']>;
  requestQueue?: Partial<PerformanceConfigDto['requestQueue']>;
  circuitBreaker?: Partial<PerformanceConfigDto['circuitBreaker']>;
  rateLimiter?: Partial<PerformanceConfigDto['rateLimiter']>;
}

export interface PerformanceTestParams {
  duration: number;
  concurrentUsers: number;
  requestsPerSecond: number;
  models: string[];
  payloadSize: 'small' | 'medium' | 'large';
}

export interface PerformanceTestResult {
  summary: {
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    avgLatency: number;
    p50Latency: number;
    p95Latency: number;
    p99Latency: number;
    throughput: number;
  };
  timeline: PerformanceDataPoint[];
  errors: ErrorSummary[];
  recommendations: string[];
}

export interface PerformanceDataPoint {
  timestamp: string;
  requestsPerSecond: number;
  avgLatency: number;
  errorRate: number;
  activeConnections: number;
}

export interface ErrorSummary {
  type: string;
  count: number;
  message: string;
  firstOccurred: string;
  lastOccurred: string;
}