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
  IconServer,
  IconPlus,
  IconCircleCheck,
  IconCircleX,
  IconRefresh,
  IconAlertCircle,
  IconDownload,
  IconFileTypeCsv,
  IconJson,
  IconSearch,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { ProvidersTable } from '@/components/providers/ProvidersTable';
import { notifications } from '@mantine/notifications';
import { useRouter } from 'next/navigation';
import { exportToCSV, exportToJSON, formatDateForExport } from '@/lib/utils/export';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';
import { ApiKeyTestResult, type ProviderCredentialDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { getProviderDisplayName } from '@/lib/utils/providerTypeUtils';

// Use SDK types directly with health extensions
interface ProviderWithHealth extends ProviderCredentialDto {
  healthStatus: 'healthy' | 'unhealthy' | 'unknown';
  lastHealthCheck?: string;
  models?: string[];
  endpoint?: string;
  keyCount?: number;
}

export default function ProvidersPage() {
  const router = useRouter();
  const [searchQuery, setSearchQuery] = useState('');
  const [providers, setProviders] = useState<ProviderWithHealth[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [testingProviders, setTestingProviders] = useState<Set<number>>(new Set());

  // Fetch providers on mount
  useEffect(() => {
    void fetchProviders();
  }, []);

  const fetchProviders = async () => {
    try {
      setIsLoading(true);
      const result = await withAdminClient(client => 
        client.providers.list(1, 1000)
      );
      
      const providersList = result.items;
      
      // Fetch key counts for each provider
      const providersWithKeyCount = await Promise.all(
        providersList.map(async (provider: ProviderCredentialDto) => {
          let keyCount = 0;
          if (provider.id) {
            try {
              const keys = await withAdminClient(client => 
                client.providers.listKeys(provider.id)
              ) as unknown as ProviderCredentialDto[];
              keyCount = Array.isArray(keys) ? keys.length : 0;
            } catch {
              // Silently fail, keyCount remains 0
            }
          }
          
          const providerWithHealth: ProviderWithHealth = {
            ...provider,
            healthStatus: 'unknown' as const,
            models: [],
            keyCount
          };
          
          return providerWithHealth;
        })
      );
      
      setProviders(providersWithKeyCount);
    } catch (error) {
      setError(error instanceof Error ? error : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  };

  const handleTestProvider = async (providerId: number) => {
    setTestingProviders(prev => new Set(prev).add(providerId));
    try {
      const result = await withAdminClient(client => 
        client.providers.testConnectionById(providerId)
      );
      
      notifications.show({
        title: result.result === ApiKeyTestResult.SUCCESS ? 'Connection Successful' : 'Connection Failed',
        message: result.message ?? (result.result === ApiKeyTestResult.SUCCESS ? 'Provider is working correctly' : 'Failed to connect to provider'),
        color: result.result === ApiKeyTestResult.SUCCESS ? 'green' : 'red',
      });
      
      // Refresh providers to get updated health status
      void fetchProviders();
    } catch {
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

  const handleDelete = async (providerId: number) => {
    try {
      await withAdminClient(client => 
        client.providers.deleteById(providerId)
      );
      notifications.show({
        title: 'Success',
        message: 'Provider deleted successfully',
        color: 'green',
      });
      void fetchProviders();
    } catch {
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
    const displayName = provider.providerType ? getProviderDisplayName(provider.providerType) : 'Unknown Provider';
    
    return (
      displayName.toLowerCase().includes(query) ||
      (provider.providerName?.toLowerCase().includes(query) ?? false) ||
      (provider.id?.toString().toLowerCase().includes(query) ?? false) ||
      (provider.baseUrl?.toLowerCase().includes(query) ?? false)
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

  const handleEdit = (provider: ProviderWithHealth) => {
    router.push(`/llm-providers/edit/${provider.id}`);
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

    const exportData = filteredProviders.map((provider) => {
      const displayName = provider.providerType ? getProviderDisplayName(provider.providerType) : 'Unknown Provider';
      
      return {
        name: provider.providerName ?? displayName,
        type: displayName,
        status: provider.isEnabled ? 'Enabled' : 'Disabled',
        health: provider.healthStatus,
        endpoint: provider.baseUrl ?? '',
        models: provider.models?.join('; ') ?? '',
        lastHealthCheck: formatDateForExport(provider.lastHealthCheck),
        createdAt: formatDateForExport(provider.createdAt),
      };
    });

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
            onClick={() => void fetchProviders()}
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
                onClick={() => void handleExportCSV()}
              >
                Export as CSV
              </Menu.Item>
              <Menu.Item
                leftSection={<IconJson style={{ width: rem(14), height: rem(14) }} />}
                onClick={() => void handleExportJSON()}
              >
                Export as JSON
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>

          <Button
            leftSection={<IconPlus size={16} />}
            onClick={() => router.push('/llm-providers/add')}
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
            onTest={(providerId: number) => void handleTestProvider(providerId)}
            onDelete={(providerId: number) => void handleDelete(providerId)}
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

    </Stack>
  );
}