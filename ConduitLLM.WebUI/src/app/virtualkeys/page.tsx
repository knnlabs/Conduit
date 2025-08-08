'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  SimpleGrid,
  ThemeIcon,
  LoadingOverlay,
  Alert,
  Menu,
  rem,
  TextInput,
} from '@mantine/core';
import {
  IconKey,
  IconPlus,
  IconUsers,
  IconAlertCircle,
  IconDownload,
  IconFileTypeCsv,
  IconJson,
  IconSearch,
  IconLayersLinked,
} from '@tabler/icons-react';
import { useState, useEffect, useCallback, useMemo } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { VirtualKeysTable } from '@/components/virtualkeys/VirtualKeysTable';
import { 
  LazyCreateVirtualKeyModal as CreateVirtualKeyModal,
  LazyEditVirtualKeyModal as EditVirtualKeyModal,
  LazyViewVirtualKeyModal as ViewVirtualKeyModal
} from '@/components/lazy/LazyModals';
import { exportToCSV, exportToJSON, formatDateForExport } from '@/lib/utils/export';
import { notifications } from '@mantine/notifications';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';
import type { VirtualKeyDto, VirtualKeyGroupDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

export default function VirtualKeysPage() {
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [editModalOpened, { open: openEditModal, close: closeEditModal }] = useDisclosure(false);
  const [viewModalOpened, { open: openViewModal, close: closeViewModal }] = useDisclosure(false);
  const [selectedKey, setSelectedKey] = useState<VirtualKeyDto | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [virtualKeys, setVirtualKeys] = useState<VirtualKeyDto[]>([]);
  const [virtualKeyGroups, setVirtualKeyGroups] = useState<VirtualKeyGroupDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const fetchVirtualKeys = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      
      const result = await withAdminClient(client => 
        client.virtualKeys.list(1, 1000)
      );
      
      // Filter out items without valid IDs and ensure they match VirtualKeyDto type
      const validKeys = result.items.filter((key): key is VirtualKeyDto => 
        key.id !== undefined && key.id !== null
      );
      setVirtualKeys(validKeys);
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  const fetchVirtualKeyGroups = useCallback(async () => {
    try {
      const groups = await withAdminClient(client => 
        client.virtualKeyGroups.list()
      );
      setVirtualKeyGroups(groups);
    } catch (err) {
      console.warn('Error fetching virtual key groups:', err);
    }
  }, []);

  // Fetch virtual keys and groups on mount
  useEffect(() => {
    let cancelled = false;
    
    const loadData = async () => {
      if (cancelled) return;
      await Promise.all([
        fetchVirtualKeys(),
        fetchVirtualKeyGroups()
      ]);
    };
    
    void loadData();
    
    return () => {
      cancelled = true;
    };
  }, [fetchVirtualKeys, fetchVirtualKeyGroups]);

  // Filter virtual keys based on search query
  const filteredKeys = virtualKeys.filter((key) => {
    if (!searchQuery) return true;
    
    const query = searchQuery.toLowerCase();
    return (
      key.keyName.toLowerCase().includes(query) ||
      (key.keyPrefix?.toLowerCase().includes(query) ?? false) ||
      key.id.toString().toLowerCase().includes(query) ||
      key.virtualKeyGroupId.toString().includes(query) ||
      (key.metadata && JSON.stringify(key.metadata).toLowerCase().includes(query))
    );
  });

  // Use pagination hook
  const {
    paginatedData,
    page,
    pageSize,
    totalItems,
    handlePageChange,
    handlePageSizeChange,
  } = usePaginatedData(filteredKeys);

  // Calculate statistics based on filtered data (not paginated)
  const stats = useMemo(() => filteredKeys ? {
    totalKeys: filteredKeys.length,
    activeKeys: filteredKeys.filter((k) => k.isEnabled).length,
    totalGroups: new Set(filteredKeys.map((k) => k.virtualKeyGroupId)).size,
  } : null, [filteredKeys]);

  const handleEdit = useCallback((key: VirtualKeyDto) => {
    setSelectedKey(key);
    openEditModal();
  }, [openEditModal]);

  const handleView = useCallback((key: VirtualKeyDto) => {
    setSelectedKey(key);
    openViewModal();
  }, [openViewModal]);

  const handleDelete = useCallback(async (keyId: string) => {
    try {
      await withAdminClient(client => 
        client.virtualKeys.delete(keyId)
      );
      
      notifications.show({
        title: 'Success',
        message: 'Virtual key deleted successfully',
        color: 'green',
      });
      void fetchVirtualKeys();
    } catch {
      notifications.show({
        title: 'Error',
        message: 'Failed to delete virtual key',
        color: 'red',
      });
    }
  }, [fetchVirtualKeys]);


  const handleExportCSV = useCallback(() => {
    if (!filteredKeys || filteredKeys.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no virtual keys to export',
        color: 'orange',
      });
      return;
    }

    const exportData = filteredKeys.map((key) => ({
      name: key.keyName,
      keyPrefix: key.keyPrefix ?? 'N/A',
      virtualKeyGroupId: key.virtualKeyGroupId,
      status: key.isEnabled ? 'Active' : 'Disabled',
      createdAt: formatDateForExport(key.createdAt),
      allowedModels: key.allowedModels ?? '',
      expirationDate: key.expiresAt ? formatDateForExport(key.expiresAt) : '',
      rateLimitRpm: key.rateLimitRpm ?? '',
      rateLimitRpd: key.rateLimitRpd ?? '',
    }));

    exportToCSV(
      exportData,
      `virtual-keys-${new Date().toISOString().split('T')[0]}`,
      [
        { key: 'name', label: 'Name' },
        { key: 'keyPrefix', label: 'Key Prefix' },
        { key: 'virtualKeyGroupId', label: 'Group ID' },
        { key: 'status', label: 'Status' },
        { key: 'createdAt', label: 'Created At' },
        { key: 'allowedModels', label: 'Allowed Models' },
        { key: 'expirationDate', label: 'Expiration Date' },
        { key: 'rateLimitRpm', label: 'Rate Limit (RPM)' },
        { key: 'rateLimitRpd', label: 'Rate Limit (RPD)' },
      ]
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredKeys.length} virtual keys`,
      color: 'green',
    });
  }, [filteredKeys]);

  const handleExportJSON = useCallback(() => {
    if (!filteredKeys || filteredKeys.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no virtual keys to export',
        color: 'orange',
      });
      return;
    }

    exportToJSON(
      filteredKeys,
      `virtual-keys-${new Date().toISOString().split('T')[0]}`
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredKeys.length} virtual keys`,
      color: 'green',
    });
  }, [filteredKeys]);

  const statCards = useMemo(() => stats ? [
    {
      title: 'Total Keys',
      value: stats.totalKeys,
      icon: IconKey,
      color: 'blue',
    },
    {
      title: 'Active Keys',
      value: stats.activeKeys,
      icon: IconUsers,
      color: 'green',
    },
    {
      title: 'Total Groups',
      value: stats.totalGroups,
      icon: IconLayersLinked,
      color: 'orange',
    },
  ] : [], [stats]);

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>Virtual Keys</Title>
          <Text c="dimmed">Manage API keys and access control</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading virtual keys"
          color="red"
        >
          {error instanceof Error ? error.message : 'Failed to load virtual keys. Please try again.'}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
        <Group justify="space-between">
          <div>
            <Title order={1}>Virtual Keys</Title>
            <Text c="dimmed">Manage API keys and access control</Text>
          </div>

        <Group>
          <Menu shadow="md" width={200}>
            <Menu.Target>
              <Button variant="light" leftSection={<IconDownload size={16} />}>
                Export
              </Button>
            </Menu.Target>

            <Menu.Dropdown>
              <Menu.Item
                leftSection={<IconFileTypeCsv style={{ width: rem(14), height: rem(14) }} />}
                onClick={handleExportCSV}
              >
                Export as CSV
              </Menu.Item>
              <Menu.Item
                leftSection={<IconJson style={{ width: rem(14), height: rem(14) }} />}
                onClick={handleExportJSON}
              >
                Export as JSON
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>

          <Button
            leftSection={<IconPlus size={16} />}
            onClick={openCreateModal}
          >
            Create Virtual Key
          </Button>
        </Group>
      </Group>

      {/* Statistics Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
        {statCards.map((stat) => (
          <Card key={stat.title} p="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                  {stat.title}
                </Text>
                <Text fw={700} size="xl">
                  {stat.value}
                </Text>
              </div>
              <ThemeIcon size="lg" variant="light" color={stat.color}>
                <stat.icon size={20} />
              </ThemeIcon>
            </Group>
          </Card>
        ))}
      </SimpleGrid>

      {/* Virtual Keys Table */}
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Text fw={600}>Virtual Keys</Text>
            <Text size="sm" c="dimmed">
              {stats && `${stats.totalKeys} key${stats.totalKeys !== 1 ? 's' : ''} total`}
              {searchQuery && virtualKeys && ` (${virtualKeys.length} total)`}
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md">
          <TextInput
            placeholder="Search by name, key prefix, group ID, or metadata..."
            leftSection={<IconSearch size={16} />}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.currentTarget.value)}
            mb="md"
          />
        </Card.Section>

        <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          <VirtualKeysTable 
            onEdit={handleEdit} 
            onView={handleView} 
            data={paginatedData} 
            groups={virtualKeyGroups}
            onDelete={(id: string) => { void handleDelete(id); }} 
          />
          {filteredKeys.length > 0 && (
            <TablePagination
              total={totalItems}
              page={page}
              pageSize={pageSize}
              onPageChange={handlePageChange}
              onPageSizeChange={handlePageSizeChange}
            />
          )}
        </Card.Section>
      </Card>

      {/* Create Virtual Key Modal */}
      <CreateVirtualKeyModal
        opened={createModalOpened}
        onClose={closeCreateModal}
        onSuccess={() => {
          void fetchVirtualKeys();
          void fetchVirtualKeyGroups();
        }}
      />

      <EditVirtualKeyModal
        opened={editModalOpened}
        onClose={closeEditModal}
        virtualKey={selectedKey}
        onSuccess={() => void fetchVirtualKeys()}
      />

      <ViewVirtualKeyModal
        opened={viewModalOpened}
        onClose={closeViewModal}
        virtualKey={selectedKey}
      />
    </Stack>
  );
}