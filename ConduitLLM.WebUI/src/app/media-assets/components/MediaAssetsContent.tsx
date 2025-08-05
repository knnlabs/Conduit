'use client';

import { useState, useEffect } from 'react';
import { Stack, Group, Button, Select, Text } from '@mantine/core';
import { IconRefresh, IconTrash } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { modals } from '@mantine/modals';
import type { VirtualKeyListResponseDto } from '@knn_labs/conduit-admin-client';
import { useMediaAssets } from '../hooks/useMediaAssets';
import { useBulkSelection } from '../hooks/useBulkSelection';
import { MediaRecord, VirtualKeyInfo } from '../types';
import MediaStatsCards from './MediaStatsCards';
import MediaFilterBar from './MediaFilterBar';
import MediaGallery from './MediaGallery';
import MediaDetailModal from './MediaDetailModal';
import BulkActionsBar from './BulkActionsBar';
import CleanupModal from './CleanupModal';

export default function MediaAssetsContent() {
  const [selectedVirtualKey, setSelectedVirtualKey] = useState<number | undefined>();
  const [virtualKeys, setVirtualKeys] = useState<VirtualKeyInfo[]>([]);
  const [loadingVirtualKeys, setLoadingVirtualKeys] = useState(true);
  const [selectedMedia, setSelectedMedia] = useState<MediaRecord | null>(null);
  const [cleanupModalOpened, setCleanupModalOpened] = useState(false);

  const {
    media,
    loading,
    filters,
    applyFilters,
    deleteMedia,
    refetch,
  } = useMediaAssets(selectedVirtualKey);

  const {
    selectedIds,
    selectedCount,
    toggleSelection,
    deselectAll,
    getSelectedMedia,
  } = useBulkSelection(media);

  // Fetch virtual keys
  useEffect(() => {
    const fetchVirtualKeys = async () => {
      try {
        setLoadingVirtualKeys(true);
        const response = await fetch('/api/virtualkeys');
        if (response.ok) {
          const data = await response.json() as unknown;
          
          // Handle both array format and paginated response format
          let items: any[] = [];
          if (Array.isArray(data)) {
            items = data;
          } else if (data && typeof data === 'object' && 'items' in data && Array.isArray((data as any).items)) {
            items = (data as any).items;
          } else {
            console.warn('Unexpected virtualkeys API response format:', data);
          }
          
          const virtualKeysData: VirtualKeyInfo[] = items.map(item => ({
            id: item.id ?? 0,
            name: item.keyName,
            key: item.keyPrefix ?? `vk_${item.id ?? 'unknown'}`
          }));
          
          setVirtualKeys(virtualKeysData);
          
          // Select first virtual key by default
          if (virtualKeysData.length > 0) {
            setSelectedVirtualKey(virtualKeysData[0].id);
          }
        } else {
          console.error('Failed to fetch virtual keys:', response.status);
          notifications.show({
            title: 'Error',
            message: 'Failed to load virtual keys',
            color: 'red',
          });
        }
      } catch (error) {
        console.error('Failed to fetch virtual keys:', error);
        notifications.show({
          title: 'Error',
          message: 'Failed to load virtual keys',
          color: 'red',
        });
      } finally {
        setLoadingVirtualKeys(false);
      }
    };

    void fetchVirtualKeys();
  }, []);

  const handleDeleteMedia = async (id: string) => {
    modals.openConfirmModal({
      title: 'Delete Media',
      children: (
        <Text size="sm">
          Are you sure you want to delete this media? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => {
        void deleteMedia(id);
      },
    });
  };

  const handleBulkDelete = () => {
    const count = selectedCount;
    modals.openConfirmModal({
      title: 'Delete Multiple Items',
      children: (
        <Text size="sm">
          Are you sure you want to delete {count} media items? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete All', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => {
        void (async () => {
          const selectedMedia = getSelectedMedia();
          
          for (const media of selectedMedia) {
            await deleteMedia(media.id);
          }
          
          deselectAll();
          notifications.show({
            title: 'Success',
            message: `Deleted ${count} media items`,
            color: 'green',
          });
        })();
      },
    });
  };

  const handleBulkDownload = async () => {
    const selectedMedia = getSelectedMedia();
    
    for (const media of selectedMedia) {
      if (media.publicUrl) {
        const link = document.createElement('a');
        link.href = media.publicUrl;
        link.download = `${media.mediaType}-${media.id}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        // Small delay between downloads
        await new Promise(resolve => setTimeout(resolve, 100));
      }
    }
    
    notifications.show({
      title: 'Success',
      message: `Downloaded ${selectedCount} files`,
      color: 'green',
    });
  };

  // Get unique providers from media
  const providers = Array.from(new Set(media.map(m => m.provider).filter(Boolean))) as string[];

  return (
    <Stack gap="xl">
      <MediaStatsCards />

      <Group justify="space-between">
        <Select
          placeholder={loadingVirtualKeys ? "Loading virtual keys..." : "Select a virtual key"}
          data={virtualKeys.map(vk => ({
            value: vk.id.toString(),
            label: `${vk.name} (${vk.key.substring(0, 8)}...)`,
          }))}
          value={selectedVirtualKey?.toString()}
          onChange={(value) => setSelectedVirtualKey(value ? parseInt(value) : undefined)}
          w={300}
          disabled={loadingVirtualKeys || virtualKeys.length === 0}
          nothingFoundMessage="No virtual keys found"
        />

        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={() => void refetch()}
            loading={loading}
          >
            Refresh
          </Button>
          <Button
            variant="light"
            color="orange"
            leftSection={<IconTrash size={16} />}
            onClick={() => setCleanupModalOpened(true)}
          >
            Cleanup Tools
          </Button>
        </Group>
      </Group>

      <MediaFilterBar
        filters={filters}
        onFiltersChange={applyFilters}
        providers={providers}
        virtualKeyId={selectedVirtualKey}
      />

      <BulkActionsBar
        selectedCount={selectedCount}
        onDeleteSelected={() => void handleBulkDelete()}
        onDownloadSelected={() => void handleBulkDownload()}
        onClearSelection={deselectAll}
      />

      <div>
        <Group justify="space-between" mb="md">
          <Text c="dimmed">
            {media.length} media item{media.length !== 1 ? 's' : ''} found
          </Text>
        </Group>

        <MediaGallery
          media={media}
          loading={loading}
          selectedIds={selectedIds}
          onSelectMedia={toggleSelection}
          onViewMedia={setSelectedMedia}
          onDeleteMedia={(id) => void handleDeleteMedia(id)}
        />
      </div>

      <MediaDetailModal
        media={selectedMedia}
        opened={!!selectedMedia}
        onClose={() => setSelectedMedia(null)}
        onDelete={(id) => void handleDeleteMedia(id)}
      />

      <CleanupModal
        opened={cleanupModalOpened}
        onClose={() => setCleanupModalOpened(false)}
        onSuccess={() => void refetch()}
      />
    </Stack>
  );
}