import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import type { 
  ModelProviderMappingDto, 
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto,
  DiscoveredModel
} from '@/types/api-types';

const QUERY_KEY = 'model-mappings';

export function useModelMappings() {
  const queryClient = useQueryClient();
  
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
        const error = await response.json();
        throw new Error(error.message || 'Failed to create model mapping');
      }

      return response.json() as Promise<ModelProviderMappingDto>;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
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
        const error = await response.json();
        throw new Error(error.message || 'Failed to update model mapping');
      }

      return response.json() as Promise<ModelProviderMappingDto>;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
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
        const error = await response.json();
        throw new Error(error.message || 'Failed to delete model mapping');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
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

export function useTestModelMapping() {
  return useMutation({
    mutationFn: async (id: number) => {
      const response = await fetch(`/api/model-mappings/${id}/test`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to test model mapping');
      }

      return response.json() as Promise<{
        isSuccessful: boolean;
        message: string;
        details?: {
          modelId: string;
          providerId: string;
          providerModelId: string;
          capability: string;
          isSupported: boolean;
        };
      }>;
    },
    onSuccess: (data: { isSuccessful: boolean; message: string; details?: { modelId: string; providerId: string; providerModelId: string; capability: string; isSupported: boolean; } }) => {
      notifications.show({
        title: data.isSuccessful ? 'Test Passed' : 'Test Failed',
        message: data.message,
        color: data.isSuccessful ? 'green' : 'orange',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Test Error',
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
        const error = await response.json();
        throw new Error(error.message || 'Failed to discover models');
      }

      const result = await response.json() as DiscoveredModelWithStatus[];
      
      if (autoCreate) {
        // Invalidate cache to show new mappings
        await queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
        
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