import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from './useAdminApi';
import { createAdminClient } from '@/lib/clients/conduit';
import { notifications } from '@mantine/notifications';

export function useOverallMediaStats() {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'overallStats'],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.media.getOverallStorageStats();
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useMediaByVirtualKey(virtualKeyId: number | undefined) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'byVirtualKey', virtualKeyId],
    queryFn: async () => {
      if (!virtualKeyId) return [];
      const adminClient = createAdminClient();
      return await adminClient.media.getMediaByVirtualKey(virtualKeyId);
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
      const adminClient = createAdminClient();
      return await adminClient.media.getStorageStatsByVirtualKey(virtualKeyId);
    },
    enabled: !!virtualKeyId,
    staleTime: 5 * 60 * 1000,
  });
}

export function useMediaStorageByProvider() {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'storageByProvider'],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.media.getStorageStatsByProvider();
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useMediaStorageByType() {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'storageByType'],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.media.getStorageStatsByMediaType();
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useSearchMedia(storageKeyPattern: string) {
  return useQuery({
    queryKey: [adminApiKeys.all, 'media', 'search', storageKeyPattern],
    queryFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.media.searchMediaByStorageKey(storageKeyPattern);
    },
    enabled: !!storageKeyPattern,
    staleTime: 5 * 60 * 1000,
  });
}

export function useDeleteMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (mediaId: string) => {
      const adminClient = createAdminClient();
      return await adminClient.media.deleteMedia(mediaId);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Media Deleted',
        message: 'The media file has been deleted successfully',
        color: 'green',
      });
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Delete Failed',
        message: error.message || 'Failed to delete media file',
        color: 'red',
      });
    },
  });
}

export function useCleanupExpiredMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.media.cleanupExpiredMedia();
    },
    onSuccess: (count) => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Cleanup Complete',
        message: `${count} expired media files have been cleaned up`,
        color: 'green',
      });
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Cleanup Failed',
        message: error.message || 'Failed to cleanup expired media',
        color: 'red',
      });
    },
  });
}

export function useCleanupOrphanedMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const adminClient = createAdminClient();
      return await adminClient.media.cleanupOrphanedMedia();
    },
    onSuccess: (count) => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Cleanup Complete',
        message: `${count} orphaned media files have been cleaned up`,
        color: 'green',
      });
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Cleanup Failed',
        message: error.message || 'Failed to cleanup orphaned media',
        color: 'red',
      });
    },
  });
}

export function usePruneOldMedia() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (daysToKeep: number) => {
      const adminClient = createAdminClient();
      return await adminClient.media.pruneOldMedia(daysToKeep);
    },
    onSuccess: (count) => {
      queryClient.invalidateQueries({ queryKey: [adminApiKeys.all, 'media'] });
      notifications.show({
        title: 'Prune Complete',
        message: `${count} old media files have been pruned`,
        color: 'green',
      });
    },
    onError: (error: any) => {
      notifications.show({
        title: 'Prune Failed',
        message: error.message || 'Failed to prune old media',
        color: 'red',
      });
    },
  });
}