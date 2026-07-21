import type { ConfigValue, RouterActionParameters, ExtendedMetadata } from './common-types';

// Extended Routing types from issue #380
export interface RoutingConfigDto {
  defaultStrategy: 'round_robin' | 'least_latency' | 'cost_optimized' | 'priority';
  fallbackEnabled: boolean;
  retryPolicy: RetryPolicy;
  timeoutMs: number;
  maxConcurrentRequests: number;
}

export interface RetryPolicy {
  maxAttempts: number;
  initialDelayMs: number;
  maxDelayMs: number;
  backoffMultiplier: number;
  retryableStatuses: number[];
}

export interface UpdateRoutingConfigDto {
  defaultStrategy?: 'round_robin' | 'least_latency' | 'cost_optimized' | 'priority';
  fallbackEnabled?: boolean;
  retryPolicy?: Partial<RetryPolicy>;
  timeoutMs?: number;
  maxConcurrentRequests?: number;
}

export interface RoutingRule {
  id: string;
  name: string;
  priority: number;
  conditions: RuleCondition[];
  actions: RuleAction[];
  enabled: boolean;
  stats?: {
    matchCount: number;
    lastMatched?: string;
  };
}

export interface RuleCondition {
  type: 'model' | 'header' | 'body' | 'time' | 'load';
  field?: string;
  operator: 'equals' | 'contains' | 'regex' | 'gt' | 'lt' | 'between';
  value: ConfigValue;
}

export interface RuleAction {
  type: 'route' | 'transform' | 'cache' | 'rate_limit' | 'log';
  target?: string;
  parameters?: RouterActionParameters;
}

export interface CreateRoutingRuleDto {
  name: string;
  priority?: number;
  conditions: RuleCondition[];
  actions: RuleAction[];
  enabled?: boolean;
}

export interface UpdateRoutingRuleDto {
  name?: string;
  priority?: number;
  conditions?: RuleCondition[];
  actions?: RuleAction[];
  enabled?: boolean;
}

// Issue #437 - Routing Health and Configuration SDK Methods

/**
 * Routing health status information
 */
export interface RoutingHealthStatus {
  /** Overall routing system health */
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  /** Last health check timestamp */
  lastChecked: string;
  /** Total number of active routes */
  totalRoutes: number;
  /** Number of healthy routes */
  healthyRoutes: number;
  /** Number of degraded routes */
  degradedRoutes: number;
  /** Number of failed routes */
  failedRoutes: number;
  /** Load balancer status */
  loadBalancer: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    activeNodes: number;
    totalNodes: number;
    avgResponseTime: number;
  };
  /** Circuit breaker status */
  circuitBreakers: {
    totalBreakers: number;
    openBreakers: number;
    halfOpenBreakers: number;
    closedBreakers: number;
  };
  /** Overall performance metrics */
  performance: {
    avgLatency: number;
    p95Latency: number;
    requestsPerSecond: number;
    errorRate: number;
    successRate: number;
  };
}

/**
 * Individual route health information
 */
export interface RouteHealthDetails {
  /** Route identifier */
  routeId: string;
  /** Route name or description */
  routeName: string;
  /** Route pattern or path */
  pattern: string;
  /** Current health status */
  status: 'healthy' | 'degraded' | 'unhealthy' | 'disabled';
  /** Target provider or endpoint */
  target: string;
  /** Health check results */
  healthCheck: {
    status: 'passing' | 'failing' | 'warning';
    lastCheck: string;
    responseTime: number;
    statusCode?: number;
    errorMessage?: string;
  };
  /** Performance metrics */
  metrics: {
    requestCount: number;
    successCount: number;
    errorCount: number;
    avgResponseTime: number;
    p95ResponseTime: number;
    throughput: number;
  };
  /** Circuit breaker status */
  circuitBreaker: {
    state: 'closed' | 'open' | 'half-open';
    failureCount: number;
    successCount: number;
    lastStateChange: string;
    nextRetryAttempt?: string;
  };
  /** Configuration details */
  configuration: {
    enabled: boolean;
    weight: number;
    timeout: number;
    retryPolicy: {
      maxRetries: number;
      backoffMultiplier: number;
      maxBackoffMs: number;
    };
  };
}

/**
 * Routing health history data point
 */
export interface RoutingHealthDataPoint {
  timestamp: string;
  overallStatus: 'healthy' | 'degraded' | 'unhealthy';
  healthyRoutes: number;
  totalRoutes: number;
  avgLatency: number;
  requestsPerSecond: number;
  errorRate: number;
  activeCircuitBreakers: number;
}

/**
 * Routing health history response
 */
export interface RoutingHealthHistory {
  /** Time-series health data */
  dataPoints: RoutingHealthDataPoint[];
  /** Summary statistics */
  summary: {
    timeRange: string;
    avgHealthyPercentage: number;
    maxLatency: number;
    minLatency: number;
    avgLatency: number;
    totalRequests: number;
    totalErrors: number;
    uptimePercentage: number;
  };
  /** Significant incidents during the period */
  incidents: Array<{
    id: string;
    timestamp: string;
    type: 'outage' | 'degradation' | 'circuit_breaker' | 'configuration';
    affectedRoutes: string[];
    duration: number;
    resolved: boolean;
    description: string;
  }>;
}

