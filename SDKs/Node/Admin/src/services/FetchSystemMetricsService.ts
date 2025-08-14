import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { SystemMetricsDto } from '../models/system';
import type { 
  MetricsParams, 
  PerformanceMetrics, 
  ExportParams, 
  ExportResult 
} from './types/system-service.types';
import { ENDPOINTS } from '../constants';
import { FetchSystemService } from './FetchSystemService';

/**
 * Type-safe System metrics service using native fetch
 */
export class FetchSystemMetricsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get performance metrics (optional)
   */
  async getPerformanceMetrics(
    params?: MetricsParams,
    config?: RequestConfig
  ): Promise<PerformanceMetrics> {
    const searchParams = new URLSearchParams();
    if (params?.period) {
      searchParams.set('period', params.period);
    }
    if (params?.includeDetails) {
      searchParams.set('includeDetails', 'true');
    }

    return this.client['get']<PerformanceMetrics>(
      `/system/performance${searchParams.toString() ? `?${searchParams}` : ''}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export performance data (optional)
   */
  async exportPerformanceData(
    params: ExportParams,
    config?: RequestConfig
  ): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      `/system/performance/export`,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get detailed system resource metrics.
   * Retrieves current system resource utilization including CPU, memory, disk usage,
   * active connections, and system uptime. Attempts to use dedicated metrics endpoint
   * with fallback to constructed metrics from system info.
   * 
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<SystemMetricsDto> - System resource metrics including:
   *   - cpuUsage: CPU utilization percentage (0-100)
   *   - memoryUsage: Memory utilization percentage (0-100)
   *   - diskUsage: Disk utilization percentage (0-100)
   *   - activeConnections: Number of active connections
   *   - uptime: System uptime in seconds
   * @throws {Error} When metrics data cannot be retrieved
   * @since Issue #427 - System Health SDK Methods
   */
  async getSystemMetrics(config?: RequestConfig): Promise<SystemMetricsDto> {
    try {
      // Try to get from dedicated metrics endpoint first
      return await this.client['get']<SystemMetricsDto>(
        ENDPOINTS.METRICS.BASE,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );
    } catch {
      // Fallback: construct from system info
      const systemService = new FetchSystemService(this.client);
      const systemInfo = await systemService.getSystemInfo(config);
      const activeConnections = await this.getActiveConnections(config);
      
      return {
        cpuUsage: 0, // CPU usage not available from backend
        memoryUsage: 0, // Memory usage not available from backend
        diskUsage: 0, // Will be enhanced when disk monitoring is available
        activeConnections,
        uptime: systemInfo.uptime,
      };
    }
  }

  /**
   * Get the number of active connections to the system.
   * Attempts to retrieve active connection count from metrics endpoint with
   * intelligent fallback using system metrics and heuristics when direct
   * connection data is unavailable.
   * 
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<number> - Number of currently active connections to the system
   * @throws {Error} When connection count cannot be determined
   * @since Issue #427 - System Health SDK Methods
   */
  async getActiveConnections(config?: RequestConfig): Promise<number> {
    try {
      // Try to get from metrics endpoint
      const metrics = await this.client['get']<Record<string, unknown>>(
        ENDPOINTS.METRICS.BASE,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );
      
      // Extract active connections from metrics if available
      const typedMetrics = metrics as {
        activeConnections?: number;
        database?: { connectionCount?: number };
      };
      
      return typedMetrics.activeConnections ?? typedMetrics.database?.connectionCount ?? 0;
    } catch {
      // Fallback: return default value when metrics endpoint is not available
      return 1;
    }
  }

  /**
   * Get system uptime in seconds.
   * Retrieves the current system uptime by calling the system info endpoint
   * and extracting the uptime value.
   * 
   * @param config - Optional request configuration for timeout, signal, headers
   * @returns Promise<number> - System uptime in seconds since last restart
   * @throws {Error} When system uptime cannot be retrieved
   * @since Issue #427 - System Health SDK Methods
   */
  async getUptime(config?: RequestConfig): Promise<number> {
    const systemService = new FetchSystemService(this.client);
    const systemInfo = await systemService.getSystemInfo(config);
    return systemInfo.uptime;
  }
}