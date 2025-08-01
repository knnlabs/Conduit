import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';
import type {
  MetricsSnapshot,
  DatabaseMetrics,
  HttpMetrics,
  BusinessMetrics,
  SystemMetrics,
  InfrastructureMetrics,
  ProviderHealthStatus,
  HistoricalMetricsRequest,
  HistoricalMetricsResponse,
  ModelUsageStats,
  VirtualKeyStats,
  KPISummary
} from '../models/metrics';

/**
 * Service for accessing system metrics and performance data from the Conduit Core API
 */
export class MetricsService {
  private readonly clientAdapter: IFetchBasedClientAdapter;

  constructor(client: FetchBasedClient) {
    this.clientAdapter = createClientAdapter(client);
  }

  /**
   * Gets the current comprehensive metrics snapshot
   * 
   * @returns Promise<MetricsSnapshot> A complete snapshot of current system metrics
   */
  async getCurrentMetrics(): Promise<MetricsSnapshot> {
    const response = await this.clientAdapter.get<MetricsSnapshot>(
      '/metrics'
    );
    return response;
  }

  /**
   * Gets current database connection pool metrics
   * 
   * @returns Promise<DatabaseMetrics> Database connection pool metrics
   */
  async getDatabasePoolMetrics(): Promise<DatabaseMetrics> {
    const response = await this.clientAdapter.get<DatabaseMetrics>(
      '/metrics/database/pool'
    );
    return response;
  }

  /**
   * Gets the raw Prometheus metrics format
   * 
   * @returns Promise<string> Prometheus-formatted metrics as a string
   */
  async getPrometheusMetrics(): Promise<string> {
    const response = await this.clientAdapter.get<string>(
      '/metrics',
      {
        headers: {
          'Accept': 'text/plain'
        }
      }
    );
    return response;
  }

  /**
   * Gets historical metrics data for a specified time range
   * 
   * @param request - The historical metrics request parameters
   * @returns Promise<HistoricalMetricsResponse> Historical metrics data
   */
  async getHistoricalMetrics(request: HistoricalMetricsRequest): Promise<HistoricalMetricsResponse> {
    const response = await this.clientAdapter.post<HistoricalMetricsResponse, HistoricalMetricsRequest>(
      '/metrics/historical',
      request
    );
    return response;
  }

  /**
   * Gets historical metrics for a specific time range with simplified parameters
   * 
   * @param startTime - Start time for the metrics query
   * @param endTime - End time for the metrics query
   * @param metricNames - Optional list of specific metrics to retrieve
   * @param interval - Optional interval for data aggregation (default: "5m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics data
   */
  async getHistoricalMetricsSimple(
    startTime: Date,
    endTime: Date,
    metricNames: string[] = [],
    interval: string = '5m'
  ): Promise<HistoricalMetricsResponse> {
    const request: HistoricalMetricsRequest = {
      startTime,
      endTime,
      metricNames,
      interval
    };

    return this.getHistoricalMetrics(request);
  }

  /**
   * Gets current HTTP performance metrics
   * 
   * @returns Promise<HttpMetrics> HTTP performance metrics
   */
  async getHttpMetrics(): Promise<HttpMetrics> {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.http;
  }

  /**
   * Gets current business metrics
   * 
   * @returns Promise<BusinessMetrics> Business metrics including costs and usage
   */
  async getBusinessMetrics(): Promise<BusinessMetrics> {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.business;
  }

  /**
   * Gets current system resource metrics
   * 
   * @returns Promise<SystemMetrics> System resource metrics
   */
  async getSystemMetrics(): Promise<SystemMetrics> {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.system;
  }

  /**
   * Gets current infrastructure component metrics
   * 
   * @returns Promise<InfrastructureMetrics> Infrastructure metrics including database, Redis, and messaging
   */
  async getInfrastructureMetrics(): Promise<InfrastructureMetrics> {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.infrastructure;
  }

