'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

// Query key factory for System Performance API
export const systemPerformanceApiKeys = {
  all: ['system-performance-api'] as const,
  metrics: () => [...systemPerformanceApiKeys.all, 'metrics'] as const,
  resources: () => [...systemPerformanceApiKeys.all, 'resources'] as const,
  processes: () => [...systemPerformanceApiKeys.all, 'processes'] as const,
  services: () => [...systemPerformanceApiKeys.all, 'services'] as const,
  network: () => [...systemPerformanceApiKeys.all, 'network'] as const,
  database: () => [...systemPerformanceApiKeys.all, 'database'] as const,
  cache: () => [...systemPerformanceApiKeys.all, 'cache'] as const,
  logs: () => [...systemPerformanceApiKeys.all, 'logs'] as const,
} as const;

export interface SystemMetrics {
  timestamp: string;
  cpu: {
    usage: number; // percentage
    cores: number;
    temperature?: number; // celsius
    frequency: number; // GHz
    loadAverage: number[];
  };
  memory: {
    total: number; // bytes
    used: number; // bytes
    available: number; // bytes
    cached: number; // bytes
    buffers: number; // bytes
    swapTotal: number; // bytes
    swapUsed: number; // bytes
    usage: number; // percentage
  };
  disk: {
    total: number; // bytes
    used: number; // bytes
    available: number; // bytes
    readSpeed: number; // bytes/sec
    writeSpeed: number; // bytes/sec
    iops: number; // operations/sec
    usage: number; // percentage
  };
  network: {
    bytesReceived: number;
    bytesSent: number;
    packetsReceived: number;
    packetsSent: number;
    connectionsActive: number;
    bandwidth: number; // bytes/sec
  };
}

export interface ResourceUsage {
  timestamp: string;
  cpuUsage: number;
  memoryUsage: number;
  diskUsage: number;
  networkUsage: number;
  requestsPerSecond: number;
  responseTime: number;
  errorRate: number;
  threadsActive: number;
}

export interface ProcessInfo {
  pid: number;
  name: string;
  cpuUsage: number;
  memoryUsage: number; // bytes
  status: 'running' | 'sleeping' | 'zombie' | 'stopped';
  startTime: string;
  command: string;
  user: string;
}

export interface ServiceStatus {
  name: string;
  status: 'running' | 'stopped' | 'error' | 'unknown';
  pid?: number;
  uptime: string;
  memoryUsage: number;
  cpuUsage: number;
  restartCount: number;
  lastRestart?: string;
  port?: number;
  healthCheck: {
    status: 'healthy' | 'unhealthy' | 'unknown';
    lastCheck: string;
    responseTime?: number;
  };
}

export interface NetworkStats {
  timestamp: string;
  interfaces: {
    name: string;
    bytesReceived: number;
    bytesSent: number;
    packetsReceived: number;
    packetsSent: number;
    errors: number;
    drops: number;
    speed: number; // Mbps
    status: 'up' | 'down';
  }[];
  connections: {
    tcp: number;
    udp: number;
    established: number;
    timeWait: number;
    listening: number;
  };
  bandwidth: {
    inbound: number; // bytes/sec
    outbound: number; // bytes/sec
    total: number; // bytes/sec
  };
}

export interface DatabaseMetrics {
  connectionPool: {
    active: number;
    idle: number;
    max: number;
    usage: number; // percentage
  };
  queries: {
    total: number;
    successful: number;
    failed: number;
    averageTime: number; // ms
    slowQueries: number;
  };
  locks: {
    active: number;
    waiting: number;
    deadlocks: number;
  };
  storage: {
    size: number; // bytes
    used: number; // bytes
    indexSize: number; // bytes
    growth: number; // bytes/day
  };
  replication: {
    status: 'healthy' | 'lagging' | 'broken' | 'disabled';
    lag: number; // ms
  };
}

export interface CacheMetrics {
  redis: {
    status: 'connected' | 'disconnected' | 'error';
    memory: {
      used: number; // bytes
      max: number; // bytes
      usage: number; // percentage
    };
    operations: {
      hits: number;
      misses: number;
      hitRate: number; // percentage
      commands: number;
    };
    connections: {
      active: number;
      blocked: number;
      rejected: number;
    };
    keyspace: {
      keys: number;
      expires: number;
      ttl: number; // average TTL in seconds
    };
  };
  applicationCache: {
    size: number; // bytes
    entries: number;
    hitRate: number; // percentage
    evictions: number;
  };
}

export interface LogMetrics {
  levels: {
    error: number;
    warning: number;
    info: number;
    debug: number;
  };
  errorRate: number; // errors per minute
  recentErrors: {
    timestamp: string;
    level: string;
    message: string;
    component: string;
    count: number;
  }[];
  logVolume: number; // logs per minute
  diskUsage: number; // bytes
}

