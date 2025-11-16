'use client';

import { useQuery } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';

// Types for analytics responses
interface UsageAnalytics {
  metrics: {
    totalRequests: number;
    totalCost: number;
    totalTokens: number;
    activeVirtualKeys: number;
    requestsChange: number;
    costChange: number;
    tokensChange: number;
    virtualKeysChange: number;
    systemHealth?: {
      overallHealthPercentage?: number;
      errorRate?: number;
      responseTimeP95?: number;
      activeConnections?: number;
    };
    realTimeMetrics?: {
      requestsPerSecond?: number;
      activeRequests?: number;
      averageResponseTime?: number;
      costBurnRatePerHour?: number;
      averageCostPerRequest?: number;
    };
  };
  timeSeries: Array<{
    timestamp: string;
    requests: number;
    cost: number;
    tokens: number;
  }>;
  providerUsage: Array<{
    provider: string;
    requests: number;
    cost: number;
    tokens: number;
    percentage: number;
    costPercentage: number;
    errorRate: number;
    avgResponseTime: number;
    avgCostPerRequest: number;
    minCost: number;
    maxCost: number;
  }>;
  modelUsage: Array<{
    model: string;
    provider: string;
    requests: number;
    cost: number;
    tokens: number;
    inputTokens: number;
    outputTokens: number;
    avgCostPerRequest: number;
    avgTokensPerRequest: number;
    avgResponseTime: number;
    successRate: number;
    errorRate: number;
    efficiency: number;
  }>;
  virtualKeyUsage: Array<{
    keyName: string;
    requests: number;
    cost: number;
    tokens: number;
    lastUsed: string;
    firstUsed: string;
    avgCostPerRequest: number;
    avgResponseTime: number;
    successRate: number;
    errorRate: number;
    uniqueModelsUsed: number;
    uniqueProvidersUsed: number;
    avgRequestsPerDay: number;
    daysSinceFirstUsed: number;
    efficiency: number;
  }>;
  endpointUsage: Array<{
    endpoint: string;
    requests: number;
    avgDuration: number;
    minDuration: number;
    maxDuration: number;
    p50Duration: number;
    p95Duration: number;
    p99Duration: number;
    errorRate: number;
    successRate: number;
    requestsPerMinute: number;
    realTimeAvgDuration?: number;
    realTimeErrorRate?: number;
  }>;
  timeRange: string;
  lastUpdated: string;
  dataSources: {
    historicalData: boolean;
    realTimeMetrics: boolean;
    coreSDKConnected: boolean;
    partialDataAvailable?: boolean;
  };
  error?: {
    type: string;
    message: string;
    timestamp: string;
    services: {
      adminSDK: boolean;
      coreSDK: boolean;
      costSummary: boolean;
      requestLogs: boolean;
      realTimeMetrics: boolean;
    };
  };
  warning?: string;
}

interface CostAnalytics {
  totalSpend: number;
  averageDailyCost: number;
  projectedMonthlySpend: number;
  monthlyBudget?: number;
  projectedTrend: number;
  providerCosts: Array<{
    provider: string;
    cost: number;
    usage: number;
    trend: number;
  }>;
  modelUsage: Array<{
    model: string;
    provider: string;
    requests: number;
    tokensIn: number;
    tokensOut: number;
    cost: number;
  }>;
  dailyCosts: Array<{
    date: string;
    cost: number;
    providers: Record<string, number>;
  }>;
  timeRange: string;
  lastUpdated: string;
}


