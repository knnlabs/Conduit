'use client';

// Re-export usage analytics hooks from the SDK via our centralized hook
export {
  useUsageMetrics,
  useModelUsage,
  useKeyUsage,
  useExportUsageAnalytics as useExportUsageData,
} from '@/hooks/useConduitAdmin';

// Re-export types from SDK
export type {
  UsageMetricsDto,
  ModelUsageDto,
  KeyUsageDto,
  DateRange,
} from '@knn_labs/conduit-admin-client';

// Temporary compatibility wrappers for deprecated hooks
// These extract specific data from the main useUsageMetrics hook
import { convertTimeRangeToDateRange } from '@/lib/utils/analytics-helpers';
import type { TimeRangeFilter } from '@/types/analytics-types';
import { useUsageMetrics } from '@/hooks/useConduitAdmin';

// Mock implementations to prevent build errors - components using these should be updated
export function useRequestVolumeAnalytics(timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useUsageMetrics(dateRange);
  
  return {
    data: data ? {
      totalRequests: data.totalRequests,
      successfulRequests: data.successfulRequests,
      failedRequests: data.failedRequests,
      requestsPerMinute: data.requestsPerMinute,
      peakRequestsPerMinute: data.peakRequestsPerMinute,
    } : undefined,
    ...rest
  };
}

export function useTokenUsageAnalytics(timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useUsageMetrics(dateRange);
  
  return {
    data: data ? {
      totalTokens: 0, // Not directly available in SDK
      inputTokens: 0,
      outputTokens: 0,
    } : undefined,
    ...rest
  };
}

export function useErrorAnalytics(timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useUsageMetrics(dateRange);
  
  return {
    data: data ? {
      errorRate: data.errorRate,
      totalErrors: data.failedRequests,
    } : undefined,
    ...rest
  };
}

export function useLatencyMetrics(timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useUsageMetrics(dateRange);
  
  return {
    data: data ? {
      averageLatency: data.averageLatency,
      p95Latency: data.p95Latency,
      p99Latency: data.p99Latency,
    } : undefined,
    ...rest
  };
}

export function useUserAnalytics(timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useUsageMetrics(dateRange);
  
  return {
    data: data ? {
      uniqueKeys: data.uniqueKeys,
    } : undefined,
    ...rest
  };
}

export function useEndpointUsageAnalytics(timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useUsageMetrics(dateRange);
  
  return {
    data: data ? {
      uniqueModels: data.uniqueModels,
    } : undefined,
    ...rest
  };
}