import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { notify } from '@/lib/notifications';
import { useAdminMutation } from '@/hooks/useAdminMutation';
import type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto
} from '@/lib/admin-api';
import { withAdminClient } from '@/lib/client/adminClient';
import {
  mapDiscoveredModelCapabilities,
  type DiscoveredModelCapabilities,
} from './modelCapabilities';

const QUERY_KEY = 'model-mappings';

export function useModelMappings() {

  const { data: mappings = [], isLoading, error, refetch } = useQuery({
    queryKey: [QUERY_KEY],
    queryFn: () => withAdminClient(client => client.modelMappings.list()),
  });

  return {
    mappings,
    isLoading,
    error,
    refetch,
  };
}

export function useModelMapping(id: number | null) {
  const { data: mapping, isLoading, error } = useQuery({
    queryKey: [QUERY_KEY, id],
    queryFn: () => {
      if (!id) return null;
      return withAdminClient(client => client.modelMappings.getById(id));
    },
    enabled: !!id,
  });

  return {
    mapping,
    isLoading,
    error,
  };
}

export function useCreateModelMapping() {
  return useAdminMutation({
    mutationFn: (data: CreateModelProviderMappingDto) => client => client.modelMappings.create(data),
    successMessage: 'Model mapping created successfully',
    invalidateKeys: [QUERY_KEY],
  });
}

export function useUpdateModelMapping() {
  return useAdminMutation({
    mutationFn: ({ id, data }: { id: number; data: UpdateModelProviderMappingDto }) => client => client.modelMappings.update(id, data),
    successMessage: 'Model mapping updated successfully',
    invalidateKeys: [QUERY_KEY],
  });
}

export function useDeleteModelMapping() {
  return useAdminMutation({
    mutationFn: (id: number) => client => client.modelMappings.deleteById(id),
    successMessage: 'Model mapping deleted successfully',
    invalidateKeys: [QUERY_KEY],
  });
}

export function useBulkDeleteModelMappings() {
  return useAdminMutation({
    mutationFn: (ids: number[]) => client => client.modelMappings.bulkDelete(ids),
    successMessage: (result) => result.failureCount > 0
      ? `Deleted ${result.successCount} mappings. ${result.failureCount} failed.`
      : `Successfully deleted ${result.successCount} mappings`,
    invalidateKeys: [QUERY_KEY],
  });
}

export function useBulkEnableModelMappings() {
  return useAdminMutation({
    mutationFn: (ids: number[]) => client => client.modelMappings.bulkEnable(ids),
    successMessage: (result) => result.failureCount > 0
      ? `Enabled ${result.successCount} mappings. ${result.failureCount} failed.`
      : `Successfully enabled ${result.successCount} mappings`,
    invalidateKeys: [QUERY_KEY],
  });
}

export function useBulkDisableModelMappings() {
  return useAdminMutation({
    mutationFn: (ids: number[]) => client => client.modelMappings.bulkDisable(ids),
    successMessage: (result) => result.failureCount > 0
      ? `Disabled ${result.successCount} mappings. ${result.failureCount} failed.`
      : `Successfully disabled ${result.successCount} mappings`,
    invalidateKeys: [QUERY_KEY],
  });
}



interface BulkDiscoverResult {
  providerId: string;
  providerName: string;
  models: Array<{
    modelId: string;
    displayName: string;
    providerId: string;
    providerModelId?: string;
    hasConflict: boolean;
    existingMapping: ModelProviderMappingDto | null;
    conflictReason: string | null;
    modelProviderTypeAssociationId: number | null;
    capabilities: DiscoveredModelCapabilities;
  }>;
  totalModels: number;
  conflictCount: number;
}

