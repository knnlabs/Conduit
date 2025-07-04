'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Grid,
  Group,
  Button,
  Select,
  TextInput,
  Badge,
  ActionIcon,
  Image,
  Paper,
  Pagination,
  Modal,
  Checkbox,
  Center,
  Loader,
  ThemeIcon,
  Menu,
} from '@mantine/core';
import {
  IconPhoto,
  IconVideo,
  IconSearch,
  IconFilter,
  IconDownload,
  IconTrash,
  IconEye,
  IconCalendar,
  IconKey,
  IconCloud,
  IconCopy,
  IconRefresh,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { DatePickerInput } from '@mantine/dates';
import { formatBytes, formatRelativeTime } from '@/lib/utils/formatting';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { 
  useOverallMediaStats, 
  useMediaByVirtualKey,
  useDeleteMedia,
  useMediaStorageByType,
  useCleanupExpiredMedia,
} from '@/hooks/api/useMediaApi';

export default function MediaAssetsPage() {
  const [selectedAssets, setSelectedAssets] = useState<Set<string>>(new Set());
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [virtualKeyFilter, setVirtualKeyFilter] = useState<string>('all');
  const [dateRange, setDateRange] = useState<[Date | null, Date | null]>([null, null]);
  const [currentPage, setCurrentPage] = useState(1);
  const [viewModalOpened, { open: openViewModal, close: closeViewModal }] = useDisclosure(false);
  const [selectedAsset, setSelectedAsset] = useState<unknown>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  
  // Fetch data from SDK
  const { data: virtualKeysData } = useVirtualKeys();
  const { data: overallStats, isLoading: statsLoading } = useOverallMediaStats();
  const { data: mediaByKey, isLoading: mediaLoading } = useMediaByVirtualKey(
    virtualKeyFilter !== 'all' ? parseInt(virtualKeyFilter) : undefined
  );
  const { data: _storageByType } = useMediaStorageByType();
  const { mutate: deleteMedia } = useDeleteMedia();
  const { mutate: _cleanupExpired } = useCleanupExpiredMedia();
  
  // Convert media data to display format
  const assets = mediaByKey || [];
  
  const itemsPerPage = 12;

  // Filter assets based on search and filters
  const filteredAssets = assets.filter((asset: unknown) => {
    const matchesSearch = searchQuery === '' || 
      ((asset as { storageKey?: string }).storageKey && (asset as { storageKey: string }).storageKey.toLowerCase().includes(searchQuery.toLowerCase())) ||
      ((asset as { metadata?: { model?: string } }).metadata?.model && (asset as { metadata: { model: string } }).metadata.model.toLowerCase().includes(searchQuery.toLowerCase()));
    
    const matchesType = typeFilter === 'all' || 
      (typeFilter === 'image' && (asset as { mediaType?: string }).mediaType?.includes('image')) ||
      (typeFilter === 'video' && (asset as { mediaType?: string }).mediaType?.includes('video'));
    
    let matchesDate = true;
    if (dateRange[0] && dateRange[1]) {
      const assetDate = new Date((asset as { createdDate: string }).createdDate);
      matchesDate = assetDate >= dateRange[0] && assetDate <= dateRange[1];
    }
    
    return matchesSearch && matchesType && matchesDate;
  });

  // Pagination
  const totalPages = Math.ceil(filteredAssets.length / itemsPerPage);
  const paginatedAssets = filteredAssets.slice(
    (currentPage - 1) * itemsPerPage,
    currentPage * itemsPerPage
  );

  // Calculate storage statistics
  const totalSize = overallStats?.totalSize || 0;
  const totalImages = overallStats?.imageCount || 0;
  const totalVideos = overallStats?.videoCount || 0;
  const totalAssets = overallStats?.totalCount || 0;

  const handleSelectAsset = (assetId: string) => {
    const newSelected = new Set(selectedAssets);
    if (newSelected.has(assetId)) {
      newSelected.delete(assetId);
    } else {
      newSelected.add(assetId);
    }
    setSelectedAssets(newSelected);
  };

  const handleSelectAll = () => {
    if (selectedAssets.size === paginatedAssets.length) {
      setSelectedAssets(new Set());
    } else {
      setSelectedAssets(new Set(paginatedAssets.map((a: unknown) => (a as { id: string }).id)));
    }
  };

  const handleViewAsset = (asset: unknown) => {
    setSelectedAsset(asset);
    openViewModal();
  };

  const handleDownloadAsset = (asset: unknown) => {
    // Open the media URL directly for download
    if ((asset as { mediaUrl?: string }).mediaUrl) {
      window.open((asset as { mediaUrl: string }).mediaUrl, '_blank');
      notifications.show({
        title: 'Download Started',
        message: `Opening media file for download...`,
        color: 'blue',
      });
    }
  };

  const handleDeleteAssets = async () => {
    const _count = selectedAssets.size;
    const deletePromises = Array.from(selectedAssets).map(id => deleteMedia(id));
    
    try {
      await Promise.all(deletePromises);
      setSelectedAssets(new Set());
    } catch (error) {
      console.error('Error deleting assets:', error);
    }
  };

  const handleCopyUrl = (url: string) => {
    navigator.clipboard.writeText(url);
    notifications.show({
      title: 'URL Copied',
      message: 'Media URL has been copied to clipboard',
      color: 'green',
    });
  };

  const handleRefresh = () => {
    setIsRefreshing(true);
    // Refetch will be handled by React Query
    setTimeout(() => {
      setIsRefreshing(false);
    }, 500);
  };

  const virtualKeys = [
    { value: 'all', label: 'All Virtual Keys' },
    ...(virtualKeysData?.data || []).map((key: unknown) => ({
      value: (key as { id: number }).id.toString(),
      label: (key as { name: string }).name,
    })),
  ];
  
  if (statsLoading || mediaLoading) {
    return (
      <Center h={400}>
        <Loader size="lg" />
      </Center>
    );
  }

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Media Assets</Title>
          <Text c="dimmed">Browse and manage generated media files</Text>
        </div>
        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
            loading={isRefreshing}
          >
            Refresh
          </Button>
          {selectedAssets.size > 0 && (
            <Button
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={handleDeleteAssets}
            >
              Delete ({selectedAssets.size})
            </Button>
          )}
        </Group>
      </Group>

      {/* Summary Cards */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Total Assets
                </Text>
                <Text size="xl" fw={700}>
                  {totalAssets}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light">
                <IconCloud size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Images
                </Text>
                <Text size="xl" fw={700}>
                  {totalImages}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="blue">
                <IconPhoto size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Videos
                </Text>
                <Text size="xl" fw={700}>
                  {totalVideos}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="red">
                <IconVideo size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Storage Used
                </Text>
                <Text size="xl" fw={700}>
                  {formatBytes(totalSize)}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="green">
                <IconCloud size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Filters */}
      <Card withBorder>
        <Grid>
          <Grid.Col span={{ base: 12, md: 3 }}>
            <TextInput
              placeholder="Search by filename, prompt, or model..."
              leftSection={<IconSearch size={16} />}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.currentTarget.value)}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 2 }}>
            <Select
              placeholder="Type"
              data={[
                { value: 'all', label: 'All Types' },
                { value: 'image', label: 'Images' },
                { value: 'video', label: 'Videos' },
              ]}
              value={typeFilter}
              onChange={(value) => setTypeFilter(value || 'all')}
              leftSection={<IconFilter size={16} />}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 3 }}>
            <Select
              placeholder="Virtual Key"
              data={virtualKeys}
              value={virtualKeyFilter}
              onChange={(value) => setVirtualKeyFilter(value || 'all')}
              leftSection={<IconKey size={16} />}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <DatePickerInput
              type="range"
              placeholder="Date range"
              value={dateRange}
              onChange={(value) => setDateRange(value as [Date | null, Date | null])}
              leftSection={<IconCalendar size={16} />}
              clearable
            />
          </Grid.Col>
        </Grid>
      </Card>

      {/* Selection Controls */}
      {filteredAssets.length > 0 && (
        <Group justify="space-between">
          <Checkbox
            label={`Select all (${paginatedAssets.length})`}
            checked={selectedAssets.size === paginatedAssets.length && paginatedAssets.length > 0}
            indeterminate={selectedAssets.size > 0 && selectedAssets.size < paginatedAssets.length}
            onChange={handleSelectAll}
          />
          <Text size="sm" c="dimmed">
            {filteredAssets.length} assets found
          </Text>
        </Group>
      )}

      {/* Media Grid */}
      <Grid>
        {paginatedAssets.map((asset: unknown) => (
          <Grid.Col key={(asset as { id: string }).id} span={{ base: 12, sm: 6, md: 4, lg: 3 }}>
            <Card withBorder p={0} style={{ position: 'relative' }}>
              <Card.Section>
                <div style={{ position: 'relative' }}>
                  <Image
                    src={(asset as { thumbnailUrl: string }).thumbnailUrl}
                    height={200}
                    alt={(asset as { filename: string }).filename}
                    style={{ cursor: 'pointer' }}
                    onClick={() => handleViewAsset(asset)}
                  />
                  <Checkbox
                    checked={selectedAssets.has((asset as { id: string }).id)}
                    onChange={() => handleSelectAsset((asset as { id: string }).id)}
                    style={{
                      position: 'absolute',
                      top: 8,
                      left: 8,
                    }}
                  />
                  <Badge
                    variant="filled"
                    color={(asset as { type: string }).type === 'video' ? 'red' : 'blue'}
                    style={{
                      position: 'absolute',
                      top: 8,
                      right: 8,
                    }}
                  >
                    {(asset as { type: string }).type}
                  </Badge>
                  {(asset as { type: string; metadata: { duration?: number } }).type === 'video' && (asset as { metadata: { duration?: number } }).metadata.duration && (
                    <Badge
                      variant="filled"
                      color="dark"
                      style={{
                        position: 'absolute',
                        bottom: 8,
                        right: 8,
                      }}
                    >
                      {(asset as { metadata: { duration: number } }).metadata.duration}s
                    </Badge>
                  )}
                </div>
              </Card.Section>
              
              <Stack gap="xs" p="sm">
                <Text size="sm" fw={500} lineClamp={1}>
                  {(asset as { filename: string }).filename}
                </Text>
                <Group gap="xs">
                  <Badge size="xs" variant="light">
                    {(asset as { model: string }).model}
                  </Badge>
                  <Text size="xs" c="dimmed">
                    {formatBytes((asset as { size: number }).size)}
                  </Text>
                </Group>
                <Text size="xs" c="dimmed" lineClamp={2}>
                  {(asset as { prompt: string }).prompt}
                </Text>
                <Group justify="space-between">
                  <Text size="xs" c="dimmed">
                    {formatRelativeTime((asset as { createdAt: Date }).createdAt)}
                  </Text>
                  <Group gap={4}>
                    <ActionIcon size="sm" variant="subtle" onClick={() => handleViewAsset(asset)}>
                      <IconEye size={16} />
                    </ActionIcon>
                    <ActionIcon size="sm" variant="subtle" onClick={() => handleDownloadAsset(asset)}>
                      <IconDownload size={16} />
                    </ActionIcon>
                    <Menu position="bottom-end">
                      <Menu.Target>
                        <ActionIcon size="sm" variant="subtle">
                          <IconCopy size={16} />
                        </ActionIcon>
                      </Menu.Target>
                      <Menu.Dropdown>
                        <Menu.Item
                          leftSection={<IconCopy size={14} />}
                          onClick={() => handleCopyUrl((asset as { url: string }).url)}
                        >
                          Copy URL
                        </Menu.Item>
                        {(asset as { storage: { cdnUrl?: string } }).storage.cdnUrl && (
                          <Menu.Item
                            leftSection={<IconCloud size={14} />}
                            onClick={() => handleCopyUrl(`${(asset as { storage: { cdnUrl: string }; filename: string }).storage.cdnUrl}/${(asset as { filename: string }).filename}`)}
                          >
                            Copy CDN URL
                          </Menu.Item>
                        )}
                      </Menu.Dropdown>
                    </Menu>
                  </Group>
                </Group>
              </Stack>
            </Card>
          </Grid.Col>
        ))}
      </Grid>

      {/* Empty State */}
      {filteredAssets.length === 0 && (
        <Center h={200}>
          <Stack align="center" gap="md">
            <ThemeIcon size="xl" variant="light" color="gray">
              <IconPhoto size={32} />
            </ThemeIcon>
            <Text c="dimmed">No media assets found</Text>
          </Stack>
        </Center>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <Center>
          <Pagination
            total={totalPages}
            value={currentPage}
            onChange={setCurrentPage}
          />
        </Center>
      )}

      {/* View Modal */}
      <Modal
        opened={viewModalOpened}
        onClose={closeViewModal}
        title={(selectedAsset as { filename?: string } | null)?.filename}
        size="xl"
      >
        {selectedAsset && selectedAsset as any && (
          <Stack gap="md">
            {(selectedAsset as { type: string }).type === 'image' ? (
              <Image
                src={(selectedAsset as { url: string }).url}
                alt={(selectedAsset as { filename: string }).filename}
                maw="100%"
                style={{ maxHeight: '70vh', objectFit: 'contain' }}
              />
            ) : (
              <video
                src={(selectedAsset as { url: string }).url}
                controls
                style={{ width: '100%', maxHeight: '70vh' }}
              />
            )}
            
            <Grid>
              <Grid.Col span={6}>
                <Text size="sm" fw={500}>Details</Text>
                <Stack gap="xs" mt="xs">
                  <Group gap="xs">
                    <Text size="sm" c="dimmed">Type:</Text>
                    <Badge variant="light">{(selectedAsset as { type: string }).type}</Badge>
                  </Group>
                  <Group gap="xs">
                    <Text size="sm" c="dimmed">Size:</Text>
                    <Text size="sm">{formatBytes((selectedAsset as { size: number }).size)}</Text>
                  </Group>
                  <Group gap="xs">
                    <Text size="sm" c="dimmed">Dimensions:</Text>
                    <Text size="sm">{(selectedAsset as { dimensions: { width: number; height: number } }).dimensions.width} × {(selectedAsset as { dimensions: { width: number; height: number } }).dimensions.height}</Text>
                  </Group>
                  {(selectedAsset as { type: string }).type === 'video' && (
                    <>
                      <Group gap="xs">
                        <Text size="sm" c="dimmed">Duration:</Text>
                        <Text size="sm">{(selectedAsset as { metadata: { duration: number } }).metadata.duration}s</Text>
                      </Group>
                      <Group gap="xs">
                        <Text size="sm" c="dimmed">FPS:</Text>
                        <Text size="sm">{(selectedAsset as { metadata: { fps: number } }).metadata.fps}</Text>
                      </Group>
                    </>
                  )}
                </Stack>
              </Grid.Col>
              
              <Grid.Col span={6}>
                <Text size="sm" fw={500}>Generation Info</Text>
                <Stack gap="xs" mt="xs">
                  <Group gap="xs">
                    <Text size="sm" c="dimmed">Model:</Text>
                    <Badge variant="light">{(selectedAsset as { model: string }).model}</Badge>
                  </Group>
                  <Group gap="xs">
                    <Text size="sm" c="dimmed">Virtual Key:</Text>
                    <Text size="sm">{(selectedAsset as { virtualKeyName: string }).virtualKeyName}</Text>
                  </Group>
                  <Group gap="xs">
                    <Text size="sm" c="dimmed">Created:</Text>
                    <Text size="sm">{(selectedAsset as { createdAt: Date }).createdAt.toLocaleString()}</Text>
                  </Group>
                </Stack>
              </Grid.Col>
            </Grid>
            
            {(selectedAsset as { metadata?: { prompt?: string } } | null)?.metadata?.prompt && (
              <div>
                <Text size="sm" fw={500} mb="xs">Prompt</Text>
                <Paper p="sm" withBorder>
                  <Text size="sm">{(selectedAsset as { metadata: { prompt: string } }).metadata.prompt}</Text>
                </Paper>
              </div>
            )}
            
            <Group justify="flex-end">
              <Button
                variant="light"
                leftSection={<IconCopy size={16} />}
                onClick={() => handleCopyUrl((selectedAsset as { url: string }).url)}
              >
                Copy URL
              </Button>
              <Button
                leftSection={<IconDownload size={16} />}
                onClick={() => handleDownloadAsset(selectedAsset)}
              >
                Download
              </Button>
            </Group>
          </Stack>
        )}
      </Modal>
    </Stack>
  );
}