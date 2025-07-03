'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { DateRange } from '@knn_labs/conduit-admin-client';
import type { UsageMetricsDto } from '@/types/sdk-extensions';

// Query key factory for Usage Analytics API
export const usageAnalyticsApiKeys = {
  all: ['usage-analytics-api'] as const,
  metrics: () => [...usageAnalyticsApiKeys.all, 'metrics'] as const,
  requests: () => [...usageAnalyticsApiKeys.all, 'requests'] as const,
  tokens: () => [...usageAnalyticsApiKeys.all, 'tokens'] as const,
  errors: () => [...usageAnalyticsApiKeys.all, 'errors'] as const,
  latency: () => [...usageAnalyticsApiKeys.all, 'latency'] as const,
  users: () => [...usageAnalyticsApiKeys.all, 'users'] as const,
  endpoints: () => [...usageAnalyticsApiKeys.all, 'endpoints'] as const,
} as const;

export interface UsageMetrics {
  totalRequests: number;
  totalTokens: number;
  totalUsers: number;
  averageLatency: number;
  errorRate: number;
  requestsPerSecond: number;
  tokensPerRequest: number;
  successRate: number;
  uniqueVirtualKeys: number;
  activeProviders: number;
  requestsTrend: number; // percentage change
  tokensTrend: number;
  errorsTrend: number;
  latencyTrend: number;
}

export interface RequestVolumeData {
  timestamp: string;
  requests: number;
  successfulRequests: number;
  failedRequests: number;
  averageLatency: number;
  tokensProcessed: number;
}

export interface TokenUsageData {
  timestamp: string;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  cost: number;
  averageTokensPerRequest: number;
}

export interface ErrorAnalyticsData {
  errorType: string;
  count: number;
  percentage: number;
  lastOccurrence: string;
  affectedEndpoints: string[];
  examples: {
    message: string;
    timestamp: string;
    virtualKey?: string;
    provider?: string;
  }[];
}

export interface LatencyMetrics {
  endpoint: string;
  averageLatency: number;
  p50: number;
  p90: number;
  p95: number;
  p99: number;
  requestCount: number;
  slowestRequests: {
    latency: number;
    timestamp: string;
    virtualKey?: string;
    model?: string;
  }[];
}

export interface UserAnalytics {
  virtualKeyId: string;
  virtualKeyName: string;
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  averageLatency: number;
  errorRate: number;
  lastActivity: string;
  topModels: string[];
  topEndpoints: string[];
  requestsOverTime: { timestamp: string; requests: number }[];
}

export interface EndpointUsage {
  endpoint: string;
  method: string;
  totalRequests: number;
  averageLatency: number;
  errorRate: number;
  successRate: number;
  tokensPerRequest: number;
  costPerRequest: number;
  popularModels: string[];
  requestsOverTime: { timestamp: string; requests: number }[];
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

// Usage Metrics Hook
export function useUsageMetrics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.metrics(), timeRange],
    queryFn: async (): Promise<UsageMetrics> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getUsageMetrics(dateRange);
        
        // Transform SDK response to UI format
        const metrics = response as UsageMetricsDto;
        const totalInputTokens = metrics.totalInputTokens || 0;
        const totalOutputTokens = metrics.totalOutputTokens || 0;
        const totalTokens = totalInputTokens + totalOutputTokens;
        
        const usageMetrics: UsageMetrics = {
          totalRequests: response.totalRequests || 0,
          totalTokens,
          totalUsers: response.uniqueKeys || 0,
          averageLatency: response.averageLatency || 0,
          errorRate: response.errorRate || 0,
          requestsPerSecond: (response.requestsPerMinute || 0) / 60,
          tokensPerRequest: response.totalRequests > 0 ? totalTokens / response.totalRequests : 0,
          successRate: response.totalRequests > 0 ? ((response.successfulRequests || 0) / response.totalRequests) * 100 : 100,
          uniqueVirtualKeys: response.uniqueKeys || 0,
          activeProviders: response.uniqueModels || 0, // Using unique models as proxy for active providers
          requestsTrend: 0, // TODO: Calculate trend from historical data
          tokensTrend: 0,
          errorsTrend: 0,
          latencyTrend: 0,
        };

        return usageMetrics;
      } catch (error: any) {
        reportError(error, 'Failed to fetch usage metrics');
        throw new Error(error?.message || 'Failed to fetch usage metrics');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 60 * 1000, // 1 minute
  });
}

