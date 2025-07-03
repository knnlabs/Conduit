import { useQuery } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { createAdminClient } from '@/lib/clients/conduit';
import type { 
  VirtualKeyCost, 
  CostDashboard, 
  CostTrendPoint,
  ModelCost 
} from '@/types/sdk-responses';

export function useVirtualKeysCostSummary(
  startDate?: Date,
  endDate?: Date
) {
  return useQuery<VirtualKeyCost[]>({
    queryKey: [adminApiKeys.all, 'virtualKeys', 'costSummary', startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      const dateRange = {
        startDate: startDate?.toISOString() || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        endDate: endDate?.toISOString() || new Date().toISOString()
      };
      const result = await adminClient.analytics.getCostByKey(dateRange);
      // Transform the response to match VirtualKeyCost[]
      return result.keys.map(key => ({
        virtualKeyId: key.keyId,
        virtualKeyName: key.keyName || `Key ${key.keyId}`,
        totalCost: key.totalCost,
        requestCount: key.totalRequests,
        budgetUsed: key.budgetUsed,
        remainingBudget: key.budgetRemaining,
      }));
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useCostDashboardSummary(
  timeframe: string = 'daily',
  startDate?: Date,
  endDate?: Date
) {
  return useQuery<CostDashboard>({
    queryKey: [adminApiKeys.all, 'costDashboard', 'summary', timeframe, startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      const dateRange = {
        startDate: startDate?.toISOString() || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        endDate: endDate?.toISOString() || new Date().toISOString()
      };
      const result = await adminClient.analytics.getCostSummary(dateRange);
      
      // Transform to match CostDashboard type
      return {
        totalCost: result.totalCost,
        totalRequests: 0, // Not available in CostSummaryDto
        averageCostPerRequest: 0, // Calculate if needed
        timeframe,
        startDate: dateRange.startDate,
        endDate: dateRange.endDate,
        costByProvider: [], // Not available in this endpoint
        costByModel: (result.costByModel || []).map(m => ({
          model: m.modelId,
          provider: '', // Not available
          totalCost: m.cost,
          requestCount: 0, // Not available
          averageCostPerRequest: 0, // Not available
        })),
        costByVirtualKey: [], // Not available in this endpoint
        costTrend: [], // Not available in this endpoint
      };
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useCostTrends(
  period: string = 'daily',
  startDate?: Date,
  endDate?: Date
) {
  return useQuery<{ dailyData: CostTrendPoint[]; requestGrowthPercent: number; costGrowthPercent: number }>({
    queryKey: [adminApiKeys.all, 'costDashboard', 'trends', period, startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      const dateRange = {
        startDate: startDate?.toISOString() || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        endDate: endDate?.toISOString() || new Date().toISOString()
      };
      const groupBy = period === 'daily' ? 'day' : period === 'weekly' ? 'week' : 'month' as 'day' | 'week' | 'month';
      const result = await adminClient.analytics.getCostByPeriod(dateRange, groupBy);
      
      // Transform to match expected format
      const dailyData: CostTrendPoint[] = result.periods.map(p => ({
        date: p.startDate,
        totalCost: p.totalCost,
        requestCount: p.requestCount,
        providers: {}, // Not available from this endpoint
      }));
      
      // Calculate growth percentages (comparing last two periods)
      let requestGrowthPercent = 0;
      let costGrowthPercent = 0;
      if (dailyData.length >= 2) {
        const current = dailyData[dailyData.length - 1];
        const previous = dailyData[dailyData.length - 2];
        if (previous.requestCount > 0) {
          requestGrowthPercent = ((current.requestCount - previous.requestCount) / previous.requestCount) * 100;
        }
        if (previous.totalCost > 0) {
          costGrowthPercent = ((current.totalCost - previous.totalCost) / previous.totalCost) * 100;
        }
      }
      
      return {
        dailyData,
        requestGrowthPercent,
        costGrowthPercent,
      };
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useModelCosts(
  startDate?: Date,
  endDate?: Date
) {
  return useQuery<ModelCost[]>({
    queryKey: [adminApiKeys.all, 'costDashboard', 'modelCosts', startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      const dateRange = {
        startDate: startDate?.toISOString() || new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        endDate: endDate?.toISOString() || new Date().toISOString()
      };
      const result = await adminClient.analytics.getCostByModel(dateRange);
      
      // Transform to match ModelCost[]
      return result.models.map(model => ({
        model: model.modelId,
        provider: '', // Not available in ModelUsageDto
        totalCost: model.totalCost,
        requestCount: model.totalRequests,
        averageCostPerRequest: model.averageCostPerRequest,
      }));
    },
    staleTime: 5 * 60 * 1000,
  });
}