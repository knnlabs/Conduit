import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { SystemInfoDto, SystemResourceMetricsDto } from '../models/system';
import type {
  MetricsParams,
  SystemPerformanceMetrics,
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
  ): Promise<SystemPerformanceMetrics> {
    const searchParams = new URLSearchParams();
    if (params?.period) {
      searchParams.set('period', params.period);
    }
    if (params?.includeDetails) {
      searchParams.set('includeDetails', 'true');
    }

    return this.client['get']<SystemPerformanceMetrics>(
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
   * @returns Promise<SystemResourceMetricsDto> - System resource metrics. Fields that
   *   the backend does not measure (cpuUsage, memoryUsage, diskUsage in the fallback
   *   path) are null, never a fabricated 0.
   * @throws {Error} When metrics data cannot be retrieved
   * @since Issue #427 - System Health SDK Methods
   */
  async getSystemMetrics(config?: RequestConfig): Promise<SystemResourceMetricsDto> {
    try {
      // Try to get from dedicated metrics endpoint first
      return await this.client['get']<SystemResourceMetricsDto>(
        ENDPOINTS.METRICS.BASE,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );
    } catch {
      // Fallback: construct from system info. Resource percentages are not
      // available from the backend — report them as unknown, not as 0%.
      const systemService = new FetchSystemService(this.client);
      const systemInfo = await systemService.getSystemInfo(config);
      const activeConnections = await this.getActiveConnections(config);

      return {
        cpuUsage: null,
        memoryUsage: null,
        diskUsage: null,
        activeConnections,
        uptime: this.computeUptimeSeconds(systemInfo),
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
   * @returns Promise<number | null> - Number of currently active connections, or null
   *   when the metrics endpoint is unavailable or does not report a count. Null is
   *   deliberately not coerced to a plausible number — a fabricated reading is
   *   indistinguishable from a real one.
   * @since Issue #427 - System Health SDK Methods
   */
  async getActiveConnections(config?: RequestConfig): Promise<number | null> {
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

      return typedMetrics.activeConnections ?? typedMetrics.database?.connectionCount ?? null;
    } catch {
      // Metrics endpoint unavailable — the count is unknown
      return null;
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
    return this.computeUptimeSeconds(systemInfo);
  }

  /**
   * Derives uptime in seconds from the system info's runtime.startTime.
   * The endpoint no longer returns a numeric uptime (issue #1038); returns 0 when unknown.
   */
  private computeUptimeSeconds(systemInfo: SystemInfoDto): number {
    const startTime = systemInfo.runtime?.startTime;
    if (!startTime) {
      return 0;
    }
    const started = new Date(startTime).getTime();
    if (Number.isNaN(started)) {
      return 0;
    }
    return Math.max(0, Math.floor((Date.now() - started) / 1000));
  }
}
