'use client';

import { useState } from 'react';
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
  Tooltip,
} from '@mantine/core';
import {
  IconSearch,
  IconEdit,
  IconTrash,
  IconDots,
  IconEye,
  IconAdjustments,
  IconDatabase,
  IconStairs,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { ModelCost } from '../types/modelCost';
import { EditModelCostModal } from './EditModelCostModal';
import { ViewModelCostModal } from './ViewModelCostModal';
import { formatters } from '@/lib/utils/formatters';
import { getProviderDisplayName, getProviderTypeFromDto, ProviderType } from '@/lib/utils/providerTypeUtils';

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
  const [providerFilter, setProviderFilter] = useState<number | null>(null);
  const [activeFilter, setActiveFilter] = useState<string | null>('true');
  
  // Modal state
  const [editingCost, setEditingCost] = useState<ModelCost | null>(null);
  const [viewingCost, setViewingCost] = useState<ModelCost | null>(null);

  // Fetch data
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['model-costs', page, pageSize, providerFilter, activeFilter],
    queryFn: () => fetchModelCosts(page, pageSize, {
      provider: providerFilter ?? undefined,
      isActive: (() => {
        if (activeFilter === 'true') return true;
        if (activeFilter === 'false') return false;
        return undefined;
      })(),
    }),
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: deleteModelCost,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['model-costs'] });
      onRefresh?.();
    },
  });

  // Filter data client-side for search
  const filteredData = data?.items?.filter(cost => {
    if (!searchTerm) return true;
    const search = searchTerm.toLowerCase();
    return (
      cost.modelIdPattern.toLowerCase().includes(search) ||
      (cost.providerName ?? '').toLowerCase().includes(search) ||
      cost.modelType.toLowerCase().includes(search)
    );
  }) ?? [];

  // Get unique providers for filter
  const uniqueProviders = Array.from(new Set(
    data?.items?.map(c => {
      try {
        const providerType = getProviderTypeFromDto(c);
        return providerType.toString();
      } catch {
        return null;
      }
    }).filter(Boolean) ?? []
  )) as string[];
  
  // Create provider options with display names
  const providerOptions = uniqueProviders.map(providerTypeStr => ({
    value: parseInt(providerTypeStr).toString(), // Keep as string for Select component
    label: getProviderDisplayName(parseInt(providerTypeStr) as ProviderType)
  }));

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
      const hasCachedRates = cost.cachedInputCostPerMillionTokens !== undefined || 
                            cost.cachedInputWriteCostPerMillionTokens !== undefined;
      
      return (
        <Stack gap={2}>
          <Text size="xs">
            Input: {formatters.currency(((cost.inputCostPerMillionTokens ?? 0) / 1000), { currency: 'USD', precision: 4 })}/1K
          </Text>
          <Text size="xs">
            Output: {formatters.currency(((cost.outputCostPerMillionTokens ?? 0) / 1000), { currency: 'USD', precision: 4 })}/1K
          </Text>
          {hasCachedRates && (
            <Group gap="xs">
              <Badge size="xs" variant="light" color="blue">Cached</Badge>
            </Group>
          )}
        </Stack>
      );
    }
    if (cost.imageCostPerImage !== undefined) {
      const hasMultipliers = cost.imageQualityMultipliers && 
        cost.imageQualityMultipliers !== '{}';
      
      return (
        <Group gap="xs">
          <Text size="xs">{formatters.currency(cost.imageCostPerImage, { currency: 'USD' })}/image</Text>
          {hasMultipliers && (
            <Tooltip label="Has quality multipliers">
              <IconAdjustments size={14} />
            </Tooltip>
          )}
        </Group>
      );
    }
    if (cost.costPerImage !== undefined) {
      return <Text size="xs">{formatters.currency(cost.costPerImage, { currency: 'USD' })}/image</Text>;
    }
    if (cost.costPerSecond !== undefined) {
      return <Text size="xs">{formatters.currency(cost.costPerSecond, { currency: 'USD' })}/second</Text>;
    }
    if (cost.costPerSearchUnit !== undefined) {
      return (
        <Stack gap={2}>
          <Text size="xs">
            Search: {formatters.currency(cost.costPerSearchUnit, { currency: 'USD', precision: 4 })}/1K units
          </Text>
          <Badge size="xs" variant="light" color="violet">Rerank</Badge>
        </Stack>
      );
    }
    if (cost.costPerInferenceStep !== undefined) {
      return (
        <Stack gap={2}>
          <Text size="xs">
            Steps: {formatters.currency(cost.costPerInferenceStep, { currency: 'USD', precision: 4 })}/step
          </Text>
          {cost.defaultInferenceSteps && (
            <Badge size="xs" variant="light" color="teal">Default: {cost.defaultInferenceSteps} steps</Badge>
          )}
        </Stack>
      );
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
              {data?.totalCount ?? 0} total configurations
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
              value={providerFilter?.toString() ?? null}
              onChange={(value) => setProviderFilter(value ? parseInt(value) : null)}
              data={providerOptions}
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
                    <Table.Th>Batch</Table.Th>
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
                          {(() => {
                            try {
                              const providerType = getProviderTypeFromDto(cost);
                              return getProviderDisplayName(providerType);
                            } catch {
                              return cost.providerName ?? 'Unknown';
                            }
                          })()}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Badge variant="outline" size="sm">
                            {getCostTypeLabel(cost.modelType)}
                          </Badge>
                          {(cost.cachedInputCostPerMillionTokens ?? cost.cachedInputWriteCostPerMillionTokens) && (
                            <Tooltip label="Supports prompt caching">
                              <IconDatabase size={14} style={{ opacity: 0.7 }} />
                            </Tooltip>
                          )}
                          {cost.costPerSearchUnit && (
                            <Tooltip label="Search/rerank model">
                              <IconSearch size={14} style={{ opacity: 0.7 }} />
                            </Tooltip>
                          )}
                          {cost.costPerInferenceStep && (
                            <Tooltip label="Step-based pricing">
                              <IconStairs size={14} style={{ opacity: 0.7 }} />
                            </Tooltip>
                          )}
                        </Group>
                      </Table.Td>
                      <Table.Td>{getCostDisplay(cost)}</Table.Td>
                      <Table.Td>
                        {cost.supportsBatchProcessing ? (
                          <Badge color="green" size="sm">
                            {cost.batchProcessingMultiplier 
                              ? `${(cost.batchProcessingMultiplier * 100).toFixed(0)}%` 
                              : 'Yes'}
                          </Badge>
                        ) : null}
                      </Table.Td>
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
            void refetch();
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