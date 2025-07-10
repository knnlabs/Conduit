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
  Badge,
  Menu,
  rem,
  TextInput,
} from '@mantine/core';
import {
  IconServer,
  IconPlus,
  IconCircleCheck,
  IconCircleX,
  IconClock,
  IconRefresh,
  IconAlertCircle,
  IconDownload,
  IconFileTypeCsv,
  IconJson,
  IconSearch,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { ProvidersTable } from '@/components/providers/ProvidersTable';
import { CreateProviderModal } from '@/components/providers/CreateProviderModal';
import { EditProviderModal } from '@/components/providers/EditProviderModal';
import { notifications } from '@mantine/notifications';
import { exportToCSV, exportToJSON, formatDateForExport } from '@/lib/utils/export';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';

interface Provider {
  id: string;
  providerName: string;
  providerType?: string;
  isEnabled: boolean;
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  createdAt: string;
  endpoint?: string;
  models?: string[];
}

export default function ProvidersPage() {
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [editModalOpened, { open: openEditModal, close: closeEditModal }] = useDisclosure(false);
  const [selectedProvider, setSelectedProvider] = useState<Provider | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [providers, setProviders] = useState<Provider[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [testingProviders, setTestingProviders] = useState<Set<string>>(new Set());

  // Fetch providers on mount
  useEffect(() => {
    fetchProviders();
  }, []);

  const fetchProviders = async () => {
    try {
      setIsLoading(true);
      const response = await fetch('/api/providers');
      if (!response.ok) {
        throw new Error('Failed to fetch providers');
      }
      const data = await response.json();
      setProviders(data);
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  };

  const handleTestProvider = async (providerId: string) => {
    setTestingProviders(prev => new Set(prev).add(providerId));
    try {
      const response = await fetch(`/api/providers/${providerId}/test`, {
        method: 'POST',
      });
      if (!response.ok) {
        throw new Error('Failed to test provider');
      }
      const result = await response.json();
      
      notifications.show({
        title: result.isSuccessful ? 'Connection Successful' : 'Connection Failed',
        message: result.message || (result.isSuccessful ? 'Provider is working correctly' : 'Failed to connect to provider'),
        color: result.isSuccessful ? 'green' : 'red',
      });
      
      // Refresh providers to get updated health status
      await fetchProviders();
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to test provider connection',
        color: 'red',
      });
    } finally {
      setTestingProviders(prev => {
        const newSet = new Set(prev);
        newSet.delete(providerId);
        return newSet;
      });
    }
  };

  const handleDelete = async (providerId: string) => {
    try {
      const response = await fetch(`/api/providers/${providerId}`, {
        method: 'DELETE',
      });
      if (!response.ok) {
        throw new Error('Failed to delete provider');
      }
      notifications.show({
        title: 'Success',
        message: 'Provider deleted successfully',
        color: 'green',
      });
      fetchProviders();
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to delete provider',
        color: 'red',
      });
    }
  };

  // Filter providers based on search query
  const filteredProviders = providers.filter((provider) => {
    if (!searchQuery) return true;
    
    const query = searchQuery.toLowerCase();
    return (
      provider.providerName.toLowerCase().includes(query) ||
      provider.id.toLowerCase().includes(query) ||
      (provider.providerType && provider.providerType.toLowerCase().includes(query)) ||
      (provider.endpoint && provider.endpoint.toLowerCase().includes(query))
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
  } = usePaginatedData(filteredProviders);

  const stats = {
    totalProviders: filteredProviders.length,
    activeProviders: filteredProviders.filter((p) => p.isEnabled).length,
    healthyProviders: filteredProviders.filter((p) => p.healthStatus === 'healthy').length,
    unhealthyProviders: filteredProviders.filter((p) => p.healthStatus === 'unhealthy').length,
  };

  const handleEdit = (provider: Provider) => {
    setSelectedProvider(provider);
    openEditModal();
  };

  const handleExportCSV = () => {
    if (filteredProviders.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no providers to export',
        color: 'orange',
      });
      return;
    }

    const exportData = filteredProviders.map((provider) => ({
      name: provider.providerName,
      type: provider.providerType || '',
      status: provider.isEnabled ? 'Enabled' : 'Disabled',
      health: provider.healthStatus,
      endpoint: provider.endpoint || '',
      models: provider.models?.join('; ') || '',
      lastHealthCheck: formatDateForExport(provider.lastHealthCheck),
      createdAt: formatDateForExport(provider.createdAt),
    }));

    exportToCSV(
      exportData,
      `providers-${new Date().toISOString().split('T')[0]}`,
      [
        { key: 'name', label: 'Provider Name' },
        { key: 'type', label: 'Type' },
        { key: 'status', label: 'Status' },
        { key: 'health', label: 'Health' },
        { key: 'endpoint', label: 'Endpoint' },
        { key: 'models', label: 'Models' },
        { key: 'lastHealthCheck', label: 'Last Health Check' },
        { key: 'createdAt', label: 'Created At' },
      ]
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredProviders.length} providers`,
      color: 'green',
    });
  };

  const handleExportJSON = () => {
    if (filteredProviders.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no providers to export',
        color: 'orange',
      });
      return;
    }

    exportToJSON(
      filteredProviders,
      `providers-${new Date().toISOString().split('T')[0]}`
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredProviders.length} providers`,
      color: 'green',
    });
  };

  const statCards = [
    {
      title: 'Total Providers',
      value: stats.totalProviders,
      icon: IconServer,
      color: 'blue',
    },
    {
      title: 'Active',
      value: stats.activeProviders,
      icon: IconCircleCheck,
      color: 'green',
    },
    {
      title: 'Healthy',
      value: stats.healthyProviders,
      icon: IconCircleCheck,
      color: 'teal',
    },
    {
      title: 'Unhealthy',
      value: stats.unhealthyProviders,
      icon: IconCircleX,
      color: 'red',
    },
  ];

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>LLM Providers</Title>
          <Text c="dimmed">Manage provider configurations and connections</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading providers"
          color="red"
        >
          {error.message}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>LLM Providers</Title>
          <Text c="dimmed">Manage provider configurations and connections</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={fetchProviders}
            loading={isLoading}
          >
            Refresh
          </Button>

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
            Add Provider
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

      {/* Providers Table */}
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Text fw={600}>Configured Providers</Text>
            <Text size="sm" c="dimmed">
              {stats.totalProviders} provider{stats.totalProviders !== 1 ? 's' : ''} total
              {searchQuery && providers && ` (${providers.length} total)`}
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md">
          <TextInput
            placeholder="Search by name, type, endpoint..."
            leftSection={<IconSearch size={16} />}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.currentTarget.value)}
            mb="md"
          />
        </Card.Section>

        <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          <ProvidersTable 
            data={paginatedData}
            onEdit={handleEdit}
            onTest={handleTestProvider}
            onDelete={handleDelete}
            testingProviders={testingProviders}
          />
          {filteredProviders.length > 0 && (
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

      {/* Create Provider Modal */}
      <CreateProviderModal
        opened={createModalOpened}
        onClose={closeCreateModal}
        onSuccess={fetchProviders}
      />

      {/* Edit Provider Modal */}
      <EditProviderModal
        opened={editModalOpened}
        onClose={closeEditModal}
        provider={selectedProvider}
        onSuccess={fetchProviders}
      />
    </Stack>
  );
}