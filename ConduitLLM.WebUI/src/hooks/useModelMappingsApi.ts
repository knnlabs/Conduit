import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import type { 
  ModelProviderMappingDto, 
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  DiscoveredModel
} from '@knn_labs/conduit-admin-client';

const QUERY_KEY = 'model-mappings';

export function useModelMappings() {
  
  const { data: mappings = [], isLoading, error, refetch } = useQuery({
    queryKey: [QUERY_KEY],
    queryFn: async () => {
      const response = await fetch('/api/model-mappings');
      if (!response.ok) {
        throw new Error('Failed to fetch model mappings');
      }
      return response.json() as Promise<ModelProviderMappingDto[]>;
    },
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
    queryFn: async () => {
      if (!id) return null;
      const response = await fetch(`/api/model-mappings/${id}`);
      if (!response.ok) {
        throw new Error('Failed to fetch model mapping');
      }
      return response.json() as Promise<ModelProviderMappingDto>;
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
    mutationFn: async (data: CreateModelProviderMappingDto) => {
      const response = await fetch('/api/model-mappings', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const result = await response.json() as unknown;
        const error = result as { message?: string };
        throw new Error(error.message ?? 'Failed to create model mapping');
      }

      return response.json() as Promise<ModelProviderMappingDto>;
    },
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
    mutationFn: async ({ id, data }: { id: number; data: UpdateModelProviderMappingDto }) => {
      
      const response = await fetch(`/api/model-mappings/${id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });


      if (!response.ok) {
        const result = await response.json() as unknown;
        const error = result as { message?: string; details?: string };
        
        // Try to parse the details if it's a JSON string
        let detailsMessage = error.message ?? 'Failed to update model mapping';
        if (error.details) {
          try {
            const details = JSON.parse(error.details) as { detail?: string; message?: string };
            detailsMessage = details.detail ?? details.message ?? detailsMessage;
          } catch {
            detailsMessage = error.details;
          }
        }
        
        throw new Error(detailsMessage);
      }

      const result = await response.json() as ModelProviderMappingDto;
      return result;
    },
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
    mutationFn: async (id: number) => {
      const response = await fetch(`/api/model-mappings/${id}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        const result = await response.json() as unknown;
        const error = result as { message?: string };
        throw new Error(error.message ?? 'Failed to delete model mapping');
      }
    },
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


interface DiscoveredModelWithStatus extends DiscoveredModel {
  created?: boolean;
}

export function useDiscoverModels() {
  const [isDiscovering, setIsDiscovering] = useState(false);
  const queryClient = useQueryClient();

  const discoverModels = async (autoCreate: boolean = false, enableNewMappings: boolean = false) => {
    setIsDiscovering(true);
    try {
      const response = await fetch('/api/model-mappings/discover', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          autoCreate,
          enableNewMappings,
        }),
      });

      if (!response.ok) {
        const result = await response.json() as unknown;
        const error = result as { message?: string };
        throw new Error(error.message ?? 'Failed to discover models');
      }

      const result = await response.json() as DiscoveredModelWithStatus[];
      
      if (autoCreate) {
        // Invalidate cache to show new mappings
        void await queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
        
        const created = result.filter(m => m.created === true).length;
        notifications.show({
          title: 'Discovery Complete',
          message: `Created ${created} new model mappings`,
          color: 'green',
        });
      } else {
        notifications.show({
          title: 'Discovery Complete',
          message: `Found ${result.length} models across all providers`,
          color: 'green',
        });
      }

      return result;
    } catch (error) {
      notifications.show({
        title: 'Discovery Failed',
        message: error instanceof Error ? error.message : 'Failed to discover models',
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
      const response = await fetch('/api/model-mappings/bulk-discover', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          providerId,
          providerName,
        }),
      });

      if (!response.ok) {
        const result = await response.json() as unknown;
        const error = result as { message?: string };
        throw new Error(error.message ?? 'Failed to discover models');
      }

      const result = await response.json() as BulkDiscoverResult;
      return result;
    } catch (error) {
      notifications.show({
        title: 'Discovery Failed',
        message: error instanceof Error ? error.message : 'Failed to discover models',
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
      const response = await fetch('/api/model-mappings/bulk-create', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const result = await response.json() as unknown;
        const error = result as { message?: string };
        throw new Error(error.message ?? 'Failed to create mappings');
      }

      const result = await response.json() as BulkCreateResult;
      
      // Invalidate cache to show new mappings
      void await queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
      
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