export function useBulkDiscoverModels() {
  const [isDiscovering, setIsDiscovering] = useState(false);

  const discoverModels = async (providerId: string, providerName: string): Promise<BulkDiscoverResult> => {
    setIsDiscovering(true);
    try {
      // Fetch models available from this specific provider using the new SDK method
      const providerModels = await withAdminClient(client =>
        client.models.getByProvider(providerName.toLowerCase())
      );

      const discoveredModels = providerModels.map(model => {
        const providerModelId = (model as { providerModelId?: string }).providerModelId ?? model.name ?? '';
        return { model, providerModelId };
      });
      if (discoveredModels.length === 0) {
        return {
          providerId,
          providerName,
          models: [],
          totalModels: 0,
          conflictCount: 0,
        };
      }
      const preview = await withAdminClient(client => client.modelMappings.previewBulk({
        mappings: discoveredModels.map(({ providerModelId }) => ({
          modelAlias: providerModelId,
          providerId: Number.parseInt(providerId, 10),
          providerModelId,
        })),
      }));

      // Transform provider-specific models to discovery result format
      const result: BulkDiscoverResult = {
        providerId,
        providerName,
        models: discoveredModels.map(({ model, providerModelId }, index) => {
          const resolution = preview.items[index];

          return {
            modelId: model.id?.toString() ?? '',
            displayName: model.name ?? model.id?.toString() ?? '',
            providerId,
            providerModelId,
            hasConflict: resolution?.hasConflict ?? true,
            existingMapping: resolution?.existingMapping ?? null,
            conflictReason: resolution?.errorMessage ?? null,
            modelProviderTypeAssociationId: resolution?.modelProviderTypeAssociationId ?? null,
            capabilities: mapDiscoveredModelCapabilities(model),
          };
        }),
        totalModels: providerModels.length,
        conflictCount: preview.conflictCount ?? preview.items.filter(item => item.hasConflict).length,
      };

      return result;
    } catch (err) {
      const error = err instanceof Error ? err : new Error('Failed to discover models');
      notify.error(err, 'Failed to discover models');
      throw error;
    } finally {
      setIsDiscovering(false);
    }
  };

  return {
    discoverModels,
    isDiscovering,
  };
}

interface BulkCreateRequest {
  models: Array<{
    modelId: string;
    displayName: string;
    providerId: string;
    providerModelId?: string;
    capabilities: DiscoveredModelCapabilities;
  }>;
  defaultPriority?: number;
  enableByDefault?: boolean;
}

interface BulkCreateResult {
  success: boolean;
  created: number;
  existing: number;
  failed: number;
  details: {
    created: ModelProviderMappingDto[];
    existing: ModelProviderMappingDto[];
    failed: Array<{
      modelId: string;
      error: string;
    }>;
  };
}

export function useBulkCreateMappings() {
  const [isCreating, setIsCreating] = useState(false);
  const queryClient = useQueryClient();

  const createMappings = async (request: BulkCreateRequest): Promise<BulkCreateResult> => {
    setIsCreating(true);
    try {
      const bulkRequest = {
        mappings: request.models.map(model => ({
          modelAlias: model.providerModelId ?? model.displayName,
          providerId: parseInt(model.providerId, 10),
          providerModelId: model.providerModelId ?? model.displayName,
        })),
        isEnabled: request.enableByDefault ?? true,
        priority: request.defaultPriority ?? 50,
        weight: 1,
      };

      const sdkResult = await withAdminClient(client =>
        client.modelMappings.bulkCreate(bulkRequest)
      );

      // Transform result back to expected format
      const result: BulkCreateResult = {
        success: sdkResult.isSuccess ?? sdkResult.failed.length === 0,
        created: sdkResult.createdCount ?? sdkResult.created.length,
        existing: sdkResult.existingCount ?? sdkResult.existing.length,
        failed: sdkResult.failureCount ?? sdkResult.failed.length,
        details: {
          created: sdkResult.created,
          existing: sdkResult.existing,
          failed: sdkResult.failed.map(error => ({
            modelId: request.models[error.index]?.modelId ?? 'unknown',
            error: error.errorMessage ?? 'The mapping could not be created.',
          })),
        },
      };

      // Invalidate cache to show new mappings
      void queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });

      return result;
    } catch (err) {
      notify.error(err, 'Failed to create mappings');
      throw err;
    } finally {
      setIsCreating(false);
    }
  };

  return {
    createMappings,
    isCreating,
  };
}
