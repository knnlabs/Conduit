import { BaseApiClient } from '../client/BaseApiClient';
import { DatabasePoolMetricsResponse, AdminMetricsResponse } from '../models/metrics';

/**
 * Service for accessing Admin API metrics and performance data
 */
export class MetricsService extends BaseApiClient {

  /**
   * Gets database connection pool metrics for the Admin API
   * 
   * @returns Promise<DatabasePoolMetricsResponse> Database pool statistics and health information
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const poolMetrics = await adminClient.metrics.getDatabasePoolMetrics();
   * console.log(`Active connections: ${poolMetrics.metrics.activeConnections}`);
   * console.log(`Pool efficiency: ${poolMetrics.metrics.poolEfficiency}%`);
   * console.log(`Pool is healthy: ${poolMetrics.isHealthy}`);
   * ```
   */
  async getDatabasePoolMetrics(): Promise<DatabasePoolMetricsResponse> {
    const response = await this.get<DatabasePoolMetricsResponse>('/metrics/database/pool');
    return response;
  }

  /**
   * Gets comprehensive Admin API metrics including database, memory, CPU, and request statistics
   * 
   * @returns Promise<AdminMetricsResponse> Complete system metrics and health information
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const systemMetrics = await adminClient.metrics.getAllMetrics();
   * 
   * console.log('Database Pool:');
   * console.log(`  Active connections: ${systemMetrics.metrics.databasePool.activeConnections}`);
   * console.log(`  Pool efficiency: ${systemMetrics.metrics.databasePool.poolEfficiency}%`);
   * 
   * console.log('Memory Usage:');
   * console.log(`  Working set: ${Math.round(systemMetrics.metrics.memory.workingSet / 1024 / 1024)} MB`);
   * console.log(`  GC heap size: ${Math.round(systemMetrics.metrics.memory.gcHeapSize / 1024 / 1024)} MB`);
   * 
   * console.log('CPU Usage:');
   * console.log(`  CPU usage: ${systemMetrics.metrics.cpu.usage}%`);
   * console.log(`  Thread count: ${systemMetrics.metrics.cpu.threadCount}`);
   * 
   * console.log('Request Statistics:');
   * console.log(`  Total requests: ${systemMetrics.metrics.requests.totalRequests}`);
   * console.log(`  Requests per second: ${systemMetrics.metrics.requests.requestsPerSecond}`);
   * console.log(`  Average response time: ${systemMetrics.metrics.requests.averageResponseTime}ms`);
   * console.log(`  Error rate: ${systemMetrics.metrics.requests.errorRate}%`);
   * 
   * console.log(`System is healthy: ${systemMetrics.isHealthy}`);
   * console.log(`Uptime: ${Math.round(systemMetrics.uptime / 1000 / 60)} minutes`);
   * ```
   */
  async getAllMetrics(): Promise<AdminMetricsResponse> {
    const response = await this.get<AdminMetricsResponse>('/metrics');
    return response;
  }

  /**
   * Checks if the database connection pool is healthy
   * 
   * @returns Promise<boolean> True if the pool is healthy, false otherwise
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const isHealthy = await adminClient.metrics.isDatabasePoolHealthy();
   * if (!isHealthy) {
   *   console.warn('Database pool is unhealthy - check connection limits');
   * }
   * ```
   */
  async isDatabasePoolHealthy(): Promise<boolean> {
    try {
      const poolMetrics = await this.getDatabasePoolMetrics();
      return poolMetrics.isHealthy;
    } catch {
      return false;
    }
  }

  /**
   * Checks if the overall Admin API system is healthy
   * 
   * @returns Promise<boolean> True if the system is healthy, false otherwise
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const isHealthy = await adminClient.metrics.isSystemHealthy();
   * if (!isHealthy) {
   *   console.warn('Admin API system is unhealthy - check logs');
   * }
   * ```
   */
  async isSystemHealthy(): Promise<boolean> {
    try {
      const systemMetrics = await this.getAllMetrics();
      return systemMetrics.isHealthy;
    } catch {
      return false;
    }
  }

