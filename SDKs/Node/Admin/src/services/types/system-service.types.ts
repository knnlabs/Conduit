import type { RequestConfig } from '../../client/types';
import type { 
  SystemInfoDto, 
  HealthStatusDto,
  SystemHealthDto,
  SystemMetricsDto,
  ServiceStatusDto,
  HealthEventsResponseDto,
  HealthEventSubscriptionOptions,
  HealthEventSubscription
} from '../../models/system';

// Exact backend response type based on C# SystemInfoDto
// From ConduitLLM.Admin/Interfaces/IAdminSystemInfoService.cs
export interface BackendSystemInfoResponse {
  version: {
    appVersion: string;
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
    tableCount: number;
  };
  runtime: {
    runtimeVersion: string;
    startTime: string; // DateTime serialized as ISO string
    uptime: string;    // TimeSpan serialized as string (e.g., "1.02:03:04.5")
  };
  recordCounts: {
    virtualKeys: number;
    requests: number;
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
  getPerformanceMetrics(params?: MetricsParams, config?: RequestConfig): Promise<PerformanceMetrics>;
  exportPerformanceData(params: ExportParams, config?: RequestConfig): Promise<ExportResult>;
  getWebUIVirtualKey(config?: RequestConfig): Promise<string>;
}

// Service interface for health operations
export interface ISystemHealthService {
  getSystemHealth(config?: RequestConfig): Promise<SystemHealthDto>;
  getSystemMetrics(config?: RequestConfig): Promise<SystemMetricsDto>;
  getServiceStatus(config?: RequestConfig): Promise<ServiceStatusDto>;
  getUptime(config?: RequestConfig): Promise<number>;
  getActiveConnections(config?: RequestConfig): Promise<number>;
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
  isFeatureEnabled(systemInfo: SystemInfoDto, feature: keyof SystemInfoDto['features']): boolean;
  transformSystemInfoResponse(response: BackendSystemInfoResponse): SystemInfoDto;
}