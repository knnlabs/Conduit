import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  RequestLogParams,
  RequestLogPage,
  RequestLogDto,
  ModelUsageParams,
  ModelUsageAnalytics,
  CostParams,
  CostAnalytics,
  CostSummaryDto,
  CostByPeriodDto,
  ExportParams,
  ExportResult,
  ComprehensiveUsageAnalyticsParams,
  ComprehensiveUsageAnalytics,
  RequestLogStatisticsParams,
  RequestLogStatistics,
  SystemPerformanceMetricsParams,
  SystemPerformanceMetrics,
  ProviderHealthSummaryParams,
  ProviderHealthAnalyticsSummary,
  ComprehensiveVirtualKeyAnalyticsParams,
  ComprehensiveVirtualKeyAnalytics,
} from '../models/analytics';

/**
 * Type-safe Analytics service using native fetch
 */
export class FetchAnalyticsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get paginated request logs
   */
  async getRequestLogs(params?: RequestLogParams, config?: RequestConfig): Promise<RequestLogPage> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.page) queryParams.append('page', params.page.toString());
      if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.virtualKeyId) queryParams.append('virtualKeyId', params.virtualKeyId);
      if (params.provider) queryParams.append('provider', params.provider);
      if (params.model) queryParams.append('model', params.model);
      if (params.statusCode) queryParams.append('statusCode', params.statusCode.toString());
      if (params.minLatency) queryParams.append('minLatency', params.minLatency.toString());
      if (params.maxLatency) queryParams.append('maxLatency', params.maxLatency.toString());
      if (params.sortBy) queryParams.append('sortBy', params.sortBy);
      if (params.sortOrder) queryParams.append('sortOrder', params.sortOrder);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.REQUEST_LOGS}?${queryString}` : ENDPOINTS.ANALYTICS.REQUEST_LOGS;

    return this.client['get']<RequestLogPage>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific request log by ID
   */
  async getRequestLogById(id: string, config?: RequestConfig): Promise<RequestLogDto> {
    return this.client['get']<RequestLogDto>(
      ENDPOINTS.ANALYTICS.REQUEST_LOG_BY_ID(id),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export request logs
   */
  async exportRequestLogs(params: ExportParams, config?: RequestConfig): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      ENDPOINTS.ANALYTICS.EXPORT_REQUEST_LOGS,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get comprehensive usage analytics for WebUI dashboards
   * This method provides all analytics data needed for dashboard views in a single call
   */
  async getUsageAnalytics(params: ComprehensiveUsageAnalyticsParams, config?: RequestConfig): Promise<ComprehensiveUsageAnalytics> {
    const queryParams = new URLSearchParams();
    
    queryParams.append('timeRange', params.timeRange);
    if (params.includeTimeSeries !== undefined) queryParams.append('includeTimeSeries', params.includeTimeSeries.toString());
    if (params.includeProviderBreakdown !== undefined) queryParams.append('includeProviderBreakdown', params.includeProviderBreakdown.toString());
    if (params.includeModelBreakdown !== undefined) queryParams.append('includeModelBreakdown', params.includeModelBreakdown.toString());
    if (params.includeVirtualKeyBreakdown !== undefined) queryParams.append('includeVirtualKeyBreakdown', params.includeVirtualKeyBreakdown.toString());
    if (params.includeEndpointBreakdown !== undefined) queryParams.append('includeEndpointBreakdown', params.includeEndpointBreakdown.toString());

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.USAGE_ANALYTICS}?${queryString}` : ENDPOINTS.ANALYTICS.USAGE_ANALYTICS;

    return this.client['get']<ComprehensiveUsageAnalytics>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export usage analytics
   */
  async exportUsageAnalytics(params: ExportParams, config?: RequestConfig): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      ENDPOINTS.ANALYTICS.EXPORT_USAGE,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get comprehensive virtual key analytics including usage patterns and quotas
   */
  async getVirtualKeyAnalytics(params?: ComprehensiveVirtualKeyAnalyticsParams, config?: RequestConfig): Promise<ComprehensiveVirtualKeyAnalytics> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.timeRange) queryParams.append('timeRange', params.timeRange);
      if (params.virtualKeyIds?.length) {
        params.virtualKeyIds.forEach(id => queryParams.append('virtualKeyIds', id));
      }
      if (params.includeUsagePatterns !== undefined) queryParams.append('includeUsagePatterns', params.includeUsagePatterns.toString());
      if (params.includeQuotaStatus !== undefined) queryParams.append('includeQuotaStatus', params.includeQuotaStatus.toString());
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.VIRTUAL_KEY_ANALYTICS}?${queryString}` : ENDPOINTS.ANALYTICS.VIRTUAL_KEY_ANALYTICS;

    return this.client['get']<ComprehensiveVirtualKeyAnalytics>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export virtual key analytics
   */
  async exportVirtualKeyAnalytics(params: ExportParams, config?: RequestConfig): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      ENDPOINTS.ANALYTICS.EXPORT_VIRTUAL_KEY,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get model usage analytics
   */
  async getModelUsageAnalytics(params?: ModelUsageParams, config?: RequestConfig): Promise<ModelUsageAnalytics> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.models?.length) {
        params.models.forEach(m => queryParams.append('models', m));
      }
      if (params.providers?.length) {
        params.providers.forEach(p => queryParams.append('providers', p));
      }
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.MODEL_USAGE_ANALYTICS}?${queryString}` : ENDPOINTS.ANALYTICS.MODEL_USAGE_ANALYTICS;

    return this.client['get']<ModelUsageAnalytics>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cost analytics
   */
  async getCostAnalytics(params?: CostParams, config?: RequestConfig): Promise<CostAnalytics> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.groupBy) queryParams.append('groupBy', params.groupBy);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.COST_ANALYTICS}?${queryString}` : ENDPOINTS.ANALYTICS.COST_ANALYTICS;

    return this.client['get']<CostAnalytics>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Export cost analytics
   */
  async exportCostAnalytics(params: ExportParams, config?: RequestConfig): Promise<ExportResult> {
    return this.client['post']<ExportResult, ExportParams>(
      ENDPOINTS.ANALYTICS.EXPORT_COST,
      params,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cost summary (legacy endpoint)
   */
  async getCostSummary(startDate?: string, endDate?: string, config?: RequestConfig): Promise<CostSummaryDto> {
    const queryParams = new URLSearchParams();
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.COST_SUMMARY}?${queryString}` : ENDPOINTS.ANALYTICS.COST_SUMMARY;

    return this.client['get']<CostSummaryDto>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get cost by period (legacy endpoint)
   */
  async getCostByPeriod(
    period: 'hour' | 'day' | 'week' | 'month',
    startDate?: string,
    endDate?: string,
    config?: RequestConfig
  ): Promise<CostByPeriodDto> {
    const queryParams = new URLSearchParams({ period });
    if (startDate) queryParams.append('startDate', startDate);
    if (endDate) queryParams.append('endDate', endDate);

    return this.client['get']<CostByPeriodDto>(
      `${ENDPOINTS.ANALYTICS.COST_BY_PERIOD}?${queryParams.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to get export status
   */
  async getExportStatus(exportId: string, config?: RequestConfig): Promise<ExportResult> {
    return this.client['get']<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_STATUS(exportId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Helper method to download export
   */
  async downloadExport(exportId: string, config?: RequestConfig): Promise<Blob> {
    const response = await this.client['get']<Response>(
      ENDPOINTS.ANALYTICS.EXPORT_DOWNLOAD(exportId),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
        responseType: 'raw',
      }
    );

    return response.blob();
  }

  /**
   * Helper method to format date range
   */
  formatDateRange(days: number): { startDate: string; endDate: string } {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - days);

    return {
      startDate: startDate.toISOString().split('T')[0],
      endDate: endDate.toISOString().split('T')[0],
    };
  }

  /**
   * Helper method to calculate growth rate
   */
  calculateGrowthRate(current: number, previous: number): number {
    if (previous === 0) return current > 0 ? 100 : 0;
    return ((current - previous) / previous) * 100;
  }

  /**
   * Helper method to get top items from analytics
   */
  getTopItems<T extends { value: number }>(items: T[], limit: number = 10): T[] {
    return [...items].sort((a, b) => b.value - a.value).slice(0, limit);
  }

  /**
   * Helper method to aggregate time series data
   */
  aggregateTimeSeries(
    data: Array<{ timestamp: string; value: number }>,
    groupBy: 'hour' | 'day' | 'week' | 'month'
  ): Array<{ period: string; value: number }> {
    const grouped = new Map<string, number>();

    data.forEach(item => {
      const date = new Date(item.timestamp);
      let period: string;

      switch (groupBy) {
        case 'hour':
          period = `${date.toISOString().slice(0, 13)}:00`;
          break;
        case 'day':
          period = date.toISOString().slice(0, 10);
          break;
        case 'week': {
          const weekStart = new Date(date);
          weekStart.setDate(date.getDate() - date.getDay());
          period = weekStart.toISOString().slice(0, 10);
          break;
        }
        case 'month':
          period = date.toISOString().slice(0, 7);
          break;
      }

      grouped.set(period, (grouped.get(period) || 0) + item.value);
    });

    return Array.from(grouped.entries())
      .map(([period, value]) => ({ period, value }))
      .sort((a, b) => a.period.localeCompare(b.period));
  }

  /**
   * Helper method to validate date range
   */
  validateDateRange(startDate?: string, endDate?: string): boolean {
    if (!startDate || !endDate) return true;

    const start = new Date(startDate);
    const end = new Date(endDate);

    return start <= end && end <= new Date();
  }

  /**
   * Get aggregated request log statistics without fetching all logs
   */
  async getRequestLogStatistics(params?: RequestLogStatisticsParams, config?: RequestConfig): Promise<RequestLogStatistics> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.startDate) queryParams.append('startDate', params.startDate);
      if (params.endDate) queryParams.append('endDate', params.endDate);
      if (params.virtualKeyId) queryParams.append('virtualKeyId', params.virtualKeyId);
      if (params.provider) queryParams.append('provider', params.provider);
      if (params.model) queryParams.append('model', params.model);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.REQUEST_LOG_STATS}?${queryString}` : ENDPOINTS.ANALYTICS.REQUEST_LOG_STATS;

    return this.client['get']<RequestLogStatistics>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get system performance metrics, service status, and alerts
   */
  async getSystemPerformanceMetrics(params?: SystemPerformanceMetricsParams, config?: RequestConfig): Promise<SystemPerformanceMetrics> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.timeRange) queryParams.append('timeRange', params.timeRange);
      if (params.includeServiceHealth !== undefined) queryParams.append('includeServiceHealth', params.includeServiceHealth.toString());
      if (params.includeQueueMetrics !== undefined) queryParams.append('includeQueueMetrics', params.includeQueueMetrics.toString());
      if (params.includeDatabaseMetrics !== undefined) queryParams.append('includeDatabaseMetrics', params.includeDatabaseMetrics.toString());
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.SYSTEM_PERFORMANCE}?${queryString}` : ENDPOINTS.ANALYTICS.SYSTEM_PERFORMANCE;

    return this.client['get']<SystemPerformanceMetrics>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get comprehensive provider health data including uptime, response times, and incidents
   */
  async getProviderHealthSummary(params?: ProviderHealthSummaryParams, config?: RequestConfig): Promise<ProviderHealthAnalyticsSummary> {
    const queryParams = new URLSearchParams();
    
    if (params) {
      if (params.includeHistory !== undefined) queryParams.append('includeHistory', params.includeHistory.toString());
      if (params.historyDays !== undefined) queryParams.append('historyDays', params.historyDays.toString());
      if (params.includeIncidents !== undefined) queryParams.append('includeIncidents', params.includeIncidents.toString());
    }

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ANALYTICS.PROVIDER_HEALTH_SUMMARY}?${queryString}` : ENDPOINTS.ANALYTICS.PROVIDER_HEALTH_SUMMARY;

    return this.client['get']<ProviderHealthAnalyticsSummary>(
      url,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Enhanced version of getVirtualKeyAnalytics that provides additional data fields
   * including change percentages, endpoint breakdowns, and per-key time series.
   * This method enriches the standard analytics response with calculated fields
   * and aggregated data from request logs.
   * 
   * @param params - Analytics parameters including time range and filters
   * @param config - Optional request configuration
   * @returns Enhanced virtual key analytics with all required fields
   * @since Issue #450 - Enhanced Virtual Key Analytics
   */
  async getVirtualKeyAnalyticsEnhanced(
    params?: ComprehensiveVirtualKeyAnalyticsParams, 
    config?: RequestConfig
  ): Promise<ComprehensiveVirtualKeyAnalytics> {
    try {
      // Get base analytics data
      const baseAnalytics = await this.getVirtualKeyAnalytics(params, config);
      
      // TODO: When API supports native change percentages, remove this calculation
      // Calculate time ranges for comparison periods
      const timeRange = params?.timeRange || '7d';
      const currentPeriodDays = this.parseTimeRange(timeRange);
      const comparePeriodStart = new Date();
      comparePeriodStart.setDate(comparePeriodStart.getDate() - (currentPeriodDays * 2));
      const comparePeriodEnd = new Date();
      comparePeriodEnd.setDate(comparePeriodEnd.getDate() - currentPeriodDays);
      
      // Get comparison period data for change calculations
      const comparisonParams = {
        ...params,
        timeRange: `${currentPeriodDays}d`, // Same duration for comparison
      };
      
      // TODO: This makes an extra API call. When API supports change percentages natively, remove this
      const comparisonAnalytics = await this.getVirtualKeyAnalytics(comparisonParams, config);
      
      // Get request logs for endpoint breakdown if we have virtual key IDs
      let endpointBreakdowns: Record<string, any[]> = {};
      if (params?.virtualKeyIds?.length) {
        // TODO: This is inefficient for large datasets. API should provide endpoint breakdown directly
        const requestLogs = await this.getRequestLogs({
          virtualKeyId: params.virtualKeyIds[0], // API only supports single virtualKeyId
          startDate: comparePeriodEnd.toISOString().split('T')[0],
          endDate: new Date().toISOString().split('T')[0],
          pageSize: 1000, // Limited to avoid performance issues
        }, config);
        
        // Aggregate endpoint data by virtual key
        endpointBreakdowns = this.aggregateEndpointData(requestLogs.items);
      }
      
      // Get virtual keys service instance to fetch metadata
      const virtualKeysService = new (await import('./FetchVirtualKeyService')).FetchVirtualKeyService(this.client);
      
      // Get time series data
      const usageAnalytics = await this.getUsageAnalytics({
        timeRange: timeRange,
        includeTimeSeries: true,
        includeVirtualKeyBreakdown: true,
      }, config);
      
      // Enhance each virtual key with calculated fields
      const enhancedVirtualKeys = await Promise.all(
        baseAnalytics.virtualKeys.map(async (vkDetail) => {
          // Find comparison data for this key
          const comparisonKey = comparisonAnalytics.virtualKeys.find(
            ck => ck.keyId === vkDetail.keyId
          );
          
          // Get virtual key details to access metadata
          let tokenLimit: number | undefined;
          let tokenPeriod: string | undefined;
          try {
            const keyDetails = await virtualKeysService.get(vkDetail.keyId, config);
            if (keyDetails.metadata) {
              const metadata = JSON.parse(keyDetails.metadata) as any;
              tokenLimit = metadata.tokenLimit;
              tokenPeriod = metadata.tokenPeriod;
            }
          } catch (error) {
            // TODO: Log error when proper logging is available
          }
          
          // Calculate percentage changes
          const requestsChange = comparisonKey 
            ? this.calculateGrowthRate(vkDetail.usage.requests, comparisonKey.usage.requests)
            : 0;
          const tokensChange = comparisonKey
            ? this.calculateGrowthRate(vkDetail.usage.tokens, comparisonKey.usage.tokens)
            : 0;
          const costChange = comparisonKey
            ? this.calculateGrowthRate(vkDetail.usage.cost, comparisonKey.usage.cost)
            : 0;
          
          // Get endpoint breakdown for this key
          const endpoints = endpointBreakdowns[vkDetail.keyId] || [];
          
          // TODO: Per-key time series should come from API. Currently using aggregated data
          // This is a workaround that distributes the global time series proportionally
          const keyTimeSeries = usageAnalytics.timeSeries?.map(point => ({
            timestamp: point.timestamp,
            requests: Math.floor(point.requests * (vkDetail.usage.requests / baseAnalytics.aggregateMetrics.totalRequests)),
            tokens: Math.floor(point.tokens * (vkDetail.usage.tokens / baseAnalytics.aggregateMetrics.totalRequests)),
            cost: point.cost * (vkDetail.usage.cost / baseAnalytics.aggregateMetrics.totalCost),
            errorRate: vkDetail.performance.errorRate, // Use key's error rate
          })) || [];
          
          return {
            ...vkDetail,
            usage: {
              ...vkDetail.usage,
              requestsChange,
              tokensChange,
              costChange,
              errorRate: vkDetail.performance.errorRate,
            },
            endpointBreakdown: endpoints,
            timeSeries: keyTimeSeries,
            tokenLimit,
            tokenPeriod,
          };
        })
      );
      
      return {
        ...baseAnalytics,
        virtualKeys: enhancedVirtualKeys,
      };
    } catch (error) {
      // If enhancement fails, return base analytics with default values
      // TODO: Implement proper error logging
      const baseAnalytics = await this.getVirtualKeyAnalytics(params, config);
      
      return {
        ...baseAnalytics,
        virtualKeys: baseAnalytics.virtualKeys.map(vk => ({
          ...vk,
          usage: {
            ...vk.usage,
            requestsChange: 0,
            tokensChange: 0,
            costChange: 0,
            errorRate: vk.performance?.errorRate || 0,
          },
          endpointBreakdown: [],
          timeSeries: [],
          tokenLimit: undefined,
          tokenPeriod: undefined,
        })),
      };
    }
  }
  
  /**
   * Helper method to parse time range string to days
   * @private
   */
  private parseTimeRange(timeRange: string): number {
    const match = timeRange.match(/^(\d+)([dhwm])$/);
    if (!match) return 7; // Default to 7 days
    
    const value = parseInt(match[1]);
    const unit = match[2];
    
    switch (unit) {
      case 'h': return value / 24;
      case 'd': return value;
      case 'w': return value * 7;
      case 'm': return value * 30;
      default: return 7;
    }
  }
  
  /**
   * Helper method to aggregate endpoint data from request logs
   * @private
   */
  private aggregateEndpointData(logs: RequestLogDto[]): Record<string, any[]> {
    const endpointsByKey: Record<string, Record<string, any>> = {};
    
    logs.forEach(log => {
      if (!log.virtualKeyId) return;
      
      const keyId = log.virtualKeyId.toString();
      if (!endpointsByKey[keyId]) {
        endpointsByKey[keyId] = {};
      }
      
      // Extract endpoint from metadata or use model as fallback
      // TODO: API should provide endpoint information directly in request logs
      const endpoint = (log.metadata as any)?.endpoint || `/v1/${log.model}`;
      
      if (!endpointsByKey[keyId][endpoint]) {
        endpointsByKey[keyId][endpoint] = {
          path: endpoint,
          requests: 0,
          totalDuration: 0,
          errors: 0,
        };
      }
      
      endpointsByKey[keyId][endpoint].requests++;
      endpointsByKey[keyId][endpoint].totalDuration += log.duration;
      if (log.status === 'error') {
        endpointsByKey[keyId][endpoint].errors++;
      }
    });
    
    // Convert to array format with calculated averages
    const result: Record<string, any[]> = {};
    Object.entries(endpointsByKey).forEach(([keyId, endpoints]) => {
      result[keyId] = Object.values(endpoints).map(ep => ({
        path: ep.path,
        requests: ep.requests,
        avgDuration: Math.round(ep.totalDuration / ep.requests),
        errorRate: (ep.errors / ep.requests) * 100,
      }));
    });
    
    return result;
  }
}