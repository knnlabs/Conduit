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
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { ModelMappingsTable } from '@/components/modelmappings/ModelMappingsTable';
import { CreateModelMappingModal } from '@/components/modelmappings/CreateModelMappingModal';
import { EditModelMappingModal } from '@/components/modelmappings/EditModelMappingModal';
import { useModelMappings, useTestModelMapping, useBulkDiscoverModelMappings } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { exportToCSV, exportToJSON, formatDateForExport } from '@/lib/utils/export';
import { RealTimeStatus } from '@/components/realtime/RealTimeStatus';
import { TablePagination } from '@/components/common/TablePagination';
import { usePaginatedData } from '@/hooks/usePaginatedData';
import type { ModelProviderMappingDto } from '@knn_labs/conduit-admin-client';

export default function ModelMappingsPage() {
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [editModalOpened, { open: openEditModal, close: closeEditModal }] = useDisclosure(false);
  const [selectedMapping, setSelectedMapping] = useState<ModelProviderMappingDto | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const { data: modelMappings, isLoading, error, refetch } = useModelMappings();
  const testModelMapping = useTestModelMapping();
  const bulkDiscoverModelMappings = useBulkDiscoverModelMappings();

  const handleEdit = (mapping: unknown) => {
    setSelectedMapping(mapping as ModelProviderMappingDto);
    openEditModal();
  };

  const handleTest = async (mapping: unknown) => {
    await testModelMapping.mutateAsync((mapping as { id: string }).id);
  };

  const handleRefreshAll = () => {
    refetch();
    notifications.show({
      title: 'Refreshing Mappings',
      message: 'Updating model mappings and capabilities...',
      color: 'blue',
    });
  };

  const handleBulkDiscovery = async () => {
    await bulkDiscoverModelMappings.mutateAsync();
  };

  const handleExportCSV = () => {
    if (!filteredMappings || filteredMappings.length === 0) {
      notifications.show({
        title: 'No data to export',
        message: 'There are no model mappings to export',
        color: 'orange',
      });
      return;
    }

    const exportData = [];
    const mappingsToExport = filteredMappings || [];
    for (let i = 0; i < mappingsToExport.length; i++) {
      const mapping = mappingsToExport[i] as ModelProviderMappingDto;
      
      // Build capabilities array from boolean flags
      const capabilities = [];
      if (mapping.supportsVision) capabilities.push('vision');
      if (mapping.supportsImageGeneration) capabilities.push('image_generation');
      if (mapping.supportsAudioTranscription) capabilities.push('audio_transcription');
      if (mapping.supportsTextToSpeech) capabilities.push('text_to_speech');
      if (mapping.supportsRealtimeAudio) capabilities.push('realtime_audio');
      if (mapping.supportsFunctionCalling) capabilities.push('function_calling');
      if (mapping.supportsStreaming) capabilities.push('streaming');
      
      exportData.push({
        internalModel: mapping.modelId || '',
        providerModel: mapping.providerModelId || '',
        provider: mapping.providerId || '',
        status: mapping.isEnabled ? 'Active' : 'Disabled',
        priority: mapping.priority || 0,
        capabilities: capabilities.join('; '),
        createdAt: formatDateForExport(mapping.createdAt || ''),
        lastUsed: formatDateForExport(mapping.updatedAt || ''),
      });
    }

    exportToCSV(
      exportData,
      `model-mappings-${new Date().toISOString().split('T')[0]}`,
      [
        { key: 'internalModel', label: 'Internal Model' },
        { key: 'providerModel', label: 'Provider Model' },
        { key: 'provider', label: 'Provider' },
        { key: 'status', label: 'Status' },
        { key: 'priority', label: 'Priority' },
        { key: 'capabilities', label: 'Capabilities' },
        { key: 'createdAt', label: 'Created At' },
        { key: 'lastUsed', label: 'Last Used' },
      ]
    );

    notifications.show({
      title: 'Export successful',
      message: `Exported ${filteredMappings.length} model mappings`,
      color: 'green',
    });
  };

  const handleExportJSON = () => {
    if (!filteredMappings || filteredMappings.length === 0) {
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

  // Filter mappings based on active tab and search query
  const getFilteredMappings = () => {
    console.log('getFilteredMappings called', {
      modelMappings,
      'modelMappings type': typeof modelMappings,
      'is array': Array.isArray(modelMappings),
      'length': modelMappings?.length,
      'first item': modelMappings?.[0],
    });
    
    if (!modelMappings) return [];
    
    // Validate modelMappings is an array
    if (!Array.isArray(modelMappings)) {
      console.error('modelMappings is not an array:', modelMappings);
      return [];
    }
    
    // First apply search filter
    let filtered = modelMappings;
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = modelMappings.filter((m: unknown) => {
        const mapping = m as ModelProviderMappingDto;
        
        // Check basic string properties
        if (mapping.modelId.toLowerCase().includes(query) ||
            mapping.providerModelId.toLowerCase().includes(query) ||
            mapping.providerId.toLowerCase().includes(query)) {
          return true;
        }
        
        // Check capability boolean flags
        if (query === 'vision' && mapping.supportsVision) return true;
        if (query === 'image' && mapping.supportsImageGeneration) return true;
        if (query === 'audio' && (mapping.supportsAudioTranscription || mapping.supportsTextToSpeech || mapping.supportsRealtimeAudio)) return true;
        if (query === 'function' && mapping.supportsFunctionCalling) return true;
        if (query === 'streaming' && mapping.supportsStreaming) return true;
        
        return false;
      });
    }
    
    // Then apply tab filter
    switch (activeTab) {
      case 'active':
        return filtered.filter((m: unknown) => (m as { isEnabled: boolean }).isEnabled);
      case 'by-provider':
        // Group by provider - return sorted by provider name
        return [...filtered].sort((a: unknown, b: unknown) => 
          (a as { providerName: string }).providerName.localeCompare((b as { providerName: string }).providerName)
        );
      default:
        return filtered;
    }
  };

  const filteredMappings = getFilteredMappings();

  // Use pagination hook
  const {
    paginatedData,
    page,
    pageSize,
    totalItems,
    handlePageChange,
    handlePageSizeChange,
  } = usePaginatedData(filteredMappings);

  // Calculate statistics based on filtered data (not paginated)
  const stats = filteredMappings && Array.isArray(filteredMappings) && filteredMappings.length > 0 ? {
    totalMappings: filteredMappings.length,
    activeMappings: filteredMappings.filter((m: unknown) => (m as { isEnabled: boolean }).isEnabled).length,
    uniqueProviders: (() => {
      const providers = new Set();
      for (let i = 0; i < filteredMappings.length; i++) {
        const mapping = filteredMappings[i] as ModelProviderMappingDto;
        if (mapping && mapping.providerId) {
          providers.add(mapping.providerId);
        }
      }
      return providers.size;
    })(),
    totalRequests: 0, // Not available in SDK type
  } : {
    totalMappings: 0,
    activeMappings: 0,
    uniqueProviders: 0,
    totalRequests: 0,
  };

  const statCards = stats ? [
    {
      title: 'Total Mappings',
      value: stats.totalMappings,
      icon: IconRoute,
      color: 'blue',
    },
    {
      title: 'Active Mappings',
      value: stats.activeMappings,
      icon: IconActivity,
      color: 'green',
    },
    {
      title: 'Providers Used',
      value: stats.uniqueProviders,
      icon: IconServer,
      color: 'purple',
    },
    {
      title: 'Total Requests',
      value: stats.totalRequests.toLocaleString(),
      icon: IconChartLine,
      color: 'orange',
    },
  ] : [];

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>Model Mappings</Title>
          <Text c="dimmed">Configure model routing and capabilities</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading model mappings"
          color="red"
        >
          {error.message || 'Failed to load model mappings. Please try again.'}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Model Mappings</Title>
          <Text c="dimmed">Configure model routing and capabilities</Text>
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
            onClick={handleBulkDiscovery}
            loading={bulkDiscoverModelMappings.isPending}
          >
            Discover Models
          </Button>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefreshAll}
            loading={isLoading}
          >
            Refresh
          </Button>
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
        {(() => {
          const cards = [];
          if (statCards && Array.isArray(statCards)) {
            for (let i = 0; i < statCards.length; i++) {
              const stat = statCards[i];
              cards.push(
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
              );
            }
          }
          return cards;
        })()}
      </SimpleGrid>

      {/* System Status Overview */}
      {stats && stats.totalMappings > 0 && (
        <Card>
          <Card.Section p="md" withBorder>
            <Text fw={600}>Routing Status</Text>
          </Card.Section>
          <Card.Section p="md">
            <Group>
              <Group gap="xs">
                <Text size="sm" fw={500}>
                  {stats.activeMappings}/{stats.totalMappings}
                </Text>
                <Text size="sm" c="dimmed">mappings active</Text>
              </Group>
              
              <Group gap="xs">
                <Text size="sm" fw={500}>
                  {stats.uniqueProviders}
                </Text>
                <Text size="sm" c="dimmed">providers in use</Text>
              </Group>

              {stats.activeMappings !== stats.totalMappings && (
                <Badge color="orange" variant="light">
                  {stats.totalMappings - stats.activeMappings} Disabled
                </Badge>
              )}
            </Group>
          </Card.Section>
        </Card>
      )}

      {/* Model Mappings Table */}
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Group>
              <Text fw={600}>Model Mappings</Text>
              <RealTimeStatus />
            </Group>
            <Text size="sm" c="dimmed">
              {stats && `${stats.totalMappings} mapping${stats.totalMappings !== 1 ? 's' : ''} configured`}
              {searchQuery && modelMappings && ` (${modelMappings.length} total)`}
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md">
          <TextInput
            placeholder="Search by model name, provider, or capability..."
            leftSection={<IconSearch size={16} />}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.currentTarget.value)}
            mb="md"
          />
        </Card.Section>

        <Card.Section>
          <Tabs value={activeTab} onChange={setActiveTab} variant="pills" p="md" pt={0}>
            <Tabs.List>
              <Tabs.Tab value="all">
                All Mappings ({modelMappings?.length || 0})
              </Tabs.Tab>
              <Tabs.Tab value="active">
                Active Only ({modelMappings?.filter((m: unknown) => (m as { isEnabled: boolean }).isEnabled).length || 0})
              </Tabs.Tab>
              <Tabs.Tab value="by-provider">
                By Provider
              </Tabs.Tab>
            </Tabs.List>

            <Tabs.Panel value="all" pt="md">
              <div style={{ position: 'relative' }}>
                <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
                <ModelMappingsTable 
                  onEdit={handleEdit} 
                  onTest={handleTest} 
                  data={activeTab === 'all' ? paginatedData : filteredMappings}
                />
                {activeTab === 'all' && filteredMappings.length > 0 && (
                  <TablePagination
                    total={totalItems}
                    page={page}
                    pageSize={pageSize}
                    onPageChange={handlePageChange}
                    onPageSizeChange={handlePageSizeChange}
                  />
                )}
              </div>
            </Tabs.Panel>

            <Tabs.Panel value="active" pt="md">
              <div style={{ position: 'relative' }}>
                <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
                <ModelMappingsTable 
                  onEdit={handleEdit} 
                  onTest={handleTest} 
                  data={activeTab === 'active' ? paginatedData : filteredMappings}
                />
                {activeTab === 'active' && filteredMappings.length > 0 && (
                  <TablePagination
                    total={totalItems}
                    page={page}
                    pageSize={pageSize}
                    onPageChange={handlePageChange}
                    onPageSizeChange={handlePageSizeChange}
                  />
                )}
              </div>
            </Tabs.Panel>

            <Tabs.Panel value="by-provider" pt="md">
              <div style={{ position: 'relative' }}>
                <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
                {/* Group mappings by provider */}
                {filteredMappings && filteredMappings.length > 0 ? (
                  <Stack gap="md">
                    {(() => {
                      // Group mappings by provider
                      const groupedMappings: Record<string, unknown[]> = {};
                      for (let i = 0; i < filteredMappings.length; i++) {
                        const mapping = filteredMappings[i] as ModelProviderMappingDto;
                        const providerId = mapping.providerId || 'Unknown';
                        if (!groupedMappings[providerId]) {
                          groupedMappings[providerId] = [];
                        }
                        groupedMappings[providerId].push(mapping);
                      }
                      
                      // Create components for each provider group
                      const providerGroups = [];
                      const providers = Object.keys(groupedMappings);
                      for (let i = 0; i < providers.length; i++) {
                        const provider = providers[i];
                        const mappings = groupedMappings[provider];
                        providerGroups.push(
                          <div key={provider}>
                            <Group gap="xs" mb="xs">
                              <Badge variant="dot" size="lg">{provider}</Badge>
                              <Text size="sm" c="dimmed">
                                {mappings.length} mapping{mappings.length !== 1 ? 's' : ''}
                              </Text>
                            </Group>
                            <ModelMappingsTable 
                              onEdit={handleEdit} 
                              onTest={handleTest} 
                              data={mappings as never}
                              showProvider={false}
                            />
                          </div>
                        );
                      }
                      return providerGroups;
                    })()}
                  </Stack>
                ) : (
                  <Text c="dimmed" ta="center" py="xl">
                    No mappings configured
                  </Text>
                )}
              </div>
            </Tabs.Panel>
          </Tabs>
        </Card.Section>
      </Card>

      {/* Create Model Mapping Modal */}
      <CreateModelMappingModal
        opened={createModalOpened}
        onClose={closeCreateModal}
      />

      {/* Edit Model Mapping Modal */}
      {editModalOpened && selectedMapping && (
        <EditModelMappingModal
          opened={editModalOpened}
          onClose={closeEditModal}
          modelMapping={selectedMapping}
        />
      )}
    </Stack>
  );
}