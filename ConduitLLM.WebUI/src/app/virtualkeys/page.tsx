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
import { useState, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { VirtualKeysTable } from '@/components/virtualkeys/VirtualKeysTable';
import { 
  LazyCreateVirtualKeyModal as CreateVirtualKeyModal,
  LazyEditVirtualKeyModal as EditVirtualKeyModal,
  LazyViewVirtualKeyModal as ViewVirtualKeyModal
} from '@/components/lazy/LazyModals';
import { exportToCSV, exportToJSON, formatDateForExport, formatCurrencyForExport } from '@/lib/utils/export';
import { notifications } from '@mantine/notifications';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';
import type { VirtualKeyDto } from '@knn_labs/conduit-admin-client';

// Extend VirtualKeyDto with UI-specific fields added by the API
interface VirtualKeyWithUI extends VirtualKeyDto {
  displayKey: string;
}

export default function VirtualKeysPage() {
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [editModalOpened, { open: openEditModal, close: closeEditModal }] = useDisclosure(false);
  const [viewModalOpened, { open: openViewModal, close: closeViewModal }] = useDisclosure(false);
  const [selectedKey, setSelectedKey] = useState<VirtualKeyWithUI | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [virtualKeys, setVirtualKeys] = useState<VirtualKeyWithUI[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  // Fetch virtual keys on mount
  useEffect(() => {
    let cancelled = false;
    
    const loadVirtualKeys = async () => {
      if (cancelled) return;
      await fetchVirtualKeys();
    };
    
    loadVirtualKeys();
    
    return () => {
      cancelled = true;
    };
  }, []);

  const fetchVirtualKeys = async () => {
    try {
      setIsLoading(true);
      setError(null); // Clear any previous errors
      
      const response = await fetch('/api/virtualkeys');
      
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to fetch virtual keys: ${errorText}`);
      }
      
      // Parse the response directly
      const data = await response.json();
      console.log('[VirtualKeys] Fetched data:', data);
      
      // Handle both array and paginated response formats
      let virtualKeysData: VirtualKeyWithUI[];
      if (Array.isArray(data)) {
        virtualKeysData = data;
      } else if (data.items && Array.isArray(data.items)) {
        virtualKeysData = data.items;
      } else {
        virtualKeysData = [];
      }
      
      setVirtualKeys(virtualKeysData);
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  };

  // Filter virtual keys based on search query
  const filteredKeys = virtualKeys.filter((key) => {
    if (!searchQuery) return true;
    
    const query = searchQuery.toLowerCase();
    return (
      key.keyName.toLowerCase().includes(query) ||
      key.displayKey.toLowerCase().includes(query) ||
      key.id.toString().toLowerCase().includes(query) ||
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
  const stats = filteredKeys ? {
    totalKeys: filteredKeys.length,
    activeKeys: filteredKeys.filter((k) => k.isEnabled).length,
    totalSpend: filteredKeys.reduce((sum: number, k) => sum + k.currentSpend, 0),
    totalRequests: filteredKeys.reduce((sum: number, k) => sum + (k.requestCount || 0), 0),
  } : null;

  const handleEdit = (key: VirtualKeyWithUI) => {
    setSelectedKey(key);
    openEditModal();
  };

  const handleView = (key: VirtualKeyWithUI) => {
    setSelectedKey(key);
    openViewModal();
  };

  const handleDelete = async (keyId: string) => {
    try {
      const response = await fetch(`/api/virtualkeys/${keyId}`, {
        method: 'DELETE',
      });
      if (!response.ok) {
        throw new Error('Failed to delete virtual key');
      }
      notifications.show({
        title: 'Success',
        message: 'Virtual key deleted successfully',
        color: 'green',
      });
      fetchVirtualKeys(); // Refresh the list
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to delete virtual key',
        color: 'red',
      });
    }
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
      keyHash: key.displayKey,
      status: key.isEnabled ? 'Active' : 'Disabled',
      currentSpend: formatCurrencyForExport(key.currentSpend),
      maxBudget: key.maxBudget ? formatCurrencyForExport(key.maxBudget) : '',
      requestCount: key.requestCount || 0,
      createdAt: formatDateForExport(key.createdAt),
      lastUsed: key.lastUsedAt ? formatDateForExport(key.lastUsedAt) : '',
      allowedModels: key.allowedModels || '',
      allowedProviders: '', // TODO: SDK should include allowedProviders field
      budgetPeriod: key.budgetDuration || '',
      expirationDate: key.expiresAt ? formatDateForExport(key.expiresAt) : '',
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
        { key: 'allowedProviders', label: 'Allowed Providers' },
        { key: 'budgetPeriod', label: 'Budget Period' },
        { key: 'expirationDate', label: 'Expiration Date' },
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
            placeholder="Search by name, key hash, ID, or description..."
            leftSection={<IconSearch size={16} />}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.currentTarget.value)}
            mb="md"
          />
        </Card.Section>

        <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          <VirtualKeysTable onEdit={handleEdit} onView={handleView} data={paginatedData} onDelete={handleDelete} />
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
        onSuccess={fetchVirtualKeys}
      />

      <EditVirtualKeyModal
        opened={editModalOpened}
        onClose={closeEditModal}
        virtualKey={selectedKey}
        onSuccess={fetchVirtualKeys}
      />

      <ViewVirtualKeyModal
        opened={viewModalOpened}
        onClose={closeViewModal}
        virtualKey={selectedKey}
      />
    </Stack>
  );
}