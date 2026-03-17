'use client';

import { useState, useEffect, useMemo, useCallback } from 'react';
import { Table, TextInput, Select, Group, ActionIcon, Badge, Text, Tooltip, Stack, HoverCard, Pagination } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import {
  IconEdit,
  IconTrash,
  IconSearch,
  IconEye,
  IconLink,
  IconAlertTriangle,
  IconAlertCircle,
  IconCurrencyDollar,
  IconReceiptDollar
} from '@tabler/icons-react';
import { useAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';
import { EditModelModal } from './EditModelModal';
import { ViewModelModal } from './ViewModelModal';
import { DeleteModelModal } from './DeleteModelModal';
import { ModelCostPreviewModal } from './ModelCostPreviewModal';
import { ModelCostEditorModal } from './ModelCostEditorModal';
import { useModelMappings } from '@/hooks/useModelMappingsApi';
import type { ModelCostDto, ModelDto } from '@knn_labs/conduit-admin-client';
import { extractCapabilities, getErrorMessage } from '@/utils/typeGuards';
import { CapabilityIcons } from '@/components/common/CapabilityIcons';
import { getTokenizerDisplayName } from '@/lib/utils/tokenizerTypes';

// Extended model type with provider mapping status and details
type ModelWithMappingStatus = ModelDto & { 
  hasProviderMappings: boolean;
  providerCount: number;
  providers: Array<{
    id: number;
    identifier: string;
    provider: number | null;
    isPrimary: boolean;
    normalizedProvider?: number | null;
    providerName?: string | null;
  }>;
  seriesParameters?: string | null;
};


interface ModelsTableProps {
  onRefresh?: () => void;
}

export function ModelsTable({ onRefresh }: ModelsTableProps) {
  const [models, setModels] = useState<ModelWithMappingStatus[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [debouncedSearch] = useDebouncedValue(search, 300);
  const [capabilityFilter, setCapabilityFilter] = useState<string | null>(null);
  const [providerFilter, setProviderFilter] = useState<string | null>(null);
  const [selectedModel, setSelectedModel] = useState<ModelDto | null>(null);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [viewModalOpen, setViewModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [costPreviewModalOpen, setCostPreviewModalOpen] = useState(false);
  const [costEditorModalOpen, setCostEditorModalOpen] = useState(false);
  const [existingModelCost, setExistingModelCost] = useState<ModelCostDto | null>(null);

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 50;

  const { executeWithAdmin } = useAdminClient();
  const [seriesNames, setSeriesNames] = useState<Record<number, string>>({});
  const { mappings: modelMappings } = useModelMappings();

  // Build Set of model aliases for O(1) lookup
  const mappedModelAliases = useMemo(() => {
    return new Set(
      modelMappings.map(m => m.modelAlias?.toLowerCase() ?? '')
    );
  }, [modelMappings]);

  // Check if a model has a routing mapping configured
  const hasModelMapping = (modelName: string | null | undefined): boolean => {
    if (!modelName) return false;
    return mappedModelAliases.has(modelName.toLowerCase());
  };

  // Convert providerFilter to hasProviders boolean for the API
  const getHasProviders = (filter: string | null): boolean | undefined => {
    if (filter === 'with-provider') return true;
    if (filter === 'without-provider') return false;
    return undefined;
  };
  const hasProvidersParam = getHasProviders(providerFilter);

  const loadModels = useCallback(async (page: number) => {
    try {
      setLoading(true);
      // Single paginated API call with server-side search/filter + series list in parallel
      const [data, allSeries] = await Promise.all([
        executeWithAdmin(client => client.models.listPaginated({
          page,
          pageSize,
          search: debouncedSearch || undefined,
          capability: capabilityFilter ?? undefined,
          hasProviders: hasProvidersParam,
        })),
        executeWithAdmin(client => client.modelSeries.list()),
      ]);

      // Build lookup maps from the single series list call
      const seriesParametersMap: Record<number, string | null> = {};
      const seriesNamesMap: Record<number, string> = {};
      for (const series of allSeries) {
        if (series.id) {
          seriesParametersMap[series.id] = series.parameters ?? null;
          seriesNamesMap[series.id] = series.name ?? `Series ${series.id}`;
        }
      }
      setSeriesNames(seriesNamesMap);

      // Enhance models with series parameters
      const enhancedModels = data.items.map(model => ({
        ...model,
        seriesParameters: model.modelSeriesId ? seriesParametersMap[model.modelSeriesId] ?? null : null
      }));

      setModels(enhancedModels);
      setTotalPages(data.totalPages);
      setTotalCount(data.totalCount);
    } catch (error) {
      const errorMessage = getErrorMessage(error);
      console.warn('Failed to load models:', errorMessage);
      notifications.show({
        title: 'Error',
        message: `Failed to load models: ${errorMessage}`,
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch, capabilityFilter, hasProvidersParam]);

  // Reset to page 1 when filters change, then load
  useEffect(() => {
    setCurrentPage(1);
    void loadModels(1);
  }, [loadModels]);

  // Load when page changes (but not on filter change — that's handled above)
  const handlePageChange = (page: number) => {
    setCurrentPage(page);
    void loadModels(page);
  };

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

  const handleViewCost = (model: ModelDto) => {
    setSelectedModel(model);
    setCostPreviewModalOpen(true);
  };

  const handleEditCost = async (model: ModelDto) => {
    setSelectedModel(model);

    // Fetch existing cost for this model
    try {
      const result = await executeWithAdmin(client =>
        client.modelCosts.list({ pageSize: 200 })
      );

      const items: ModelCostDto[] = Array.isArray(result)
        ? result as ModelCostDto[]
        : (result?.items ?? []);

      // Find a cost that has this model's name in its associated aliases
      const modelNameLower = model.name?.toLowerCase() ?? '';
      const matchingCost = items.find((cost: ModelCostDto) =>
        cost.associatedModelAliases?.some((alias: string) => {
          const aliasLower = alias.toLowerCase();
          return aliasLower === modelNameLower ||
                 aliasLower.endsWith(`/${modelNameLower}`);
        })
      );

      setExistingModelCost(matchingCost ?? null);
    } catch (err) {
      console.warn('Failed to fetch existing model cost:', err);
      setExistingModelCost(null);
    }

    setCostEditorModalOpen(true);
  };

  const handleDeleteSuccess = () => {
    setDeleteModalOpen(false);
    setSelectedModel(null);
    void loadModels(currentPage);
    onRefresh?.();
  };

  const renderParameterWarning = (model: ModelWithMappingStatus) => {
    const hasModelParameters = model.modelParameters !== null && model.modelParameters !== undefined;
    const hasSeriesParameters = model.seriesParameters !== null && 
                               model.seriesParameters !== undefined && 
                               model.seriesParameters !== "{}";
    
    // No warning if model has parameters
    if (hasModelParameters) {
      return null;
    }
    
    // Critical warning if both model lacks parameters and series has empty/no parameters
    if (!hasModelParameters && !hasSeriesParameters) {
      return (
        <Tooltip 
          label="Critical: This model has no parameter configuration and its series also lacks parameter guidance. Models need parameter configuration for proper UI generation."
          multiline
          w={250}
        >
          <IconAlertCircle 
            size={20} 
            color="var(--mantine-color-red-6)" 
            style={{ cursor: 'help' }}
          />
        </Tooltip>
      );
    }
    
    // Warning if only model lacks parameters (but series has valid parameters)
    return (
      <Tooltip 
        label="Warning: This model has no parameter configuration. Using series defaults."
        multiline
        w={200}
      >
        <IconAlertTriangle 
          size={18} 
          color="var(--mantine-color-yellow-6)" 
          style={{ cursor: 'help' }}
        />
      </Tooltip>
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
                  {provider.providerName ?? 'Unknown'}
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
            <Table.Th>Tokenizer</Table.Th>
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
                  <Table.Td colSpan={7}>
                    <Text ta="center" c="dimmed">Loading...</Text>
                  </Table.Td>
                </Table.Tr>
              );
            }
            if (models.length === 0) {
              return (
                <Table.Tr>
                  <Table.Td colSpan={7}>
                    <Text ta="center" c="dimmed">No models found</Text>
                  </Table.Td>
                </Table.Tr>
              );
            }
            return models.map((model) => (
              <Table.Tr key={model.id}>
                <Table.Td>
                  <Group gap="xs">
                    <Text
                      fw={500}
                      c={hasModelMapping(model.name) ? 'blue' : undefined}
                    >
                      {model.name ?? 'Unnamed'}
                    </Text>
                    {hasModelMapping(model.name) && (
                      <Tooltip label="Has routing mapping configured">
                        <Badge size="xs" color="blue" variant="dot">Mapped</Badge>
                      </Tooltip>
                    )}
                    {renderParameterWarning(model)}
                  </Group>
                </Table.Td>
                <Table.Td>
                  <CapabilityIcons capabilities={extractCapabilities(model)} />
                </Table.Td>
                <Table.Td>
                  {model.modelSeriesId ? (
                    <Text>{seriesNames[model.modelSeriesId] ?? `Series ${model.modelSeriesId}`}</Text>
                  ) : (
                    <Text c="dimmed">-</Text>
                  )}
                </Table.Td>
                <Table.Td>
                  <Tooltip label={getTokenizerDisplayName(model.tokenizerType ?? 0, false)}>
                    <Text size="sm">{getTokenizerDisplayName(model.tokenizerType ?? 0, true)}</Text>
                  </Tooltip>
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
                    <Tooltip label="View Pricing">
                      <ActionIcon
                        variant="subtle"
                        color="green"
                        onClick={() => handleViewCost(model)}
                      >
                        <IconCurrencyDollar size={16} />
                      </ActionIcon>
                    </Tooltip>
                    <Tooltip label="Edit Pricing">
                      <ActionIcon
                        variant="subtle"
                        color="teal"
                        onClick={() => void handleEditCost(model)}
                      >
                        <IconReceiptDollar size={16} />
                      </ActionIcon>
                    </Tooltip>
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

      {totalPages > 1 && (
        <Group justify="space-between">
          <Text size="sm" c="dimmed">
            Showing {models.length} of {totalCount} models
          </Text>
          <Pagination
            total={totalPages}
            value={currentPage}
            onChange={handlePageChange}
          />
        </Group>
      )}

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
              void loadModels(currentPage);
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

          <ModelCostPreviewModal
            isOpen={costPreviewModalOpen}
            model={selectedModel}
            onClose={() => {
              setCostPreviewModalOpen(false);
              setSelectedModel(null);
            }}
          />

          <ModelCostEditorModal
            isOpen={costEditorModalOpen}
            model={selectedModel}
            existingCost={existingModelCost}
            onClose={() => {
              setCostEditorModalOpen(false);
              setExistingModelCost(null);
              setSelectedModel(null);
            }}
            onSuccess={() => {
              setCostEditorModalOpen(false);
              setExistingModelCost(null);
              setSelectedModel(null);
              void loadModels(currentPage);
              onRefresh?.();
            }}
          />
        </>
      )}
    </Stack>
  );
}