import type { RequestConfig } from '../../client/types';
import type { components } from '../../generated/admin-api';
import type {
  SystemInfoDto,
  HealthStatusDto,
  SystemHealthDto,
  SystemResourceMetricsDto,
  ServiceStatusMapDto,
  HealthEventsResponseDto,
  HealthEventSubscriptionOptions,
  HealthEventSubscription
} from '../../models/system';

// Exact backend response type based on C# SystemInfoDto
// From ConduitLLM.Admin/Interfaces/IAdminSystemInfoService.cs
export interface BackendSystemInfoResponse {
  version: {
    appVersion: string;
    commitSha: string;
    buildTimestamp: string;
    buildDate: string | null; // DateTime? serialized as ISO string or null
  };
  operatingSystem: {
    description: string;
    architecture: string;
  };
  database: {
    provider: string;
    version: string;
    connected: boolean;
    connectionString: string;
    location: string;
    size: string;
    tableCount: number | null;
  };
  runtime: {
    runtimeVersion: string;
    startTime: string; // DateTime serialized as ISO string
    uptime: string;    // TimeSpan serialized as string (e.g., "1.02:03:04.5")
    customerMode: string; // "Internal" | "External" (CONDUIT_CUSTOMER_MODE)
  };
  recordCounts: {
    virtualKeys: number;
    requests?: number | null;
    settings: number;
    providers: number;
    modelMappings: number;
  };
}

// Performance types (not in generated schemas yet)
export interface MetricsParams {
  period?: 'hour' | 'day' | 'week' | 'month';
  includeDetails?: boolean;
}

export interface PerformanceMetrics {
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

export interface ExportParams {
  format: 'json' | 'csv' | 'excel';
  startDate?: string;
  endDate?: string;
  metrics?: string[];
}

export interface ExportResult {
  fileUrl: string;
  fileName: string;
  expiresAt: string;
  size: number;
}

// Service interface for system operations
export interface ISystemService {
  getSystemInfo(config?: RequestConfig): Promise<SystemInfoDto>;
  getHealth(config?: RequestConfig): Promise<HealthStatusDto>;
  getServiceHealth(
    config?: RequestConfig,
  ): Promise<components['schemas']['ServiceHealthResponse']>;
  getPerformanceMetrics(params?: MetricsParams, config?: RequestConfig): Promise<PerformanceMetrics>;
  exportPerformanceData(params: ExportParams, config?: RequestConfig): Promise<ExportResult>;
  getWebAdminVirtualKey(config?: RequestConfig): Promise<string>;
  invalidateFunctionDiscoveryCache(config?: RequestConfig): Promise<{
    message: string;
    timestamp: string;
    note?: string;
  }>;
}

// Service interface for health operations
export interface ISystemHealthService {
  getSystemHealth(config?: RequestConfig): Promise<SystemHealthDto>;
  getSystemMetrics(config?: RequestConfig): Promise<SystemResourceMetricsDto>;
  getServiceStatus(config?: RequestConfig): Promise<ServiceStatusMapDto>;
  getUptime(config?: RequestConfig): Promise<number>;
  getActiveConnections(config?: RequestConfig): Promise<number | null>;
  getHealthEvents(limit?: number, config?: RequestConfig): Promise<HealthEventsResponseDto>;
  subscribeToHealthEvents(
    options?: HealthEventSubscriptionOptions,
    config?: RequestConfig
  ): Promise<HealthEventSubscription>;
}

// Helper functions interface
export interface ISystemHelpers {
  isSystemHealthy(health: HealthStatusDto): boolean;
  getUnhealthyServices(health: HealthStatusDto): string[];
  formatUptime(uptimeSeconds: number): string;
  isFeatureEnabled(systemInfo: SystemInfoDto, feature: string): boolean;
  transformSystemInfoResponse(response: BackendSystemInfoResponse): SystemInfoDto;
}