  /**
   * Gets database connection pool efficiency percentage
   * 
   * @returns Promise<number> Pool efficiency as a percentage (0-100)
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const efficiency = await adminClient.metrics.getDatabasePoolEfficiency();
   * if (efficiency < 80) {
   *   console.warn(`Database pool efficiency is low: ${efficiency}%`);
   * }
   * ```
   */
  async getDatabasePoolEfficiency(): Promise<number> {
    const poolMetrics = await this.getDatabasePoolMetrics();
    return poolMetrics.metrics.poolEfficiency;
  }

  /**
   * Gets current memory usage information
   * 
   * @returns Promise<{workingSetMB: number, gcHeapSizeMB: number, usage: string}> Memory usage summary
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const memoryInfo = await adminClient.metrics.getMemoryUsage();
   * console.log(`Working set: ${memoryInfo.workingSetMB} MB`);
   * console.log(`GC heap: ${memoryInfo.gcHeapSizeMB} MB`);
   * console.log(`Usage summary: ${memoryInfo.usage}`);
   * ```
   */
  async getMemoryUsage(): Promise<{workingSetMB: number, gcHeapSizeMB: number, usage: string}> {
    const systemMetrics = await this.getAllMetrics();
    const memory = systemMetrics.metrics.memory;
    
    const workingSetMB = Math.round(memory.workingSet / 1024 / 1024);
    const gcHeapSizeMB = Math.round(memory.gcHeapSize / 1024 / 1024);
    const totalAllocatedMB = Math.round(memory.totalAllocated / 1024 / 1024);
    
    return {
      workingSetMB,
      gcHeapSizeMB,
      usage: `Working Set: ${workingSetMB} MB, GC Heap: ${gcHeapSizeMB} MB, Total Allocated: ${totalAllocatedMB} MB`
    };
  }

  /**
   * Gets current request processing statistics
   * 
   * @returns Promise<{rps: number, avgResponseTime: number, errorRate: number, activeRequests: number}> Request statistics summary
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const requestStats = await adminClient.metrics.getRequestStatistics();
   * console.log(`Requests per second: ${requestStats.rps}`);
   * console.log(`Average response time: ${requestStats.avgResponseTime}ms`);
   * console.log(`Error rate: ${requestStats.errorRate}%`);
   * console.log(`Active requests: ${requestStats.activeRequests}`);
   * ```
   */
  async getRequestStatistics(): Promise<{rps: number, avgResponseTime: number, errorRate: number, activeRequests: number}> {
    const systemMetrics = await this.getAllMetrics();
    const requests = systemMetrics.metrics.requests;
    
    return {
      rps: requests.requestsPerSecond,
      avgResponseTime: requests.averageResponseTime,
      errorRate: requests.errorRate,
      activeRequests: requests.activeRequests
    };
  }

  /**
   * Gets system uptime information
   * 
   * @returns Promise<{uptimeMs: number, uptimeMinutes: number, uptimeHours: number, uptimeString: string}> Uptime information
   * @throws {ConduitAdminError} When the API request fails
   * 
   * @example
   * ```typescript
   * const uptime = await adminClient.metrics.getSystemUptime();
   * console.log(`System uptime: ${uptime.uptimeString}`);
   * console.log(`Uptime in hours: ${uptime.uptimeHours}`);
   * ```
   */
  async getSystemUptime(): Promise<{uptimeMs: number, uptimeMinutes: number, uptimeHours: number, uptimeString: string}> {
    const systemMetrics = await this.getAllMetrics();
    const uptimeMs = systemMetrics.uptime;
    const uptimeMinutes = Math.floor(uptimeMs / 1000 / 60);
    const uptimeHours = Math.floor(uptimeMinutes / 60);
    const remainingMinutes = uptimeMinutes % 60;
    
    const uptimeString = uptimeHours > 0 
      ? `${uptimeHours}h ${remainingMinutes}m`
      : `${uptimeMinutes}m`;
    
    return {
      uptimeMs,
      uptimeMinutes,
      uptimeHours,
      uptimeString
    };
  }
}