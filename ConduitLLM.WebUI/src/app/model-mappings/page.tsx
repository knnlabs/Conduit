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
  Tabs,
  Menu,
  rem,
  TextInput,
} from '@mantine/core';
import {
  IconRoute,
  IconPlus,
  IconServer,
  IconRefresh,
  IconAlertCircle,
  IconActivity,
  IconChartLine,
  IconDownload,
  IconFileTypeCsv,
  IconJson,
  IconSearch,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { ModelMappingsTable } from '@/components/modelmappings/ModelMappingsTable';
import { CreateModelMappingModal } from '@/components/modelmappings/CreateModelMappingModal';
import { EditModelMappingModal } from '@/components/modelmappings/EditModelMappingModal';
import { notifications } from '@mantine/notifications';
import { exportToCSV, exportToJSON, formatDateForExport } from '@/lib/utils/export';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';

interface ModelMapping {
  id: string;
  modelName: string;
  providerModelId: string;
  providerId: number;
  providerName?: string;
  isEnabled: boolean;
  priority: number;
  createdAt: string;
  lastUsed?: string;
  requestCount: number;
  successRate?: number;
  averageLatency?: number;
}

export default function ModelMappingsPage() {
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [editModalOpened, { open: openEditModal, close: closeEditModal }] = useDisclosure(false);
  const [selectedMapping, setSelectedMapping] = useState<ModelMapping | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [mappings, setMappings] = useState<ModelMapping[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [testingMappings, setTestingMappings] = useState<Set<string>>(new Set());
  const [isDiscovering, setIsDiscovering] = useState(false);

  // Fetch mappings on mount
  useEffect(() => {
    fetchMappings();
  }, []);

  const fetchMappings = async () => {
    try {
      setIsLoading(true);
      const response = await fetch('/api/model-mappings');
      if (!response.ok) {
        throw new Error('Failed to fetch model mappings');
      }
      const data = await response.json();
      setMappings(data);
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  };

  const handleTestMapping = async (mappingId: string) => {
    setTestingMappings(prev => new Set(prev).add(mappingId));
    try {
      const response = await fetch(`/api/model-mappings/${mappingId}/test`, {
        method: 'POST',
      });
      if (!response.ok) {
        throw new Error('Failed to test mapping');
      }
      const result = await response.json();
      
      notifications.show({
        title: result.isSuccessful ? 'Test Successful' : 'Test Failed',
        message: result.message || (result.isSuccessful ? 'Model mapping is working correctly' : 'Failed to test model mapping'),
        color: result.isSuccessful ? 'green' : 'red',
      });
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to test model mapping',
        color: 'red',
      });
    } finally {
      setTestingMappings(prev => {
        const newSet = new Set(prev);
        newSet.delete(mappingId);
        return newSet;
      });
    }
  };

  const handleDelete = async (mappingId: string) => {
    try {
      const response = await fetch(`/api/model-mappings/${mappingId}`, {
        method: 'DELETE',
      });
      if (!response.ok) {
        throw new Error('Failed to delete mapping');
      }
      notifications.show({
        title: 'Success',
        message: 'Model mapping deleted successfully',
        color: 'green',
      });
      fetchMappings();
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to delete model mapping',
        color: 'red',
      });
    }
  };

  const handleBulkDiscover = async () => {
    setIsDiscovering(true);
    try {
      const response = await fetch('/api/model-mappings/discover', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          autoCreate: true,
          enableNewMappings: true,
        }),
      });
      if (!response.ok) {
        throw new Error('Failed to discover mappings');
      }
      const results = await response.json();
      
      const created = results.filter((r: any) => r.created).length;
      const updated = results.filter((r: any) => r.updated).length;
      
      notifications.show({
        title: 'Discovery Complete',
        message: `Created ${created} new mappings, updated ${updated} existing mappings`,
        color: 'green',
      });
      
      fetchMappings();
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to discover model mappings',
        color: 'red',
      });
    } finally {
      setIsDiscovering(false);
    }
  };

  // Filter mappings based on search and tab
  const filteredMappings = mappings.filter((mapping) => {
    // Tab filter
    if (activeTab === 'enabled' && !mapping.isEnabled) return false;
    if (activeTab === 'disabled' && mapping.isEnabled) return false;
    
    // Search filter
    if (!searchQuery) return true;
    
    const query = searchQuery.toLowerCase();
    return (
      mapping.modelName.toLowerCase().includes(query) ||
      mapping.providerModelId.toLowerCase().includes(query) ||
      (mapping.providerName && mapping.providerName.toLowerCase().includes(query)) ||
      mapping.id.toLowerCase().includes(query)
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
  } = usePaginatedData(filteredMappings);

  const stats = {
    totalMappings: mappings.length,
    enabledMappings: mappings.filter((m) => m.isEnabled).length,
    disabledMappings: mappings.filter((m) => !m.isEnabled).length,
    totalRequests: mappings.reduce((sum, m) => sum + m.requestCount, 0),
  };

  const handleEdit = (mapping: ModelMapping) => {
    setSelectedMapping(mapping);
    openEditModal();
  };

  const handleExportCSV = () => {
    if (filteredMappings.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no model mappings to export',
        color: 'orange',
      });
      return;
    }

    const exportData = filteredMappings.map((mapping) => ({
      modelName: mapping.modelName,
      providerModelId: mapping.providerModelId,
      providerName: mapping.providerName || '',
      status: mapping.isEnabled ? 'Enabled' : 'Disabled',
      priority: mapping.priority,
      requestCount: mapping.requestCount,
      successRate: mapping.successRate ? `${mapping.successRate}%` : '',
      averageLatency: mapping.averageLatency ? `${mapping.averageLatency}ms` : '',
      lastUsed: formatDateForExport(mapping.lastUsed),
      createdAt: formatDateForExport(mapping.createdAt),
    }));

    exportToCSV(
      exportData,
      `model-mappings-${new Date().toISOString().split('T')[0]}`,
      [
        { key: 'modelName', label: 'Model Name' },
        { key: 'providerModelId', label: 'Provider Model ID' },
        { key: 'providerName', label: 'Provider' },
        { key: 'status', label: 'Status' },
        { key: 'priority', label: 'Priority' },
        { key: 'requestCount', label: 'Request Count' },
        { key: 'successRate', label: 'Success Rate' },
        { key: 'averageLatency', label: 'Avg Latency' },
        { key: 'lastUsed', label: 'Last Used' },
        { key: 'createdAt', label: 'Created At' },
      ]
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredMappings.length} model mappings`,
      color: 'green',
    });
  };

  const handleExportJSON = () => {
    if (filteredMappings.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no model mappings to export',
        color: 'orange',
      });
      return;
    }

    exportToJSON(
      filteredMappings,
      `model-mappings-${new Date().toISOString().split('T')[0]}`
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredMappings.length} model mappings`,
      color: 'green',
    });
  };

  const statCards = [
    {
      title: 'Total Mappings',
      value: stats.totalMappings,
      icon: IconRoute,
      color: 'blue',
    },
    {
      title: 'Enabled',
      value: stats.enabledMappings,
      icon: IconServer,
      color: 'green',
    },
    {
      title: 'Disabled',
      value: stats.disabledMappings,
      icon: IconServer,
      color: 'gray',
    },
    {
      title: 'Total Requests',
      value: stats.totalRequests.toLocaleString(),
      icon: IconActivity,
      color: 'purple',
    },
  ];

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>Model Mappings</Title>
          <Text c="dimmed">Configure how models map to providers</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading model mappings"
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
          <Title order={1}>Model Mappings</Title>
          <Text c="dimmed">Configure how models map to providers</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleBulkDiscover}
            loading={isDiscovering}
          >
            Discover Models
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
            Add Mapping
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

      {/* Model Mappings Table */}
      <Card>
        <Tabs value={activeTab} onChange={setActiveTab}>
          <Tabs.List>
            <Tabs.Tab value="all">
              All Mappings
              {stats.totalMappings > 0 && (
                <Badge ml="xs" variant="light">
                  {stats.totalMappings}
                </Badge>
              )}
            </Tabs.Tab>
            <Tabs.Tab value="enabled" color="green">
              Enabled
              {stats.enabledMappings > 0 && (
                <Badge ml="xs" variant="light" color="green">
                  {stats.enabledMappings}
                </Badge>
              )}
            </Tabs.Tab>
            <Tabs.Tab value="disabled" color="gray">
              Disabled
              {stats.disabledMappings > 0 && (
                <Badge ml="xs" variant="light" color="gray">
                  {stats.disabledMappings}
                </Badge>
              )}
            </Tabs.Tab>
          </Tabs.List>

          <Card.Section p="md">
            <Group justify="space-between" mb="md">
              <Text fw={600}>Model Routing Configuration</Text>
              <Text size="sm" c="dimmed">
                {filteredMappings.length} mapping{filteredMappings.length !== 1 ? 's' : ''}
                {searchQuery && ` (${mappings.length} total)`}
              </Text>
            </Group>

            <TextInput
              placeholder="Search by model name, provider model ID, or provider..."
              leftSection={<IconSearch size={16} />}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.currentTarget.value)}
              mb="md"
            />
          </Card.Section>

          <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
            <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
            <ModelMappingsTable 
              data={paginatedData}
              onEdit={handleEdit}
              onTest={handleTestMapping}
              onDelete={handleDelete}
              testingMappings={testingMappings}
            />
            {filteredMappings.length > 0 && (
              <TablePagination
                total={totalItems}
                page={page}
                pageSize={pageSize}
                onPageChange={handlePageChange}
                onPageSizeChange={handlePageSizeChange}
              />
            )}
          </Card.Section>
        </Tabs>
      </Card>

      {/* Create Model Mapping Modal */}
      <CreateModelMappingModal
        opened={createModalOpened}
        onClose={closeCreateModal}
        onSuccess={fetchMappings}
      />

      {/* Edit Model Mapping Modal */}
      <EditModelMappingModal
        opened={editModalOpened}
        onClose={closeEditModal}
        mapping={selectedMapping}
        onSuccess={fetchMappings}
      />
    </Stack>
  );
}