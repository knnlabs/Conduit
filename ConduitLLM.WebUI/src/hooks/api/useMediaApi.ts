import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { createAdminClient } from '@/lib/clients/conduit';
import { notifications } from '@mantine/notifications';

export function useOverallMediaStats() {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'overallStats'],
    queryFn: async () => {
      // Media API not yet available in SDK
      return {
        totalSize: 0,
        totalCount: 0,
        imageCount: 0,
        videoCount: 0,
        audioCount: 0,
      };
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useMediaByVirtualKey(virtualKeyId: number | undefined) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'byVirtualKey', virtualKeyId],
    queryFn: async () => {
      if (!virtualKeyId) return [];
      // Media API not yet available in SDK
      return [];
    },
    enabled: !!virtualKeyId,
    staleTime: 5 * 60 * 1000,
  });
}

export function useMediaStorageStatsByVirtualKey(virtualKeyId: number | undefined) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'statsByVirtualKey', virtualKeyId],
    queryFn: async () => {
      if (!virtualKeyId) return null;
      // Media API not yet available in SDK
      return {
        totalSize: 0,
        totalCount: 0,
        imageCount: 0,
        videoCount: 0,
        audioCount: 0,
      };
    },
    enabled: !!virtualKeyId,
    staleTime: 5 * 60 * 1000,
  });
}

export function useMediaStorageByProvider() {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'storageByProvider'],
    queryFn: async () => {
      // Media API not yet available in SDK
      return [];
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useMediaStorageByType() {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'storageByType'],
    queryFn: async () => {
      // Media API not yet available in SDK
      return [];
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useSearchMedia(storageKeyPattern: string) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'search', storageKeyPattern],
    queryFn: async () => {
      // Media API not yet available in SDK
      return [];
    },
    enabled: !!storageKeyPattern,
    staleTime: 5 * 60 * 1000,
  });
}

export function useDeleteMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (mediaId: string) => {
      // Media API not yet available in SDK
      throw new Error('Media deletion is not yet available');
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Media Deleted',
        message: 'The media file has been deleted successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to delete media file';
      notifications.show({
        title: 'Delete Failed',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function useCleanupExpiredMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      // Media API not yet available in SDK
      throw new Error('Media cleanup is not yet available');
    },
    onSuccess: (count: number) => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Cleanup Complete',
        message: `${count} expired media files have been cleaned up`,
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to cleanup expired media';
      notifications.show({
        title: 'Cleanup Failed',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function useCleanupOrphanedMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      // Media API not yet available in SDK
      throw new Error('Orphaned media cleanup is not yet available');
    },
    onSuccess: (count: number) => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Cleanup Complete',
        message: `${count} orphaned media files have been cleaned up`,
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to cleanup orphaned media';
      notifications.show({
        title: 'Cleanup Failed',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function usePruneOldMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (daysToKeep: number) => {
      // Media API not yet available in SDK
      throw new Error('Media pruning is not yet available');
    },
    onSuccess: (count: number) => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Prune Complete',
        message: `${count} old media files have been pruned`,
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to prune old media';
      notifications.show({
        title: 'Prune Failed',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}