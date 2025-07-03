'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

// Query key factory for Dashboard API
export const dashboardApiKeys = {
  all: ['dashboard-api'] as const,
  realtime: () => [...dashboardApiKeys.all, 'realtime'] as const,
  timeseries: (period: string) => [...dashboardApiKeys.all, 'timeseries', period] as const,
  providers: () => [...dashboardApiKeys.all, 'providers'] as const,
} as const;

export interface SystemMetrics {
  totalRequestsHour: number;
  totalRequestsDay: number;
  avgLatencyHour: number;
  errorRateHour: number;
  activeProviders: number;
  activeKeys: number;
}

export interface ModelMetric {
  model: string;
  requestCount: number;
  avgLatency: number;
  totalTokens: number;
  totalCost: number;
  errorRate: number;
}

export interface ProviderStatus {
  providerName: string;
  isEnabled: boolean;
  lastHealthCheck: {
    isHealthy: boolean;
    checkedAt: string;
    responseTime: number;
  } | null;
}

export interface KeyMetric {
  id: number;
  name: string;
  requestsToday: number;
  costToday: number;
  budgetUtilization: number;
}

export interface RealtimeMetrics {
  timestamp: string;
  system: SystemMetrics;
  modelMetrics: ModelMetric[];
  providerStatus: ProviderStatus[];
  topKeys: KeyMetric[];
  refreshIntervalSeconds: number;
}

export interface TimeSeriesPoint {
  timestamp: string;
  requests: number;
  avgLatency: number;
  errors: number;
  totalCost: number;
  totalTokens: number;
}

export interface TimeSeriesData {
  period: string;
  startTime: string;
  endTime: string;
  intervalMinutes: number;
  series: TimeSeriesPoint[];
}

/**
 * Hook to fetch real-time dashboard metrics
 */
export function useRealtimeMetrics() {
  return useQuery({
    queryKey: dashboardApiKeys.realtime(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        // Use Admin SDK to get system metrics and analytics
        const [systemMetrics, todayCosts, usageMetrics] = await Promise.all([
          client.metrics.getAllMetrics(),
          client.analytics.getTodayCosts(),
          client.analytics.getUsageMetrics({
            startDate: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
            endDate: new Date().toISOString(),
          }),
        ]);
        
        // Transform to dashboard format
        const realtimeMetrics: RealtimeMetrics = {
          timestamp: new Date().toISOString(),
          system: {
            totalRequestsHour: Math.floor(systemMetrics.metrics.requests.requestsPerSecond * 3600),
            totalRequestsDay: systemMetrics.metrics.requests.totalRequests,
            avgLatencyHour: systemMetrics.metrics.requests.averageResponseTime,
            errorRateHour: systemMetrics.metrics.requests.errorRate,
            activeProviders: todayCosts.costByProvider.length,
            activeKeys: todayCosts.costByKey.length,
          },
          modelMetrics: todayCosts.costByModel.map(model => ({
            model: model.modelId,
            requestCount: model.requestCount,
            avgLatency: systemMetrics.metrics.requests.averageResponseTime, // Rough estimate
            totalTokens: model.inputTokens + model.outputTokens,
            totalCost: model.cost,
            errorRate: usageMetrics.errorRate || 0,
          })),
          providerStatus: todayCosts.costByProvider.map(provider => ({
            providerName: provider.providerName,
            isEnabled: true, // Assume enabled if has cost data
            lastHealthCheck: {
              isHealthy: true,
              checkedAt: new Date().toISOString(),
              responseTime: systemMetrics.metrics.requests.averageResponseTime,
            },
          })),
          topKeys: todayCosts.costByKey.slice(0, 10).map(key => ({
            id: key.keyId,
            name: key.keyName,
            requestsToday: key.requestCount,
            costToday: key.cost,
            budgetUtilization: 80, // TODO: Get real budget data
          })),
          refreshIntervalSeconds: 10,
        };
        
        return realtimeMetrics;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch realtime metrics');
        throw error;
      }
    },
    // Refresh every 10 seconds as indicated by the API
    refetchInterval: 10000,
    staleTime: 9000,
  });
}

/**
 * Hook to fetch time-series metrics
 */
