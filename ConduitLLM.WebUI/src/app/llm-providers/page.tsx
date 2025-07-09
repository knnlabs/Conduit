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
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { ProvidersTable } from '@/components/providers/ProvidersTable';
import { CreateProviderModal } from '@/components/providers/CreateProviderModal';
import { EditProviderModal } from '@/components/providers/EditProviderModal';
import { useProviders, useTestProvider } from '@/hooks/useConduitAdmin';
import { notifications } from '@mantine/notifications';
import { exportToCSV, exportToJSON, formatDateForExport } from '@/lib/utils/export';
import { RealTimeStatus } from '@/components/realtime/RealTimeStatus';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';

interface Provider {
  id: string;
  providerName: string;
  providerType?: string;
  isEnabled: boolean;
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  modelsAvailable: number;
  createdAt: string;
  apiEndpoint?: string;
  description?: string;
  organizationId?: string;
}

export default function ProvidersPage() {
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [editModalOpened, { open: openEditModal, close: closeEditModal }] = useDisclosure(false);
  const [selectedProvider, setSelectedProvider] = useState<Provider | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const { data: providers, isLoading, error, refetch } = useProviders();
  const testProvider = useTestProvider();

  // Filter providers based on search query
  const filteredProviders = (providers as Provider[] | undefined)?.filter((provider) => {
    if (!searchQuery) return true;
    
    const query = searchQuery.toLowerCase();
    return (
      provider.providerName.toLowerCase().includes(query) ||
      (provider.providerType && provider.providerType.toLowerCase().includes(query)) ||
      (provider.description && provider.description.toLowerCase().includes(query))
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
  } = usePaginatedData(filteredProviders, { defaultPageSize: 10 });

  // Calculate statistics based on filtered data (not paginated)
  const stats = filteredProviders ? {
    totalProviders: filteredProviders.length,
    enabledProviders: filteredProviders.filter((p) => p.isEnabled).length,
    healthyProviders: filteredProviders.filter((p) => p.healthStatus === 'healthy').length,
    totalModels: filteredProviders.reduce((sum: number, p) => sum + (p.modelsAvailable || 0), 0),
  } : null;

  const handleEdit = (provider: Provider) => {
    setSelectedProvider(provider);
    openEditModal();
  };

  const handleTest = async (provider: Provider) => {
    await testProvider.mutateAsync(provider.id);
  };

  const handleRefreshAll = () => {
    refetch();
    notifications.show({
      title: 'Refreshing Providers',
      message: 'Updating provider status and model lists...',
      color: 'blue',
    });
  };

  const handleExportCSV = () => {
    if (!filteredProviders || filteredProviders.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no providers to export',
        color: 'orange',
      });
      return;
    }

    const exportData = filteredProviders.map((provider) => ({
      name: provider.providerName,
      type: provider.providerType || 'Unknown',
      status: provider.isEnabled ? 'Enabled' : 'Disabled',
      healthStatus: provider.healthStatus || 'Unknown',
      modelsAvailable: provider.modelsAvailable || 0,
      lastHealthCheck: formatDateForExport(provider.lastHealthCheck),
      createdAt: formatDateForExport(provider.createdAt),
      description: provider.description || '',
    }));

    exportToCSV(
      exportData,
      `providers-${new Date().toISOString().split('T')[0]}`,
      [
        { key: 'name', label: 'Provider Name' },
        { key: 'type', label: 'Type' },
        { key: 'status', label: 'Status' },
        { key: 'healthStatus', label: 'Health' },
        { key: 'modelsAvailable', label: 'Models' },
        { key: 'lastHealthCheck', label: 'Last Health Check' },
        { key: 'createdAt', label: 'Created At' },
        { key: 'description', label: 'Description' },
      ]
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredProviders.length} providers`,
      color: 'green',
    });
  };

  const handleExportJSON = () => {
    if (!filteredProviders || filteredProviders.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no providers to export',
        color: 'orange',
      });
      return;
    }

    // Export with sensitive data removed
    const sanitizedProviders = filteredProviders.map((provider) => ({
      ...provider,
      credentials: undefined,
      apiKey: undefined,
    }));

    exportToJSON(
      sanitizedProviders,
      `providers-${new Date().toISOString().split('T')[0]}`
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredProviders.length} providers`,
      color: 'green',
    });
  };

  const statCards = stats ? [
    {
      title: 'Total Providers',
      value: stats.totalProviders,
      icon: IconServer,
      color: 'blue',
    },
    {
      title: 'Enabled',
      value: stats.enabledProviders,
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
      title: 'Models Available',
      value: stats.totalModels,
      icon: IconServer,
      color: 'purple',
    },
  ] : [];

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>LLM Providers</Title>
          <Text c="dimmed">Configure and manage LLM provider integrations</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading providers"
          color="red"
        >
          {error.message || 'Failed to load providers. Please try again.'}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>LLM Providers</Title>
          <Text c="dimmed">Configure and manage LLM provider integrations</Text>
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
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefreshAll}
            loading={isLoading}
          >
            Refresh All
          </Button>
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

      {/* Health Status Overview */}
      {stats && stats.totalProviders > 0 && (
        <Card>
          <Card.Section p="md" withBorder>
            <Text fw={600}>System Health Overview</Text>
          </Card.Section>
          <Card.Section p="md">
            <Group>
              <Group gap="xs">
                <ThemeIcon size="sm" color="green" variant="light">
                  <IconCircleCheck size={14} />
                </ThemeIcon>
                <Text size="sm">
                  {stats.healthyProviders} Healthy
                </Text>
              </Group>
              
              <Group gap="xs">
                <ThemeIcon size="sm" color="red" variant="light">
                  <IconCircleX size={14} />
                </ThemeIcon>
                <Text size="sm">
                  {stats.totalProviders - stats.healthyProviders} Issues
                </Text>
              </Group>
              
              <Group gap="xs">
                <ThemeIcon size="sm" color="orange" variant="light">
                  <IconClock size={14} />
                </ThemeIcon>
                <Text size="sm">
                  {stats.totalModels} Models
                </Text>
              </Group>

              {stats.enabledProviders !== stats.totalProviders && (
                <Badge color="orange" variant="light">
                  {stats.totalProviders - stats.enabledProviders} Disabled
                </Badge>
              )}
            </Group>
          </Card.Section>
        </Card>
      )}

      {/* Providers Table */}
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Group>
              <Text fw={600}>Configured Providers</Text>
              <RealTimeStatus />
            </Group>
            <Text size="sm" c="dimmed">
              {stats && `${stats.totalProviders} provider${stats.totalProviders !== 1 ? 's' : ''} total`}
              {searchQuery && providers && ` (${providers.length} total)`}
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md">
          <TextInput
            placeholder="Search by name, type, or description..."
            leftSection={<IconSearch size={16} />}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.currentTarget.value)}
            mb="md"
          />
        </Card.Section>

        <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          <ProvidersTable onEdit={handleEdit} onTest={handleTest} data={paginatedData} />
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
      />

      {/* Edit Provider Modal */}
      <EditProviderModal
        opened={editModalOpened}
        onClose={closeEditModal}
        provider={selectedProvider}
        onTest={handleTest}
      />
    </Stack>
  );
}