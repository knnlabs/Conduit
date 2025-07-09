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
} from '@knn_labs/conduit-admin-client';

// Additional custom hooks that are not in SDK yet
export { useTestProvider, useTestProviderConnection, useTestModelMapping, useBulkDiscoverModelMappings } from '@/hooks/api/useAdminApi';