import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL, DEFAULT_PAGE_SIZE } from '../constants';
import {
  CostSummaryDto,
  CostByPeriodDto,
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
  ExportUsageParams,
  ExportCostParams,
  ExportVirtualKeyParams,
  ExportProviderParams,
  ExportSecurityParams,
  ExportResult,
  CreateExportScheduleDto,
  ExportSchedule,
  ExportHistory,
  ExportRequestLogsParams,
  RequestLogStatistics,
  RequestLogSummaryParams,
  RequestLogSummary,
  RequestLog,
  ExportStatus,
} from '../models/analyticsExport';
import { PagedResult } from '../models/security';
import { PaginatedResponse, DateRange } from '../models/common';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const dateRangeSchema = z.object({
  startDate: z.string().datetime(),
  endDate: z.string().datetime(),
});


export class AnalyticsService extends BaseApiClient {
  // Cost Analytics
  async getCostSummary(dateRange: DateRange): Promise<CostSummaryDto> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', error);
    }

    const cacheKey = this.getCacheKey('cost-summary', dateRange);
    return this.withCache(
      cacheKey,
      () => super.get<CostSummaryDto>(ENDPOINTS.ANALYTICS.COST_SUMMARY, dateRange),
      CACHE_TTL.SHORT
    );
  }

  async getCostByPeriod(
    dateRange: DateRange,
    groupBy: 'hour' | 'day' | 'week' | 'month' = 'day'
  ): Promise<CostByPeriodDto> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', error);
    }

    const params = { ...dateRange, groupBy };
    const cacheKey = this.getCacheKey('cost-by-period', params);
    return this.withCache(
      cacheKey,
      () => super.get<CostByPeriodDto>(ENDPOINTS.ANALYTICS.COST_BY_PERIOD, params),
      CACHE_TTL.SHORT
    );
  }

  async getCostByModel(dateRange: DateRange): Promise<{
    models: ModelUsageDto[];
    totalCost: number;
  }> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', error);
    }

    const cacheKey = this.getCacheKey('cost-by-model', dateRange);
    return this.withCache(
      cacheKey,
      () => super.get<{ models: ModelUsageDto[]; totalCost: number }>(
        ENDPOINTS.ANALYTICS.COST_BY_MODEL,
        dateRange
      ),
      CACHE_TTL.SHORT
    );
  }

  async getCostByKey(dateRange: DateRange): Promise<{
    keys: KeyUsageDto[];
    totalCost: number;
  }> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', error);
    }

    const cacheKey = this.getCacheKey('cost-by-key', dateRange);
    return this.withCache(
      cacheKey,
      () => super.get<{ keys: KeyUsageDto[]; totalCost: number }>(
        ENDPOINTS.ANALYTICS.COST_BY_KEY,
        dateRange
      ),
      CACHE_TTL.SHORT
    );
  }

  // Request Logs
  async getRequestLogs(
    filters?: RequestLogFilters
  ): Promise<PaginatedResponse<RequestLogDto>> {
    const params = {
      pageNumber: filters?.pageNumber || 1,
      pageSize: filters?.pageSize || DEFAULT_PAGE_SIZE,
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
      throw new ValidationError('Invalid date range', error);
    }

    const cacheKey = this.getCacheKey('usage-metrics', dateRange);
    return this.withCache(
      cacheKey,
      () => super.get<UsageMetricsDto>('/api/analytics/usage-metrics', dateRange),
      CACHE_TTL.SHORT
    );
  }

  async getModelUsage(modelId: string, dateRange: DateRange): Promise<ModelUsageDto> {
    try {
      dateRangeSchema.parse(dateRange);
    } catch (error) {
      throw new ValidationError('Invalid date range', error);
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
      throw new ValidationError('Invalid date range', error);
    }

    const params = { keyId, ...dateRange };
    const cacheKey = this.getCacheKey('key-usage', params);
    return this.withCache(
      cacheKey,
      () => super.get<KeyUsageDto>('/api/analytics/key-usage', params),
      CACHE_TTL.SHORT
    );
  }

  // Convenience methods
  async getTodayCosts(): Promise<CostSummaryDto> {
    const today = new Date();
    const startDate = new Date(today.setHours(0, 0, 0, 0)).toISOString();
    const endDate = new Date(today.setHours(23, 59, 59, 999)).toISOString();
    return this.getCostSummary({ startDate, endDate });
  }

  async getMonthCosts(): Promise<CostSummaryDto> {
    const now = new Date();
    const startDate = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
    const endDate = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59, 999).toISOString();
    return this.getCostSummary({ startDate, endDate });
  }

  // Specialized export methods
  async exportUsageAnalytics(params: ExportUsageParams): Promise<ExportResult> {
    const response = await this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_USAGE,
      params
    );
    return response;
  }

  async exportCostAnalytics(params: ExportCostParams): Promise<ExportResult> {
    const response = await this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_COST,
      params
    );
    return response;
  }

  async exportVirtualKeyAnalytics(params: ExportVirtualKeyParams): Promise<ExportResult> {
    const response = await this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_VIRTUAL_KEY,
      params
    );
    return response;
  }

  async exportProviderAnalytics(params: ExportProviderParams): Promise<ExportResult> {
    const response = await this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_PROVIDER,
      params
    );
    return response;
  }

  async exportSecurityAnalytics(params: ExportSecurityParams): Promise<ExportResult> {
    const response = await this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_SECURITY,
      params
    );
    return response;
  }

  // Request logs export and analytics
  async exportRequestLogs(params: ExportRequestLogsParams): Promise<ExportResult> {
    const response = await this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_REQUEST_LOGS,
      params
    );
    return response;
  }

  async getRequestLogStatistics(logs: RequestLog[]): Promise<RequestLogStatistics> {
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
    stats.totalCost = logs.reduce((sum, log) => sum + (log.cost || 0), 0);
    stats.totalTokensUsed = logs.reduce((sum, log) => sum + (log.tokensUsed?.total || 0), 0);
    const errorCount = logs.filter(l => l.error).length;
    stats.errorRate = (errorCount / logs.length) * 100;

    // Status code distribution
    logs.forEach(log => {
      stats.statusCodeDistribution[log.statusCode] = 
        (stats.statusCodeDistribution[log.statusCode] || 0) + 1;
    });

    // Endpoint distribution
    const endpointMap = new Map<string, { count: number; totalTime: number }>();
    logs.forEach(log => {
      const current = endpointMap.get(log.endpoint) || { count: 0, totalTime: 0 };
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

  async getRequestLogSummary(params: RequestLogSummaryParams): Promise<RequestLogSummary> {
    const response = await this.post<RequestLogSummary>(
      ENDPOINTS.ANALYTICS.REQUEST_LOG_SUMMARY,
      params
    );
    return response;
  }

  // Scheduled exports
  async createExportSchedule(schedule: CreateExportScheduleDto): Promise<ExportSchedule> {
    const response = await this.post<ExportSchedule>(
      ENDPOINTS.ANALYTICS.EXPORT_SCHEDULES,
      schedule
    );
    return response;
  }

  async listExportSchedules(): Promise<ExportSchedule[]> {
    return this.withCache(
      ENDPOINTS.ANALYTICS.EXPORT_SCHEDULES,
      () => this.get<ExportSchedule[]>(ENDPOINTS.ANALYTICS.EXPORT_SCHEDULES),
      CACHE_TTL.MEDIUM
    );
  }

  async deleteExportSchedule(id: string): Promise<void> {
    await this.delete(ENDPOINTS.ANALYTICS.EXPORT_SCHEDULE_BY_ID(id));
    
    // Invalidate cache
    if (this.cache) {
      await this.cache.delete(ENDPOINTS.ANALYTICS.EXPORT_SCHEDULES);
    }
  }

  // Export history
  async getExportHistory(params?: { limit?: number; offset?: number }): Promise<PagedResult<ExportHistory>> {
    const queryParams = new URLSearchParams();
    if (params?.limit) queryParams.append('limit', params.limit.toString());
    if (params?.offset) queryParams.append('offset', params.offset.toString());

    const url = `${ENDPOINTS.ANALYTICS.EXPORT_HISTORY}?${queryParams.toString()}`;
    return this.get<PagedResult<ExportHistory>>(url);
  }

  async getExportStatus(exportId: string): Promise<ExportStatus> {
    return this.get<ExportStatus>(ENDPOINTS.ANALYTICS.EXPORT_STATUS(exportId));
  }

  async downloadExport(exportId: string): Promise<Blob> {
    const response = await this.get<Blob>(ENDPOINTS.ANALYTICS.EXPORT_DOWNLOAD(exportId), {
      responseType: 'blob',
    } as any);
    return response;
  }

  // Stub methods
  async getDetailedCostBreakdown(_filters: AnalyticsFilters): Promise<any> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getDetailedCostBreakdown requires Admin API endpoint implementation. ' +
        'The WebUI currently calculates this client-side'
    );
  }

  async predictFutureCosts(_basePeriod: DateRange, _forecastDays: number): Promise<CostForecastDto> {
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

    const response = await this.axios.get('/api/analytics/export', {
      params,
      responseType: 'blob',
    });

    return response.data as Blob;
  }

  async exportAnalytics(
    filters: AnalyticsFilters,
    format: 'csv' | 'excel' | 'json'
  ): Promise<Blob> {
    // Deprecated: Use export() instead
    return this.export(filters, format);
  }

  async detectAnomalies(_dateRange: DateRange): Promise<AnomalyDto[]> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'detectAnomalies requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/analytics/anomalies'
    );
  }

  async streamRequestLogs(
    _filters?: RequestLogFilters,
    _onMessage?: (log: RequestLogDto) => void,
    _onError?: (error: Error) => void
  ): Promise<() => void> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'streamRequestLogs requires Admin API SSE endpoint implementation. ' +
        'Consider implementing GET /api/logs/stream as Server-Sent Events'
    );
  }

  async generateReport(
    _type: 'cost' | 'usage' | 'performance',
    _dateRange: DateRange,
    _options?: Record<string, any>
  ): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'generateReport requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/analytics/generate-report'
    );
  }
}