export function useTimeSeriesMetrics(period: 'hour' | 'day' | 'week' | 'month' = 'hour') {
  return useQuery({
    queryKey: dashboardApiKeys.timeseries(period),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        // Calculate date range based on period
        const now = new Date();
        let startDate: Date;
        let groupBy: 'hour' | 'day' | 'week' | 'month';
        
        switch (period) {
          case 'hour':
            startDate = new Date(now.getTime() - 60 * 60 * 1000);
            groupBy = 'hour';
            break;
          case 'day':
            startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
            groupBy = 'hour';
            break;
          case 'week':
            startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
            groupBy = 'day';
            break;
          case 'month':
            startDate = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
            groupBy = 'day';
            break;
        }
        
        const dateRange = {
          startDate: startDate.toISOString(),
          endDate: now.toISOString(),
        };
        
        const costByPeriod = await client.analytics.getCostByPeriod(dateRange, groupBy);
        
        // Transform to time series format
        const timeSeriesData: TimeSeriesData = {
          period,
          startTime: dateRange.startDate,
          endTime: dateRange.endDate,
          intervalMinutes: groupBy === 'hour' ? 60 : groupBy === 'day' ? 1440 : 10080,
          series: costByPeriod.periods.map(p => ({
            timestamp: p.startDate,
            requests: p.requestCount,
            avgLatency: 800, // TODO: Get real latency data
            errors: Math.floor(p.requestCount * 0.02), // Estimate 2% error rate
            totalCost: p.totalCost,
            totalTokens: p.inputTokens + p.outputTokens,
          })),
        };
        
        return timeSeriesData;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch time series metrics');
        throw error;
      }
    },
    staleTime: period === 'hour' ? 60000 : 300000, // 1 min for hour, 5 min for others
  });
}

export interface ProviderMetrics {
  model: string;
  metrics: {
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    avgLatency: number;
    p95Latency: number;
    totalCost: number;
    totalTokens: number;
  };
}

export interface HealthHistory {
  provider: string;
  healthChecks: number;
  successRate: number;
  avgResponseTime: number;
  lastCheck: string;
}

export interface ProviderMetricsData {
  timestamp: string;
  modelMetrics: ProviderMetrics[];
  healthHistory: HealthHistory[];
}

/**
 * Hook to fetch provider-specific metrics
 */
export function useProviderMetrics() {
  return useQuery({
    queryKey: dashboardApiKeys.providers(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        
        const dateRange = {
          startDate: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
          endDate: new Date().toISOString(),
        };
        
        const [costSummary, requestLogs] = await Promise.all([
          client.analytics.getCostSummary(dateRange),
          client.analytics.getRequestLogs({
            startDate: dateRange.startDate,
            endDate: dateRange.endDate,
            pageSize: 1000,
          }),
        ]);
        
        // Transform to provider metrics format
        const providerMetrics: ProviderMetricsData = {
          timestamp: new Date().toISOString(),
          modelMetrics: costSummary.costByModel.map(model => {
            const modelRequests = requestLogs.items.filter(log => log.model === model.modelId);
            const successfulRequests = modelRequests.filter(log => log.status === 'success').length;
            const avgLatency = modelRequests.length > 0 
              ? modelRequests.reduce((sum, log) => sum + log.duration, 0) / modelRequests.length
              : 0;
            const p95Latency = modelRequests.length > 0
              ? modelRequests.sort((a, b) => a.duration - b.duration)[Math.floor(modelRequests.length * 0.95)]?.duration || 0
              : 0;
            
            return {
              model: model.modelId,
              metrics: {
                totalRequests: model.requestCount,
                successfulRequests,
                failedRequests: model.requestCount - successfulRequests,
                avgLatency,
                p95Latency,
                totalCost: model.cost,
                totalTokens: model.inputTokens + model.outputTokens,
              },
            };
          }),
          healthHistory: costSummary.costByProvider.map(provider => ({
            provider: provider.providerName,
            healthChecks: 24, // Assume hourly checks for 24 hours
            successRate: 98.5, // TODO: Get real health data
            avgResponseTime: 800,
            lastCheck: new Date().toISOString(),
          })),
        };
        
        return providerMetrics;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch provider metrics');
        throw error;
      }
    },
    staleTime: 60000, // 1 minute
  });
}

/**
 * Hook to invalidate dashboard queries
 */
export function useInvalidateDashboard() {
  const queryClient = useQueryClient();
  
  return {
    invalidateAll: () => queryClient.invalidateQueries({ queryKey: dashboardApiKeys.all }),
    invalidateRealtime: () => queryClient.invalidateQueries({ queryKey: dashboardApiKeys.realtime() }),
    invalidateTimeseries: (period?: string) => {
      if (period) {
        queryClient.invalidateQueries({ queryKey: dashboardApiKeys.timeseries(period) });
      } else {
        queryClient.invalidateQueries({ 
          predicate: (query) => 
            query.queryKey[0] === 'dashboard-api' && 
            query.queryKey[1] === 'timeseries'
        });
      }
    },
    invalidateProviders: () => queryClient.invalidateQueries({ queryKey: dashboardApiKeys.providers() }),
  };
}