'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { DateRange } from '@knn_labs/conduit-admin-client';
import type { CostByModelResponse, CostByKeyResponse } from '@/types/sdk-extensions';

// Query key factory for Analytics API
export const analyticsApiKeys = {
  all: ['analytics-api'] as const,
  costs: () => [...analyticsApiKeys.all, 'costs'] as const,
  usage: () => [...analyticsApiKeys.all, 'usage'] as const,
  trends: () => [...analyticsApiKeys.all, 'trends'] as const,
  providers: () => [...analyticsApiKeys.all, 'providers'] as const,
  models: () => [...analyticsApiKeys.all, 'models'] as const,
  virtualKeys: () => [...analyticsApiKeys.all, 'virtual-keys'] as const,
} as const;

export interface CostTrendData {
  date: string;
  spend: number;
  requests: number;
  tokens: number;
  averageCostPerRequest: number;
}

export interface ProviderCostData {
  provider: string;
  spend: number;
  requests: number;
  percentage: number;
  models: string[];
  averageCost: number;
}

export interface ModelCostData {
  model: string;
  provider: string;
  spend: number;
  requests: number;
  tokens: number;
  averageCostPerRequest: number;
  averageCostPerToken: number;
}

export interface VirtualKeyCostData {
  keyId: string;
  keyName: string;
  spend: number;
  budget: number;
  requests: number;
  usagePercentage: number;
  isOverBudget: boolean;
  lastActivity: string;
  topModels: string[];
}

export interface CostSummary {
  totalSpend: number;
  totalBudget: number;
  totalRequests: number;
  totalTokens: number;
  activeVirtualKeys: number;
  averageCostPerRequest: number;
  averageCostPerToken: number;
  spendTrend: number; // percentage change from previous period
  requestTrend: number;
}

export interface TimeRangeFilter {
  range: '1h' | '24h' | '7d' | '30d' | '90d' | 'custom';
  startDate?: string;
  endDate?: string;
}

// Helper functions for SDK integration
function convertTimeRangeToDateRange(timeRange: TimeRangeFilter): DateRange {
  if (timeRange.range === 'custom' && timeRange.startDate && timeRange.endDate) {
    return {
      startDate: timeRange.startDate,
      endDate: timeRange.endDate,
    };
  }
  
  const now = new Date();
  let startDate: Date;
  
  switch (timeRange.range) {
    case '1h':
      startDate = new Date(now.getTime() - 60 * 60 * 1000);
      break;
    case '24h':
      startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
      break;
    case '7d':
      startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      break;
    case '30d':
      startDate = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      break;
    case '90d':
      startDate = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
      break;
    default:
      startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
  }
  
  return {
    startDate: startDate.toISOString(),
    endDate: now.toISOString(),
  };
}

function getGroupByFromTimeRange(timeRange: TimeRangeFilter): 'hour' | 'day' | 'week' | 'month' {
  switch (timeRange.range) {
    case '1h':
    case '24h':
      return 'hour';
    case '7d':
      return 'day';
    case '30d':
      return 'day';
    case '90d':
      return 'week';
    default:
      return 'day';
  }
}

// Cost Analytics Hooks
export function useCostSummary(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...analyticsApiKeys.costs(), 'summary', timeRange],
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
    queryKey: [...analyticsApiKeys.trends(), timeRange],
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
    queryKey: [...analyticsApiKeys.providers(), timeRange],
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
    queryKey: [...analyticsApiKeys.models(), timeRange],
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
          let provider = 'Unknown';
          const modelLower = model.modelId.toLowerCase();
          if (modelLower.includes('gpt') || modelLower.includes('dall-e') || modelLower.includes('whisper')) {
            provider = modelLower.includes('azure') ? 'Azure OpenAI' : 'OpenAI';
          } else if (modelLower.includes('claude')) {
            provider = 'Anthropic';
          } else if (modelLower.includes('minimax')) {
            provider = 'MiniMax';
          }
          
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
    queryKey: [...analyticsApiKeys.virtualKeys(), timeRange],
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
export function useExportCostData() {
  const _queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      type, 
      timeRange, 
      format = 'csv' 
    }: { 
      type: 'summary' | 'trends' | 'providers' | 'models' | 'virtual-keys';
      timeRange: TimeRangeFilter;
      format?: 'csv' | 'json' | 'xlsx';
    }) => {
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
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const filename = `cost-${type}-${timestamp}.${format}`;
        
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
    queryKey: [...analyticsApiKeys.all, 'alerts'],
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