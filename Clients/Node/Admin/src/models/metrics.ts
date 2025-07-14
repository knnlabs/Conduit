/**
 * Database connection pool statistics
 */
export interface DatabasePoolMetrics {
  /** Current number of active connections */
  activeConnections: number;
  /** Current number of idle connections */
  idleConnections: number;
  /** Total number of connections in the pool */
  totalConnections: number;
  /** Maximum allowed connections */
  maxConnections: number;
  /** Total number of connections created */
  totalConnectionsCreated: number;
  /** Total number of connections closed */
  totalConnectionsClosed: number;
  /** Current wait count for connections */
  waitCount: number;
  /** Current wait time in milliseconds for new connections */
  waitTimeMs: number;
  /** Pool efficiency as a percentage (0-100) */
  poolEfficiency: number;
  /** Time when metrics were collected */
  collectedAt: string;
}

/**
 * System metrics for the Admin API
 */
export interface SystemMetrics {
  /** Database connection pool metrics */
  databasePool: DatabasePoolMetrics;
  /** Memory usage statistics */
  memory: MemoryMetrics;
  /** CPU usage statistics */
  cpu: CpuMetrics;
  /** Garbage collection statistics */
  gc?: GcMetrics;
  /** Request statistics */
  requests: RequestMetrics;
  /** Time when metrics were collected */
  collectedAt: string;
}

/**
 * Memory usage statistics
 */
export interface MemoryMetrics {
  /** Total memory allocated in bytes */
  totalAllocated: number;
  /** Working set memory in bytes */
  workingSet: number;
  /** Private memory in bytes */
  privateMemory: number;
  /** GC heap size in bytes */
  gcHeapSize: number;
  /** Gen 0 heap size in bytes */
  gen0HeapSize: number;
  /** Gen 1 heap size in bytes */
  gen1HeapSize: number;
  /** Gen 2 heap size in bytes */
  gen2HeapSize: number;
  /** Large object heap size in bytes */
  largeObjectHeapSize: number;
}

/**
 * CPU usage statistics
 */
export interface CpuMetrics {
  /** CPU usage percentage (0-100) */
  usage: number;
  /** Total processor time in milliseconds */
  totalProcessorTime: number;
  /** User processor time in milliseconds */
  userProcessorTime: number;
  /** Privileged processor time in milliseconds */
  privilegedProcessorTime: number;
  /** Number of threads */
  threadCount: number;
}

/**
 * Garbage collection statistics
 */
export interface GcMetrics {
  /** Total number of GC collections across all generations */
  totalCollections: number;
  /** Gen 0 collection count */
  gen0Collections: number;
  /** Gen 1 collection count */
  gen1Collections: number;
  /** Gen 2 collection count */
  gen2Collections: number;
  /** Total time spent in GC in milliseconds */
  totalTimeInGc: number;
  /** Total allocated bytes */
  totalAllocatedBytes: number;
  /** Total bytes allocated since last collection */
  allocatedSinceLastGc: number;
}

/**
 * Request statistics
 */
export interface RequestMetrics {
  /** Total number of requests processed */
  totalRequests: number;
  /** Number of requests per second (current rate) */
  requestsPerSecond: number;
  /** Average response time in milliseconds */
  averageResponseTime: number;
  /** Number of failed requests */
  failedRequests: number;
  /** Error rate as a percentage (0-100) */
  errorRate: number;
  /** Number of active requests currently being processed */
  activeRequests: number;
  /** Longest running request duration in milliseconds */
  longestRunningRequest?: number;
}

/**
 * Response containing database pool metrics
 */
export interface DatabasePoolMetricsResponse {
  /** Database connection pool statistics */
  metrics: DatabasePoolMetrics;
  /** Whether the pool is healthy */
  isHealthy: boolean;
  /** Optional health message */
  healthMessage?: string;
}

/**
 * Response containing all Admin API metrics
 */
export interface AdminMetricsResponse {
  /** Comprehensive system metrics */
  metrics: SystemMetrics;
  /** Overall system health status */
  isHealthy: boolean;
  /** Optional health message */
  healthMessage?: string;
  /** API version */
  apiVersion: string;
  /** Service uptime in milliseconds */
  uptime: number;
}

// Issue #434 - Comprehensive Metrics SDK Methods

/**
 * Options for configuring performance metrics retrieval
 */
export interface PerformanceMetricsOptions {
  /** Time range for metrics (e.g., '1h', '24h', '7d', '30d') */
  timeRange?: string;
  /** Start date for custom range (ISO string) */
  startDate?: string;
  /** End date for custom range (ISO string) */
  endDate?: string;
  /** Data resolution (minute, hour, day) */
  resolution?: 'minute' | 'hour' | 'day';
  /** Include detailed breakdown by provider */
  includeProviderBreakdown?: boolean;
  /** Include model-specific metrics */
  includeModelBreakdown?: boolean;
}

