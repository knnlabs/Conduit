import type { SystemInfoDto, HealthStatusDto } from '../models/system';
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
      .filter(([_, check]) => check.status !== 'healthy')
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
  isFeatureEnabled(_systemInfo: SystemInfoDto, _feature: string): boolean {
    return false;
  }

  /**
   * Helper function to ensure valid status values
   */
  normalizeStatus(status?: string): 'healthy' | 'degraded' | 'unhealthy' {
    if (status === 'healthy' || status === 'degraded' || status === 'unhealthy') {
      return status;
    }
    return 'healthy'; // Default fallback
  }
}