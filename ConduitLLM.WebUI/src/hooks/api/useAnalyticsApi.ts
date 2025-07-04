'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { DateRange } from '@knn_labs/conduit-admin-client';
import { queryKeys } from '@/lib/utils/query-keys';
import { analyticsHelpers, TimeRangeOption } from '@/lib/utils/analytics-helpers';
import { TimeRangeFilter } from './types/analytics-types';

// Re-export types for backward compatibility
export * from './types/analytics-types';

// Analytics Hook Options
export interface AnalyticsHookOptions {
  section: 'overview' | 'usage' | 'virtual-keys' | 'costs';
  timeRange?: TimeRangeOption;
  virtualKeyId?: string;
  aggregation?: 'hourly' | 'daily' | 'weekly' | 'monthly';
  filters?: Record<string, unknown>;
}

// Helper function to handle TimeRangeFilter or DateRange input
function parseTimeRangeInput(input: TimeRangeFilter | DateRange | null): {
  dateRange: DateRange | null;
  timeRange: TimeRangeOption | undefined;
} {
  if (!input) {
    return { dateRange: null, timeRange: undefined };
  }
  
  if ('range' in input) {
    // It's a TimeRangeFilter
    const filter = input as TimeRangeFilter;
    const helperRange = analyticsHelpers.convertTimeRangeToDateRange(filter.range);
    return {
      dateRange: { startDate: helperRange.startDate, endDate: helperRange.endDate } as DateRange,
      timeRange: filter.range
    };
  }
  
  // It's a DateRange
  return {
    dateRange: input as DateRange,
    timeRange: undefined
  };
}