// Request Volume Analytics
export function useRequestVolumeAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.requests(), timeRange],
    queryFn: async (): Promise<RequestVolumeData[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const groupBy = getGroupByFromTimeRange(timeRange);
        const response = await client.analytics.getCostByPeriod(dateRange, groupBy);
        
        // Transform SDK response to UI format
        const requestVolumeData: RequestVolumeData[] = response.periods.map(period => ({
          timestamp: period.startDate,
          requests: period.requestCount,
          successfulRequests: period.requestCount, // Actual success/failure breakdown not available in cost data
          failedRequests: 0, // Error counts would need to come from request logs
          averageLatency: 0, // Latency data not available in cost endpoint
          tokensProcessed: period.inputTokens + period.outputTokens,
        }));

        return requestVolumeData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch request volume analytics');
        throw new Error(error?.message || 'Failed to fetch request volume analytics');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
  });
}

// Token Usage Analytics
export function useTokenUsageAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.tokens(), timeRange],
    queryFn: async (): Promise<TokenUsageData[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const groupBy = getGroupByFromTimeRange(timeRange);
        const response = await client.analytics.getCostByPeriod(dateRange, groupBy);
        
        // Transform SDK response to UI format
        const tokenUsageData: TokenUsageData[] = response.periods.map(period => {
          const totalTokens = period.inputTokens + period.outputTokens;
          const averageTokensPerRequest = period.requestCount > 0 ? totalTokens / period.requestCount : 0;
          
          return {
            timestamp: period.startDate,
            inputTokens: period.inputTokens,
            outputTokens: period.outputTokens,
            totalTokens,
            cost: period.totalCost,
            averageTokensPerRequest: Math.round(averageTokensPerRequest * 10) / 10,
          };
        });

        return tokenUsageData;
      } catch (error: any) {
        reportError(error, 'Failed to fetch token usage analytics');
        throw new Error(error?.message || 'Failed to fetch token usage analytics');
      }
    },
    staleTime: 30 * 1000, // 30 seconds
  });
}