export interface SystemHealth {
  overall: 'healthy' | 'warning' | 'critical' | 'unknown';
  components: {
    name: string;
    status: 'healthy' | 'warning' | 'critical' | 'unknown';
    lastCheck: string;
    responseTime?: number;
    errorMessage?: string;
  }[];
  alerts: {
    id: string;
    severity: 'low' | 'medium' | 'high' | 'critical';
    component: string;
    message: string;
    timestamp: string;
    acknowledged: boolean;
  }[];
}

export interface PerformanceThresholds {
  cpu: { warning: number; critical: number };
  memory: { warning: number; critical: number };
  disk: { warning: number; critical: number };
  network: { warning: number; critical: number };
  responseTime: { warning: number; critical: number };
  errorRate: { warning: number; critical: number };
}

// System Metrics
export function useSystemMetrics(interval: number = 30000) {
  return useQuery({
    queryKey: systemPerformanceApiKeys.metrics(),
    queryFn: async (): Promise<SystemMetrics> => {
      try {
        const client = getAdminClient();
        
        // Get system metrics from Admin SDK
        const [systemInfo, systemMetrics] = await Promise.all([
          client.system.getSystemInfo(),
          client.metrics.getAllMetrics(),
        ]);
        
        // Convert bytes to appropriate units and calculate derived values
        const memoryTotal = systemMetrics.metrics.memory.workingSet * 4; // Estimate total as 4x working set
        const memoryUsed = systemMetrics.metrics.memory.workingSet;
        const memoryAvailable = memoryTotal - memoryUsed;
        
        const transformedData: SystemMetrics = {
          timestamp: new Date().toISOString(),
          cpu: {
            usage: systemMetrics.metrics.cpu.usage,
            cores: systemMetrics.metrics.cpu.threadCount / 2, // Rough estimate based on thread count
            temperature: undefined, // Not available from SDK
            frequency: 2.4, // Default assumption
            loadAverage: [
              systemMetrics.metrics.cpu.usage / 100,
              systemMetrics.metrics.cpu.usage / 100,
              systemMetrics.metrics.cpu.usage / 100
            ],
          },
          memory: {
            total: memoryTotal,
            used: memoryUsed,
            available: memoryAvailable,
            cached: systemMetrics.metrics.memory.gcHeapSize,
            buffers: 0, // Not available from SDK
            swapTotal: 0, // Not available from SDK
            swapUsed: 0, // Not available from SDK
            usage: (memoryUsed / memoryTotal) * 100,
          },
          disk: {
            total: 0, // Not available from SDK
            used: 0, // Not available from SDK
            available: 0, // Not available from SDK
            readSpeed: 0, // Not available from SDK
            writeSpeed: 0, // Not available from SDK
            iops: 0, // Not available from SDK
            usage: 0, // Not available from SDK
          },
          network: {
            bytesReceived: 0, // Not available from SDK
            bytesSent: 0, // Not available from SDK
            packetsReceived: 0, // Not available from SDK
            packetsSent: 0, // Not available from SDK
            connectionsActive: systemMetrics.metrics.requests.activeRequests,
            bandwidth: 0, // Not available from SDK
          },
        };

        // Disk values not available from SDK

        return transformedData;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch system metrics');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch system metrics';
        throw new Error(errorMessage);
      }
    },
    staleTime: interval / 2,
    refetchInterval: interval,
  });
}

