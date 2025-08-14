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
      const models = await withAdminClient(client => 
        client.modelMappings.discoverProviderModels(parseInt(providerId, 10))
      ) as unknown as Array<{ modelId: string; displayName?: string; capabilities?: Record<string, boolean>; contextWindow?: number; maxOutputTokens?: number }>;

      // Transform the discovered models to match the expected interface
      const result: BulkDiscoverResult = {
        providerId,
        providerName,
        models: (models as Array<{ modelId: string; displayName?: string; capabilities?: Record<string, boolean>; contextWindow?: number; maxOutputTokens?: number }>).map(model => ({
          modelId: model.modelId,
          displayName: model.displayName ?? model.modelId,
          providerId,
          hasConflict: false, // The SDK doesn't provide this info directly
          existingMapping: null, // Would need a separate lookup
          capabilities: {
            supportsVision: (model.capabilities?.supportsVision as boolean) ?? false,
            supportsImageGeneration: (model.capabilities?.supportsImageGeneration as boolean) ?? false,
            supportsAudioTranscription: (model.capabilities?.supportsAudioTranscription as boolean) ?? false,
            supportsTextToSpeech: (model.capabilities?.supportsTextToSpeech as boolean) ?? false,
            supportsRealtimeAudio: (model.capabilities?.supportsRealtimeAudio as boolean) ?? false,
            supportsFunctionCalling: (model.capabilities?.supportsFunctionCalling as boolean) ?? false,
            supportsStreaming: (model.capabilities?.supportsStreaming as boolean) ?? true,
            supportsVideoGeneration: (model.capabilities?.supportsVideoGeneration as boolean) ?? false,
            supportsEmbeddings: (model.capabilities?.supportsEmbeddings as boolean) ?? false,
            supportsChat: (model.capabilities?.supportsChat as boolean) ?? true,
            maxContextLength: model.contextWindow ?? null,
            maxOutputTokens: model.maxOutputTokens ?? null,
          },
        })),
        totalModels: Array.isArray(models) ? models.length : 0,
        conflictCount: 0,
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
          modelId: model.modelId,
          providerId: parseInt(model.providerId, 10),
          providerModelId: model.modelId,
          isEnabled: request.enableByDefault ?? true,
          priority: request.defaultPriority ?? 50,
          isDefault: false,
          // Capability flags
          supportsVision: model.capabilities.supportsVision,
          supportsImageGeneration: model.capabilities.supportsImageGeneration,
          supportsAudioTranscription: model.capabilities.supportsAudioTranscription,
          supportsTextToSpeech: model.capabilities.supportsTextToSpeech,
          supportsRealtimeAudio: model.capabilities.supportsRealtimeAudio,
          supportsFunctionCalling: model.capabilities.supportsFunctionCalling,
          supportsStreaming: model.capabilities.supportsStreaming,
          supportsVideoGeneration: model.capabilities.supportsVideoGeneration,
          supportsEmbeddings: model.capabilities.supportsEmbeddings,
          supportsChat: model.capabilities.supportsChat,
          maxContextLength: model.capabilities.maxContextLength ?? undefined,
          maxOutputTokens: model.capabilities.maxOutputTokens ?? undefined,
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