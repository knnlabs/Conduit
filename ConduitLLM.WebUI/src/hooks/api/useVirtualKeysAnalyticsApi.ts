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

