'use client';

// Re-export analytics hooks from the SDK via our centralized hook
export {
  useCostSummary,
  useCostByPeriod,
  useCostByModel,
  useCostByKey,
  useRequestLogs,
  useRequestLog,
  useSearchLogs,
  useUsageMetrics,
  useModelUsage,
  useKeyUsage,
  useExportAnalytics,
  useExportUsageAnalytics,
  useExportCostAnalytics,
  useExportRequestLogs,
} from '@/hooks/useConduitAdmin';

// Re-export types from SDK
export type {
  CostSummaryDto,
  CostByPeriodDto,
  RequestLogDto,
  RequestLogFilters,
  UsageMetricsDto,
  ModelUsageDto,
  KeyUsageDto,
  DateRange,
} from '@knn_labs/conduit-admin-client';

// Note: Components using TimeRangeFilter will need to be updated to use DateRange
// The following hooks are deprecated and replaced by SDK hooks:
// - useCostTrends -> use useCostByPeriod
// - useProviderCosts -> data available in useCostSummary response
// - useModelCosts -> use useCostByModel
// - useVirtualKeyCosts -> use useCostByKey
// - useCostAlerts -> not yet implemented in SDK