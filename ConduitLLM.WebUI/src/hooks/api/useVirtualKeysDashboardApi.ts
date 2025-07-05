import { useQuery } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { apiFetch } from '@/lib/utils/fetch-wrapper';
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
      const params = new URLSearchParams();
      if (startDate) params.append('startDate', startDate.toISOString());
      if (endDate) params.append('endDate', endDate.toISOString());
      
      const response = await apiFetch(`/api/admin/analytics/cost-by-key?${params}`, {
        method: 'GET',
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch cost summary');
      }
      
      const result = await response.json();
      // Transform the response to match VirtualKeyCost[]
      return result.keys.map((key: { keyId: number; keyName?: string; totalCost: number; totalRequests: number; budgetUsed: number; budgetRemaining: number }) => ({
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
      const params = new URLSearchParams();
      params.append('timeframe', timeframe);
      if (startDate) params.append('startDate', startDate.toISOString());
      if (endDate) params.append('endDate', endDate.toISOString());
      
      const response = await apiFetch(`/api/admin/analytics/cost-summary?${params}`, {
        method: 'GET',
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch cost dashboard summary');
      }
      
      return response.json();
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
      const params = new URLSearchParams();
      params.append('period', period);
      if (startDate) params.append('startDate', startDate.toISOString());
      if (endDate) params.append('endDate', endDate.toISOString());
      
      const response = await apiFetch(`/api/admin/analytics/cost-trends?${params}`, {
        method: 'GET',
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch cost trends');
      }
      
      return response.json();
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
      const params = new URLSearchParams();
      if (startDate) params.append('startDate', startDate.toISOString());
      if (endDate) params.append('endDate', endDate.toISOString());
      
      const response = await apiFetch(`/api/admin/analytics/model-costs?${params}`, {
        method: 'GET',
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch model costs');
      }
      
      return response.json();
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useProviderCosts(
  startDate?: Date,
  endDate?: Date
) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'costDashboard', 'providerCosts', startDate, endDate],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (startDate) params.append('startDate', startDate.toISOString());
      if (endDate) params.append('endDate', endDate.toISOString());
      
      const response = await apiFetch(`/api/admin/analytics/provider-costs?${params}`, {
        method: 'GET',
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch provider costs');
      }
      
      return response.json();
    },
    staleTime: 5 * 60 * 1000,
  });
}