  /**
   * Gets current provider health status for all providers
   * 
   * @returns Promise<ProviderHealthStatus[]> List of provider health statuses
   */
  async getProviderHealth(): Promise<ProviderHealthStatus[]> {
    const snapshot = await this.getCurrentMetrics();
    return snapshot.providerHealth;
  }

  /**
   * Gets health status for a specific provider by ID
   * 
   * @param providerId - The ID of the provider
   * @returns Promise<ProviderHealthStatus | null> Provider health status, or null if not found
   */
  async getProviderHealthById(providerId: number): Promise<ProviderHealthStatus | null> {
    if (!providerId || providerId <= 0) {
      throw new Error('Valid provider ID is required');
    }

    await this.getProviderHealth();
    // Note: This requires the backend to include provider ID in health status
    // For now, this is a stub implementation
    throw new Error('Provider health by ID is not yet implemented. Backend needs to include provider ID in health status.');
  }

  /**
   * @deprecated Provider names are no longer unique identifiers. Use getProviderHealthById instead.
   * Gets health status for a specific provider by name
   * 
   * @param providerName - The name of the provider
   * @returns Promise<ProviderHealthStatus | null> Provider health status, or null if not found
   */
  async getProviderHealthByName(): Promise<ProviderHealthStatus | null> {
    throw new Error('Provider names are no longer unique identifiers. Use getProviderHealthById with provider ID instead.');
  }

  /**
   * Gets the top performing models by request volume
   * 
   * @param count - Number of top models to return (default: 10)
   * @returns Promise<ModelUsageStats[]> List of top performing models ordered by request volume
   */
  async getTopModelsByRequestVolume(count: number = 10): Promise<ModelUsageStats[]> {
    if (count <= 0) {
      throw new Error('Count must be greater than 0');
    }

    const metrics = await this.getBusinessMetrics();
    return metrics.modelUsage
      .sort((a, b) => b.requestsPerMinute - a.requestsPerMinute)
      .slice(0, count);
  }

  /**
   * Gets the top spending virtual keys
   * 
   * @param count - Number of top virtual keys to return (default: 10)
   * @returns Promise<VirtualKeyStats[]> List of top spending virtual keys ordered by current spend
   */
  async getTopSpendingVirtualKeys(count: number = 10): Promise<VirtualKeyStats[]> {
    if (count <= 0) {
      throw new Error('Count must be greater than 0');
    }

    const metrics = await this.getBusinessMetrics();
    return metrics.virtualKeyStats
      .sort((a, b) => b.currentSpend - a.currentSpend)
      .slice(0, count);
  }

  /**
   * Gets providers that are currently unhealthy
   * 
   * @returns Promise<ProviderHealthStatus[]> List of unhealthy providers
   */
  async getUnhealthyProviders(): Promise<ProviderHealthStatus[]> {
    const allProviders = await this.getProviderHealth();
    return allProviders.filter(p => !p.isHealthy);
  }

  /**
   * Calculates the overall system health percentage
   * 
   * @returns Promise<number> System health percentage (0-100)
   */
  async getSystemHealthPercentage(): Promise<number> {
    const providers = await this.getProviderHealth();
    if (providers.length === 0) return 100; // If no providers, assume healthy

    const healthyCount = providers.filter(p => p.isHealthy).length;
    return (healthyCount / providers.length) * 100;
  }

  /**
   * Gets the current cost burn rate in USD per hour
   * 
   * @returns Promise<number> Current cost burn rate in USD per hour
   */
  async getCurrentCostBurnRate(): Promise<number> {
    const metrics = await this.getBusinessMetrics();
    return metrics.cost.costPerMinute * 60; // Convert per-minute to per-hour
  }