/**
 * System-wide metrics response
 */
export interface SystemMetricsResponse {
  /** System performance metrics */
  system: {
    cpu: CpuMetrics;
    memory: MemoryMetrics;
    gc?: GcMetrics;
    uptime: number;
    startTime: string;
  };
  /** Request processing metrics */
  requests: RequestMetrics & {
    /** Request distribution by HTTP method */
    byMethod: Record<string, number>;
    /** Request distribution by status code */
    byStatusCode: Record<string, number>;
    /** Top endpoints by request count */
    topEndpoints: Array<{
      path: string;
      count: number;
      avgResponseTime: number;
    }>;
  };
  /** Database performance metrics */
  database: DatabasePoolMetrics & {
    /** Query performance metrics */
    queryMetrics: {
      averageQueryTime: number;
      slowestQuery: number;
      totalQueries: number;
      failedQueries: number;
    };
  };
  /** Cache performance metrics */
  cache: {
    hitRate: number;
    missRate: number;
    totalHits: number;
    totalMisses: number;
    evictions: number;
    size: number;
    maxSize: number;
  };
  /** Collection timestamp */
  timestamp: string;
  /** Time range for aggregated data */
  timeRange: string;
}

/**
 * Performance metrics with time-series data
 */
export interface PerformanceMetricsResponse {
  /** Time-series data points */
  timeSeries: Array<{
    timestamp: string;
    /** Request rate (requests per minute) */
    requestRate: number;
    /** Average response time in milliseconds */
    responseTime: number;
    /** Error rate percentage */
    errorRate: number;
    /** CPU usage percentage */
    cpuUsage: number;
    /** Memory usage in bytes */
    memoryUsage: number;
    /** Active connections */
    activeConnections: number;
    /** Throughput (requests per second) */
    throughput: number;
  }>;
  /** Performance summary */
  summary: {
    /** Average metrics over the time period */
    averages: {
      requestRate: number;
      responseTime: number;
      errorRate: number;
      cpuUsage: number;
      memoryUsage: number;
      throughput: number;
    };
    /** Peak values during the time period */
    peaks: {
      requestRate: number;
      responseTime: number;
      errorRate: number;
      cpuUsage: number;
      memoryUsage: number;
      throughput: number;
    };
    /** Overall trend (improving, degrading, stable) */
    trend: 'improving' | 'degrading' | 'stable';
  };
  /** Provider breakdown if requested */
  providerBreakdown?: Record<string, {
    requestCount: number;
    avgResponseTime: number;
    errorRate: number;
    throughput: number;
  }>;
  /** Model breakdown if requested */
  modelBreakdown?: Record<string, {
    requestCount: number;
    avgResponseTime: number;
    errorRate: number;
    tokenThroughput: number;
  }>;
  /** Data collection period */
  period: {
    startTime: string;
    endTime: string;
    resolution: string;
    totalDataPoints: number;
  };
}

/**
 * Provider-specific metrics response
 */
export interface ProviderMetricsResponse {
  /** Metrics for each provider */
  providers: Record<string, {
    /** Provider identification */
    id: string;
    name: string;
    isEnabled: boolean;
    /** Usage statistics */
    usage: {
      totalRequests: number;
      successfulRequests: number;
      failedRequests: number;
      successRate: number;
      requestsPerHour: number;
    };
    /** Performance metrics */
    performance: {
      avgResponseTime: number;
      p50ResponseTime: number;
      p95ResponseTime: number;
      p99ResponseTime: number;
      minResponseTime: number;
      maxResponseTime: number;
    };
    /** Error analysis */
    errors: {
      totalErrors: number;
      errorRate: number;
      errorsByType: Record<string, number>;
      recentErrors: Array<{
        timestamp: string;
        type: string;
        message: string;
        count: number;
      }>;
    };
    /** Token and quota usage */
    quotas: {
      tokensUsed: number;
      requestsUsed: number;
      quotaUtilization: number;
      rateLimitHits: number;
      costEstimate: number;
    };
    /** Health and availability */
    health: {
      status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
      uptime: number;
      lastChecked: string;
      consecutiveFailures: number;
      lastSuccessfulRequest: string;
    };
  }>;
  /** Provider comparison metrics */
  comparison: {
    /** Fastest provider by average response time */
    fastest: { providerId: string; responseTime: number };
    /** Most reliable provider by success rate */
    mostReliable: { providerId: string; successRate: number };
    /** Most used provider by request count */
    mostUsed: { providerId: string; requestCount: number };
    /** Best value provider by cost efficiency */
    bestValue: { providerId: string; costEfficiency: number };
  };
  /** Aggregated totals */
  totals: {
    totalRequests: number;
    totalErrors: number;
    totalTokens: number;
    totalCost: number;
    overallSuccessRate: number;
    avgResponseTime: number;
  };
  /** Collection timestamp */
  timestamp: string;
  /** Time range for data */
  timeRange: string;
}

