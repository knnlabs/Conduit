'use client';

import {
  // Provider hooks
  useProviders as useSDKProviders,
  useProvider as useSDKProvider,
  useCreateProvider as useSDKCreateProvider,
  useUpdateProvider as useSDKUpdateProvider,
  useDeleteProvider as useSDKDeleteProvider,
  useDiscoverProviderModels as useSDKDiscoverProviderModels,
  
  // Virtual Key hooks
  useVirtualKeys as useSDKVirtualKeys,
  useVirtualKey as useSDKVirtualKey,
  useCreateVirtualKey as useSDKCreateVirtualKey,
  useUpdateVirtualKey as useSDKUpdateVirtualKey,
  useDeleteVirtualKey as useSDKDeleteVirtualKey,
  
  // Model Mapping hooks
  useModelMappings as useSDKModelMappings,
  useModelMapping as useSDKModelMapping,
  useCreateModelMapping as useSDKCreateModelMapping,
  useUpdateModelMapping as useSDKUpdateModelMapping,
  useDeleteModelMapping as useSDKDeleteModelMapping,
  
  // Analytics hooks
  useCostSummary as useSDKCostSummary,
  useCostByPeriod as useSDKCostByPeriod,
  useCostByModel as useSDKCostByModel,
  useCostByKey as useSDKCostByKey,
  useRequestLogs as useSDKRequestLogs,
  useRequestLog as useSDKRequestLog,
  useSearchLogs as useSDKSearchLogs,
  useUsageMetrics as useSDKUsageMetrics,
  useModelUsage as useSDKModelUsage,
  useKeyUsage as useSDKKeyUsage,
  useExportAnalytics as useSDKExportAnalytics,
  useExportUsageAnalytics as useSDKExportUsageAnalytics,
  useExportCostAnalytics as useSDKExportCostAnalytics,
  useExportRequestLogs as useSDKExportRequestLogs,
} from '@knn_labs/conduit-admin-client/react-query';

// Re-export the SDK hooks with WebUI-specific wrappers if needed
export function useProviders() {
  return useSDKProviders();
}

export function useProvider(id: string) {
  return useSDKProvider({ id: parseInt(id, 10) });
}

export function useCreateProvider() {
  return useSDKCreateProvider();
}

export function useUpdateProvider() {
  return useSDKUpdateProvider();
}

export function useDeleteProvider() {
  return useSDKDeleteProvider();
}

export function useDiscoverProviderModels() {
  return useSDKDiscoverProviderModels();
}

// Virtual Keys
export function useVirtualKeys() {
  return useSDKVirtualKeys();
}

export function useVirtualKey(id: string) {
  return useSDKVirtualKey(parseInt(id, 10));
}

export function useCreateVirtualKey() {
  return useSDKCreateVirtualKey();
}

export function useUpdateVirtualKey() {
  return useSDKUpdateVirtualKey();
}

export function useDeleteVirtualKey() {
  return useSDKDeleteVirtualKey();
}

// Model Mappings
export function useModelMappings() {
  return useSDKModelMappings();
}

export function useModelMapping(id: string) {
  return useSDKModelMapping(parseInt(id, 10));
}

export function useCreateModelMapping() {
  return useSDKCreateModelMapping();
}

export function useUpdateModelMapping() {
  return useSDKUpdateModelMapping();
}

export function useDeleteModelMapping() {
  return useSDKDeleteModelMapping();
}

// Analytics hooks
export function useCostSummary(dateRange: any) {
  return useSDKCostSummary(dateRange);
}

export function useCostByPeriod(params: any) {
  return useSDKCostByPeriod(params);
}

export function useCostByModel(dateRange: any) {
  return useSDKCostByModel(dateRange);
}

export function useCostByKey(dateRange: any) {
  return useSDKCostByKey(dateRange);
}

export function useRequestLogs(filters?: any) {
  return useSDKRequestLogs(filters);
}

export function useRequestLog(id: string) {
  return useSDKRequestLog(id);
}

export function useSearchLogs(query: string, filters?: any) {
  return useSDKSearchLogs({ query, filters });
}

export function useUsageMetrics(dateRange: any) {
  return useSDKUsageMetrics(dateRange);
}

export function useModelUsage(modelId: string, dateRange: any) {
  return useSDKModelUsage({ modelId, dateRange });
}

export function useKeyUsage(keyId: number, dateRange: any) {
  return useSDKKeyUsage({ keyId, dateRange });
}

export function useExportAnalytics(params: any) {
  return useSDKExportAnalytics(params);
}

export function useExportUsageAnalytics() {
  return useSDKExportUsageAnalytics();
}

export function useExportCostAnalytics() {
  return useSDKExportCostAnalytics();
}

export function useExportRequestLogs() {
  return useSDKExportRequestLogs();
}

// Re-export types for convenience
export type {
  ProviderCredentialDto,
  CreateProviderCredentialDto,
  UpdateProviderCredentialDto,
  VirtualKeyDto,
  CreateVirtualKeyRequest,
  UpdateVirtualKeyRequest,
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  CostSummaryDto,
  CostByPeriodDto,
  RequestLogDto,
  UsageMetricsDto,
} from '@knn_labs/conduit-admin-client';

// Additional custom hooks that are not in SDK yet
export { useTestProvider, useTestProviderConnection, useTestModelMapping, useBulkDiscoverModelMappings } from '@/hooks/api/useAdminApi';