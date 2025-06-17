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

  async exportAnalytics(
    _filters: AnalyticsFilters,
    _format: 'csv' | 'excel' | 'json'
  ): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportAnalytics requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/analytics/export'
    );
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