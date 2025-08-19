'use client';

import { useState, useEffect } from 'react';
import { Table, TextInput, Select, Group, ActionIcon, Badge, Text, Tooltip, Stack } from '@mantine/core';
import { IconEdit, IconTrash, IconSearch, IconEye } from '@tabler/icons-react';
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
  const [typeFilter, setTypeFilter] = useState<string | null>(null);
  const [selectedModel, setSelectedModel] = useState<ModelDto | null>(null);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [viewModalOpen, setViewModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  
  const { executeWithAdmin } = useAdminClient();

  const loadModels = async () => {
    try {
      setLoading(true);
      const data = await executeWithAdmin(client => client.models.list());
      setModels(data);
      setFilteredModels(data);
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

    if (typeFilter) {
      filtered = filtered.filter(model => model.modelType?.toString() === typeFilter);
    }

    setFilteredModels(filtered);
  }, [search, typeFilter, models]);

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

  const getTypeBadgeColor = (type: number | undefined) => {
    switch (type) {
      case 0: return 'blue'; // Text/Chat
      case 1: return 'purple'; // Image
      case 2: return 'orange'; // Audio
      case 3: return 'pink'; // Video
      case 4: return 'green'; // Embedding
      default: return 'gray';
    }
  };

  const getTypeName = (type: number | undefined) => {
    switch (type) {
      case 0: return 'Text';
      case 1: return 'Image';
      case 2: return 'Audio';
      case 3: return 'Video';
      case 4: return 'Embedding';
      default: return 'Unknown';
    }
  };

  const modelTypes = Array.from(new Set(models.map(m => m.modelType).filter(t => t !== undefined)))
    .map(type => ({
      value: type?.toString() ?? '',
      label: getTypeName(type)
    }));

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
          placeholder="Filter by type"
          data={modelTypes}
          value={typeFilter}
          onChange={setTypeFilter}
          clearable
          w={200}
        />
      </Group>

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Display Name</Table.Th>
            <Table.Th>Type</Table.Th>
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
                  <Text c="dimmed">-</Text>
                </Table.Td>
                <Table.Td>
                  <Badge color={getTypeBadgeColor(model.modelType)} variant="light">
                    {getTypeName(model.modelType)}
                  </Badge>
                </Table.Td>
                <Table.Td>
                  <Text c="dimmed">{model.modelSeriesId ? `ID: ${model.modelSeriesId}` : '-'}</Text>
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