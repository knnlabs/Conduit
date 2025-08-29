import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
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
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateModelProviderMappingDto) => 
      withAdminClient(client => client.modelMappings.create(data)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
      notifications.show({
        title: 'Success',
        message: 'Model mapping created successfully',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Error',
        message: error.message,
        color: 'red',
      });
    },
  });
}

export function useUpdateModelMapping() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: UpdateModelProviderMappingDto }) => 
      withAdminClient(client => client.modelMappings.update(id, data)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
      notifications.show({
        title: 'Success',
        message: 'Model mapping updated successfully',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Error',
        message: error.message,
        color: 'red',
      });
    },
  });
}

export function useDeleteModelMapping() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => 
      withAdminClient(client => client.modelMappings.deleteById(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
      notifications.show({
        title: 'Success',
        message: 'Model mapping deleted successfully',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Error',
        message: error.message,
        color: 'red',
      });
    },
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
      const [providerModels, existingMappings] = await Promise.all([
        withAdminClient(client => client.models.getByProvider(providerName.toLowerCase())),
        withAdminClient(client => client.modelMappings.list())
      ]);

      // Create a set of model IDs that already have mappings for this provider
      const mappedModelIds = new Set(
        existingMappings
          .filter(m => m.providerId?.toString() === providerId)
          .map(m => m.modelId)
      );

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
            hasConflict: model.id ? mappedModelIds.has(model.id) : false,
            existingMapping: null,
            capabilities: {
              supportsVision: model.capabilities?.supportsVision ?? false,
              supportsImageGeneration: model.capabilities?.supportsImageGeneration ?? false,
              supportsAudioTranscription: false, // Audio capabilities removed from project
              supportsTextToSpeech: false, // Audio capabilities removed from project
              supportsRealtimeAudio: false, // Audio capabilities removed from project
              supportsFunctionCalling: model.capabilities?.supportsFunctionCalling ?? false,
              supportsStreaming: model.capabilities?.supportsStreaming ?? true,
              supportsVideoGeneration: model.capabilities?.supportsVideoGeneration ?? false,
              supportsEmbeddings: model.capabilities?.supportsEmbeddings ?? false,
              supportsChat: model.capabilities?.supportsChat ?? true,
              maxContextLength: model.capabilities?.maxTokens ?? null,
              maxOutputTokens: null,
            },
          };
        }),
        totalModels: providerModels.length,
        conflictCount: providerModels.filter(m => m.id && mappedModelIds.has(m.id)).length,
      };

      return result;
    } catch (err) {
      const error = err instanceof Error ? err : new Error('Failed to discover models');
      notifications.show({
        title: 'Discovery Failed',
        message: error.message,
        color: 'red',
      });
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
      const bulkRequest = {
        mappings: request.models.map(model => ({
          modelAlias: model.providerModelId ?? model.displayName,  // Use provider model ID as alias
          modelId: parseInt(model.modelId, 10),  // Use the actual model ID from the Model entity
          providerId: parseInt(model.providerId, 10),
          providerModelId: model.providerModelId ?? model.displayName,  // Provider-specific model identifier
          isEnabled: request.enableByDefault ?? true,
          priority: request.defaultPriority ?? 50,
          isDefault: false,
          maxContextTokensOverride: model.capabilities.maxContextLength ?? undefined,
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
    } catch (error) {
      notifications.show({
        title: 'Bulk Creation Failed',
        message: error instanceof Error ? error.message : 'Failed to create mappings',
        color: 'red',
      });
      throw error;
    } finally {
      setIsCreating(false);
    }
  };

  return {
    createMappings,
    isCreating,
  };
}