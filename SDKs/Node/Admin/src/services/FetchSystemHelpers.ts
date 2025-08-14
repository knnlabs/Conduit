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
    // Calculate uptime in seconds from the TimeSpan format
    let uptimeSeconds = 0;
    if (response.runtime.uptime) {
      // Parse TimeSpan format (e.g., "00:05:30" or "1.02:03:04.5")
      const timeSpanMatch = response.runtime.uptime.match(/^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/);
      if (timeSpanMatch) {
        const days = parseInt(timeSpanMatch[1] ?? '0', 10);
        const hours = parseInt(timeSpanMatch[2], 10);
        const minutes = parseInt(timeSpanMatch[3], 10);
        const seconds = parseInt(timeSpanMatch[4], 10);
        uptimeSeconds = (days * 24 * 60 * 60) + (hours * 60 * 60) + (minutes * 60) + seconds;
      }
    }
    
    return {
      version: response.version.appVersion,
      buildDate: response.version.buildDate ? new Date(response.version.buildDate).toISOString() : '',
      environment: 'production',
      uptime: uptimeSeconds,
      systemTime: new Date().toISOString(),
      features: {
        ipFiltering: false,
        providerHealth: true,
        costTracking: false,
        audioSupport: false
      },
      runtime: {
        dotnetVersion: response.runtime.runtimeVersion,
        os: response.operatingSystem.description,
        architecture: response.operatingSystem.architecture
      },
      database: {
        provider: response.database.provider,
        connectionString: response.database.connectionString,
        isConnected: response.database.connected,
        pendingMigrations: []
      }
    };
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
   * Helper method to check if a feature is enabled
   */
  isFeatureEnabled(systemInfo: SystemInfoDto, feature: keyof SystemInfoDto['features']): boolean {
    return systemInfo.features[feature] === true;
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