/**
 * Options for routing health monitoring
 */
export interface RoutingHealthOptions {
  /** Include detailed route information */
  includeRouteDetails?: boolean;
  /** Include historical data */
  includeHistory?: boolean;
  /** Historical data time range */
  historyTimeRange?: '1h' | '24h' | '7d' | '30d';
  /** Data resolution for history */
  historyResolution?: 'minute' | 'hour' | 'day';
  /** Include performance metrics */
  includePerformanceMetrics?: boolean;
  /** Include circuit breaker status */
  includeCircuitBreakers?: boolean;
}

/**
 * Comprehensive routing health response
 */
export interface RoutingHealthResponse {
  /** Overall health status */
  health: RoutingHealthStatus;
  /** Individual route details */
  routes: RouteHealthDetails[];
  /** Historical health data */
  history?: RoutingHealthHistory;
  /** Real-time subscription information */
  subscription?: {
    endpoint: string;
    connectionId: string;
    events: string[];
  };
}

/**
 * Route performance test parameters
 */
export interface RoutePerformanceTestParams {
  /** Routes to test (empty for all) */
  routeIds?: string[];
  /** Test duration in seconds */
  duration: number;
  /** Concurrent requests per route */
  concurrency: number;
  /** Request rate per second */
  requestRate: number;
  /** Test payload configuration */
  payload?: {
    size: number;
    complexity: 'simple' | 'medium' | 'complex';
    customData?: ExtendedMetadata;
  };
  /** Performance thresholds */
  thresholds?: {
    maxLatency: number;
    maxErrorRate: number;
    minThroughput: number;
  };
}

/**
 * Route performance test results
 */
export interface RoutePerformanceTestResult {
  /** Test execution details */
  testInfo: {
    testId: string;
    startTime: string;
    endTime: string;
    duration: number;
    params: RoutePerformanceTestParams;
  };
  /** Overall test results */
  summary: {
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    avgLatency: number;
    p50Latency: number;
    p95Latency: number;
    p99Latency: number;
    maxLatency: number;
    minLatency: number;
    throughput: number;
    errorRate: number;
    thresholdsPassed: boolean;
  };
  /** Per-route results */
  routeResults: Array<{
    routeId: string;
    routeName: string;
    requests: number;
    successes: number;
    failures: number;
    avgLatency: number;
    p95Latency: number;
    throughput: number;
    errorRate: number;
    thresholdsPassed: boolean;
    errors: Array<{
      type: string;
      count: number;
      percentage: number;
      lastOccurrence: string;
    }>;
  }>;
  /** Timeline data */
  timeline: Array<{
    timestamp: string;
    requestsPerSecond: number;
    avgLatency: number;
    errorRate: number;
    activeRoutes: number;
  }>;
  /** Recommendations */
  recommendations: string[];
}

/**
 * Real-time routing event
 */
export interface RoutingHealthEvent {
  /** Event identifier */
  id: string;
  /** Event timestamp */
  timestamp: string;
  /** Event type */
  type: 'route_health_change' | 'circuit_breaker_state_change' | 'load_balancer_event' | 'performance_alert';
  /** Event severity */
  severity: 'info' | 'warning' | 'critical';
  /** Affected route or component */
  target: {
    type: 'route' | 'load_balancer' | 'circuit_breaker' | 'system';
    id: string;
    name: string;
  };
  /** Event details */
  details: {
    previousState?: string;
    currentState: string;
    metrics?: Record<string, number>;
    message: string;
    causedBy?: string;
  };
  /** Event context */
  context?: {
    requestId?: string;
    userId?: string;
    clientIp?: string;
    userAgent?: string;
  };
}

/**
 * Circuit breaker configuration
 */
export interface CircuitBreakerConfig {
  /** Circuit breaker identifier */
  id: string;
  /** Associated route ID */
  routeId: string;
  /** Failure threshold to open circuit */
  failureThreshold: number;
  /** Success threshold to close circuit */
  successThreshold: number;
  /** Timeout before attempting half-open */
  timeout: number;
  /** Sliding window size for failure tracking */
  slidingWindowSize: number;
  /** Minimum number of calls before evaluation */
  minimumNumberOfCalls: number;
  /** Slow call duration threshold */
  slowCallDurationThreshold: number;
  /** Slow call rate threshold */
  slowCallRateThreshold: number;
  /** Whether circuit breaker is enabled */
  enabled: boolean;
}

/**
 * Circuit breaker status
 */
export interface CircuitBreakerStatus {
  /** Circuit breaker configuration */
  config: CircuitBreakerConfig;
  /** Current state */
  state: 'closed' | 'open' | 'half-open' | 'disabled';
  /** Failure metrics */
  metrics: {
    failureRate: number;
    slowCallRate: number;
    numberOfCalls: number;
    numberOfFailedCalls: number;
    numberOfSlowCalls: number;
    numberOfSuccessfulCalls: number;
  };
  /** State transitions */
  stateTransitions: Array<{
    timestamp: string;
    fromState: string;
    toState: string;
    reason: string;
  }>;
  /** Last state change */
  lastStateChange: string;
  /** Next retry attempt (for open state) */
  nextRetryAttempt?: string;
}