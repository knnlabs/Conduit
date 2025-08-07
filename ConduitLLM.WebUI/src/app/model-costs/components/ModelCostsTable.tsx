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
  IconVideo,
  IconPhoto,
  IconMicrophone,
  IconLetterT,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { useModelCostsApi } from '../hooks/useModelCostsApi';
import { ModelCost } from '../types/modelCost';
import { PricingModel } from '@knn_labs/conduit-admin-client';
import { EditModelCostModalV2 } from './EditModelCostModalV2';
import { ViewModelCostModal } from './ViewModelCostModal';
import { formatters } from '@/lib/utils/formatters';
import { CallToActionEmpty } from './CallToActionEmpty';
import { useEnrichedModelCosts } from '../hooks/useEnrichedModelCosts';

interface ModelCostsTableProps {
  onRefresh?: () => void;
  hasProviders?: boolean;
  hasModelMappings?: boolean;
}

export function ModelCostsTable({ onRefresh, hasProviders, hasModelMappings }: ModelCostsTableProps) {
  const queryClient = useQueryClient();
  const { fetchModelCosts, deleteModelCost } = useModelCostsApi();
  
  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  
  // Filter state
  const [searchTerm, setSearchTerm] = useState('');
  const [activeFilter, setActiveFilter] = useState<string | null>('true');
  
  // Modal state
  const [editingCost, setEditingCost] = useState<ModelCost | null>(null);
  const [viewingCost, setViewingCost] = useState<ModelCost | null>(null);

  // Fetch data
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['model-costs', page, pageSize, activeFilter],
    queryFn: () => fetchModelCosts(page, pageSize, {
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
      // Invalidate all model-costs queries regardless of their parameters
      void queryClient.invalidateQueries({ 
        queryKey: ['model-costs'],
        exact: false 
      });
      onRefresh?.();
    },
  });

  // Enrich model costs with provider information
  const { enrichedCosts, isLoading: enrichmentLoading } = useEnrichedModelCosts(data?.items);

  // Filter data client-side for search
  const filteredData = enrichedCosts.filter((cost) => {
    if (!searchTerm) return true;
    const search = searchTerm.toLowerCase();
    return (
      cost.costName.toLowerCase().includes(search) ||
      cost.associatedModelAliases.some((alias: string) => alias.toLowerCase().includes(search)) ||
      cost.modelType.toLowerCase().includes(search) ||
      cost.providers.some(p => 
        p.providerName.toLowerCase().includes(search) || 
        p.providerType.toLowerCase().includes(search)
      )
    );
  });

  const handleDelete = (cost: ModelCost) => {
    modals.openConfirmModal({
      title: 'Delete Model Pricing',
      children: (
        <Text size="sm">
          Are you sure you want to delete the pricing configuration{' '}
          <Text span fw={600}>{cost.costName}</Text>?
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

  const getPricingModelBadge = (model?: PricingModel): React.ReactNode => {
    if (model === undefined || model === PricingModel.Standard) return null;
    
    const badges: Record<PricingModel, { label: string; color: string; icon?: React.ReactNode }> = {
      [PricingModel.Standard]: { label: 'Standard', color: 'gray' },
      [PricingModel.PerVideo]: { label: 'Per Video', color: 'purple', icon: <IconVideo size={14} /> },
      [PricingModel.PerSecondVideo]: { label: 'Per Second', color: 'indigo', icon: <IconVideo size={14} /> },
      [PricingModel.InferenceSteps]: { label: 'Steps', color: 'teal', icon: <IconStairs size={14} /> },
      [PricingModel.TieredTokens]: { label: 'Tiered', color: 'orange' },
      [PricingModel.PerImage]: { label: 'Per Image', color: 'pink', icon: <IconPhoto size={14} /> },
      [PricingModel.PerMinuteAudio]: { label: 'Per Minute', color: 'cyan', icon: <IconMicrophone size={14} /> },
      [PricingModel.PerThousandCharacters]: { label: 'Per 1K Chars', color: 'lime', icon: <IconLetterT size={14} /> },
    };
    
    const badge = badges[model];
    if (!badge) return null;
    
    return (
      <Badge size="xs" variant="light" color={badge.color}>
        <Group gap={2}>
          {badge.icon}
          {badge.label}
        </Group>
      </Badge>
    );
  };

  const getCostDisplay = (cost: ModelCost): React.ReactNode => {
    if (cost.inputCostPerMillionTokens !== undefined && cost.outputCostPerMillionTokens !== undefined) {
      const hasCachedRates = cost.cachedInputCostPerMillionTokens !== undefined || 
                            cost.cachedInputWriteCostPerMillionTokens !== undefined;
      
      return (
        <Stack gap={2}>
          <Text size="xs">
            Input: {formatters.currency(cost.inputCostPerMillionTokens, { currency: 'USD', precision: 2 })}/M
          </Text>
          <Text size="xs">
            Output: {formatters.currency(cost.outputCostPerMillionTokens, { currency: 'USD', precision: 2 })}/M
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
    if (cost.imageCostPerImage !== undefined) {
      return <Text size="xs">{formatters.currency(cost.imageCostPerImage, { currency: 'USD' })}/image</Text>;
    }
    if (cost.videoCostPerSecond !== undefined) {
      return <Text size="xs">{formatters.currency(cost.videoCostPerSecond, { currency: 'USD' })}/second</Text>;
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
              placeholder="Search by cost name, model aliases, or provider..."
              leftSection={<IconSearch size={16} />}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.currentTarget.value)}
              style={{ flex: 1 }}
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
          <LoadingOverlay visible={isLoading || enrichmentLoading} />
          
          {error && (
            <Center py="xl">
              <Text c="red">Failed to load model costs</Text>
            </Center>
          )}

          {!error && filteredData.length === 0 && !isLoading && (
            <>
              {hasProviders !== undefined && hasModelMappings !== undefined && (!hasProviders || !hasModelMappings) ? (
                <CallToActionEmpty hasProviders={hasProviders} hasModelMappings={hasModelMappings} />
              ) : (
                <Center py="xl">
                  <Text c="dimmed">No model pricing configurations found</Text>
                </Center>
              )}
            </>
          )}

          {!error && filteredData.length > 0 && (
            <ScrollArea>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Cost Name</Table.Th>
                    <Table.Th>Model Aliases</Table.Th>
                    <Table.Th>Providers</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Pricing Model</Table.Th>
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
                          {cost.costName}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="xs" style={{ maxWidth: 200 }} truncate title={cost.associatedModelAliases.join(', ')}>
                          {cost.associatedModelAliases.length > 0 
                            ? cost.associatedModelAliases.join(', ')
                            : <Text span c="dimmed">No models assigned</Text>
                          }
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Group gap={4}>
                          {cost.providers.length > 0 ? (
                            cost.providers.slice(0, 2).map((provider, idx) => (
                              <Badge 
                                key={provider.providerId} 
                                size="xs" 
                                variant="light"
                                color={idx === 0 ? 'blue' : 'gray'}
                              >
                                {provider.providerName}
                              </Badge>
                            ))
                          ) : (
                            <Text size="xs" c="dimmed">No providers</Text>
                          )}
                          {cost.providers.length > 2 && (
                            <Tooltip label={cost.providers.map(p => p.providerName).join(', ')}>
                              <Badge size="xs" variant="light" color="gray">
                                +{cost.providers.length - 2}
                              </Badge>
                            </Tooltip>
                          )}
                        </Group>
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
                      <Table.Td>
                        {getPricingModelBadge(cost.pricingModel)}
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
        <EditModelCostModalV2
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