// Error Analytics
export function useErrorAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.errors(), timeRange],
    queryFn: async (): Promise<ErrorAnalyticsData[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getRequestLogs({
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          status: 'error',
          pageSize: 1000, // Get more data for error analysis
        });
        
        // Aggregate error data by type
        const errorCounts = new Map<string, {
          count: number;
          examples: typeof response.items;
          lastOccurrence: string;
        }>();
        
        response.items.forEach(log => {
          const errorType = log.errorMessage || 'Unknown Error';
          const existing = errorCounts.get(errorType) || {
            count: 0,
            examples: [],
            lastOccurrence: log.timestamp,
          };
          
          existing.count++;
          if (existing.examples.length < 3) {
            existing.examples.push(log);
          }
          if (new Date(log.timestamp) > new Date(existing.lastOccurrence)) {
            existing.lastOccurrence = log.timestamp;
          }
          
          errorCounts.set(errorType, existing);
        });
        
        const totalErrors = response.items.length;
        
        // Transform to UI format
        const errorAnalytics: ErrorAnalyticsData[] = Array.from(errorCounts.entries()).map(([errorType, data]) => ({
          errorType,
          count: data.count,
          percentage: totalErrors > 0 ? (data.count / totalErrors) * 100 : 0,
          lastOccurrence: data.lastOccurrence,
          affectedEndpoints: [...new Set(data.examples.map(ex => `/v1/${ex.model || 'unknown'}`))],
          examples: data.examples.map(ex => ({
            message: ex.errorMessage || 'Unknown error',
            timestamp: ex.timestamp,
            virtualKey: ex.virtualKeyName,
            provider: ex.provider,
          })),
        }));

        return errorAnalytics.sort((a, b) => b.count - a.count); // Sort by count descending
      } catch (error: any) {
        reportError(error, 'Failed to fetch error analytics');
        throw new Error(error?.message || 'Failed to fetch error analytics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// Latency Metrics
export function useLatencyMetrics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.latency(), timeRange],
    queryFn: async (): Promise<LatencyMetrics[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getRequestLogs({
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          pageSize: 1000,
        });
        
        // Group by endpoint/model to calculate latency metrics
        const endpointMetrics = new Map<string, {
          durations: number[];
          requests: typeof response.items;
        }>();
        
        response.items.forEach(log => {
          const endpoint = `/v1/${log.model.includes('chat') ? 'chat/completions' : 
                                log.model.includes('dall-e') ? 'images/generations' :
                                log.model.includes('whisper') ? 'audio/transcriptions' :
                                'chat/completions'}`;
          
          const existing = endpointMetrics.get(endpoint) || {
            durations: [],
            requests: [],
          };
          
          existing.durations.push(log.duration);
          existing.requests.push(log);
          endpointMetrics.set(endpoint, existing);
        });
        
        // Calculate percentiles for each endpoint
        const latencyMetrics: LatencyMetrics[] = Array.from(endpointMetrics.entries()).map(([endpoint, data]) => {
          const sortedDurations = data.durations.sort((a, b) => a - b);
          const count = sortedDurations.length;
          
          const getPercentile = (p: number) => {
            if (count === 0) return 0;
            const index = Math.floor((p / 100) * count);
            return sortedDurations[Math.min(index, count - 1)];
          };
          
          const averageLatency = count > 0 ? sortedDurations.reduce((sum, d) => sum + d, 0) / count : 0;
          
          // Get slowest requests
          const slowestRequests = data.requests
            .sort((a, b) => b.duration - a.duration)
            .slice(0, 3)
            .map(log => ({
              latency: log.duration,
              timestamp: log.timestamp,
              virtualKey: log.virtualKeyName || 'unknown',
              model: log.model,
            }));
          
          return {
            endpoint,
            averageLatency: Math.round(averageLatency),
            p50: Math.round(getPercentile(50)),
            p90: Math.round(getPercentile(90)),
            p95: Math.round(getPercentile(95)),
            p99: Math.round(getPercentile(99)),
            requestCount: count,
            slowestRequests,
          };
        });

        return latencyMetrics.sort((a, b) => b.requestCount - a.requestCount);
      } catch (error: any) {
        reportError(error, 'Failed to fetch latency metrics');
        throw new Error(error?.message || 'Failed to fetch latency metrics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// User Analytics
export function useUserAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.users(), timeRange],
    queryFn: async (): Promise<UserAnalytics[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const costByKeyResponse = await client.analytics.getCostByKey(dateRange);
        
        // Get detailed data for each key
        const userAnalyticsPromises = costByKeyResponse.keys.map(async (key) => {
          try {
            const keyUsage = await client.analytics.getKeyUsage(key.keyId, dateRange);
            
            // Get request logs for this key to calculate additional metrics
            const requestLogs = await client.analytics.getRequestLogs({
              startDate: dateRange.startDate,
              endDate: dateRange.endDate,
              virtualKeyId: key.keyId,
              pageSize: 1000,
            });
            
            // Calculate error rate
            const errorCount = requestLogs.items.filter(log => log.status === 'error').length;
            const errorRate = requestLogs.items.length > 0 ? (errorCount / requestLogs.items.length) * 100 : 0;
            
            // Calculate average latency
            const averageLatency = requestLogs.items.length > 0 
              ? requestLogs.items.reduce((sum, log) => sum + log.duration, 0) / requestLogs.items.length
              : 0;
            
            // Get top models
            const modelCounts = new Map<string, number>();
            requestLogs.items.forEach(log => {
              modelCounts.set(log.model, (modelCounts.get(log.model) || 0) + 1);
            });
            const topModels = Array.from(modelCounts.entries())
              .sort((a, b) => b[1] - a[1])
              .slice(0, 3)
              .map(([model]) => model);
            
            // Requests over time not available without hourly/daily breakdown
            const requestsOverTime: { timestamp: string; requests: number; }[] = [];
            
            return {
              virtualKeyId: key.keyId.toString(),
              virtualKeyName: keyUsage.keyName,
              totalRequests: keyUsage.totalRequests,
              totalTokens: 0, // Token count would need to come from request logs
              totalCost: keyUsage.totalCost,
              averageLatency,
              errorRate,
              lastActivity: keyUsage.lastUsed,
              topModels,
              topEndpoints: [], // Endpoint breakdown not available in current data
              requestsOverTime,
            };
          } catch (error) {
            // If detailed data fails, return basic data
            return {
              virtualKeyId: key.keyId.toString(),
              virtualKeyName: key.keyName,
              totalRequests: key.totalRequests,
              totalTokens: 0,
              totalCost: key.totalCost,
              averageLatency: 0,
              errorRate: 0,
              lastActivity: new Date().toISOString(),
              topModels: [],
              topEndpoints: [],
              requestsOverTime: [],
            };
          }
        });
        
        const userAnalytics = await Promise.all(userAnalyticsPromises);
        return userAnalytics.sort((a, b) => b.totalCost - a.totalCost); // Sort by cost descending
      } catch (error: any) {
        reportError(error, 'Failed to fetch user analytics');
        throw new Error(error?.message || 'Failed to fetch user analytics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// Endpoint Usage Analytics
export function useEndpointUsageAnalytics(timeRange: TimeRangeFilter) {
  return useQuery({
    queryKey: [...usageAnalyticsApiKeys.endpoints(), timeRange],
    queryFn: async (): Promise<EndpointUsage[]> => {
      try {
        const client = getAdminClient();
        
        // Convert TimeRangeFilter to DateRange for SDK
        const dateRange = convertTimeRangeToDateRange(timeRange);
        const response = await client.analytics.getRequestLogs({
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          pageSize: 1000,
        });
        
        // Group by endpoint
        const endpointData = new Map<string, {
          requests: typeof response.items;
          models: Set<string>;
        }>();
        
        response.items.forEach(log => {
          const endpoint = `/v1/${log.model.includes('chat') ? 'chat/completions' : 
                                log.model.includes('dall-e') ? 'images/generations' :
                                log.model.includes('whisper') ? 'audio/transcriptions' :
                                log.model.includes('tts') ? 'audio/speech' :
                                'chat/completions'}`;
          
          const existing = endpointData.get(endpoint) || {
            requests: [],
            models: new Set<string>(),
          };
          
          existing.requests.push(log);
          existing.models.add(log.model);
          endpointData.set(endpoint, existing);
        });
        
        // Calculate metrics for each endpoint
        const endpointUsage: EndpointUsage[] = Array.from(endpointData.entries()).map(([endpoint, data]) => {
          const totalRequests = data.requests.length;
          const successfulRequests = data.requests.filter(log => log.status === 'success').length;
          const errorRate = totalRequests > 0 ? ((totalRequests - successfulRequests) / totalRequests) * 100 : 0;
          const successRate = 100 - errorRate;
          
          const averageLatency = totalRequests > 0 
            ? data.requests.reduce((sum, log) => sum + log.duration, 0) / totalRequests
            : 0;
          
          const totalTokens = data.requests.reduce((sum, log) => sum + log.inputTokens + log.outputTokens, 0);
          const tokensPerRequest = totalRequests > 0 ? totalTokens / totalRequests : 0;
          
          const totalCost = data.requests.reduce((sum, log) => sum + log.cost, 0);
          const costPerRequest = totalRequests > 0 ? totalCost / totalRequests : 0;
          
          // Get popular models
          const modelCounts = new Map<string, number>();
          data.requests.forEach(log => {
            modelCounts.set(log.model, (modelCounts.get(log.model) || 0) + 1);
          });
          const popularModels = Array.from(modelCounts.entries())
            .sort((a, b) => b[1] - a[1])
            .slice(0, 3)
            .map(([model]) => model);
          
          // Requests over time not available without temporal breakdown
          const requestsOverTime: { timestamp: string; requests: number; }[] = [];
          
          return {
            endpoint,
            method: 'POST',
            totalRequests,
            averageLatency: Math.round(averageLatency),
            errorRate: Math.round(errorRate * 100) / 100,
            successRate: Math.round(successRate * 100) / 100,
            tokensPerRequest: Math.round(tokensPerRequest * 10) / 10,
            costPerRequest: Math.round(costPerRequest * 10000) / 10000,
            popularModels,
            requestsOverTime,
          };
        });

        return endpointUsage.sort((a, b) => b.totalRequests - a.totalRequests);
      } catch (error: any) {
        reportError(error, 'Failed to fetch endpoint usage analytics');
        throw new Error(error?.message || 'Failed to fetch endpoint usage analytics');
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// Export usage data
export function useExportUsageData() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ 
      type, 
      timeRange, 
      format = 'csv' 
    }: { 
      type: 'metrics' | 'requests' | 'tokens' | 'errors' | 'latency' | 'users' | 'endpoints';
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
        const filename = `usage-${type}-${timestamp}.${format}`;
        
        return {
          filename,
          url,
          size: blob.size,
        };
      } catch (error: any) {
        reportError(error, 'Failed to export usage data');
        throw new Error(error?.message || 'Failed to export usage data');
      }
    },
  });
}