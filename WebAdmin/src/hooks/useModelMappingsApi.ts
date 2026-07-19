import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { notify } from '@/lib/notifications';
import { useAdminMutation } from '@/hooks/useAdminMutation';
import type {
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto
} from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

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

export function useReorderFailoverChain() {
  return useAdminMutation({
    // Writes priority = list index for an alias's mappings after a drag-reorder. The Admin
    // API has no batch-priority endpoint; the SDK's bulkUpdate fans out to per-mapping
    // updates. Unchanged rows are skipped.
    mutationFn: (orderedMappings: ModelProviderMappingDto[]) => client =>
      client.modelMappings.bulkUpdate(
        orderedMappings
          .map((mapping, index) => ({ id: mapping.id, data: { id: mapping.id, priority: index } }))
          .filter((update, index) => orderedMappings[index].priority !== index)
      ),
    successMessage: 'Failover order saved',
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
    capabilities: {
      supportsVision: boolean;
      supportsImageGeneration: boolean;
      supportsAudioTranscription: boolean;
      supportsTextToSpeech: boolean;
      supportsRealtimeAudio: boolean;
      supportsFunctionCalling: boolean;
      supportsStreaming: boolean;
      supportsVideoGeneration: boolean;
      supportsEmbeddings: boolean;
      supportsChat: boolean;
      maxContextLength?: number | null;
      maxOutputTokens?: number | null;
    };
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

      // TODO: Use mapped aliases to check for conflicts
      // const mappedModelAliases = new Set(
      //   existingMappings
      //     .filter(m => m.providerId?.toString() === providerId)
      //     .map(m => m.modelAlias)
      // );

      // Transform provider-specific models to discovery result format
      const result: BulkDiscoverResult = {
        providerId,
        providerName,
        models: providerModels.map(model => {
          // The backend now returns providerModelId for provider-specific endpoints
          const providerModelId = (model as { providerModelId?: string }).providerModelId ?? model.name ?? undefined;

          return {
            modelId: model.id?.toString() ?? '',
            displayName: model.name ?? model.id?.toString() ?? '',
            providerId,
            providerModelId, // Store the provider-specific model ID
            hasConflict: false, // TODO: Check against mapped aliases
            existingMapping: null,
            capabilities: {
              supportsVision: model.supportsVision ?? false,
              supportsImageGeneration: model.supportsImageGeneration ?? false,
              supportsAudioTranscription: false, // Audio capabilities removed from project
              supportsTextToSpeech: false, // Audio capabilities removed from project
              supportsRealtimeAudio: false, // Audio capabilities removed from project
              supportsFunctionCalling: model.supportsFunctionCalling ?? false,
              supportsStreaming: model.supportsStreaming ?? true,
              supportsVideoGeneration: model.supportsVideoGeneration ?? false,
              supportsEmbeddings: model.supportsEmbeddings ?? false,
              supportsChat: model.supportsChat ?? true,
              maxContextLength: model.maxInputTokens ?? null,
              maxOutputTokens: model.maxOutputTokens ?? null,
            },
          };
        }),
        totalModels: providerModels.length,
        conflictCount: 0, // TODO: Implement proper conflict detection
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
    capabilities: {
      supportsVision: boolean;
      supportsImageGeneration: boolean;
      supportsAudioTranscription: boolean;
      supportsTextToSpeech: boolean;
      supportsRealtimeAudio: boolean;
      supportsFunctionCalling: boolean;
      supportsStreaming: boolean;
      supportsVideoGeneration: boolean;
      supportsEmbeddings: boolean;
      supportsChat: boolean;
      maxContextLength?: number | null;
      maxOutputTokens?: number | null;
    };
  }>;
  defaultPriority?: number;
  enableByDefault?: boolean;
}

interface BulkCreateResult {
  success: boolean;
  created: number;
  failed: number;
  details: {
    created: ModelProviderMappingDto[];
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
      // Transform request to Admin SDK format
      // TODO: This needs to be updated to create/find ModelProviderTypeAssociations first
      // For now, using a placeholder value of 1 - this will need proper implementation
      const bulkRequest = {
        mappings: request.models.map(model => ({
          modelAlias: model.providerModelId ?? model.displayName,  // Use provider model ID as alias
          providerId: parseInt(model.providerId, 10),
          providerModelId: model.providerModelId ?? model.displayName,  // Provider-specific model identifier
          modelProviderTypeAssociationId: 1, // TODO: Need to create/find association for each model
          isEnabled: request.enableByDefault ?? true,
          priority: request.defaultPriority ?? 50,
        })),
        replaceExisting: false,
      };

      const sdkResult = await withAdminClient(client =>
        client.modelMappings.bulkCreate(bulkRequest)
      );

      // Transform result back to expected format
      const result: BulkCreateResult = {
        success: sdkResult.failureCount === 0,
        created: sdkResult.successCount,
        failed: sdkResult.failureCount,
        details: {
          created: sdkResult.created,
          failed: sdkResult.errors.map((error, index) => ({
            modelId: request.models[index]?.modelId ?? 'unknown',
            error: error,
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