// Cost Analytics Hooks (exported for backward compatibility)
export function useCostSummary(timeRangeOrDateRange: TimeRangeFilter | DateRange | null) {
  const { dateRange, timeRange } = parseTimeRangeInput(timeRangeOrDateRange);
  
  return useQuery({
    queryKey: queryKeys.analytics.costs.summary(timeRange?.toString()),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        if (!dateRange) {
          // Default to last 7 days if no date range provided
          const helperRange = analyticsHelpers.convertTimeRangeToDateRange('7d');
          const defaultRange = { startDate: helperRange.startDate, endDate: helperRange.endDate } as DateRange;
          const response = await client.analytics.getCostSummary(defaultRange);
          
          return {
            totalSpend: response.totalCost,
            totalBudget: 0,
            totalRequests: response.costByKey.reduce((sum, key) => sum + key.requestCount, 0),
            totalTokens: response.totalInputTokens + response.totalOutputTokens,
            activeVirtualKeys: response.costByKey.length,
            averageCostPerRequest: response.costByKey.reduce((sum, key) => sum + key.requestCount, 0) > 0 
              ? response.totalCost / response.costByKey.reduce((sum, key) => sum + key.requestCount, 0)
              : 0,
            averageCostPerToken: (response.totalInputTokens + response.totalOutputTokens) > 0
              ? response.totalCost / (response.totalInputTokens + response.totalOutputTokens)
              : 0,
            spendTrend: 0,
            requestTrend: 0,
          };
        }

        const response = await client.analytics.getCostSummary(dateRange);
        
        return {
          totalSpend: response.totalCost,
          totalBudget: 0,
          totalRequests: response.costByKey.reduce((sum, key) => sum + key.requestCount, 0),
          totalTokens: response.totalInputTokens + response.totalOutputTokens,
          activeVirtualKeys: response.costByKey.length,
          averageCostPerRequest: response.costByKey.reduce((sum, key) => sum + key.requestCount, 0) > 0 
            ? response.totalCost / response.costByKey.reduce((sum, key) => sum + key.requestCount, 0)
            : 0,
          averageCostPerToken: (response.totalInputTokens + response.totalOutputTokens) > 0
            ? response.totalCost / (response.totalInputTokens + response.totalOutputTokens)
            : 0,
          spendTrend: 0,
          requestTrend: 0,
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch cost summary');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch cost summary';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000,
    refetchInterval: 10 * 60 * 1000,
  });
}

export function useCostTrends(timeRangeOrDateRange: TimeRangeFilter | DateRange | null, groupByParam?: string) {
  const { dateRange, timeRange } = parseTimeRangeInput(timeRangeOrDateRange);
  const groupBy = groupByParam || (timeRange ? analyticsHelpers.getGroupByFromTimeRange(timeRange) : 'day');
  
  return useQuery({
    queryKey: queryKeys.analytics.costs.trends(timeRange?.toString(), groupBy),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        if (!dateRange) {
          const defaultRange = analyticsHelpers.convertTimeRangeToDateRange('7d');
          const response = await client.analytics.getCostByPeriod(defaultRange, groupBy as 'hour' | 'day' | 'week' | 'month');
          
          return response.periods.map(period => ({
            date: period.startDate.split('T')[0],
            spend: period.totalCost,
            requests: period.requestCount,
            tokens: period.inputTokens + period.outputTokens,
            averageCostPerRequest: period.requestCount > 0 ? period.totalCost / period.requestCount : 0,
          }));
        }

        const response = await client.analytics.getCostByPeriod(dateRange, groupBy as 'hour' | 'day' | 'week' | 'month');
        
        return response.periods.map(period => ({
          date: period.startDate.split('T')[0],
          spend: period.totalCost,
          requests: period.requestCount,
          tokens: period.inputTokens + period.outputTokens,
          averageCostPerRequest: period.requestCount > 0 ? period.totalCost / period.requestCount : 0,
        }));
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch cost trends');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch cost trends';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useProviderCosts(timeRangeOrDateRange: TimeRangeFilter | DateRange | null) {
  const { dateRange, timeRange } = parseTimeRangeInput(timeRangeOrDateRange);
  
  return useQuery({
    queryKey: queryKeys.analytics.costs.byProvider(timeRange?.toString()),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        const effectiveDateRange = dateRange || analyticsHelpers.convertTimeRangeToDateRange('7d');
        const response = await client.analytics.getCostSummary(effectiveDateRange);
        const totalCost = response.totalCost;
        
        return response.costByProvider.map(provider => {
          const percentage = totalCost > 0 ? (provider.cost / totalCost) * 100 : 0;
          const averageCost = provider.requestCount > 0 ? provider.cost / provider.requestCount : 0;
          
          const providerModels = response.costByModel
            .filter(model => {
              const modelLower = model.modelId.toLowerCase();
              const providerLower = provider.providerName.toLowerCase();
              return modelLower.includes(providerLower) || 
                     providerLower.includes(modelLower.split('-')[0]) ||
                     (providerLower.includes('openai') && (modelLower.includes('gpt') || modelLower.includes('dall-e'))) ||
                     (providerLower.includes('anthropic') && modelLower.includes('claude')) ||
                     (providerLower.includes('azure') && modelLower.includes('azure'));
            })
            .map(model => model.modelId);
          
          return {
            provider: provider.providerName,
            spend: provider.cost,
            requests: provider.requestCount,
            percentage: Math.round(percentage * 10) / 10,
            models: providerModels.length > 0 ? providerModels : ['Unknown'],
            averageCost: Math.round(averageCost * 10000) / 10000,
          };
        }).sort((a, b) => b.spend - a.spend);
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch provider costs');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch provider costs';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useModelCosts(timeRangeOrDateRange: TimeRangeFilter | DateRange | null) {
  const { dateRange, timeRange } = parseTimeRangeInput(timeRangeOrDateRange);
  
  return useQuery({
    queryKey: queryKeys.analytics.costs.byModel(timeRange?.toString()),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        const effectiveDateRange = dateRange || analyticsHelpers.convertTimeRangeToDateRange('7d');
        const response = await client.analytics.getCostSummary(effectiveDateRange);
        
        return response.costByModel.map(model => ({
          model: model.modelId,
          provider: model.modelId.split('-')[0] || 'Unknown',
          spend: model.cost,
          requests: model.requestCount,
          tokens: model.inputTokens + model.outputTokens,
          averageCostPerRequest: model.requestCount > 0 ? model.cost / model.requestCount : 0,
          averageCostPerToken: (model.inputTokens + model.outputTokens) > 0
            ? model.cost / (model.inputTokens + model.outputTokens)
            : 0,
        })).sort((a, b) => b.spend - a.spend);
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch model costs');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch model costs';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useVirtualKeyCosts(timeRangeOrDateRange: TimeRangeFilter | DateRange | null) {
  const { dateRange, timeRange } = parseTimeRangeInput(timeRangeOrDateRange);
  
  return useQuery({
    queryKey: queryKeys.analytics.costs.byVirtualKey(timeRange?.toString()),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        const effectiveDateRange = dateRange || analyticsHelpers.convertTimeRangeToDateRange('7d');
        const response = await client.analytics.getCostSummary(effectiveDateRange);
        
        return response.costByKey.map(key => ({
          keyId: key.keyId,
          keyName: key.keyId,
          spend: key.cost,
          budget: 0,
          requests: key.requestCount,
          usagePercentage: 0,
          isOverBudget: false,
          lastActivity: new Date().toISOString(),
          topModels: [],
        })).sort((a, b) => b.spend - a.spend);
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual key costs');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual key costs';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useCostAlerts() {
  return useQuery({
    queryKey: queryKeys.analytics.costs.alerts(),
    queryFn: async () => {
      // Mock data for now
      return [
        {
          id: '1',
          severity: 'high' as const,
          message: 'Monthly spend has exceeded 80% of budget',
          acknowledged: false,
          timestamp: new Date().toISOString()
        },
        {
          id: '2', 
          severity: 'medium' as const,
          message: 'OpenAI usage increased by 50% this week',
          acknowledged: false,
          timestamp: new Date().toISOString()
        }
      ];
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useExportCostData(timeRangeOrDateRange?: TimeRangeFilter | DateRange | null) {
  const queryClient = useQueryClient();
  const { dateRange, timeRange: _timeRange } = timeRangeOrDateRange ? parseTimeRangeInput(timeRangeOrDateRange) : { dateRange: null, timeRange: undefined };
  
  return useMutation({
    mutationFn: async (format: 'csv' | 'json' | { format: 'csv' | 'json'; type?: string }) => {
      try {
        const client = getAdminClient();
        const effectiveDateRange = dateRange || analyticsHelpers.convertTimeRangeToDateRange('7d');
        
        // Handle both old and new API formats
        const exportFormat = typeof format === 'string' ? format : format.format;
        
        // Fetch all cost data
        const [costSummary, costByPeriod] = await Promise.all([
          client.analytics.getCostSummary(effectiveDateRange),
          client.analytics.getCostByPeriod(effectiveDateRange, 'day')
        ]);

        // Format data based on requested format
        let data: string;
        let filename: string;
        let mimeType: string;

        if (exportFormat === 'csv') {
          // Convert to CSV
          const headers = ['Date', 'Total Cost', 'Requests', 'Input Tokens', 'Output Tokens'];
          const rows = costByPeriod.periods.map(period => [
            period.startDate.split('T')[0],
            period.totalCost.toString(),
            period.requestCount.toString(),
            period.inputTokens.toString(),
            period.outputTokens.toString()
          ]);
          
          data = [headers, ...rows].map(row => row.join(',')).join('\n');
          filename = `cost-analytics-${new Date().toISOString().split('T')[0]}.csv`;
          mimeType = 'text/csv';
        } else {
          // Convert to JSON
          data = JSON.stringify({
            summary: costSummary,
            periods: costByPeriod.periods
          }, null, 2);
          filename = `cost-analytics-${new Date().toISOString().split('T')[0]}.json`;
          mimeType = 'application/json';
        }

        // Create download
        const blob = new Blob([data], { type: mimeType });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);

        return { success: true, filename, url };
      } catch (error: unknown) {
        reportError(error, 'Failed to export cost data');
        const errorMessage = error instanceof Error ? error.message : 'Failed to export cost data';
        throw new Error(errorMessage);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.analytics.costs.all() });
    },
  });
}

// Usage Analytics Hooks (placeholders for now)
export function useUsageMetrics(timeRange: TimeRangeOption) {
  return useQuery({
    queryKey: queryKeys.analytics.usage.metrics(timeRange.toString()),
    queryFn: async () => ({
      totalRequests: 0,
      totalTokens: 0,
      totalUsers: 0,
      averageLatency: 0,
      errorRate: 0,
      requestsPerSecond: 0,
      tokensPerRequest: 0,
      successRate: 100,
      uniqueVirtualKeys: 0,
      activeProviders: 0,
      requestsTrend: 0,
      tokensTrend: 0,
      errorsTrend: 0,
      latencyTrend: 0,
    }),
    enabled: false,
  });
}

// Legacy exports
export const analyticsApiKeys = queryKeys.analytics;
export const usageAnalyticsApiKeys = queryKeys.analytics.usage;

// Main consolidated analytics hook (placeholder)
export function useAnalyticsApi(options: AnalyticsHookOptions) {
  return {
    section: options.section,
    message: 'Please use individual hooks like useCostSummary, useUsageMetrics, etc.'
  };
}