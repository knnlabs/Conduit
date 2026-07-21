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