// Usage Analytics Hook
export function useUsageAnalytics(timeRange = '7d') {
  return useQuery<UsageAnalytics, Error>({
    queryKey: ['usage-analytics', timeRange],
    queryFn: async () => {
      // Parse time range into date objects
      const now = new Date();
      const startDate = new Date();
      
      switch (timeRange) {
        case '24h':
          startDate.setHours(now.getHours() - 24);
          break;
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
          startDate.setDate(now.getDate() - 7);
      }

      // Calculate previous period for change calculations
      const periodLength = now.getTime() - startDate.getTime();
      const previousStartDate = new Date(startDate.getTime() - periodLength);
      const previousEndDate = new Date(startDate.getTime());

      try {
        // Fetch data from Admin SDK
        const [
          currentCostSummary,
          currentRequestLogs,
          previousCostSummary,
        ] = await Promise.all([
          withAdminClient(client => 
            client.analytics.getCostSummary(
              'daily',
              startDate.toISOString().split('T')[0],
              now.toISOString().split('T')[0]
            )
          ),
          withAdminClient(client => 
            client.analytics.getRequestLogs({
              startDate: startDate.toISOString(),
              endDate: now.toISOString(),
              pageSize: 100,
              page: 1
            })
          ),
          withAdminClient(client => 
            client.analytics.getCostSummary(
              'daily',
              previousStartDate.toISOString().split('T')[0],
              previousEndDate.toISOString().split('T')[0]
            )
          ).catch(() => null),
        ]);

        // Transform the data into the expected format
        const requestLogs = currentRequestLogs as { totalCount: number; items: unknown[] };
        const costSummary = currentCostSummary as { 
          totalCost: number; 
          topProvidersBySpend?: Array<{ name: string; cost: number; percentage: number }>;
        };
        const prevCostSummary = previousCostSummary as { totalCost: number } | null;

        // Calculate basic metrics
        const totalRequests = requestLogs?.totalCount ?? requestLogs?.items?.length ?? 0;
        const totalCost = costSummary?.totalCost ?? 0;
        const totalTokens = 0; // Would need to calculate from request logs
        const activeVirtualKeys = 0; // Would need to calculate from request logs
        
        // Calculate changes
        const costChange = (prevCostSummary && prevCostSummary.totalCost > 0)
          ? ((totalCost - prevCostSummary.totalCost) / prevCostSummary.totalCost) * 100 
          : 0;

        const response: UsageAnalytics = {
          metrics: {
            totalRequests,
            totalCost,
            totalTokens,
            activeVirtualKeys,
            requestsChange: 0,
            costChange: isFinite(costChange) ? costChange : 0,
            tokensChange: 0,
            virtualKeysChange: 0,
          },
          timeSeries: [],
          providerUsage: costSummary?.topProvidersBySpend?.map(provider => ({
            provider: provider.name,
            requests: 0,
            cost: provider.cost,
            tokens: 0,
            percentage: provider.percentage,
            costPercentage: provider.percentage,
            errorRate: 0,
            avgResponseTime: 0,
            avgCostPerRequest: 0,
            minCost: 0,
            maxCost: 0,
          })) ?? [],
          modelUsage: [],
          virtualKeyUsage: [],
          endpointUsage: [],
          timeRange,
          lastUpdated: new Date().toISOString(),
          dataSources: {
            historicalData: true,
            realTimeMetrics: false,
            coreSDKConnected: false,
          },
        };

        return response;
      } catch (error) {
        console.error('Analytics fetch error:', error);
        throw error;
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes
    retry: 2,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}

// Cost Analytics Hook
export function useCostAnalytics(timeRange = '30d') {
  return useQuery<CostAnalytics, Error>({
    queryKey: ['cost-analytics', timeRange],
    queryFn: async () => {
      // Parse time range
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
        case 'ytd':
          startDate.setMonth(0, 1);
          break;
        default:
          startDate.setDate(now.getDate() - 30);
      }
      
      // Get data from analytics endpoints
      const [costSummary, costTrends] = await Promise.all([
        withAdminClient(client => 
          client.analytics.getCostSummary('daily', startDate.toISOString(), now.toISOString())
        ),
        withAdminClient(client => 
          client.analytics.getCostTrends('daily', startDate.toISOString(), now.toISOString())
        ),
      ]);

      // Transform the data
      const summary = costSummary as { 
        totalCost: number; 
        last24HoursCost: number;
        last7DaysCost: number;
        last30DaysCost: number;
        topProvidersBySpend?: Array<{ name: string; cost: number; percentage: number }>;
        topModelsBySpend?: Array<{ name: string; cost: number; percentage: number; requestCount: number }>;
      };
      const trends = costTrends as { data?: Array<{ date: string; cost: number; requestCount: number }> };

      // Calculate daily average
      const dayCount = Math.ceil((now.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24));
      const averageDailyCost = summary.totalCost / dayCount;

      // Calculate projected monthly spend
      const daysInMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
      const dayOfMonth = now.getDate();
      const projectedMonthlySpend = (summary.totalCost / dayOfMonth) * daysInMonth;

      // Calculate trend from last 7 days vs previous 7 days
      const projectedTrend = summary.last7DaysCost > 0 && summary.last30DaysCost > 0
        ? ((summary.last7DaysCost - (summary.last30DaysCost - summary.last7DaysCost) / 3) / ((summary.last30DaysCost - summary.last7DaysCost) / 3)) * 100
        : 0;

      const response: CostAnalytics = {
        totalSpend: summary.totalCost,
        averageDailyCost,
        projectedMonthlySpend,
        monthlyBudget: undefined, // Budget info not available from new endpoint
        projectedTrend,
        providerCosts: summary.topProvidersBySpend?.map(provider => ({
          provider: provider.name,
          cost: provider.cost,
          usage: provider.percentage,
          trend: 0, // Not available
        })) ?? [],
        modelUsage: summary.topModelsBySpend?.map(model => ({
          model: model.name,
          provider: model.name.includes('/') ? model.name.split('/')[0] : 'unknown',
          requests: model.requestCount,
          tokensIn: 0, // Not available from cost summary
          tokensOut: 0, // Not available from cost summary
          cost: model.cost,
        })) ?? [],
        dailyCosts: trends?.data?.map(trend => {
          const providers: Record<string, number> = {};
          
          // Distribute cost across providers
          summary.topProvidersBySpend?.forEach(provider => {
            providers[provider.name] = (trend.cost * provider.percentage) / 100;
          });

          return {
            date: trend.date,
            cost: trend.cost,
            providers,
          };
        }) ?? [],
        timeRange,
        lastUpdated: new Date().toISOString(),
      };

      return response;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes
    retry: 2,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}

