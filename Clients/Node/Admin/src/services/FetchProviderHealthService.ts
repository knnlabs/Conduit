import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  HealthSummaryDto,
  ProviderHealthDto,
  ProviderHealthStatusDto,
  ProviderHealthSummaryDto,
  ProviderHealthSummary,
  HealthHistory,
  HistoryParams,
  HealthAlert,
  HealthAlertListResponseDto,
  AlertParams,
  ConnectionTestResult,
  PerformanceParams,
  PerformanceMetrics,
  ProviderHealthConfigurationDto,
  CreateProviderHealthConfigurationDto,
  UpdateProviderHealthConfigurationDto,
  ProviderHealthRecordDto,
} from '../models/providerHealth';

/**
 * Type-safe Provider Health service using native fetch
 */
export class FetchProviderHealthService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get current health summary for all providers
   */
  async getHealthSummary(config?: RequestConfig): Promise<HealthSummaryDto> {
    return this.client['get']<HealthSummaryDto>(
      ENDPOINTS.HEALTH.SUMMARY,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get legacy health summary (using existing endpoint)
   */
  async getLegacyHealthSummary(config?: RequestConfig): Promise<ProviderHealthSummaryDto> {
    return this.client['get']<ProviderHealthSummaryDto>(
      ENDPOINTS.HEALTH.STATUS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get detailed health status for a specific provider
   */
  async getProviderHealth(providerId: string, config?: RequestConfig): Promise<ProviderHealthDto> {
    return this.client['get']<ProviderHealthDto>(
      ENDPOINTS.HEALTH.STATUS_BY_PROVIDER(providerId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get legacy provider health status
   */
  async getLegacyProviderStatus(providerId: string, config?: RequestConfig): Promise<ProviderHealthStatusDto> {
    return this.client['get']<ProviderHealthStatusDto>(
      ENDPOINTS.HEALTH.STATUS_BY_PROVIDER(providerId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get health history for a provider
   */
  async getHealthHistory(
    providerId: string, 
    params?: HistoryParams, 
    config?: RequestConfig
  ): Promise<HealthHistory> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.resolution) queryParams.append('resolution', params.resolution);
      if (params.includeIncidents !== undefined) {
        queryParams.append('includeIncidents', params.includeIncidents.toString());
      }
    }

    const queryString = queryParams.toString();
    const url = queryString 
      ? `${ENDPOINTS.HEALTH.HISTORY_BY_PROVIDER(providerId)}?${queryString}`
      : ENDPOINTS.HEALTH.HISTORY_BY_PROVIDER(providerId);

    return this.client['get']<HealthHistory>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get all health history records
   */
  async getAllHealthHistory(
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<ProviderHealthRecordDto[]> {
    const queryParams = new URLSearchParams();
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    const queryString = queryParams.toString();
    const url = queryString 
      ? `${ENDPOINTS.HEALTH.HISTORY}?${queryString}`
      : ENDPOINTS.HEALTH.HISTORY;

    return this.client['get']<ProviderHealthRecordDto[]>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get health alerts
   */
  async getHealthAlerts(params?: AlertParams, config?: RequestConfig): Promise<HealthAlert[]> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.pageNumber) queryParams.append('page', params.pageNumber.toString());
      if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
      if (params.severity?.length) {
        params.severity.forEach(s => queryParams.append('severity', s));
      }
      if (params.type?.length) {
        params.type.forEach(t => queryParams.append('type', t));
      }
      if (params.providerId) queryParams.append('providerId', params.providerId);
      if (params.acknowledged !== undefined) {
        queryParams.append('acknowledged', params.acknowledged.toString());
      }
      if (params.resolved !== undefined) {
        queryParams.append('resolved', params.resolved.toString());
      }
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.HEALTH.ALERTS}?${queryString}` : ENDPOINTS.HEALTH.ALERTS;

    return this.client['get']<HealthAlert[]>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Test provider connectivity
   */
  async testProviderConnection(
    providerId: string, 
    config?: RequestConfig
  ): Promise<ConnectionTestResult> {
    return this.client['post']<ConnectionTestResult>(
      ENDPOINTS.HEALTH.CHECK(providerId),
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get provider performance metrics
   */
  async getProviderPerformance(
    providerId: string, 
    params?: PerformanceParams, 
    config?: RequestConfig
  ): Promise<PerformanceMetrics> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.resolution) queryParams.append('resolution', params.resolution);
    }

    const queryString = queryParams.toString();
    const url = queryString 
      ? `${ENDPOINTS.HEALTH.PERFORMANCE(providerId)}?${queryString}`
      : ENDPOINTS.HEALTH.PERFORMANCE(providerId);

    return this.client['get']<PerformanceMetrics>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get provider health configurations
   */
  async getHealthConfigurations(config?: RequestConfig): Promise<ProviderHealthConfigurationDto[]> {
    return this.client['get']<ProviderHealthConfigurationDto[]>(
      ENDPOINTS.HEALTH.CONFIGURATIONS,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get health configuration for a specific provider
   */
  async getProviderHealthConfiguration(
    providerId: string,
    config?: RequestConfig
  ): Promise<ProviderHealthConfigurationDto> {
    return this.client['get']<ProviderHealthConfigurationDto>(
      ENDPOINTS.HEALTH.CONFIG_BY_PROVIDER(providerId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create health configuration for a provider
   */
  async createHealthConfiguration(
    data: CreateProviderHealthConfigurationDto,
    config?: RequestConfig
  ): Promise<ProviderHealthConfigurationDto> {
    return this.client['post']<ProviderHealthConfigurationDto, CreateProviderHealthConfigurationDto>(
      ENDPOINTS.HEALTH.CONFIGURATIONS,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update health configuration for a provider
   */
  async updateHealthConfiguration(
    providerId: string,
    data: UpdateProviderHealthConfigurationDto,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['put']<void, UpdateProviderHealthConfigurationDto>(
      ENDPOINTS.HEALTH.CONFIG_BY_PROVIDER(providerId),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Acknowledge a health alert
   */
  async acknowledgeAlert(alertId: string, config?: RequestConfig): Promise<void> {
    return this.client['post']<void>(
      `${ENDPOINTS.HEALTH.ALERTS}/${alertId}/acknowledge`,
      {},
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Resolve a health alert
   */
  async resolveAlert(alertId: string, resolution?: string, config?: RequestConfig): Promise<void> {
    return this.client['post']<void>(
      `${ENDPOINTS.HEALTH.ALERTS}/${alertId}/resolve`,
      { resolution },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to calculate health score
   */
  calculateHealthScore(metrics: {
    uptime: number;
    errorRate: number;
    avgLatency: number;
    expectedLatency: number;
  }): number {
    const uptimeScore = metrics.uptime;
    const errorScore = 100 - metrics.errorRate;
    const latencyScore = Math.max(0, 100 - ((metrics.avgLatency / metrics.expectedLatency - 1) * 100));
    
    // Weighted average: uptime 40%, errors 40%, latency 20%
    return (uptimeScore * 0.4) + (errorScore * 0.4) + (latencyScore * 0.2);
  }

  /**
   * Helper method to determine health status from score
   */
  getHealthStatus(score: number): 'healthy' | 'degraded' | 'unhealthy' {
    if (score >= 90) return 'healthy';
    if (score >= 70) return 'degraded';
    return 'unhealthy';
  }

  /**
   * Helper method to format uptime percentage
   */
  formatUptime(uptime: number): string {
    if (uptime >= 99.99) return '99.99%';
    if (uptime >= 99.9) return `${uptime.toFixed(2)}%`;
    return `${uptime.toFixed(1)}%`;
  }

  /**
   * Helper method to get severity color
   */
  getSeverityColor(severity: 'info' | 'warning' | 'critical'): string {
    switch (severity) {
      case 'info': return '#3B82F6'; // blue
      case 'warning': return '#F59E0B'; // amber
      case 'critical': return '#EF4444'; // red
      default: return '#6B7280'; // gray
    }
  }

  /**
   * Helper method to check if provider needs attention
   */
  needsAttention(provider: ProviderHealthSummary): boolean {
    return provider.status !== 'healthy' || 
           provider.errorRate > 5 || 
           provider.uptime < 99.5;
  }

  /**
   * Helper method to group alerts by provider
   */
  groupAlertsByProvider(alerts: HealthAlert[]): Record<string, HealthAlert[]> {
    return alerts.reduce((acc, alert) => {
      if (!acc[alert.providerId]) {
        acc[alert.providerId] = [];
      }
      acc[alert.providerId].push(alert);
      return acc;
    }, {} as Record<string, HealthAlert[]>);
  }

  /**
   * Helper method to calculate MTBF (Mean Time Between Failures)
   */
  calculateMTBF(incidents: Array<{ startTime: string; endTime?: string }>, timeRangeHours: number): number {
    if (incidents.length === 0) return timeRangeHours * 3600; // Return total time if no incidents
    
    const totalDowntime = incidents.reduce((sum, incident) => {
      const start = new Date(incident.startTime).getTime();
      const end = incident.endTime ? new Date(incident.endTime).getTime() : Date.now();
      return sum + (end - start) / 1000; // Convert to seconds
    }, 0);

    const totalUptime = (timeRangeHours * 3600) - totalDowntime;
    return totalUptime / Math.max(incidents.length, 1);
  }

  /**
   * Helper method to calculate MTTR (Mean Time To Recover)
   */
  calculateMTTR(incidents: Array<{ startTime: string; endTime?: string }>): number {
    const resolvedIncidents = incidents.filter(i => i.endTime);
    if (resolvedIncidents.length === 0) return 0;

    const totalRecoveryTime = resolvedIncidents.reduce((sum, incident) => {
      const start = new Date(incident.startTime).getTime();
      const end = new Date(incident.endTime!).getTime();
      return sum + (end - start) / 1000; // Convert to seconds
    }, 0);

    return totalRecoveryTime / resolvedIncidents.length;
  }
}