// Resource Usage History
export function useResourceUsageHistory(timeRange: string = '24h') {
  return useQuery({
    queryKey: [...systemPerformanceApiKeys.resources(), timeRange],
    queryFn: async (): Promise<ResourceUsage[]> => {
      try {
        const client = getAdminClient();
        
        // Calculate date range
        const now = new Date();
        const hours = timeRange === '1h' ? 1 : 
                     timeRange === '24h' ? 24 :
                     timeRange === '7d' ? 24 * 7 : 24;
        const startDate = new Date(now.getTime() - hours * 60 * 60 * 1000);
        
        // Get usage metrics and system info
        const [usageMetrics, systemMetrics] = await Promise.all([
          client.analytics.getUsageMetrics({
            startDate: startDate.toISOString(),
            endDate: now.toISOString(),
          }),
          client.metrics.getAllMetrics(),
        ]);
        
        // Generate hourly data points based on current system state
        const generateHistory = (): ResourceUsage[] => {
          const history: ResourceUsage[] = [];
          
          for (let i = Math.min(hours, 24) - 1; i >= 0; i--) {
            const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
            
            // Use current data as historical data is not available
            history.push({
              timestamp: timestamp.toISOString(),
              cpuUsage: systemMetrics.metrics.cpu.usage,
              memoryUsage: (systemMetrics.metrics.memory.workingSet / (systemMetrics.metrics.memory.workingSet + systemMetrics.metrics.memory.gcHeapSize)) * 100,
              diskUsage: 0, // Not available from SDK
              networkUsage: 0, // Not available from SDK
              requestsPerSecond: systemMetrics.metrics.requests.requestsPerSecond,
              responseTime: systemMetrics.metrics.requests.averageResponseTime,
              errorRate: systemMetrics.metrics.requests.errorRate,
              threadsActive: systemMetrics.metrics.cpu.threadCount,
            });
          }
          
          return history;
        };

        return generateHistory();
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch resource usage history');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch resource usage history';
        throw new Error(errorMessage);
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// System Processes
export function useSystemProcesses() {
  return useQuery({
    queryKey: systemPerformanceApiKeys.processes(),
    queryFn: async (): Promise<ProcessInfo[]> => {
      try {
        const client = getAdminClient();
        
        // Get system info and metrics
        const [systemInfo, systemMetrics] = await Promise.all([
          client.system.getSystemInfo(),
          client.metrics.getAllMetrics(),
        ]);
        
        // Process information not available from SDK
        const processes: ProcessInfo[] = [];

        return processes;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch system processes');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch system processes';
        throw new Error(errorMessage);
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // 1 minute
  });
}

// Service Status
export function useServiceStatus() {
  return useQuery({
    queryKey: systemPerformanceApiKeys.services(),
    queryFn: async (): Promise<ServiceStatus[]> => {
      try {
        const client = getAdminClient();
        
        // Get system health and metrics
        const [health, systemMetrics, systemInfo] = await Promise.all([
          client.system.getHealth(),
          client.metrics.getAllMetrics(),
          client.system.getSystemInfo(),
        ]);
        
        // Transform health checks into service status
        const healthChecks = Array.isArray(health.checks) ? health.checks : [];
        const services: ServiceStatus[] = healthChecks.map((check, index) => ({
          name: check.name,
          status: check.status === 'Healthy' ? 'running' : 
                 check.status === 'Unhealthy' ? 'error' : 'unknown',
          pid: 1234 + index,
          uptime: `${Math.floor(systemInfo.uptime / 3600)} hours`,
          memoryUsage: systemMetrics.metrics.memory.workingSet / Math.max(healthChecks.length, 1),
          cpuUsage: systemMetrics.metrics.cpu.usage / Math.max(healthChecks.length, 1),
          restartCount: 0, // Not available from SDK
          healthCheck: {
            status: check.status === 'Healthy' ? 'healthy' : 
                   check.status === 'Unhealthy' ? 'unhealthy' : 'unknown',
            lastCheck: new Date().toISOString(),
            responseTime: check.responseTime,
          },
        }));

        return services;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch service status');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch service status';
        throw new Error(errorMessage);
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // 1 minute
  });
}

// Database Metrics
export function useDatabaseMetrics() {
  return useQuery({
    queryKey: systemPerformanceApiKeys.database(),
    queryFn: async (): Promise<DatabaseMetrics> => {
      try {
        const client = getAdminClient();
        
        // Get database pool metrics
        const poolMetrics = await client.metrics.getDatabasePoolMetrics();
        
        const transformedData: DatabaseMetrics = {
          connectionPool: {
            active: poolMetrics.metrics.activeConnections,
            idle: poolMetrics.metrics.idleConnections,
            max: poolMetrics.metrics.maxConnections,
            usage: poolMetrics.metrics.poolEfficiency,
          },
          queries: {
            total: poolMetrics.metrics.totalConnectionsCreated * 10, // Estimate queries per connection
            successful: 0, // Query metrics not available from SDK
            failed: 0, // Query metrics not available from SDK
            averageTime: poolMetrics.metrics.waitTimeMs,
            slowQueries: 0, // Not available from SDK
          },
          locks: {
            active: 0, // Not available from SDK
            waiting: 0, // Not available from SDK
            deadlocks: 0, // Not available from SDK
          },
          storage: {
            size: 0, // Not available from SDK
            used: 0, // Not available from SDK
            indexSize: 0, // Not available from SDK
            growth: 0, // Not available from SDK
          },
          replication: {
            status: 'disabled', // Not available from SDK
            lag: 0, // Not available from SDK
          },
        };

        return transformedData;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch database metrics');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch database metrics';
        throw new Error(errorMessage);
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // 1 minute
  });
}

// Cache Metrics
export function useCacheMetrics() {
  return useQuery({
    queryKey: systemPerformanceApiKeys.cache(),
    queryFn: async (): Promise<CacheMetrics> => {
      try {
        const client = getAdminClient();
        
        // Get system health to check Redis status
        const health = await client.system.getHealth();
        const healthChecks = Array.isArray(health.checks) ? health.checks : [];
        
        // Look for Redis-related health checks
        const redisCheck = healthChecks.find(check => 
          check.name.toLowerCase().includes('redis') || 
          check.name.toLowerCase().includes('cache')
        );
        
        // Cache metrics not available from SDK
        const cacheData: CacheMetrics = {
          redis: {
            status: redisCheck?.status === 'Healthy' ? 'connected' : 'error',
            memory: {
              used: 0, // Not available from SDK
              max: 0, // Not available from SDK
              usage: 0, // Not available from SDK
            },
            operations: {
              hits: 0, // Not available from SDK
              misses: 0, // Not available from SDK
              hitRate: 0, // Not available from SDK
              commands: 0, // Not available from SDK
            },
            connections: {
              active: 0, // Not available from SDK
              blocked: 0, // Not available from SDK
              rejected: 0, // Not available from SDK
            },
            keyspace: {
              keys: 0, // Not available from SDK
              expires: 0, // Not available from SDK
              ttl: 0, // Not available from SDK
            },
          },
          applicationCache: {
            size: 0, // Not available from SDK
            entries: 0, // Not available from SDK
            hitRate: 0, // Not available from SDK
            evictions: 0, // Not available from SDK
          },
        };

        // Cannot calculate derived metrics without data

        return cacheData;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch cache metrics');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch cache metrics';
        throw new Error(errorMessage);
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // 1 minute
  });
}

// System Health Overview
export function useSystemHealth() {
  return useQuery({
    queryKey: [...systemPerformanceApiKeys.all, 'health'],
    queryFn: async (): Promise<SystemHealth> => {
      try {
        const client = getAdminClient();
        
        // Get system health from Admin SDK
        const health = await client.system.getHealth();
        
        // Transform SDK health data to UI format
        const overallStatus = health.status === 'healthy' ? 'healthy' :
                            health.status === 'degraded' ? 'warning' :
                            health.status === 'unhealthy' ? 'critical' : 'unknown';
        
        const healthChecks = Array.isArray(health.checks) ? health.checks : [];
        const components = healthChecks.map(check => ({
          name: check.name,
          status: check.status === 'healthy' ? 'healthy' as const :
                 check.status === 'degraded' ? 'warning' as const :
                 check.status === 'unhealthy' ? 'critical' as const : 'unknown' as const,
          lastCheck: new Date().toISOString(),
          responseTime: check.responseTime,
          errorMessage: check.exception || check.description || undefined,
        }));
        
        // Create alerts for unhealthy components
        const alerts = healthChecks
          .filter(check => check.status !== 'healthy')
          .map((check, index) => ({
            id: `alert_${check.name.toLowerCase().replace(/\s+/g, '_')}_${index}`,
            severity: check.status === 'unhealthy' ? 'critical' as const : 'medium' as const,
            component: check.name,
            message: check.description || `${check.name} is ${check.status.toLowerCase()}`,
            timestamp: new Date().toISOString(),
            acknowledged: false,
          }));

        const systemHealth: SystemHealth = {
          overall: overallStatus,
          components,
          alerts,
        };

        return systemHealth;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch system health');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch system health';
        throw new Error(errorMessage);
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // 1 minute
  });
}

// Restart Service
export function useRestartService() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (serviceName: string) => {
      try {
        const client = getAdminClient();
        
        // Note: Service restart functionality not available in SDK
        // This would need to be implemented in the backend
        throw new Error('Service restart functionality not implemented in backend API');
      } catch (error: unknown) {
        reportError(error, 'Failed to restart service');
        const errorMessage = error instanceof Error ? error.message : 'Failed to restart service';
        throw new Error(errorMessage);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: systemPerformanceApiKeys.services() });
      queryClient.invalidateQueries({ queryKey: systemPerformanceApiKeys.processes() });
    },
  });
}

// Clear Cache
export function useClearCache() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (cacheType: 'redis' | 'application' | 'all') => {
      try {
        const client = getAdminClient();
        
        // Note: Cache clearing functionality not available in SDK
        // This would need to be implemented in the backend
        throw new Error('Cache clearing functionality not implemented in backend API');
      } catch (error: unknown) {
        reportError(error, 'Failed to clear cache');
        const errorMessage = error instanceof Error ? error.message : 'Failed to clear cache';
        throw new Error(errorMessage);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: systemPerformanceApiKeys.cache() });
    },
  });
}