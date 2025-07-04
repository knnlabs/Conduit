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
  IconCreditCard,
  IconActivity,
  IconUsers,
  IconAlertCircle,
  IconDownload,
  IconFileTypeCsv,
  IconJson,
  IconSearch,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { VirtualKeysTable } from '@/components/virtualkeys/VirtualKeysTable';
import { CreateVirtualKeyModal } from '@/components/virtualkeys/CreateVirtualKeyModal';
import { EditVirtualKeyModal } from '@/components/virtualkeys/EditVirtualKeyModal';
import { ViewVirtualKeyModal } from '@/components/virtualkeys/ViewVirtualKeyModal';
import { useVirtualKeys } from '@/hooks/api/useAdminApi';
import { exportToCSV, exportToJSON, formatDateForExport, formatCurrencyForExport } from '@/lib/utils/export';
import { notifications } from '@mantine/notifications';
import { RealTimeStatus } from '@/components/realtime/RealTimeStatus';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';
import { QueryErrorBoundary } from '@/components/error/QueryErrorBoundary';

interface VirtualKey {
  id: string;
  keyName: string;
  keyHash: string;
  currentSpend: number;
  maxBudget?: number;
  isEnabled: boolean;
  createdAt: string;
  lastUsed?: string;
  requestCount: number;
  description?: string;
  allowedModels?: string[];
  allowedEndpoints?: string[];
  ipWhitelist?: string[];
  rateLimitPerMinute?: number;
}

export default function VirtualKeysPage() {
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [editModalOpened, { open: openEditModal, close: closeEditModal }] = useDisclosure(false);
  const [viewModalOpened, { open: openViewModal, close: closeViewModal }] = useDisclosure(false);
  const [selectedKey, setSelectedKey] = useState<VirtualKey | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const { data: virtualKeys, isLoading, error } = useVirtualKeys();

  // Filter virtual keys based on search query
  const filteredKeys = (virtualKeys as VirtualKey[] | undefined)?.filter((key) => {
    if (!searchQuery) return true;
    
    const query = searchQuery.toLowerCase();
    return (
      key.keyName.toLowerCase().includes(query) ||
      key.keyHash.toLowerCase().includes(query) ||
      key.id.toLowerCase().includes(query) ||
      (key.description && key.description.toLowerCase().includes(query))
    );
  }) || [];

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
  const stats = filteredKeys ? {
    totalKeys: filteredKeys.length,
    activeKeys: filteredKeys.filter((k) => k.isEnabled).length,
    totalSpend: filteredKeys.reduce((sum: number, k) => sum + k.currentSpend, 0),
    totalRequests: filteredKeys.reduce((sum: number, k) => sum + k.requestCount, 0),
  } : null;

  const handleEdit = (key: VirtualKey) => {
    setSelectedKey(key);
    openEditModal();
  };

  const handleView = (key: VirtualKey) => {
    setSelectedKey(key);
    openViewModal();
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
    }).format(amount);
  };

  const handleExportCSV = () => {
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
      keyHash: key.keyHash,
      status: key.isEnabled ? 'Active' : 'Disabled',
      currentSpend: formatCurrencyForExport(key.currentSpend),
      maxBudget: key.maxBudget ? formatCurrencyForExport(key.maxBudget) : '',
      requestCount: key.requestCount,
      createdAt: formatDateForExport(key.createdAt),
      lastUsed: formatDateForExport(key.lastUsed),
      allowedModels: key.allowedModels?.join('; ') || '',
      allowedEndpoints: key.allowedEndpoints?.join('; ') || '',
      ipWhitelist: key.ipWhitelist?.join('; ') || '',
      rateLimitPerMinute: key.rateLimitPerMinute || '',
    }));

    exportToCSV(
      exportData,
      `virtual-keys-${new Date().toISOString().split('T')[0]}`,
      [
        { key: 'name', label: 'Name' },
        { key: 'keyHash', label: 'Key Hash' },
        { key: 'status', label: 'Status' },
        { key: 'currentSpend', label: 'Current Spend' },
        { key: 'maxBudget', label: 'Max Budget' },
        { key: 'requestCount', label: 'Request Count' },
        { key: 'createdAt', label: 'Created At' },
        { key: 'lastUsed', label: 'Last Used' },
        { key: 'allowedModels', label: 'Allowed Models' },
        { key: 'allowedEndpoints', label: 'Allowed Endpoints' },
        { key: 'ipWhitelist', label: 'IP Whitelist' },
        { key: 'rateLimitPerMinute', label: 'Rate Limit/Min' },
      ]
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredKeys.length} virtual keys`,
      color: 'green',
    });
  };

  const handleExportJSON = () => {
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
  };

  const statCards = stats ? [
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
      title: 'Total Spend',
      value: formatCurrency(stats.totalSpend),
      icon: IconCreditCard,
      color: 'orange',
    },
    {
      title: 'Total Requests',
      value: stats.totalRequests.toLocaleString(),
      icon: IconActivity,
      color: 'purple',
    },
  ] : [];

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
    <QueryErrorBoundary>
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
            <Group>
              <Text fw={600}>Virtual Keys</Text>
              <RealTimeStatus />
            </Group>
            <Text size="sm" c="dimmed">
              {stats && `${stats.totalKeys} key${stats.totalKeys !== 1 ? 's' : ''} total`}
              {searchQuery && virtualKeys && ` (${virtualKeys.length} total)`}
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md">
          <TextInput
            placeholder="Search by name, key hash, ID, or description..."
            leftSection={<IconSearch size={16} />}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.currentTarget.value)}
            mb="md"
          />
        </Card.Section>

        <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          <VirtualKeysTable onEdit={handleEdit} onView={handleView} data={paginatedData} />
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
      />

      <EditVirtualKeyModal
        opened={editModalOpened}
        onClose={closeEditModal}
        virtualKey={selectedKey}
      />

      <ViewVirtualKeyModal
        opened={viewModalOpened}
        onClose={closeViewModal}
        virtualKey={selectedKey}
      />
    </Stack>
    </QueryErrorBoundary>
  );
}