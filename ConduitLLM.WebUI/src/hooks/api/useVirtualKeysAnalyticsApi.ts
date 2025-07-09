'use client';

// Re-export virtual keys analytics hooks from the SDK via our centralized hook
export {
  useKeyUsage,
  useCostByKey,
  useExportCostAnalytics as useExportVirtualKeysData,
} from '@/hooks/useConduitAdmin';

// Also need virtual keys from the main hooks
export { useVirtualKeys } from '@/hooks/useConduitAdmin';

// Re-export types from SDK
export type {
  KeyUsageDto,
  DateRange,
} from '@knn_labs/conduit-admin-client';

// Temporary compatibility wrappers for deprecated hooks
// These provide mock implementations to prevent build errors
import { convertTimeRangeToDateRange } from '@/lib/utils/analytics-helpers';
import type { TimeRangeFilter } from '@/types/analytics-types';
import { useVirtualKeys, useKeyUsage, useCostByKey } from '@/hooks/useConduitAdmin';

export function useVirtualKeysOverview() {
  const { data, ...rest } = useVirtualKeys();
  
  return {
    data: data ? data.map((key: any) => ({
      id: key.id,
      name: key.name,
      status: key.isActive ? 'active' : 'inactive',
      usage: 0, // Would need to fetch from usage metrics
      budget: key.monthlyCostLimit || 0,
      spent: 0, // Would need to fetch from cost data
    })) : undefined,
    ...rest
  };
}

export function useVirtualKeyUsageMetrics(keyId: string, timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useKeyUsage(parseInt(keyId, 10), dateRange);
  
  return {
    data: data,
    ...rest
  };
}

export function useVirtualKeyBudgetAnalytics(keyId: string, timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useCostByKey(dateRange);
  
  return {
    data: data ? data.costByKey.find((k: any) => k.keyId === parseInt(keyId, 10)) : undefined,
    ...rest
  };
}

export function useVirtualKeyPerformanceMetrics(keyId: string, timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useKeyUsage(parseInt(keyId, 10), dateRange);
  
  return {
    data: data,
    ...rest
  };
}

export function useVirtualKeySecurityMetrics(keyId: string, timeRange: TimeRangeFilter) {
  // Not implemented in SDK - return null to avoid type 'never'
  return {
    data: null as any,
    isLoading: false,
    error: null,
  };
}

export function useVirtualKeyTrends(keyId: string, timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useCostByKey(dateRange);
  
  return {
    data: data ? data.costByKey.find((k: any) => k.keyId === parseInt(keyId, 10)) : undefined,
    ...rest
  };
}

export function useVirtualKeysLeaderboard(timeRange: TimeRangeFilter) {
  const dateRange = convertTimeRangeToDateRange(timeRange);
  const { data, ...rest } = useCostByKey(dateRange);
  
  return {
    data: data ? data.costByKey.sort((a: any, b: any) => b.cost - a.cost) : undefined,
    ...rest
  };
}