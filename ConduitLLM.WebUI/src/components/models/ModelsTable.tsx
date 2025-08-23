'use client';

import { useState, useEffect } from 'react';
import { Table, TextInput, Select, Group, ActionIcon, Badge, Text, Tooltip, Stack, HoverCard } from '@mantine/core';
import { IconEdit, IconTrash, IconSearch, IconEye, IconMessageCircle, IconPhoto, IconVideo, IconEye as IconVision, IconLink } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import { EditModelModal } from './EditModelModal';
import { ViewModelModal } from './ViewModelModal';
import { DeleteModelModal } from './DeleteModelModal';
import { useModelSeries } from '@/hooks/useModelSeries';
import { extractCapabilities, getErrorMessage } from '@/utils/typeGuards';
import type { ModelDto } from '@knn_labs/conduit-admin-client';

// Extended model type with provider mapping status and details
type ModelWithMappingStatus = ModelDto & { 
  hasProviderMappings: boolean;
  providerCount: number;
  providers: Array<{
    id: number;
    identifier: string;
    provider: string;
    isPrimary: boolean;
  }>;
};


interface ModelsTableProps {
  onRefresh?: () => void;
}

export function ModelsTable({ onRefresh }: ModelsTableProps) {
  const [models, setModels] = useState<ModelWithMappingStatus[]>([]);
  const [filteredModels, setFilteredModels] = useState<ModelWithMappingStatus[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [capabilityFilter, setCapabilityFilter] = useState<string | null>(null);
  const [providerFilter, setProviderFilter] = useState<string | null>(null);
  const [selectedModel, setSelectedModel] = useState<ModelDto | null>(null);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [viewModalOpen, setViewModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  
  const { executeWithAdmin } = useAdminClient();
  const { seriesNames } = useModelSeries(models);

  const loadModels = async () => {
    try {
      setLoading(true);
      const data = await executeWithAdmin(client => client.models.listWithMappingStatus());
      setModels(data);
      setFilteredModels(data);
    } catch (error) {
      const errorMessage = getErrorMessage(error);
      console.error('Failed to load models:', errorMessage);
      notifications.show({
        title: 'Error',
        message: `Failed to load models: ${errorMessage}`,
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };


  useEffect(() => {
    void loadModels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    let filtered = [...models];

    if (search) {
      filtered = filtered.filter(model =>
        (model.name?.toLowerCase().includes(search.toLowerCase()) ?? false)
        // displayName doesn't exist in ModelDto
      );
    }

    if (capabilityFilter) {
      filtered = filtered.filter(model => {
        const capabilities = extractCapabilities(model);
        
        switch (capabilityFilter) {
          case 'chat':
            return capabilities.supportsChat;
          case 'vision':
            return capabilities.supportsVision;
          case 'image':
            return capabilities.supportsImageGeneration;
          case 'video':
            return capabilities.supportsVideoGeneration;
          default:
            return true;
        }
      });
    }

    if (providerFilter) {
      filtered = filtered.filter(model => {
        switch (providerFilter) {
          case 'with-provider':
            return model.hasProviderMappings === true;
          case 'without-provider':
            return model.hasProviderMappings === false;
          default:
            return true;
        }
      });
    }

    setFilteredModels(filtered);
  }, [search, capabilityFilter, providerFilter, models]);

  const handleEdit = (model: ModelDto) => {
    setSelectedModel(model);
    setEditModalOpen(true);
  };

  const handleView = (model: ModelDto) => {
    setSelectedModel(model);
    setViewModalOpen(true);
  };

  const handleDelete = (model: ModelDto) => {
    setSelectedModel(model);
    setDeleteModalOpen(true);
  };

  const handleDeleteSuccess = () => {
    setDeleteModalOpen(false);
    setSelectedModel(null);
    void loadModels();
    onRefresh?.();
  };




  const renderCapabilityIcons = (model: ModelDto) => {
    const capabilities = extractCapabilities(model);
    const icons = [];
    
    // Text/Chat capability
    if (capabilities.supportsChat) {
      icons.push(
        <Tooltip key="chat" label="Text Chat">
          <IconMessageCircle size={16} color="blue" />
        </Tooltip>
      );
    }

    // Vision capability (Text + Vision)
    if (capabilities.supportsVision) {
      icons.push(
        <Tooltip key="vision" label="Text + Vision">
          <IconVision size={16} color="green" />
        </Tooltip>
      );
    }

    // Image generation capability
    if (capabilities.supportsImageGeneration) {
      icons.push(
        <Tooltip key="image" label="Image Generation">
          <IconPhoto size={16} color="purple" />
        </Tooltip>
      );
    }

    // Video generation capability
    if (capabilities.supportsVideoGeneration) {
      icons.push(
        <Tooltip key="video" label="Video Generation">
          <IconVideo size={16} color="red" />
        </Tooltip>
      );
    }

    return icons.length > 0 ? (
      <Group gap="xs">{icons}</Group>
    ) : (
      <Text c="dimmed" size="sm">-</Text>
    );
  };

  const renderProviderInfo = (model: ModelWithMappingStatus) => {
    if (model.providerCount === 0) {
      return (
        <Badge color="orange" variant="light">
          No Providers
        </Badge>
      );
    }

    return (
      <HoverCard width={280} shadow="md" openDelay={200} closeDelay={100}>
        <HoverCard.Target>
          <Badge 
            color="blue" 
            variant="light" 
            leftSection={<IconLink size={14} />}
            style={{ cursor: 'pointer' }}
          >
            {model.providerCount} {model.providerCount === 1 ? 'Provider' : 'Providers'}
          </Badge>
        </HoverCard.Target>
        <HoverCard.Dropdown>
          <Stack gap="xs">
            <Text size="sm" fw={500}>Provider Mappings:</Text>
            {model.providers.map((provider, index) => (
              <Group key={index} gap="xs">
                <Badge 
                  size="sm" 
                  color={provider.isPrimary ? 'green' : 'gray'} 
                  variant="light"
                >
                  {provider.provider.toUpperCase()}
                </Badge>
                <Text size="xs" c="dimmed" style={{ flex: 1 }}>
                  {provider.identifier}
                </Text>
                {provider.isPrimary && (
                  <Badge size="xs" color="green" variant="dot">
                    Primary
                  </Badge>
                )}
              </Group>
            ))}
          </Stack>
        </HoverCard.Dropdown>
      </HoverCard>
    );
  };

  const capabilityOptions = [
    { value: 'chat', label: 'Text Chat' },
    { value: 'vision', label: 'Text + Vision' },
    { value: 'image', label: 'Image Generation' },
    { value: 'video', label: 'Video Generation' }
  ];

  const providerOptions = [
    { value: 'with-provider', label: 'With Provider' },
    { value: 'without-provider', label: 'Without Provider' }
  ];

  return (
    <Stack gap="md">
      <Group>
        <TextInput
          placeholder="Search models..."
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(e) => setSearch(e.currentTarget.value)}
          style={{ flex: 1 }}
        />
        <Select
          placeholder="Filter by capability"
          data={capabilityOptions}
          value={capabilityFilter}
          onChange={setCapabilityFilter}
          clearable
          w={200}
        />
        <Select
          placeholder="Provider mapping"
          data={providerOptions}
          value={providerFilter}
          onChange={setProviderFilter}
          clearable
          w={200}
        />
      </Group>

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Capabilities</Table.Th>
            <Table.Th>Series</Table.Th>
            <Table.Th>Provider</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Actions</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {(() => {
            if (loading) {
              return (
                <Table.Tr>
                  <Table.Td colSpan={6}>
                    <Text ta="center" c="dimmed">Loading...</Text>
                  </Table.Td>
                </Table.Tr>
              );
            }
            if (filteredModels.length === 0) {
              return (
                <Table.Tr>
                  <Table.Td colSpan={6}>
                    <Text ta="center" c="dimmed">No models found</Text>
                  </Table.Td>
                </Table.Tr>
              );
            }
            return filteredModels.map((model) => (
              <Table.Tr key={model.id}>
                <Table.Td>
                  <Text fw={500}>{model.name ?? 'Unnamed'}</Text>
                </Table.Td>
                <Table.Td>
                  {renderCapabilityIcons(model)}
                </Table.Td>
                <Table.Td>
                  {model.modelSeriesId ? (
                    <Text>{seriesNames[model.modelSeriesId] ?? `Series ${model.modelSeriesId}`}</Text>
                  ) : (
                    <Text c="dimmed">-</Text>
                  )}
                </Table.Td>
                <Table.Td>
                  {renderProviderInfo(model)}
                </Table.Td>
                <Table.Td>
                  <Badge color={model.isActive ? 'green' : 'gray'} variant="light">
                    {model.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </Table.Td>
                <Table.Td>
                  <Group gap="xs">
                    <Tooltip label="View">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => handleView(model)}
                      >
                        <IconEye size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => handleEdit(model)}
                      >
                        <IconEdit size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Delete">
                      <ActionIcon
                        variant="subtle"
                        color="red"
                        onClick={() => handleDelete(model)}
                      >
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Tooltip>
                  </Group>
                </Table.Td>
              </Table.Tr>
            ));
          })()}
        </Table.Tbody>
      </Table>

      {selectedModel && (
        <>
          <EditModelModal
            isOpen={editModalOpen}
            model={selectedModel}
            onClose={() => {
              setEditModalOpen(false);
              setSelectedModel(null);
            }}
            onSuccess={() => {
              setEditModalOpen(false);
              setSelectedModel(null);
              void loadModels();
              onRefresh?.();
            }}
          />

          <ViewModelModal
            isOpen={viewModalOpen}
            model={selectedModel}
            onClose={() => {
              setViewModalOpen(false);
              setSelectedModel(null);
            }}
          />

          <DeleteModelModal
            isOpen={deleteModalOpen}
            model={selectedModel}
            onClose={() => {
              setDeleteModalOpen(false);
              setSelectedModel(null);
            }}
            onSuccess={handleDeleteSuccess}
          />
        </>
      )}
    </Stack>
  );
}