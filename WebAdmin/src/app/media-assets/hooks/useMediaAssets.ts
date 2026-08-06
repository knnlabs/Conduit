import { useCallback, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { withAdminClient } from '@/lib/client/adminClient';
import { MediaRecord, MediaFilters } from '../types';
import { notify } from '@/lib/notifications';

export function useMediaAssets(virtualKeyId?: number) {
  const queryClient = useQueryClient();
  const queryKey = useMemo(
    () => ['media-assets', virtualKeyId] as const,
    [virtualKeyId],
  );
  const [filters, setFilters] = useState<MediaFilters>({
    mediaType: 'all',
    deletionState: 'active',
    sortBy: 'createdAt',
    sortOrder: 'desc',
  });

  const mediaQuery = useQuery({
    queryKey,
    enabled: virtualKeyId !== undefined,
    queryFn: () => {
      if (virtualKeyId === undefined) {
        throw new Error('A virtual key is required to load media assets');
      }
      return withAdminClient(client =>
        client.media.getMediaByVirtualKey(virtualKeyId, true)
      );
    },
  });

  const searchMutation = useMutation({
    mutationFn: (pattern: string) =>
      withAdminClient(client => client.media.searchMedia(pattern)),
    onSuccess: data => {
      queryClient.setQueryData(queryKey, data);
    },
  });

  const updateMedia = useCallback(
    (updater: (media: MediaRecord[]) => MediaRecord[]) => {
      queryClient.setQueryData<MediaRecord[]>(queryKey, current => updater(current ?? []));
    },
    [queryClient, queryKey],
  );

  const deleteMedia = async (mediaId: string, showNotification = true): Promise<boolean> => {
    try {
      const result = await withAdminClient(client =>
        client.media.deleteMedia(mediaId)
      );

      updateMedia(prev => result.isSoftDeleted
        ? prev.map(m => m.id === mediaId
          ? { ...m, deletedAt: result.deletedAt ?? new Date().toISOString() }
          : m)
        : prev.filter(m => m.id !== mediaId));
      if (showNotification) {
        notify.success('Media deleted successfully');
      }
      return true;
    } catch (err) {
      if (showNotification) {
        notify.error(err, 'Failed to delete media');
      }
      return false;
    }
  };

  const restoreMedia = async (mediaId: string): Promise<boolean> => {
    try {
      await withAdminClient(client => client.media.restoreMedia(mediaId));
      updateMedia(prev => prev.map(m => m.id === mediaId
        ? { ...m, deletedAt: undefined }
        : m));
      notify.success('Media restored successfully');
      return true;
    } catch (err) {
      notify.error(err, 'Failed to restore media');
      return false;
    }
  };

  const searchMedia = async (pattern: string): Promise<void> => {
    if (!pattern) {
      await mediaQuery.refetch();
      return;
    }

    try {
      await searchMutation.mutateAsync(pattern);
    } catch (err) {
      notify.error(err, 'Failed to search media');
    }
  };

  const applyFilters = useCallback((newFilters: Partial<MediaFilters>) => {
    setFilters(prev => ({ ...prev, ...newFilters }));
  }, []);

  const getFilteredMedia = useCallback(() => {
    let filtered = [...(mediaQuery.data ?? [])];

    // Filter by media type
    if (filters.mediaType && filters.mediaType !== 'all') {
      filtered = filtered.filter(m => m.mediaType === filters.mediaType);
    }

    if (filters.deletionState === 'active') {
      filtered = filtered.filter(m => !m.deletedAt);
    } else if (filters.deletionState === 'deleted') {
      filtered = filtered.filter(m => !!m.deletedAt);
    }

    // Filter by provider
    if (filters.provider) {
      filtered = filtered.filter(m => m.provider === filters.provider);
    }

    // Filter by date range
    if (filters.fromDate) {
      const fromDate = filters.fromDate instanceof Date ? filters.fromDate : new Date(filters.fromDate);
      filtered = filtered.filter(m => new Date(m.createdAt) >= fromDate);
    }
    if (filters.toDate) {
      const toDate = filters.toDate instanceof Date ? filters.toDate : new Date(filters.toDate);
      filtered = filtered.filter(m => new Date(m.createdAt) <= toDate);
    }

    // Filter by prompt search
    if (filters.searchQuery) {
      const query = filters.searchQuery.toLowerCase();
      filtered = filtered.filter(m => 
        (m.prompt?.toLowerCase().includes(query) ?? false) ||
        m.storageKey.toLowerCase().includes(query)
      );
    }

    // Sort
    filtered.sort((a, b) => {
      const aVal = a[filters.sortBy as keyof MediaRecord] as string | number;
      const bVal = b[filters.sortBy as keyof MediaRecord] as string | number;
      
      if (filters.sortOrder === 'asc') {
        return aVal > bVal ? 1 : -1;
      } else {
        return aVal < bVal ? 1 : -1;
      }
    });

    return filtered;
  }, [mediaQuery.data, filters]);

  return {
    media: getFilteredMedia(),
    isLoading: mediaQuery.isFetching || searchMutation.isPending,
    error: mediaQuery.error instanceof Error ? mediaQuery.error.message : null,
    filters,
    applyFilters,
    deleteMedia,
    restoreMedia,
    searchMedia,
    refetch: async () => {
      await mediaQuery.refetch();
    },
  };
}
