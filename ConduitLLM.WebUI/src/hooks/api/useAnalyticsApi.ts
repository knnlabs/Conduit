'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { costAnalyticsKeys } from '@/lib/queries/analytics-query-keys';
import { 
  convertTimeRangeToDateRange, 
  getGroupByFromTimeRange,
  getProviderFromModel,
  createExportFilename
} from '@/lib/utils/analytics-helpers';
import type {
  TimeRangeFilter,
  CostTrendData,
  ProviderCostData,
  ModelCostData,
  VirtualKeyCostData,
  CostSummary,
  CostByModelResponse,
  CostByKeyResponse,
  ExportResponse,
  BaseExportRequest,
} from '@/types/analytics-types';

// Re-export types for consumers
export type { TimeRangeFilter };

// Cost Analytics Hooks
export function useCostSummary(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: costAnalyticsKeys.summary(timeRange),
    queryFn: async (): Promise<CostSummary> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getCostSummary(dateRange);
        
        // Transform SDK response to UI format
        const costSummary: CostSummary = {
          totalSpend: response.totalCost,
          totalBudget: 0, // TODO: Get budget data from virtual keys
          totalRequests: response.costByKey.reduce((sum, key) => sum + key.requestCount, 0),
          totalTokens: response.totalInputTokens + response.totalOutputTokens,
          activeVirtualKeys: response.costByKey.length,
          averageCostPerRequest: response.costByKey.reduce((sum, key) => sum + key.requestCount, 0) > 0 
            ? response.totalCost / response.costByKey.reduce((sum, key) => sum + key.requestCount, 0)
            : 0,
          averageCostPerToken: (response.totalInputTokens + response.totalOutputTokens) > 0
            ? response.totalCost / (response.totalInputTokens + response.totalOutputTokens)
            : 0,
          spendTrend: 0, // TODO: Calculate trend from historical data
          requestTrend: 0,
        };

        return costSummary;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch cost summary');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch cost summary';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    refetchInterval: 10 * 60 * 1000, // 10 minutes
  });
}

