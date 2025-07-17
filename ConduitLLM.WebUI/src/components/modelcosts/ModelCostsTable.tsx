'use client';

import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Table,
  ScrollArea,
  TextInput,
  Select,
  Group,
  ActionIcon,
  Menu,
  Badge,
  Text,
  Card,
  LoadingOverlay,
  Stack,
  Pagination,
  Center,
} from '@mantine/core';
import {
  IconSearch,
  IconEdit,
  IconTrash,
  IconDots,
  IconEye,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { useModelCostsApi, ModelCost } from '@/hooks/useModelCostsApi';
import { EditModelCostModal } from './EditModelCostModal';
import { ViewModelCostModal } from './ViewModelCostModal';
import { formatters } from '@/lib/utils/formatters';

interface ModelCostsTableProps {
  onRefresh?: () => void;
}

export function ModelCostsTable({ onRefresh }: ModelCostsTableProps) {
  const queryClient = useQueryClient();
  const { fetchModelCosts, deleteModelCost } = useModelCostsApi();
  
  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  
  // Filter state
  const [searchTerm, setSearchTerm] = useState('');
  const [providerFilter, setProviderFilter] = useState<string | null>(null);
  const [activeFilter, setActiveFilter] = useState<string | null>('true');
  
  // Modal state
  const [editingCost, setEditingCost] = useState<ModelCost | null>(null);
  const [viewingCost, setViewingCost] = useState<ModelCost | null>(null);

  // Fetch data
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['model-costs', page, pageSize, providerFilter, activeFilter],
    queryFn: () => fetchModelCosts(page, pageSize, {
      provider: providerFilter || undefined,
      isActive: activeFilter === 'true' ? true : activeFilter === 'false' ? false : undefined,
    }),
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: deleteModelCost,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['model-costs'] });
      onRefresh?.();
    },
  });

  // Filter data client-side for search
  const filteredData = data?.items.filter(cost => {
    if (!searchTerm) return true;
    const search = searchTerm.toLowerCase();
    return (
      cost.modelIdPattern.toLowerCase().includes(search) ||
      cost.providerName.toLowerCase().includes(search) ||
      cost.modelType.toLowerCase().includes(search)
    );
  }) || [];

  // Get unique providers for filter
  const uniqueProviders = Array.from(new Set(data?.items.map(c => c.providerName) || []));

  const handleDelete = (cost: ModelCost) => {
    modals.openConfirmModal({
      title: 'Delete Model Pricing',
      children: (
        <Text size="sm">
          Are you sure you want to delete the pricing configuration for{' '}
          <Text span fw={600}>{cost.modelIdPattern}</Text>?
          This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => deleteMutation.mutate(cost.id),
    });
  };

  const getCostTypeLabel = (type: string) => {
    const labels: Record<string, string> = {
      chat: 'Chat',
      embedding: 'Embedding',
      image: 'Image',
      audio: 'Audio',
      video: 'Video',
    };
    return labels[type] || type;
  };

  const getCostDisplay = (cost: ModelCost) => {
    if (cost.inputCostPerMillionTokens !== undefined && cost.outputCostPerMillionTokens !== undefined) {
      return (
        <Stack gap={2}>
          <Text size="xs">
            Input: {formatters.currency((cost.inputCostPerMillionTokens / 1000), { currency: 'USD', precision: 4 })}/1K
          </Text>
          <Text size="xs">
            Output: {formatters.currency((cost.outputCostPerMillionTokens / 1000), { currency: 'USD', precision: 4 })}/1K
          </Text>
        </Stack>
      );
    }
    if (cost.costPerImage !== undefined) {
      return <Text size="xs">{formatters.currency(cost.costPerImage, { currency: 'USD' })}/image</Text>;
    }
    if (cost.costPerSecond !== undefined) {
      return <Text size="xs">{formatters.currency(cost.costPerSecond, { currency: 'USD' })}/second</Text>;
    }
    return <Text size="xs" c="dimmed">No pricing set</Text>;
  };

  return (
    <>
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Text fw={600}>Model Pricing Configurations</Text>
            <Text size="sm" c="dimmed">
              {data?.totalCount || 0} total configurations
            </Text>
          </Group>
        </Card.Section>

        <Card.Section p="md">
          <Group>
            <TextInput
              placeholder="Search models..."
              leftSection={<IconSearch size={16} />}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.currentTarget.value)}
              style={{ flex: 1 }}
            />
            <Select
              placeholder="All providers"
              value={providerFilter}
              onChange={setProviderFilter}
              data={uniqueProviders}
              clearable
              w={200}
            />
            <Select
              placeholder="Status"
              value={activeFilter}
              onChange={setActiveFilter}
              data={[
                { value: 'true', label: 'Active' },
                { value: 'false', label: 'Inactive' },
              ]}
              clearable
              w={150}
            />
          </Group>
        </Card.Section>

        <Card.Section p="md" pt={0}>
          <LoadingOverlay visible={isLoading} />
          
          {error && (
            <Center py="xl">
              <Text c="red">Failed to load model costs</Text>
            </Center>
          )}

          {!error && filteredData.length === 0 && !isLoading && (
            <Center py="xl">
              <Text c="dimmed">No model pricing configurations found</Text>
            </Center>
          )}

          {!error && filteredData.length > 0 && (
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Model Pattern</Table.Th>
                    <Table.Th>Provider</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Pricing</Table.Th>
                    <Table.Th>Priority</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Updated</Table.Th>
                    <Table.Th w={80}></Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {filteredData.map((cost) => (
                    <Table.Tr key={cost.id}>
                      <Table.Td>
                        <Text size="sm" fw={500}>
                          {cost.modelIdPattern}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light" size="sm">
                          {cost.providerName}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="outline" size="sm">
                          {getCostTypeLabel(cost.modelType)}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{getCostDisplay(cost)}</Table.Td>
                      <Table.Td>
                        <Text size="sm">{cost.priority}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          color={cost.isActive ? 'green' : 'gray'}
                          variant="light"
                          size="sm"
                        >
                          {cost.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="xs" c="dimmed">
                          {formatters.date(cost.updatedAt)}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Menu position="bottom-end" withinPortal>
                          <Menu.Target>
                            <ActionIcon variant="subtle" size="sm">
                              <IconDots size={16} />
                            </ActionIcon>
                          </Menu.Target>
                          <Menu.Dropdown>
                            <Menu.Item
                              leftSection={<IconEye size={14} />}
                              onClick={() => setViewingCost(cost)}
                            >
                              View Details
                            </Menu.Item>
                            <Menu.Item
                              leftSection={<IconEdit size={14} />}
                              onClick={() => setEditingCost(cost)}
                            >
                              Edit
                            </Menu.Item>
                            <Menu.Divider />
                            <Menu.Item
                              leftSection={<IconTrash size={14} />}
                              color="red"
                              onClick={() => handleDelete(cost)}
                            >
                              Delete
                            </Menu.Item>
                          </Menu.Dropdown>
                        </Menu>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          )}

          {data && data.totalPages > 1 && (
            <Center mt="md">
              <Pagination
                value={page}
                onChange={setPage}
                total={data.totalPages}
                size="sm"
              />
            </Center>
          )}
        </Card.Section>
      </Card>

      {editingCost && (
        <EditModelCostModal
          isOpen={!!editingCost}
          modelCost={editingCost}
          onClose={() => setEditingCost(null)}
          onSuccess={() => {
            setEditingCost(null);
            refetch();
            onRefresh?.();
          }}
        />
      )}

      {viewingCost && (
        <ViewModelCostModal
          isOpen={!!viewingCost}
          modelCost={viewingCost}
          onClose={() => setViewingCost(null)}
        />
      )}
    </>
  );
}