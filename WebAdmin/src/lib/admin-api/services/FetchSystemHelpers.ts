import type { SystemInfoDto, HealthStatusDto, ComponentHealthStatus } from '../models/system';
import type { BackendSystemInfoResponse, ISystemHelpers } from './types/system-service.types';

/**
 * Helper utilities for system service operations
 */
export class FetchSystemHelpers implements ISystemHelpers {
  /**
   * Transform backend SystemInfo response to match frontend expectations
   */
  transformSystemInfoResponse(response: BackendSystemInfoResponse): SystemInfoDto {
    // The backend response now matches the wire SystemInfoDto shape (nested
    // version/os/database/runtime/recordCounts), so no transformation is needed. See issue #1038.
    return response;
  }

  /**
   * Helper method to check if system is healthy
   */
  isSystemHealthy(health: HealthStatusDto): boolean {
    return health.status === 'healthy';
  }

  /**
   * Helper method to get unhealthy services
   */
  getUnhealthyServices(health: HealthStatusDto): string[] {
    return Object.entries(health.checks)
      .filter(([, check]) => check.status !== 'healthy')
      .map(([name]) => name);
  }

  /**
   * Helper method to format uptime
   */
  formatUptime(uptimeSeconds: number): string {
    const days = Math.floor(uptimeSeconds / 86400);
    const hours = Math.floor((uptimeSeconds % 86400) / 3600);
    const minutes = Math.floor((uptimeSeconds % 3600) / 60);

    if (days > 0) {
      return `${days}d ${hours}h ${minutes}m`;
    } else if (hours > 0) {
      return `${hours}h ${minutes}m`;
    } else {
      return `${minutes}m`;
    }
  }

  /**
   * Helper method to check if a feature is enabled.
   * The system-info endpoint no longer reports feature flags (issue #1038), so this
   * always returns false. Retained for backward compatibility of the method surface.
   */
  isFeatureEnabled(systemInfo: SystemInfoDto, feature: string): boolean {
    void systemInfo;
    void feature;
    return false;
  }

  /**
   * Coerce a wire status string into a known component status.
   *
   * An unrecognized or missing status maps to `unknown`, never `healthy`. The previous
   * `healthy` fallback meant any gap in the response — including a shape mismatch that made
   * every lookup `undefined` — was rendered as a green service (issue #1067).
   */
  normalizeStatus(status?: string): ComponentHealthStatus {
    if (
      status === 'healthy' ||
      status === 'degraded' ||
      status === 'unhealthy' ||
      status === 'unknown'
    ) {
      return status;
    }
    return 'unknown';
  }
}
