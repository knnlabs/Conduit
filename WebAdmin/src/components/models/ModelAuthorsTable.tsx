'use client';

import { useState, useEffect } from 'react';
import { Table, TextInput, Group, ActionIcon, Badge, Text, Tooltip, Stack } from '@mantine/core';
import { IconEdit, IconTrash, IconSearch, IconEye } from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import { EditModelAuthorModal } from './EditModelAuthorModal';
import { ViewModelAuthorModal } from './ViewModelAuthorModal';
import { DeleteModelAuthorModal } from './DeleteModelAuthorModal';
import type { ModelAuthorDto } from '@knn_labs/conduit-admin-client';


interface ModelAuthorsTableProps {
  onRefresh?: () => void;
}

export function ModelAuthorsTable({ onRefresh }: ModelAuthorsTableProps) {
  const [authors, setAuthors] = useState<ModelAuthorDto[]>([]);
  const [filteredAuthors, setFilteredAuthors] = useState<ModelAuthorDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [selectedAuthor, setSelectedAuthor] = useState<ModelAuthorDto | null>(null);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [viewModalOpen, setViewModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [seriesCounts, setSeriesCounts] = useState<Record<number, number>>({});
  const [modelCounts, setModelCounts] = useState<Record<number, number>>({});
  
  const { executeWithAdmin } = useAdminClient();

  const loadAuthors = async () => {
    try {
      setLoading(true);
      const data = await executeWithAdmin(client => client.modelAuthors.list());
      setAuthors(data);
      setFilteredAuthors(data);
      
      // Load series counts for each author
      await loadSeriesCounts(data);
    } catch (error) {
      console.error('Failed to load authors:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load authors',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  const loadSeriesCounts = async (authorsList: ModelAuthorDto[]) => {
    const seriesCountsMap: Record<number, number> = {};
    const modelCountsMap: Record<number, number> = {};
    
    await Promise.all(
      authorsList.map(async (author) => {
        if (author.id) {
          try {
            const series = await executeWithAdmin(client => 
              client.modelAuthors.getSeries(author.id as number)
            );
            seriesCountsMap[author.id] = series.length;
            
            // Load model counts for each series
            const modelCountPromises = series.map(async (s) => {
              if (s.id) {
                try {
                  const models = await executeWithAdmin(client => 
                    client.modelSeries.getModels(s.id as number)
                  );
                  return models.length;
                } catch (error) {
                  console.error(`Failed to load models for series ${s.id}:`, error);
                  return 0;
                }
              }
              return 0;
            });
            
            const modelCountsPerSeries = await Promise.all(modelCountPromises);
            modelCountsMap[author.id] = modelCountsPerSeries.reduce((sum, count) => sum + count, 0);
          } catch (error) {
            console.error(`Failed to load series count for author ${author.id}:`, error);
            seriesCountsMap[author.id] = 0;
            modelCountsMap[author.id] = 0;
          }
        }
      })
    );
    
    setSeriesCounts(seriesCountsMap);
    setModelCounts(modelCountsMap);
  };

  useEffect(() => {
    void loadAuthors();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    let filtered = [...authors];

    if (search) {
      filtered = filtered.filter(a =>
        (a.name?.toLowerCase().includes(search.toLowerCase()) ?? false) ||
        (a.websiteUrl?.toLowerCase().includes(search.toLowerCase()) ?? false)
      );
    }

    setFilteredAuthors(filtered);
  }, [search, authors]);

  const handleEdit = (author: ModelAuthorDto) => {
    setSelectedAuthor(author);
    setEditModalOpen(true);
  };

  const handleView = (author: ModelAuthorDto) => {
    setSelectedAuthor(author);
    setViewModalOpen(true);
  };

  const handleDelete = (author: ModelAuthorDto) => {
    setSelectedAuthor(author);
    setDeleteModalOpen(true);
  };

  const handleDeleteSuccess = () => {
    setDeleteModalOpen(false);
    setSelectedAuthor(null);
    void loadAuthors();
    onRefresh?.();
  };

  return (
    <Stack gap="md">
      <TextInput
        placeholder="Search authors..."
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(e) => setSearch(e.currentTarget.value)}
      />

      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Name</Table.Th>
            <Table.Th>Website</Table.Th>
            <Table.Th>Series Count</Table.Th>
            <Table.Th>Model Count</Table.Th>
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
            if (filteredAuthors.length === 0) {
              return (
                <Table.Tr>
                  <Table.Td colSpan={5}>
                    <Text ta="center" c="dimmed">No authors found</Text>
                  </Table.Td>
                </Table.Tr>
              );
            }
            return filteredAuthors.map((author) => (
              <Table.Tr key={author.id}>
                <Table.Td>
                  <Text fw={500}>{author.name}</Text>
                </Table.Td>
                <Table.Td>
                  {author.websiteUrl ? (
                    <a href={author.websiteUrl} target="_blank" rel="noopener noreferrer">
                      {new URL(author.websiteUrl).hostname}
                    </a>
                  ) : (
                    <Text c="dimmed">-</Text>
                  )}
                </Table.Td>
                <Table.Td>
                  <Badge variant="light">
                    {author.id ? (seriesCounts[author.id] ?? 0) : 0} series
                  </Badge>
                </Table.Td>
                <Table.Td>
                  <Badge variant="light" color="blue">
                    {author.id ? (modelCounts[author.id] ?? 0) : 0} models
                  </Badge>
                </Table.Td>
                <Table.Td>
                  <Group gap="xs">
                    <Tooltip label="View">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => handleView(author)}
                      >
                        <IconEye size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => handleEdit(author)}
                      >
                        <IconEdit size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Delete">
                      <ActionIcon
                        variant="subtle"
                        color="red"
                        onClick={() => handleDelete(author)}
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

      {selectedAuthor && (
        <>
          <EditModelAuthorModal
            isOpen={editModalOpen}
            author={selectedAuthor}
            onClose={() => {
              setEditModalOpen(false);
              setSelectedAuthor(null);
            }}
            onSuccess={() => {
              setEditModalOpen(false);
              setSelectedAuthor(null);
              void loadAuthors();
              onRefresh?.();
            }}
          />

          <ViewModelAuthorModal
            isOpen={viewModalOpen}
            author={selectedAuthor}
            onClose={() => {
              setViewModalOpen(false);
              setSelectedAuthor(null);
            }}
          />

          <DeleteModelAuthorModal
            isOpen={deleteModalOpen}
            author={selectedAuthor}
            onClose={() => {
              setDeleteModalOpen(false);
              setSelectedAuthor(null);
            }}
            onSuccess={handleDeleteSuccess}
          />
        </>
      )}
    </Stack>
  );
}