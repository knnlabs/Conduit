import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { AnalyticsOptions } from '../models/common-types';
import { ENDPOINTS, CACHE_TTL, DEFAULT_PAGE_SIZE } from '../constants';
import {
  RequestLogDto,
  RequestLogFilters,
  UsageMetricsDto,
  ModelUsageDto,
  KeyUsageDto,
  AnalyticsFilters,
  CostForecastDto,
  AnomalyDto,
} from '../models/analytics';
import {
  ExportResult,
  ExportRequestLogsParams,
  RequestLogStatistics,
  RequestLog,
  ExportStatus,
} from '../models/analyticsExport';
import { PaginatedResponse, DateRange } from '../models/common';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const dateRangeSchema = z.object({
  startDate: z.string().datetime(),
  endDate: z.string().datetime(),
});


export class AnalyticsService extends FetchBaseApiClient {
  // Request Logs
  async getRequestLogs(
    filters?: RequestLogFilters
  ): Promise<PaginatedResponse<RequestLogDto>> {
    const params = {
      pageNumber: filters?.pageNumber ?? 1,
      pageSize: filters?.pageSize ?? DEFAULT_PAGE_SIZE,
      startDate: filters?.startDate,
      endDate: filters?.endDate,
      virtualKeyId: filters?.virtualKeyId,
      model: filters?.model,
      provider: filters?.provider,
      status: filters?.status,
      minCost: filters?.minCost,
      maxCost: filters?.maxCost,
      minDuration: filters?.minDuration,
      maxDuration: filters?.maxDuration,
      ipAddress: filters?.ipAddress,
      sortBy: filters?.sortBy?.field,
      sortDirection: filters?.sortBy?.direction,
    };

    return super.get<PaginatedResponse<RequestLogDto>>(
      ENDPOINTS.ANALYTICS.REQUEST_LOGS,
      params
    );
  }

  async getRequestLog(id: string): Promise<RequestLogDto> {
    return super.get<RequestLogDto>(ENDPOINTS.ANALYTICS.REQUEST_LOG_BY_ID(id));
  }

  async searchLogs(query: string, filters?: RequestLogFilters): Promise<RequestLogDto[]> {
    const response = await this.getRequestLogs({
      ...filters,
      search: query,
      pageSize: 100,
    });
    return response.items;
  }

