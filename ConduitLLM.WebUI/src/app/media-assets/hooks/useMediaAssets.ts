import { useState, useEffect, useCallback } from 'react';
import { withAdminClient } from '@/lib/client/adminClient';
import { MediaRecord, MediaFilters } from '../types';
import { notifications } from '@mantine/notifications';

export function useMediaAssets(virtualKeyId?: number) {
  const [media, setMedia] = useState<MediaRecord[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filters, setFilters] = useState<MediaFilters>({
    mediaType: 'all',
    sortBy: 'createdAt',
    sortOrder: 'desc',
  });

  const fetchMedia = useCallback(async () => {
    if (!virtualKeyId) return;

    setLoading(true);
    setError(null);

    try {
      const data = await withAdminClient(client => 
        client.media.getMediaByVirtualKey(virtualKeyId)
      );
      setMedia(data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error';
      setError(errorMessage);
      notifications.show({
        title: 'Error',
        message: 'Failed to load media assets',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  }, [virtualKeyId]);

  const deleteMedia = async (mediaId: string): Promise<void> => {
    try {
      await withAdminClient(client => 
        client.media.deleteMedia(mediaId)
      );

      setMedia(prev => prev.filter(m => m.id !== mediaId));
      notifications.show({
        title: 'Success',
        message: 'Media deleted successfully',
        color: 'green',
      });
    } catch {
      notifications.show({
        title: 'Error',
        message: 'Failed to delete media',
        color: 'red',
      });
    }
  };

  const searchMedia = async (pattern: string): Promise<void> => {
    if (!pattern) {
      void fetchMedia();
      return;
    }

    setLoading(true);
    try {
      const data = await withAdminClient(client => 
        client.media.searchMedia(pattern)
      );
      setMedia(data);
    } catch {
      notifications.show({
        title: 'Error',
        message: 'Failed to search media',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  const applyFilters = useCallback((newFilters: Partial<MediaFilters>) => {
    setFilters(prev => ({ ...prev, ...newFilters }));
  }, []);

  const getFilteredMedia = useCallback(() => {
    let filtered = [...media];

    // Filter by media type
    if (filters.mediaType && filters.mediaType !== 'all') {
      filtered = filtered.filter(m => m.mediaType === filters.mediaType);
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
  }, [media, filters]);

  useEffect(() => {
    if (virtualKeyId) {
      void fetchMedia();
    }
  }, [virtualKeyId, fetchMedia]);

  return {
    media: getFilteredMedia(),
    loading,
    error,
    filters,
    applyFilters,
    deleteMedia,
    searchMedia,
    refetch: fetchMedia,
  };
}