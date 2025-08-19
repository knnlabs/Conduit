'use client';

import { useState, useEffect } from 'react';
import { Table, TextInput, Group, ActionIcon, Badge, Text, Tooltip, Stack } from '@mantine/core';
import { IconEdit, IconTrash, IconSearch, IconEye } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import { EditModelSeriesModal } from './EditModelSeriesModal';
import { ViewModelSeriesModal } from './ViewModelSeriesModal';
import { DeleteModelSeriesModal } from './DeleteModelSeriesModal';
import type { ModelSeriesDto } from '@knn_labs/conduit-admin-client';


interface ModelSeriesTableProps {
  onRefresh?: () => void;
}

export function ModelSeriesTable({ onRefresh }: ModelSeriesTableProps) {
  const [series, setSeries] = useState<ModelSeriesDto[]>([]);
  const [filteredSeries, setFilteredSeries] = useState<ModelSeriesDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [selectedSeries, setSelectedSeries] = useState<ModelSeriesDto | null>(null);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [viewModalOpen, setViewModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  
  const { executeWithAdmin } = useAdminClient();

  const loadSeries = async () => {
    try {
      setLoading(true);
      const data = await executeWithAdmin(client => client.modelSeries.list());
      setSeries(data);
      setFilteredSeries(data);
    } catch (error) {
      console.error('Failed to load model series:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load model series',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadSeries();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    let filtered = [...series];

    if (search) {
      filtered = filtered.filter(s =>
        (s.name?.toLowerCase().includes(search.toLowerCase()) ?? false) ||
        // displayName doesn't exist in ModelSeriesDto
        (s.authorName?.toLowerCase().includes(search.toLowerCase()) ?? false)
      );
    }

    setFilteredSeries(filtered);
  }, [search, series]);

  const handleEdit = (item: ModelSeriesDto) => {
    setSelectedSeries(item);
    setEditModalOpen(true);
  };

  const handleView = (item: ModelSeriesDto) => {
    setSelectedSeries(item);
    setViewModalOpen(true);
  };

  const handleDelete = (item: ModelSeriesDto) => {
    setSelectedSeries(item);
    setDeleteModalOpen(true);
  };

  const handleDeleteSuccess = () => {
    setDeleteModalOpen(false);
    setSelectedSeries(null);
    void loadSeries();
    onRefresh?.();
  };

  return (
    <Stack gap="md">
      <TextInput
        placeholder="Search series..."
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(e) => setSearch(e.currentTarget.value)}
      />

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Author</Table.Th>
            <Table.Th>Model Count</Table.Th>
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
            if (filteredSeries.length === 0) {
              return (
                <Table.Tr>
                  <Table.Td colSpan={5}>
                    <Text ta="center" c="dimmed">No series found</Text>
                  </Table.Td>
                </Table.Tr>
              );
            }
            return filteredSeries.map((item) => (
              <Table.Tr key={item.id}>
                <Table.Td>
                  <Text fw={500}>{item.name}</Text>
                </Table.Td>
                <Table.Td>
                  {item.authorName ?? <Text c="dimmed">-</Text>}
                </Table.Td>
                <Table.Td>
                  <Badge variant="light">
                    0 models
                  </Badge>
                </Table.Td>
                <Table.Td>
                  <Badge color="green" variant="light">
                    Active
                  </Badge>
                </Table.Td>
                <Table.Td>
                  <Group gap="xs">
                    <Tooltip label="View">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => handleView(item)}
                      >
                        <IconEye size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => handleEdit(item)}
                      >
                        <IconEdit size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Delete">
                      <ActionIcon
                        variant="subtle"
                        color="red"
                        onClick={() => handleDelete(item)}
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

      {selectedSeries && (
        <>
          <EditModelSeriesModal
            isOpen={editModalOpen}
            series={selectedSeries}
            onClose={() => {
              setEditModalOpen(false);
              setSelectedSeries(null);
            }}
            onSuccess={() => {
              setEditModalOpen(false);
              setSelectedSeries(null);
              void loadSeries();
              onRefresh?.();
            }}
          />

          <ViewModelSeriesModal
            isOpen={viewModalOpen}
            series={selectedSeries}
            onClose={() => {
              setViewModalOpen(false);
              setSelectedSeries(null);
            }}
          />

          <DeleteModelSeriesModal
            isOpen={deleteModalOpen}
            series={selectedSeries}
            onClose={() => {
              setDeleteModalOpen(false);
              setSelectedSeries(null);
            }}
            onSuccess={handleDeleteSuccess}
          />
        </>
      )}
    </Stack>
  );
}