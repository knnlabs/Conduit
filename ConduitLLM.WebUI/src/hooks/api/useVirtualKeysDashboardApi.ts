import { useQuery } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { createAdminClient } from '@/lib/clients/conduit';

export function useVirtualKeysCostSummary(
  startDate?: Date,
  endDate?: Date
) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'virtualKeys', 'costSummary', startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.costDashboard.getVirtualKeyCosts(
        startDate?.toISOString(),
        endDate?.toISOString()
      );
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useCostDashboardSummary(
  timeframe: string = 'daily',
  startDate?: Date,
  endDate?: Date
) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'costDashboard', 'summary', timeframe, startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.costDashboard.getCostSummary(
        timeframe,
        startDate?.toISOString(),
        endDate?.toISOString()
      );
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useCostTrends(
  period: string = 'daily',
  startDate?: Date,
  endDate?: Date
) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'costDashboard', 'trends', period, startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.costDashboard.getCostTrends(
        period,
        startDate?.toISOString(),
        endDate?.toISOString()
      );
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useModelCosts(
  startDate?: Date,
  endDate?: Date
) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'costDashboard', 'modelCosts', startDate, endDate],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.costDashboard.getModelCosts(
        startDate?.toISOString(),
        endDate?.toISOString()
      );
    },
    staleTime: 5 * 60 * 1000,
  });
}