/**
 * Model performance metrics response
 */
export interface ModelMetricsResponse {
  /** Metrics for each model across all providers */
  models: Record<string, {
    /** Model identification */
    name: string;
    providers: string[];
    category: 'text' | 'chat' | 'embedding' | 'image' | 'audio' | 'multimodal';
    /** Usage statistics */
    usage: {
      totalRequests: number;
      totalTokensGenerated: number;
      totalTokensConsumed: number;
      avgRequestSize: number;
      avgResponseSize: number;
      requestsPerHour: number;
    };
    /** Performance metrics */
    performance: {
      avgResponseTime: number;
      tokensPerSecond: number;
      avgLatency: number;
      throughput: number;
      efficiency: number;
    };
    /** Quality and reliability */
    reliability: {
      successRate: number;
      errorRate: number;
      timeoutRate: number;
      retryRate: number;
      availability: number;
    };
    /** Cost analysis */
    costs: {
      totalCost: number;
      costPerRequest: number;
      costPerToken: number;
      costTrend: 'increasing' | 'decreasing' | 'stable';
    };
    /** Provider comparison for this model */
    providerComparison: Record<string, {
      responseTime: number;
      successRate: number;
      costPerToken: number;
      availability: number;
    }>;
  }>;
  /** Model performance rankings */
  rankings: {
    /** Fastest models by tokens per second */
    bySpeed: Array<{ model: string; tokensPerSecond: number }>;
    /** Most reliable models by success rate */
    byReliability: Array<{ model: string; successRate: number }>;
    /** Most popular models by request count */
    byPopularity: Array<{ model: string; requestCount: number }>;
    /** Most cost-effective models */
    byCostEfficiency: Array<{ model: string; efficiency: number }>;
  };
  /** Category summaries */
  categories: Record<string, {
    totalModels: number;
    totalRequests: number;
    avgResponseTime: number;
    avgSuccessRate: number;
    totalCost: number;
  }>;
  /** Collection timestamp */
  timestamp: string;
}

/**
 * Error metrics and analysis response
 */
export interface ErrorMetricsResponse {
  /** Error statistics overview */
  overview: {
    totalErrors: number;
    errorRate: number;
    errorTrend: 'increasing' | 'decreasing' | 'stable';
    mostCommonError: string;
    errorFrequency: number; // errors per hour
  };
  /** Error breakdown by type */
  byType: Record<string, {
    count: number;
    percentage: number;
    trend: 'increasing' | 'decreasing' | 'stable';
    avgResolutionTime: number;
    recentOccurrences: Array<{
      timestamp: string;
      message: string;
      context?: Record<string, any>;
    }>;
  }>;
  /** Error breakdown by provider */
  byProvider: Record<string, {
    totalErrors: number;
    errorRate: number;
    topErrors: Array<{
      type: string;
      count: number;
      lastOccurrence: string;
    }>;
  }>;
  /** Error breakdown by HTTP status code */
  byStatusCode: Record<string, {
    count: number;
    percentage: number;
    description: string;
  }>;
  /** Error patterns and analysis */
  patterns: {
    /** Time-based error distribution */
    timeDistribution: Array<{
      hour: number;
      errorCount: number;
      errorRate: number;
    }>;
    /** Correlation with system load */
    loadCorrelation: {
      highLoadErrors: number;
      lowLoadErrors: number;
      correlationStrength: number;
    };
    /** Error cascades and dependencies */
    cascades: Array<{
      triggerError: string;
      cascadeErrors: string[];
      frequency: number;
    }>;
  };
  /** Error resolution metrics */
  resolution: {
    avgResolutionTime: number;
    automatedResolution: number;
    manualResolution: number;
    unresolved: number;
    escalated: number;
  };
  /** Recent critical errors */
  criticalErrors: Array<{
    id: string;
    timestamp: string;
    type: string;
    severity: 'low' | 'medium' | 'high' | 'critical';
    message: string;
    provider?: string;
    model?: string;
    context?: Record<string, any>;
    resolved: boolean;
    resolutionTime?: number;
  }>;
  /** Collection timestamp */
  timestamp: string;
  /** Time range for analysis */
  timeRange: string;
}