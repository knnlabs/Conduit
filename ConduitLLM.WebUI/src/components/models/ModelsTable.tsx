'use client';

import { useState, useEffect } from 'react';
import { Table, TextInput, Select, Group, ActionIcon, Badge, Text, Tooltip, Stack } from '@mantine/core';
import { IconEdit, IconTrash, IconSearch, IconEye, IconMessageCircle, IconPhoto, IconVideo, IconEye as IconVision } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import { EditModelModal } from './EditModelModal';
import { ViewModelModal } from './ViewModelModal';
import { DeleteModelModal } from './DeleteModelModal';
import type { ModelDto } from '@knn_labs/conduit-admin-client';


interface ModelsTableProps {
  onRefresh?: () => void;
}

export function ModelsTable({ onRefresh }: ModelsTableProps) {
  const [models, setModels] = useState<ModelDto[]>([]);
  const [filteredModels, setFilteredModels] = useState<ModelDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [capabilityFilter, setCapabilityFilter] = useState<string | null>(null);
  const [selectedModel, setSelectedModel] = useState<ModelDto | null>(null);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [viewModalOpen, setViewModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [seriesNames, setSeriesNames] = useState<Record<number, string>>({});
  
  const { executeWithAdmin } = useAdminClient();

  const loadModels = async () => {
    try {
      setLoading(true);
      const data = await executeWithAdmin(client => client.models.list());
      setModels(data);
      setFilteredModels(data);
      
      // Load series names for models that have a series
      await loadSeriesNames(data);
    } catch (error) {
      console.error('Failed to load models:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load models',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  const loadSeriesNames = async (modelsList: ModelDto[]) => {
    const uniqueSeriesIds = Array.from(
      new Set(
        modelsList
          .map(model => model.modelSeriesId)
          .filter((id): id is number => id !== undefined && id !== null)
      )
    );

    if (uniqueSeriesIds.length === 0) {
      setSeriesNames({});
      return;
    }

    const names: Record<number, string> = {};
    
    await Promise.all(
      uniqueSeriesIds.map(async (seriesId) => {
        try {
          const series = await executeWithAdmin(client => 
            client.modelSeries.get(seriesId)
          );
          names[seriesId] = series.name ?? `Series ${seriesId}`;
        } catch (error) {
          console.error(`Failed to load series name for ID ${seriesId}:`, error);
          names[seriesId] = `Series ${seriesId}`;
        }
      })
    );
    
    setSeriesNames(names);
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
        const capabilities = model.capabilities as unknown;
        if (!isCapabilitiesObject(capabilities)) return false;
        
        switch (capabilityFilter) {
          case 'chat':
            return capabilities.supportsChat === true;
          case 'vision':
            return capabilities.supportsVision === true;
          case 'image':
            return capabilities.supportsImageGeneration === true;
          case 'video':
            return capabilities.supportsVideoGeneration === true;
          default:
            return true;
        }
      });
    }

    setFilteredModels(filtered);
  }, [search, capabilityFilter, models]);

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



  const isCapabilitiesObject = (capabilities: unknown): capabilities is Record<string, unknown> => {
    return capabilities !== null && typeof capabilities === 'object';
  };

  const renderCapabilityIcons = (model: ModelDto) => {
    const capabilities = model.capabilities as unknown;
    
    if (!isCapabilitiesObject(capabilities)) {
      return <Text c="dimmed" size="sm">-</Text>;
    }

    const icons = [];
    
    // Text/Chat capability
    if (capabilities.supportsChat === true) {
      icons.push(
        <Tooltip key="chat" label="Text Chat">
          <IconMessageCircle size={16} color="blue" />
        </Tooltip>
      );
    }

    // Vision capability (Text + Vision)
    if (capabilities.supportsVision === true) {
      icons.push(
        <Tooltip key="vision" label="Text + Vision">
          <IconVision size={16} color="green" />
        </Tooltip>
      );
    }

    // Image generation capability
    if (capabilities.supportsImageGeneration === true) {
      icons.push(
        <Tooltip key="image" label="Image Generation">
          <IconPhoto size={16} color="purple" />
        </Tooltip>
      );
    }

    // Video generation capability
    if (capabilities.supportsVideoGeneration === true) {
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

  const capabilityOptions = [
    { value: 'chat', label: 'Text Chat' },
    { value: 'vision', label: 'Text + Vision' },
    { value: 'image', label: 'Image Generation' },
    { value: 'video', label: 'Video Generation' }
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
      </Group>

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Capabilities</Table.Th>
            <Table.Th>Series</Table.Th>
            <Table.Th>Status</Table.Th>
            <Table.Th>Actions</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {(() => {
            if (loading) {
              return (
                <Table.Tr>
                  <Table.Td colSpan={5}>
                    <Text ta="center" c="dimmed">Loading...</Text>
                  </Table.Td>
                </Table.Tr>
              );
            }
            if (filteredModels.length === 0) {
              return (
                <Table.Tr>
                  <Table.Td colSpan={5}>
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