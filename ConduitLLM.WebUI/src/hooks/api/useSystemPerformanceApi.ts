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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.getMetrics();
        
        // Mock system metrics data
        const mockData: SystemMetrics = {
          timestamp: new Date().toISOString(),
          cpu: {
            usage: 45.2 + Math.random() * 30, // 45-75%
            cores: 8,
            temperature: 55 + Math.random() * 15, // 55-70°C
            frequency: 2.4 + Math.random() * 0.8, // 2.4-3.2 GHz
            loadAverage: [
              1.2 + Math.random() * 2,
              1.5 + Math.random() * 2,
              1.8 + Math.random() * 2
            ],
          },
          memory: {
            total: 16 * 1024 * 1024 * 1024, // 16GB
            used: 8 * 1024 * 1024 * 1024 + Math.random() * 4 * 1024 * 1024 * 1024, // 8-12GB
            available: 0,
            cached: 2 * 1024 * 1024 * 1024,
            buffers: 512 * 1024 * 1024,
            swapTotal: 4 * 1024 * 1024 * 1024,
            swapUsed: Math.random() * 1024 * 1024 * 1024,
            usage: 0,
          },
          disk: {
            total: 500 * 1024 * 1024 * 1024, // 500GB
            used: 200 * 1024 * 1024 * 1024 + Math.random() * 100 * 1024 * 1024 * 1024, // 200-300GB
            available: 0,
            readSpeed: 100 * 1024 * 1024 + Math.random() * 50 * 1024 * 1024, // 100-150 MB/s
            writeSpeed: 80 * 1024 * 1024 + Math.random() * 40 * 1024 * 1024, // 80-120 MB/s
            iops: 500 + Math.random() * 1000, // 500-1500 IOPS
            usage: 0,
          },
          network: {
            bytesReceived: Math.random() * 100 * 1024 * 1024,
            bytesSent: Math.random() * 200 * 1024 * 1024,
            packetsReceived: Math.floor(Math.random() * 10000),
            packetsSent: Math.floor(Math.random() * 15000),
            connectionsActive: Math.floor(50 + Math.random() * 200),
            bandwidth: 1000 * 1024 * 1024, // 1 Gbps
          },
        };

        // Calculate derived values
        mockData.memory.available = mockData.memory.total - mockData.memory.used;
        mockData.memory.usage = (mockData.memory.used / mockData.memory.total) * 100;
        mockData.disk.available = mockData.disk.total - mockData.disk.used;
        mockData.disk.usage = (mockData.disk.used / mockData.disk.total) * 100;

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch system metrics');
        throw new Error(error?.message || 'Failed to fetch system metrics');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.getResourceHistory(timeRange);
        
        // Generate mock resource usage history
        const generateHistory = (hours: number): ResourceUsage[] => {
          const history: ResourceUsage[] = [];
          const now = new Date();
          
          for (let i = hours - 1; i >= 0; i--) {
            const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
            
            history.push({
              timestamp: timestamp.toISOString(),
              cpuUsage: 30 + Math.random() * 40, // 30-70%
              memoryUsage: 50 + Math.random() * 30, // 50-80%
              diskUsage: 60 + Math.random() * 20, // 60-80%
              networkUsage: 20 + Math.random() * 60, // 20-80%
              requestsPerSecond: 100 + Math.random() * 500, // 100-600 RPS
              responseTime: 200 + Math.random() * 300, // 200-500ms
              errorRate: Math.random() * 5, // 0-5%
              threadsActive: 50 + Math.floor(Math.random() * 100), // 50-150 threads
            });
          }
          
          return history;
        };

        const hours = timeRange === '1h' ? 1 : 
                     timeRange === '24h' ? 24 :
                     timeRange === '7d' ? 24 * 7 : 24;

        return generateHistory(hours);
      } catch (error: any) {
        reportError(error, 'Failed to fetch resource usage history');
        throw new Error(error?.message || 'Failed to fetch resource usage history');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.getProcesses();
        
        // Mock process data
        const mockProcesses: ProcessInfo[] = [
          {
            pid: 1234,
            name: 'conduit-api',
            cpuUsage: 15.2,
            memoryUsage: 512 * 1024 * 1024, // 512MB
            status: 'running',
            startTime: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(),
            command: 'dotnet ConduitLLM.Http.dll',
            user: 'conduit',
          },
          {
            pid: 1235,
            name: 'conduit-admin',
            cpuUsage: 8.7,
            memoryUsage: 256 * 1024 * 1024, // 256MB
            status: 'running',
            startTime: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(),
            command: 'dotnet ConduitLLM.Admin.dll',
            user: 'conduit',
          },
          {
            pid: 1236,
            name: 'conduit-webui',
            cpuUsage: 3.2,
            memoryUsage: 128 * 1024 * 1024, // 128MB
            status: 'running',
            startTime: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(),
            command: 'node server.js',
            user: 'conduit',
          },
          {
            pid: 1237,
            name: 'postgres',
            cpuUsage: 12.4,
            memoryUsage: 1024 * 1024 * 1024, // 1GB
            status: 'running',
            startTime: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7).toISOString(),
            command: 'postgres: conduit_db',
            user: 'postgres',
          },
          {
            pid: 1238,
            name: 'redis-server',
            cpuUsage: 4.1,
            memoryUsage: 256 * 1024 * 1024, // 256MB
            status: 'running',
            startTime: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7).toISOString(),
            command: 'redis-server *:6379',
            user: 'redis',
          },
        ];

        return mockProcesses;
      } catch (error: any) {
        reportError(error, 'Failed to fetch system processes');
        throw new Error(error?.message || 'Failed to fetch system processes');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.getServices();
        
        // Mock service status data
        const mockServices: ServiceStatus[] = [
          {
            name: 'Conduit API',
            status: 'running',
            pid: 1234,
            uptime: '2 days, 14 hours',
            memoryUsage: 512 * 1024 * 1024,
            cpuUsage: 15.2,
            restartCount: 2,
            lastRestart: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(),
            port: 8080,
            healthCheck: {
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 45,
            },
          },
          {
            name: 'Conduit Admin API',
            status: 'running',
            pid: 1235,
            uptime: '2 days, 14 hours',
            memoryUsage: 256 * 1024 * 1024,
            cpuUsage: 8.7,
            restartCount: 1,
            lastRestart: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(),
            port: 8081,
            healthCheck: {
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 32,
            },
          },
          {
            name: 'Conduit WebUI',
            status: 'running',
            pid: 1236,
            uptime: '1 day, 8 hours',
            memoryUsage: 128 * 1024 * 1024,
            cpuUsage: 3.2,
            restartCount: 0,
            port: 3000,
            healthCheck: {
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 18,
            },
          },
          {
            name: 'PostgreSQL',
            status: 'running',
            pid: 1237,
            uptime: '7 days, 3 hours',
            memoryUsage: 1024 * 1024 * 1024,
            cpuUsage: 12.4,
            restartCount: 0,
            port: 5432,
            healthCheck: {
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 8,
            },
          },
          {
            name: 'Redis',
            status: 'running',
            pid: 1238,
            uptime: '7 days, 3 hours',
            memoryUsage: 256 * 1024 * 1024,
            cpuUsage: 4.1,
            restartCount: 0,
            port: 6379,
            healthCheck: {
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 2,
            },
          },
        ];

        return mockServices;
      } catch (error: any) {
        reportError(error, 'Failed to fetch service status');
        throw new Error(error?.message || 'Failed to fetch service status');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.getDatabaseMetrics();
        
        // Mock database metrics
        const mockData: DatabaseMetrics = {
          connectionPool: {
            active: 15 + Math.floor(Math.random() * 10),
            idle: 35 + Math.floor(Math.random() * 15),
            max: 100,
            usage: 0,
          },
          queries: {
            total: 125678 + Math.floor(Math.random() * 10000),
            successful: 124456 + Math.floor(Math.random() * 9000),
            failed: 234 + Math.floor(Math.random() * 100),
            averageTime: 15 + Math.random() * 25, // 15-40ms
            slowQueries: 12 + Math.floor(Math.random() * 8),
          },
          locks: {
            active: Math.floor(Math.random() * 5),
            waiting: Math.floor(Math.random() * 3),
            deadlocks: Math.floor(Math.random() * 2),
          },
          storage: {
            size: 5 * 1024 * 1024 * 1024, // 5GB
            used: 3.2 * 1024 * 1024 * 1024, // 3.2GB
            indexSize: 800 * 1024 * 1024, // 800MB
            growth: 50 * 1024 * 1024, // 50MB/day
          },
          replication: {
            status: 'healthy',
            lag: 5 + Math.random() * 10, // 5-15ms
          },
        };

        mockData.connectionPool.usage = ((mockData.connectionPool.active + mockData.connectionPool.idle) / mockData.connectionPool.max) * 100;

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch database metrics');
        throw new Error(error?.message || 'Failed to fetch database metrics');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.getCacheMetrics();
        
        // Mock cache metrics
        const mockData: CacheMetrics = {
          redis: {
            status: 'connected',
            memory: {
              used: 180 * 1024 * 1024, // 180MB
              max: 256 * 1024 * 1024, // 256MB
              usage: 0,
            },
            operations: {
              hits: 45678 + Math.floor(Math.random() * 1000),
              misses: 2345 + Math.floor(Math.random() * 100),
              hitRate: 0,
              commands: 48023 + Math.floor(Math.random() * 1100),
            },
            connections: {
              active: 25 + Math.floor(Math.random() * 10),
              blocked: Math.floor(Math.random() * 3),
              rejected: Math.floor(Math.random() * 2),
            },
            keyspace: {
              keys: 12456 + Math.floor(Math.random() * 1000),
              expires: 8934 + Math.floor(Math.random() * 500),
              ttl: 3600 + Math.random() * 7200, // 1-3 hours
            },
          },
          applicationCache: {
            size: 64 * 1024 * 1024, // 64MB
            entries: 5678 + Math.floor(Math.random() * 500),
            hitRate: 85 + Math.random() * 10, // 85-95%
            evictions: 123 + Math.floor(Math.random() * 50),
          },
        };

        mockData.redis.memory.usage = (mockData.redis.memory.used / mockData.redis.memory.max) * 100;
        mockData.redis.operations.hitRate = (mockData.redis.operations.hits / (mockData.redis.operations.hits + mockData.redis.operations.misses)) * 100;

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch cache metrics');
        throw new Error(error?.message || 'Failed to fetch cache metrics');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.getHealth();
        
        // Mock system health data
        const mockData: SystemHealth = {
          overall: 'healthy',
          components: [
            {
              name: 'API Server',
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 45,
            },
            {
              name: 'Database',
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 8,
            },
            {
              name: 'Cache',
              status: 'healthy',
              lastCheck: new Date().toISOString(),
              responseTime: 2,
            },
            {
              name: 'File System',
              status: 'warning',
              lastCheck: new Date().toISOString(),
              errorMessage: 'Disk usage is at 78%',
            },
            {
              name: 'Network',
              status: 'healthy',
              lastCheck: new Date().toISOString(),
            },
          ],
          alerts: [
            {
              id: 'alert_disk_001',
              severity: 'medium',
              component: 'File System',
              message: 'Disk usage is approaching threshold (78% used)',
              timestamp: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
              acknowledged: false,
            },
          ],
        };

        return mockData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch system health');
        throw new Error(error?.message || 'Failed to fetch system health');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.restartService(serviceName);
        
        // Simulate service restart
        await new Promise(resolve => setTimeout(resolve, 3000));
        
        return { success: true, serviceName };
      } catch (error: any) {
        reportError(error, 'Failed to restart service');
        throw new Error(error?.message || 'Failed to restart service');
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
        
        // TODO: Replace with actual API endpoint when available
        // const response = await client.system.clearCache(cacheType);
        
        // Simulate cache clearing
        await new Promise(resolve => setTimeout(resolve, 1000));
        
        return { success: true, cacheType };
      } catch (error: any) {
        reportError(error, 'Failed to clear cache');
        throw new Error(error?.message || 'Failed to clear cache');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: systemPerformanceApiKeys.cache() });
    },
  });
}