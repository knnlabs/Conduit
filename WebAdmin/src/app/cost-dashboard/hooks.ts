import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import type {
  CostDashboardDto,
  CostTrendDto,
  DateRange,
  ProviderCost,
  ModelUsage,
  DailyCost,
  DetailedCostDataDto,
  CostTrendDataDto,
} from './types';

export function useTimeRangeUtils(timeRange: string) {
  // Calculate date range based on timeRange
  const getDateRange = (): DateRange => {
    const now = new Date();
    const startDate = new Date();
    
    switch (timeRange) {
      case '7d':
        startDate.setDate(now.getDate() - 7);
        break;
      case '30d':
        startDate.setDate(now.getDate() - 30);
        break;
      case '90d':
        startDate.setDate(now.getDate() - 90);
        break;
      default:
        startDate.setDate(now.getDate() - 30);
    }
    
    return {
      startDate: startDate.toISOString().split('T')[0],
      endDate: now.toISOString().split('T')[0],
    };
  };

  return { getDateRange };
}

export function useCostData(timeRange: string) {
  const { getDateRange } = useTimeRangeUtils(timeRange);

  // Fetch cost summary from Admin SDK
  const { 
    data: costSummary, 
    isLoading: isLoadingSummary, 
    error: summaryError, 
    refetch: refetchSummary 
  } = useQuery<CostDashboardDto>({
    queryKey: ['cost-summary', timeRange],
    queryFn: async () => {
      const { startDate, endDate } = getDateRange();
      return withAdminClient(client => 
        client.analytics.getCostSummary('daily', startDate, endDate)
      );
    },
    refetchInterval: 300000, // Refresh every 5 minutes
  });

  // Fetch cost trends from Admin SDK
  const { 
    data: costTrends, 
    isLoading: isLoadingTrends, 
    error: trendsError, 
    refetch: refetchTrends 
  } = useQuery<CostTrendDto>({
    queryKey: ['cost-trends', timeRange],
    queryFn: async () => {
      const { startDate, endDate } = getDateRange();
      return withAdminClient(client => 
        client.analytics.getCostTrends('daily', startDate, endDate)
      );
    },
    refetchInterval: 300000, // Refresh every 5 minutes
  });

  const isLoading = isLoadingSummary || isLoadingTrends;
  const error = summaryError ?? trendsError;

  const refetchAll = async () => {
    await Promise.all([refetchSummary(), refetchTrends()]);
  };

  return {
    costSummary,
    costTrends,
    isLoading,
    error,
    refetchAll,
    getDateRange,
  };
}

export function useTransformedData(costSummary: CostDashboardDto | undefined, costTrends: CostTrendDto | undefined, timeRange: string) {
  // Calculate derived metrics
  const totalSpend = costSummary?.totalCost ?? 0;
  const last7DaysCost = costSummary?.last7DaysCost ?? 0;
  const last30DaysCost = costSummary?.last30DaysCost ?? 0;
  
  // Calculate average daily cost based on the time range
  let daysInRange: number;
  if (timeRange === '7d') {
    daysInRange = 7;
  } else if (timeRange === '30d') {
    daysInRange = 30;
  } else {
    daysInRange = 90;
  }
  const averageDailyCost = totalSpend / daysInRange;
  
  // Calculate projected monthly spend
  const daysInMonth = 30;
  const projectedMonthlySpend = averageDailyCost * daysInMonth;
  
  // Trend of the last 7 days vs the prior 7-day average. This is always a
  // 7-day comparison regardless of the selected time range — surface it as
  // such rather than implying it matches the range.
  const sevenDayTrend = last7DaysCost > 0 && last30DaysCost > 0
    ? ((last7DaysCost - (last30DaysCost - last7DaysCost) / 3) / ((last30DaysCost - last7DaysCost) / 3)) * 100
    : 0;

  // Transform provider costs
  const providerCosts: ProviderCost[] = costSummary?.topProvidersBySpend?.map((provider: DetailedCostDataDto) => ({
    provider: provider.name,
    cost: provider.cost,
    usage: provider.percentage,
  })) ?? [];

  // Transform model usage. The cost summary does not attribute models to
  // providers or report token counts — those fields are deliberately absent
  // rather than filled with guesses or zeros.
  const modelUsage: ModelUsage[] = costSummary?.topModelsBySpend?.map((model: DetailedCostDataDto) => ({
    model: model.name,
    requests: model.requestCount,
    cost: model.cost,
  })) ?? [];

  // Daily costs from trends. Only the measured total is included — a per-day
  // provider split does not exist in the data (multiplying each day's total by
  // the period-wide provider share fabricates an identical daily mix).
  const dailyCosts: DailyCost[] = costTrends?.data?.map((trend: CostTrendDataDto) => ({
    date: trend.date,
    cost: trend.cost,
  })) ?? [];

  // Calculate budget utilization (if we had budget data)
  const monthlyBudget: number | null = null; // Budget feature not yet implemented
  const budgetUtilization = monthlyBudget !== null ? (projectedMonthlySpend / monthlyBudget) * 100 : null;
  const isOverBudget = budgetUtilization !== null ? budgetUtilization > 100 : false;

  return {
    totalSpend,
    last7DaysCost,
    last30DaysCost,
    averageDailyCost,
    projectedMonthlySpend,
    sevenDayTrend,
    providerCosts,
    modelUsage,
    dailyCosts,
    monthlyBudget,
    budgetUtilization,
    isOverBudget,
  };
}