  /**
   * Checks if the system is currently healthy based on configurable thresholds
   * 
   * @param options - Health check criteria
   * @returns Promise<boolean> True if the system is healthy based on the specified thresholds
   */
  async isSystemHealthy(options: {
    maxErrorRate?: number;
    maxResponseTime?: number;
    minProviderHealthPercentage?: number;
  } = {}): Promise<boolean> {
    const {
      maxErrorRate = 5.0,
      maxResponseTime = 2000.0,
      minProviderHealthPercentage = 80.0
    } = options;

    try {
      const snapshot = await this.getCurrentMetrics();

      // Check error rate
      if (snapshot.http.errorRate > maxErrorRate) {
        return false;
      }

      // Check response time
      if (snapshot.http.responseTimes.p95 > maxResponseTime) {
        return false;
      }

      // Check provider health
      const providerHealth = await this.getSystemHealthPercentage();
      if (providerHealth < minProviderHealthPercentage) {
        return false;
      }

      return true;
    } catch {
      return false; // Assume unhealthy if we can't get metrics
    }
  }

  /**
   * Gets a summary of key performance indicators
   * 
   * @returns Promise<KPISummary> A summary object with key performance indicators
   */
  async getKPISummary(): Promise<KPISummary> {
    const snapshot = await this.getCurrentMetrics();
    const systemHealth = await this.getSystemHealthPercentage();
    const costBurnRate = await this.getCurrentCostBurnRate();

    return {
      timestamp: snapshot.timestamp,
      systemHealth: {
        overallHealthPercentage: systemHealth,
        errorRate: snapshot.http.errorRate,
        responseTimeP95: snapshot.http.responseTimes.p95,
        activeConnections: snapshot.infrastructure.database.activeConnections,
        databaseUtilization: snapshot.infrastructure.database.poolUtilization
      },
      performance: {
        requestsPerSecond: snapshot.http.requestsPerSecond,
        activeRequests: snapshot.http.activeRequests,
        averageResponseTime: snapshot.http.responseTimes.average,
        cacheHitRate: snapshot.infrastructure.redis.hitRate
      },
      business: {
        activeVirtualKeys: snapshot.business.activeVirtualKeys,
        requestsPerMinute: snapshot.business.totalRequestsPerMinute,
        costBurnRatePerHour: costBurnRate,
        averageCostPerRequest: snapshot.business.cost.averageCostPerRequest
      },
      infrastructure: {
        cpuUsage: snapshot.system.cpuUsagePercent,
        memoryUsage: snapshot.system.memoryUsageMB,
        uptime: snapshot.system.uptime,
        signalRConnections: snapshot.infrastructure.signalR.activeConnections
      }
    };
  }

  /**
   * Gets metrics for the last N minutes
   * 
   * @param minutes - Number of minutes to look back
   * @param interval - Data aggregation interval (default: "1m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics for the specified period
   */
  async getMetricsForLastMinutes(minutes: number, interval: string = '1m'): Promise<HistoricalMetricsResponse> {
    const endTime = new Date();
    const startTime = new Date(endTime.getTime() - (minutes * 60 * 1000));

    return this.getHistoricalMetricsSimple(startTime, endTime, [], interval);
  }

  /**
   * Gets metrics for the last N hours
   * 
   * @param hours - Number of hours to look back
   * @param interval - Data aggregation interval (default: "5m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics for the specified period
   */
  async getMetricsForLastHours(hours: number, interval: string = '5m'): Promise<HistoricalMetricsResponse> {
    const endTime = new Date();
    const startTime = new Date(endTime.getTime() - (hours * 60 * 60 * 1000));

    return this.getHistoricalMetricsSimple(startTime, endTime, [], interval);
  }

  /**
   * Gets metrics for today
   * 
   * @param interval - Data aggregation interval (default: "15m")
   * @returns Promise<HistoricalMetricsResponse> Historical metrics for today
   */
  async getMetricsForToday(interval: string = '15m'): Promise<HistoricalMetricsResponse> {
    const endTime = new Date();
    const startTime = new Date();
    startTime.setHours(0, 0, 0, 0);

    return this.getHistoricalMetricsSimple(startTime, endTime, [], interval);
  }
}