import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type { 
  SystemInfoDto, 
  HealthStatusDto 
} from '../models/system';

// Type aliases for better readability  
type GlobalSettingDto = components['schemas']['GlobalSettingDto'];
type VirtualKeyDto = components['schemas']['VirtualKeyDto'];
type CreateVirtualKeyRequestDto = components['schemas']['CreateVirtualKeyRequestDto'];
type CreateVirtualKeyResponseDto = components['schemas']['CreateVirtualKeyResponseDto'];
type CreateGlobalSettingDto = components['schemas']['CreateGlobalSettingDto'];

// Performance types (not in generated schemas yet)
interface MetricsParams {
  period?: 'hour' | 'day' | 'week' | 'month';
  includeDetails?: boolean;
}

interface PerformanceMetrics {
  cpu: {
    usage: number;
    cores: number;
  };
  memory: {
    used: number;
    total: number;
    percentage: number;
  };
  requests: {
    total: number;
    perMinute: number;
    averageLatency: number;
  };
  timestamp: string;
}

interface ExportParams {
  format: 'json' | 'csv' | 'excel';
  startDate?: string;
  endDate?: string;
  metrics?: string[];
}

interface ExportResult {
  fileUrl: string;
  fileName: string;
  expiresAt: string;
  size: number;
}

/**
 * Type-safe System service using native fetch
 */
export class FetchSystemService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get system information
   */
  async getSystemInfo(config?: RequestConfig): Promise<SystemInfoDto> {
    return this.client['get']<SystemInfoDto>(
      ENDPOINTS.SYSTEM.INFO,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get system health status
   */
  async getHealth(config?: RequestConfig): Promise<HealthStatusDto> {
    return this.client['get']<HealthStatusDto>(
      ENDPOINTS.SYSTEM.HEALTH,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get WebUI virtual key for authentication
   * CRITICAL: This is required for WebUI authentication
   */
  async getWebUIVirtualKey(config?: RequestConfig): Promise<string> {
    try {
      // First try to get existing key from GlobalSettings
      const setting = await this.client['get']<GlobalSettingDto>(
        `${ENDPOINTS.SETTINGS.GLOBAL_BY_KEY('WebUI_VirtualKey')}`,
        {
          signal: config?.signal,
          timeout: config?.timeout,
          headers: config?.headers,
        }
      );
      
      if (setting?.value) {
        return setting.value;
      }
    } catch (error) {
      // Key doesn't exist, we'll create it
    }

    // Create metadata for the virtual key
    const metadata = {
      visibility: 'hidden',
      created: new Date().toISOString(),
      originator: 'Admin SDK'
    };

    // Create the virtual key
    const response = await this.client['post']<CreateVirtualKeyResponseDto, CreateVirtualKeyRequestDto>(
      ENDPOINTS.VIRTUAL_KEYS.BASE,
      {
        keyName: 'WebUI Internal Key',
        metadata: JSON.stringify(metadata)
      },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    // Store the unhashed key in GlobalSettings
    await this.client['post']<GlobalSettingDto, CreateGlobalSettingDto>(
      ENDPOINTS.SETTINGS.GLOBAL,
      {
        key: 'WebUI_VirtualKey',
        value: response.virtualKey,
        description: 'Virtual key for WebUI Core API access'
      },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    return response.virtualKey;
  }

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
}