export interface ProviderHealthStatus {
  providerId: string;
  providerName: string;
  providerType: string;
  status: 'healthy' | 'unhealthy' | 'unknown';
  lastCheckedAt: string;
  responseTime?: number;
  error?: string;
}

export interface ProviderHealthSummary {
  totalProviders: number;
  healthyProviders: number;
  unhealthyProviders: number;
  unknownProviders: number;
  providers: ProviderHealthStatus[];
}

/**
 * Provider health status response from API
 */
export interface ProviderHealthStatusResponse {
  providers: Array<{
    id: string;
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    responseTime: number;
    uptime: number;
    errorRate: number;
  }>;
}

/**
 * Provider with health data combined
 */
export interface ProviderWithHealthDto {
  id: string;
  name: string;
  isEnabled: boolean;
  providerType: number;
  apiKey?: string;
  health: {
    status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
    responseTime: number;
    uptime: number;
    errorRate: number;
  };
}

/**
 * Provider health metrics data
 */
export interface ProviderHealthMetricsDto {
  providerId: string;
  providerType: number;
  totalRequests: number;
  failedRequests: number;
  avgResponseTime: number;
  p95ResponseTime: number;
  p99ResponseTime: number;
  availability: number;
  lastUpdated: string;
}