export function useCostTrends(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: costAnalyticsKeys.trends(timeRange),
    queryFn: async (): Promise<CostTrendData[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const groupBy = getGroupByFromTimeRange(timeRange);
        const response = await client.analytics.getCostByPeriod(dateRange, groupBy);
        
        // Transform SDK response to UI format
        const costTrends: CostTrendData[] = response.periods.map(period => ({
          date: period.startDate.split('T')[0], // Extract date part
          spend: period.totalCost,
          requests: period.requestCount,
          tokens: period.inputTokens + period.outputTokens,
          averageCostPerRequest: period.requestCount > 0 ? period.totalCost / period.requestCount : 0,
        }));

        return costTrends;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch cost trends');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch cost trends';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useProviderCosts(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: costAnalyticsKeys.providers(timeRange),
    queryFn: async (): Promise<ProviderCostData[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getCostSummary(dateRange);
        
        const totalCost = response.totalCost;
        
        // Transform SDK response to UI format
        const providerCosts: ProviderCostData[] = response.costByProvider.map(provider => {
          const percentage = totalCost > 0 ? (provider.cost / totalCost) * 100 : 0;
          const averageCost = provider.requestCount > 0 ? provider.cost / provider.requestCount : 0;
          
          // Get models for this provider from costByModel
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
        });

        return providerCosts.sort((a, b) => b.spend - a.spend);
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch provider costs');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch provider costs';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useModelCosts(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: costAnalyticsKeys.models(timeRange),
    queryFn: async (): Promise<ModelCostData[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getCostByModel(dateRange) as CostByModelResponse;
        
        // Transform SDK response to UI format
        const modelCosts: ModelCostData[] = response.models.map(model => {
          const totalTokens = model.totalTokens;
          const averageCostPerToken = totalTokens > 0 ? model.totalCost / totalTokens : 0;
          const averageCostPerRequest = model.totalRequests > 0 ? model.totalCost / model.totalRequests : 0;
          
          // Determine provider from model name
          const provider = getProviderFromModel(model.modelId);
          
          return {
            model: model.modelId,
            provider,
            spend: model.totalCost,
            requests: model.totalRequests,
            tokens: totalTokens,
            averageCostPerRequest: Math.round(averageCostPerRequest * 10000) / 10000,
            averageCostPerToken: Math.round(averageCostPerToken * 10000000) / 10000000,
          };
        });

        return modelCosts.sort((a, b) => b.spend - a.spend);
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch model costs');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch model costs';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useVirtualKeyCosts(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: costAnalyticsKeys.virtualKeys(timeRange),
    queryFn: async (): Promise<VirtualKeyCostData[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getCostByKey(dateRange) as CostByKeyResponse;
        
        // Get detailed data for each key
        const virtualKeyCostsPromises = response.keys.map(async (key) => {
          try {
            const keyUsage = await client.analytics.getKeyUsage(key.keyId, dateRange);
            
            return {
              keyId: key.keyId.toString(),
              keyName: keyUsage.keyName,
              spend: keyUsage.totalCost,
              budget: keyUsage.budgetUsed + keyUsage.budgetRemaining,
              requests: keyUsage.totalRequests,
              usagePercentage: keyUsage.budgetUsed + keyUsage.budgetRemaining > 0 
                ? (keyUsage.budgetUsed / (keyUsage.budgetUsed + keyUsage.budgetRemaining)) * 100 
                : 0,
              isOverBudget: keyUsage.budgetRemaining < 0,
              lastActivity: keyUsage.lastUsed,
              topModels: keyUsage.popularModels.map(model => model.modelId),
            };
          } catch (_error) {
            // If detailed data fails, return basic data
            return {
              keyId: key.keyId.toString(),
              keyName: key.keyName,
              spend: key.totalCost,
              budget: 1000, // Default budget
              requests: key.totalRequests,
              usagePercentage: (key.totalCost / 1000) * 100,
              isOverBudget: key.totalCost > 1000,
              lastActivity: new Date().toISOString(),
              topModels: [],
            };
          }
        });
        
        const virtualKeyCosts = await Promise.all(virtualKeyCostsPromises);
        return virtualKeyCosts.sort((a, b) => b.spend - a.spend);
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch virtual key costs');
        const errorMessage = error instanceof Error ? error.message : 'Failed to fetch virtual key costs';
        throw new Error(errorMessage);
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

// Export cost data
interface CostExportRequest extends BaseExportRequest {
  type: 'summary' | 'trends' | 'providers' | 'models' | 'virtual-keys';
}

export function useExportCostData() {
  const _queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      type, 
      timeRange, 
      format = 'csv' 
    }: CostExportRequest): Promise<ExportResponse> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        
        // Use the SDK export functionality
        const filters = {
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          includeMetadata: true,
        };
        
        const blob = await client.analytics.export(filters, format as 'csv' | 'json' | 'excel');
        
        // Create download URL
        const url = URL.createObjectURL(blob);
        const filename = createExportFilename('cost', type, format);
        
        return {
          filename,
          url,
          size: blob.size,
        };
      } catch (error: unknown) {
        reportError(error, 'Failed to export cost data');
        const errorMessage = error instanceof Error ? error.message : 'Failed to export cost data';
        throw new Error(errorMessage);
      }
    },
  });
}

// Real-time cost alerts
export function useCostAlerts() {
  return useQuery({
    queryKey: costAnalyticsKeys.alerts(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        // Get virtual key costs to check for budget alerts
        const today = new Date();
        const startDate = new Date(today.setHours(0, 0, 0, 0)).toISOString();
        const endDate = new Date(today.setHours(23, 59, 59, 999)).toISOString();
        
        const response = await client.analytics.getCostByKey({ startDate, endDate });
        
        const alerts: Array<{
          id: string;
          type: string;
          severity: string;
          message: string;
          virtualKeyId: string;
          createdAt: string;
          acknowledged: boolean;
        }> = [];
        
        // Check for budget alerts
        for (const key of response.keys) {
          try {
            const keyUsage = await client.analytics.getKeyUsage(key.keyId, { startDate, endDate });
            const totalBudget = keyUsage.budgetUsed + keyUsage.budgetRemaining;
            const usagePercentage = totalBudget > 0 ? (keyUsage.budgetUsed / totalBudget) * 100 : 0;
            
            if (usagePercentage > 100) {
              alerts.push({
                id: `budget_exceeded_${key.keyId}`,
                type: 'budget_exceeded',
                severity: 'high',
                message: `Virtual key "${keyUsage.keyName}" has exceeded its budget by ${(usagePercentage - 100).toFixed(1)}%`,
                virtualKeyId: key.keyId.toString(),
                createdAt: new Date().toISOString(),
                acknowledged: false,
              });
            } else if (usagePercentage > 90) {
              alerts.push({
                id: `budget_warning_${key.keyId}`,
                type: 'budget_warning',
                severity: 'medium',
                message: `Virtual key "${keyUsage.keyName}" is at ${usagePercentage.toFixed(1)}% of budget`,
                virtualKeyId: key.keyId.toString(),
                createdAt: new Date().toISOString(),
                acknowledged: false,
              });
            }
          } catch (_error) {
            // Skip this key if we can't get detailed usage
            continue;
          }
        }

        return alerts;
      } catch (error: unknown) {
        reportError(error, 'Failed to fetch cost alerts');
        return [];
      }
    },
    staleTime: 1 * 60 * 1000, // 1 minute
    refetchInterval: 2 * 60 * 1000, // 2 minutes
  });
}