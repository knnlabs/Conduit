import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';

// Define response types inline since they're not in generated operations
interface MetricsResponse {
  requestsPerMinute?: number;
  activeConnections?: number;
  averageLatency?: number;
  errorRate?: number;
  timestamp?: string;
  totalRequests?: number;
  totalCost?: number;
  activeVirtualKeys?: number;
  avgResponseTime?: number;
}

interface TimeSeriesDataPoint {
  date?: string;
  requests?: number;
  cost?: number;
}

interface TimeSeriesData {
  data?: TimeSeriesDataPoint[];
  timestamps?: string[];
  values?: number[];
  metric?: string;
}

interface ProviderMetric {
  provider?: string;
  totalCost?: number;
  requests?: number;
  averageLatency?: number;
  errorRate?: number;
  successRate?: number;
}

type ProviderMetrics = ProviderMetric[];

/**
 * Type-safe Dashboard service using native fetch
 */
export class FetchDashboardService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get real-time dashboard metrics
   */
  async getMetrics(config?: RequestConfig): Promise<MetricsResponse> {
    return this.client['get']<MetricsResponse>(
      '/dashboard/metrics',
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get time series data for charts
   */
  async getTimeSeriesData(
    interval: 'day' | 'week' | 'month' = 'day',
    days: number = 7,
    config?: RequestConfig
  ): Promise<TimeSeriesData> {
    const params = new URLSearchParams({
      interval,
      days: days.toString(),
    });
    
    return this.client['get']<TimeSeriesData>(
      `/dashboard/time-series?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get provider-specific metrics
   */
  async getProviderMetrics(
    days: number = 7,
    config?: RequestConfig
  ): Promise<ProviderMetrics> {
    const params = new URLSearchParams({
      days: days.toString(),
    });
    
    return this.client['get']<ProviderMetrics>(
      `/dashboard/provider-metrics?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to calculate average requests per day
   */
  calculateAverageRequestsPerDay(timeSeriesData: TimeSeriesData): number {
    if (!timeSeriesData.data || timeSeriesData.data.length === 0) {
      return 0;
    }

    const totalRequests = timeSeriesData.data.reduce(
      (sum: number, point: TimeSeriesDataPoint) => sum + (point.requests ?? 0),
      0
    );

    return totalRequests / timeSeriesData.data.length;
  }

  /**
   * Helper method to calculate total cost from time series data
   */
  calculateTotalCost(timeSeriesData: TimeSeriesData): number {
    if (!timeSeriesData.data || timeSeriesData.data.length === 0) {
      return 0;
    }

    return timeSeriesData.data.reduce(
      (sum: number, point: TimeSeriesDataPoint) => sum + (point.cost ?? 0),
      0
    );
  }

  /**
   * Helper method to find peak usage time
   */
  findPeakUsageTime(timeSeriesData: TimeSeriesData): { date: string; requests: number } | null {
    if (!timeSeriesData.data || timeSeriesData.data.length === 0) {
      return null;
    }

    let peakPoint = timeSeriesData.data[0];
    for (const point of timeSeriesData.data) {
      if ((point.requests ?? 0) > (peakPoint.requests ?? 0)) {
        peakPoint = point;
      }
    }

    return {
      date: peakPoint.date ?? '',
      requests: peakPoint.requests ?? 0,
    };
  }

  /**
   * Helper method to calculate provider cost distribution
   */
  calculateProviderCostDistribution(
    providerMetrics: ProviderMetrics
  ): Array<{ provider: string; percentage: number }> {
    if (!providerMetrics || providerMetrics.length === 0) {
      return [];
    }

    const totalCost = providerMetrics.reduce(
      (sum: number, metric: ProviderMetric) => sum + (metric.totalCost ?? 0),
      0
    );

    if (totalCost === 0) {
      return providerMetrics.map((metric: ProviderMetric) => ({
        provider: metric.provider ?? 'Unknown',
        percentage: 0,
      }));
    }

    return providerMetrics.map((metric: ProviderMetric) => ({
      provider: metric.provider ?? 'Unknown',
      percentage: ((metric.totalCost ?? 0) / totalCost) * 100,
    }));
  }

  /**
   * Helper method to format metrics for display
   */
  formatMetrics(metrics: MetricsResponse): {
    totalRequests: string;
    totalCost: string;
    activeKeys: string;
    errorRate: string;
    avgResponseTime: string;
  } {
    return {
      totalRequests: this.formatNumber(metrics.totalRequests ?? 0),
      totalCost: this.formatCurrency(metrics.totalCost ?? 0),
      activeKeys: this.formatNumber(metrics.activeVirtualKeys ?? 0),
      errorRate: this.formatPercentage(metrics.errorRate ?? 0),
      avgResponseTime: this.formatMilliseconds(metrics.avgResponseTime ?? 0),
    };
  }

  private formatNumber(value: number): string {
    return new Intl.NumberFormat().format(value);
  }

  private formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 4,
    }).format(value);
  }

  private formatPercentage(value: number): string {
    return `${(value * 100).toFixed(2)}%`;
  }

  private formatMilliseconds(value: number): string {
    if (value < 1000) {
      return `${value.toFixed(0)}ms`;
    }
    return `${(value / 1000).toFixed(2)}s`;
  }
}