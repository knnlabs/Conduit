import { ProviderType } from './providerType';

/**
 * Comprehensive metrics snapshot from the API
 */
export interface MetricsSnapshot {
  /** Timestamp when metrics were collected */
  timestamp: Date;
  /** HTTP performance metrics */
  http: HttpMetrics;
  /** Business metrics including costs and usage */
  business: BusinessMetrics;
  /** System resource metrics */
  system: SystemMetrics;
  /** Infrastructure component metrics */
  infrastructure: InfrastructureMetrics;
  /** Provider health status for all providers */
  providerHealth: ProviderHealthStatus[];
}

/**
 * HTTP performance metrics
 */
export interface HttpMetrics {
  /** Current requests per second */
  requestsPerSecond: number;
  /** Number of active requests */
  activeRequests: number;
  /** Error rate percentage */
  errorRate: number;
  /** Response time statistics */
  responseTimes: ResponseTimeMetrics;
}

/**
 * Response time metrics
 */
export interface ResponseTimeMetrics {
  /** Average response time in milliseconds */
  average: number;
  /** 50th percentile response time */
  p50: number;
  /** 95th percentile response time */
  p95: number;
  /** 99th percentile response time */
  p99: number;
  /** Minimum response time */
  min: number;
  /** Maximum response time */
  max: number;
}

/**
 * Business metrics including costs and usage
 */
export interface BusinessMetrics {
  /** Number of active virtual keys */
  activeVirtualKeys: number;
  /** Total requests per minute across all keys */
  totalRequestsPerMinute: number;
  /** Cost-related metrics */
  cost: CostMetrics;
  /** Model usage statistics */
  modelUsage: ModelUsageStats[];
  /** Virtual key statistics */
  virtualKeyStats: VirtualKeyStats[];
}

/**
 * Cost-related metrics
 */
export interface CostMetrics {
  /** Cost per minute */
  costPerMinute: number;
  /** Average cost per request */
  averageCostPerRequest: number;
  /** Total cost for current period */
  totalCost: number;
}

/**
 * Model usage statistics
 */
export interface ModelUsageStats {
  /** Model name */
  modelName: string;
  /** Requests per minute for this model */
  requestsPerMinute: number;
  /** Total requests for this model */
  totalRequests: number;
  /** Average cost per request */
  averageCost: number;
  /** Error rate for this model */
  errorRate: number;
}

/**
 * Virtual key statistics
 */
export interface VirtualKeyStats {
  /** Virtual key ID */
  keyId: string;
  /** Key name */
  keyName: string;
  /** Current spend amount */
  currentSpend: number;
  /** Budget limit */
  budgetLimit?: number;
  /** Requests per minute */
  requestsPerMinute: number;
  /** Whether the key is enabled */
  isEnabled: boolean;
}

/**
 * System resource metrics
 */
export interface SystemMetrics {
  /** CPU usage percentage */
  cpuUsagePercent: number;
  /** Memory usage in MB */
  memoryUsageMB: number;
  /** Available memory in MB */
  availableMemoryMB: number;
  /** System uptime */
  uptime: string;
  /** Number of CPU cores */
  cpuCores: number;
  /** Thread count */
  threadCount: number;
}

/**
 * Infrastructure component metrics
 */
export interface InfrastructureMetrics {
  /** Database metrics */
  database: DatabaseMetrics;
  /** Redis cache metrics */
  redis: RedisMetrics;
  /** SignalR metrics */
  signalR: SignalRMetrics;
  /** RabbitMQ metrics (if configured) */
  rabbitmq?: RabbitMQMetrics;
}

/**
 * Database connection pool metrics
 */
export interface DatabaseMetrics {
  /** Number of active connections */
  activeConnections: number;
  /** Maximum number of connections */
  maxConnections: number;
  /** Pool utilization percentage */
  poolUtilization: number;
  /** Average connection acquisition time */
  averageConnectionAcquisitionTime: number;
  /** Number of failed connections */
  failedConnections: number;
}

/**
 * Redis cache metrics
 */
export interface RedisMetrics {
  /** Cache hit rate percentage */
  hitRate: number;
  /** Number of cache hits */
  hits: number;
  /** Number of cache misses */
  misses: number;
  /** Memory usage in MB */
  memoryUsageMB: number;
  /** Number of connected clients */
  connectedClients: number;
  /** Operations per second */
  operationsPerSecond: number;
}

/**
 * SignalR metrics
 */
export interface SignalRMetrics {
  /** Number of active connections */
  activeConnections: number;
  /** Messages per second */
  messagesPerSecond: number;
  /** Number of connected groups */
  connectedGroups: number;
  /** Connection errors */
  connectionErrors: number;
}

/**
 * RabbitMQ metrics
 */
export interface RabbitMQMetrics {
  /** Number of messages in queue */
  queueDepth: number;
  /** Messages per second */
  messagesPerSecond: number;
  /** Number of consumers */
  consumers: number;
  /** Memory usage in MB */
  memoryUsageMB: number;
}

/**
 * Provider health status
 */
export interface ProviderHealthStatus {
  /** Provider type */
  providerType: ProviderType;
  /** Whether the provider is healthy */
  isHealthy: boolean;
  /** Last check time */
  lastCheckTime?: Date;
  /** Response time in milliseconds */
  responseTimeMs?: number;
  /** Error message if unhealthy */
  errorMessage?: string;
}

/**
 * Historical metrics request
 */
export interface HistoricalMetricsRequest {
  /** Start time for the query */
  startTime: Date;
  /** End time for the query */
  endTime: Date;
  /** Specific metrics to retrieve */
  metricNames: string[];
  /** Interval for data aggregation */
  interval: string;
}

/**
 * Historical metrics response
 */
export interface HistoricalMetricsResponse {
  /** Time series data */
  series: MetricSeries[];
  /** Query metadata */
  metadata: {
    startTime: Date;
    endTime: Date;
    interval: string;
    totalPoints: number;
  };
}

/**
 * Metric time series data
 */
export interface MetricSeries {
  /** Metric name */
  name: string;
  /** Data points */
  dataPoints: MetricDataPoint[];
  /** Metric metadata */
  metadata: {
    unit: string;
    description: string;
  };
}

/**
 * Individual metric data point
 */
export interface MetricDataPoint {
  /** Timestamp */
  timestamp: Date;
  /** Metric value */
  value: number;
}

/**
 * KPI summary for dashboards
 */
export interface KPISummary {
  /** Timestamp */
  timestamp: Date;
  /** System health metrics */
  systemHealth: {
    overallHealthPercentage: number;
    errorRate: number;
    responseTimeP95: number;
    activeConnections: number;
    databaseUtilization: number;
  };
  /** Performance metrics */
  performance: {
    requestsPerSecond: number;
    activeRequests: number;
    averageResponseTime: number;
    cacheHitRate: number;
  };
  /** Business metrics */
  business: {
    activeVirtualKeys: number;
    requestsPerMinute: number;
    costBurnRatePerHour: number;
    averageCostPerRequest: number;
  };
  /** Infrastructure metrics */
  infrastructure: {
    cpuUsage: number;
    memoryUsage: number;
    uptime: string;
    signalRConnections: number;
  };
}