  // Usage Metrics
  async getUsageMetrics(dateRange: DateRange): Promise<UsageMetricsDto> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', { validationError: error });
    }

    const cacheKey = this.getCacheKey('usage-metrics', dateRange);
    return this.withCache(
      cacheKey,
      () => super.get<UsageMetricsDto>('/api/analytics/usage-metrics', { ...dateRange }),
      CACHE_TTL.SHORT
    );
  }

  async getModelUsage(modelId: string, dateRange: DateRange): Promise<ModelUsageDto> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', { validationError: error });
    }

    const params = { modelId, ...dateRange };
    const cacheKey = this.getCacheKey('model-usage', params);
    return this.withCache(
      cacheKey,
      () => super.get<ModelUsageDto>('/api/analytics/model-usage', params),
      CACHE_TTL.SHORT
    );
  }

  async getKeyUsage(keyId: number, dateRange: DateRange): Promise<KeyUsageDto> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', { validationError: error });
    }

    const params = { keyId, ...dateRange };
    const cacheKey = this.getCacheKey('key-usage', params);
    return this.withCache(
      cacheKey,
      () => super.get<KeyUsageDto>('/api/analytics/key-usage', params),
      CACHE_TTL.SHORT
    );
  }

  // Request logs export and analytics
  async exportRequestLogs(params: ExportRequestLogsParams): Promise<ExportResult> {
    const response = await this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_REQUEST_LOGS,
      params
    );
    return response;
  }

  getRequestLogStatistics(logs: RequestLog[]): RequestLogStatistics {
    // Client-side calculation of statistics
    const stats: RequestLogStatistics = {
      totalRequests: logs.length,
      uniqueVirtualKeys: new Set(logs.map(l => l.virtualKeyId)).size,
      uniqueIpAddresses: new Set(logs.map(l => l.ipAddress)).size,
      averageResponseTime: 0,
      medianResponseTime: 0,
      p95ResponseTime: 0,
      p99ResponseTime: 0,
      totalCost: 0,
      totalTokensUsed: 0,
      errorRate: 0,
      statusCodeDistribution: {},
      endpointDistribution: [],
      hourlyDistribution: Array.from({ length: 24 }, (_, i) => ({ hour: i, count: 0 })),
    };

    if (logs.length === 0) return stats;

    // Calculate response times
    const responseTimes = logs.map(l => l.responseTime).sort((a, b) => a - b);
    stats.averageResponseTime = responseTimes.reduce((a, b) => a + b, 0) / responseTimes.length;
    stats.medianResponseTime = responseTimes[Math.floor(responseTimes.length / 2)];
    stats.p95ResponseTime = responseTimes[Math.floor(responseTimes.length * 0.95)];
    stats.p99ResponseTime = responseTimes[Math.floor(responseTimes.length * 0.99)];

    // Calculate other metrics
    stats.totalCost = logs.reduce((sum, log) => sum + (log.cost ?? 0), 0);
    stats.totalTokensUsed = logs.reduce((sum, log) => sum + (log.tokensUsed?.total ?? 0), 0);
    const errorCount = logs.filter(l => l.error).length;
    stats.errorRate = (errorCount / logs.length) * 100;

    // Status code distribution
    logs.forEach(log => {
      stats.statusCodeDistribution[log.statusCode] = 
        (stats.statusCodeDistribution[log.statusCode] ?? 0) + 1;
    });

    // Endpoint distribution
    const endpointMap = new Map<string, { count: number; totalTime: number }>();
    logs.forEach(log => {
      const current = endpointMap.get(log.endpoint) ?? { count: 0, totalTime: 0 };
      current.count++;
      current.totalTime += log.responseTime;
      endpointMap.set(log.endpoint, current);
    });
    
    stats.endpointDistribution = Array.from(endpointMap.entries())
      .map(([endpoint, data]) => ({
        endpoint,
        count: data.count,
        avgResponseTime: data.totalTime / data.count,
      }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 10);

    // Hourly distribution
    logs.forEach(log => {
      const hour = new Date(log.timestamp).getHours();
      stats.hourlyDistribution[hour].count++;
    });

    return stats;
  }






  async getExportStatus(exportId: string): Promise<ExportStatus> {
    return this.get<ExportStatus>(ENDPOINTS.ANALYTICS.EXPORT_STATUS(exportId));
  }

  async downloadExport(exportId: string): Promise<Blob> {
    const response = await this.get<Blob>(ENDPOINTS.ANALYTICS.EXPORT_DOWNLOAD(exportId), {
      responseType: 'blob',
    });
    return response;
  }

  // Stub methods
  getDetailedCostBreakdown(_filters: AnalyticsFilters): Promise<never> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getDetailedCostBreakdown requires Admin API endpoint implementation. ' +
        'The WebUI currently calculates this client-side'
    );
  }

  predictFutureCosts(_basePeriod: DateRange, _forecastDays: number): Promise<CostForecastDto> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'predictFutureCosts requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/analytics/forecast'
    );
  }

  async export(
    filters: AnalyticsFilters,
    format: 'csv' | 'excel' | 'json' = 'csv'
  ): Promise<Blob> {
    const params = {
      format,
      startDate: filters.startDate,
      endDate: filters.endDate,
      virtualKeyIds: filters.virtualKeyIds,
      models: filters.models,
      providers: filters.providers,
      includeMetadata: filters.includeMetadata,
    };

    return this.get<Blob>('/api/analytics/export', params, {
      responseType: 'blob',
    });
  }

  async exportAnalytics(
    filters: AnalyticsFilters,
    format: 'csv' | 'excel' | 'json'
  ): Promise<Blob> {
    // Deprecated: Use export() instead
    return this.export(filters, format);
  }

  detectAnomalies(_dateRange: DateRange): Promise<AnomalyDto[]> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'detectAnomalies requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/analytics/anomalies'
    );
  }

  streamRequestLogs(
    _filters?: RequestLogFilters,
    _onMessage?: (log: RequestLogDto) => void,
    _onError?: (error: Error) => void
  ): never {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'streamRequestLogs requires Admin API SSE endpoint implementation. ' +
        'Consider implementing GET /api/logs/stream as Server-Sent Events'
    );
  }

  generateReport(
    _type: 'cost' | 'usage' | 'performance',
    _dateRange: DateRange,
    _options?: AnalyticsOptions
  ): never {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'generateReport requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/analytics/generate-report'
    );
  }
}