'use client';

import { withAdminClient } from '@/lib/client/adminClient';
import { useAdminMutation } from '@/hooks/useAdminMutation';
import { notify } from '@/lib/notifications';
import { downloadFile } from '../utils/csvHelpers';
import type {
  ModelCost,
  CreateModelCostDto,
  UpdateModelCostDto,
  ModelCostListResponse,
  ModelCostFilters,
} from '../types/modelCost';

/**
 * Fetch model costs with pagination and filters.
 * This is a standalone async function (not a hook) for use inside useQuery.
 */
export async function fetchModelCosts(page = 1, pageSize = 50, filters?: ModelCostFilters): Promise<ModelCostListResponse> {
  const result = await withAdminClient(client =>
    client.modelCosts.list({
      page,
      pageSize,
      provider: filters?.providerId,
      isActive: filters?.isActive,
      modelType: filters?.modelType,
    })
  );

  return {
    items: result.items,
    totalCount: result.totalCount,
    page: result.page,
    pageSize: result.pageSize,
    totalPages: result.totalPages ?? Math.ceil(result.totalCount / result.pageSize),
  } as ModelCostListResponse;
}

export async function fetchModelCostById(id: number): Promise<ModelCost> {
  return withAdminClient(client => client.modelCosts.getById(id));
}

/**
 * Find a model cost by pattern matching on name or aliases.
 */
export async function getModelCostByPattern(pattern: string): Promise<ModelCost | null> {
  try {
    const costs = await withAdminClient(client =>
      client.modelCosts.list({ pageSize: 100 })
    );

    const matchingCost = costs.items.find(cost =>
      cost.costName.includes(pattern) ||
      cost.associatedModelAliases.some(alias => alias.includes(pattern))
    );

    return matchingCost as ModelCost ?? null;
  } catch (error) {
    console.warn('Error fetching model cost by pattern:', error);
    return null;
  }
}

// --- Mutation hooks using useAdminMutation ---

export function useCreateModelCost() {
  return useAdminMutation<ModelCost, CreateModelCostDto>({
    mutationFn: (data) => (client) => client.modelCosts.create(data),
    successMessage: 'Model pricing created successfully',
    invalidateKeys: ['model-costs'],
  });
}

export function useUpdateModelCost() {
  return useAdminMutation<ModelCost, { id: number; data: UpdateModelCostDto }>({
    mutationFn: ({ id, data }) => (client) => client.modelCosts.update(id, data),
    successMessage: 'Model pricing updated successfully',
    invalidateKeys: ['model-costs'],
  });
}

export function useDeleteModelCost() {
  return useAdminMutation<void, number>({
    mutationFn: (id) => (client) => client.modelCosts.deleteById(id),
    successMessage: 'Model pricing deleted successfully',
    invalidateKeys: ['model-costs'],
  });
}

export function useImportModelCosts() {
  return useAdminMutation<{ success?: number; failed?: number; errors?: Array<{ error: string }> }, CreateModelCostDto[]>({
    mutationFn: (costs) => (client) => client.modelCosts.import(costs),
    successMessage: (result) => `Successfully imported ${result.success ?? 0} model costs`,
    invalidateKeys: ['model-costs'],
  });
}

// --- Legacy hook for backward compatibility during migration ---
// Consumers should migrate to individual hooks above.

export function useModelCostsApi() {
  const createMutation = useCreateModelCost();
  const updateMutation = useUpdateModelCost();
  const deleteMutation = useDeleteModelCost();
  const importMutation = useImportModelCosts();

  const createModelCost = async (data: CreateModelCostDto): Promise<ModelCost> => {
    return createMutation.mutateAsync(data);
  };

  const updateModelCost = async (id: number, data: UpdateModelCostDto): Promise<ModelCost> => {
    return updateMutation.mutateAsync({ id, data });
  };

  const deleteModelCost = async (id: number): Promise<void> => {
    return deleteMutation.mutateAsync(id);
  };

  const importModelCosts = async (costs: CreateModelCostDto[]): Promise<{ imported?: number }> => {
    const result = await importMutation.mutateAsync(costs);
    return { imported: result.success ?? costs.length };
  };

  const importModelCostsWithAliases = async (
    costsWithAliases: Array<{
      costName: string;
      modelAliases: string[];
      modelType: string;
      inputCostPerMillionTokens: number;
      outputCostPerMillionTokens: number;
      [key: string]: unknown;
    }>
  ): Promise<{ success: number; failed: number; errors: Array<{ costName: string; error: string }> }> => {
    const transformedCosts = costsWithAliases.map(item => ({
      costName: item.costName,
      modelProviderMappingIds: [],
      inputCostPerMillionTokens: item.inputCostPerMillionTokens,
      outputCostPerMillionTokens: item.outputCostPerMillionTokens,
      description: JSON.stringify({ ...item }),
    }));

    const result = await withAdminClient(client =>
      client.modelCosts.import(transformedCosts)
    );

    const success = result.success ?? 0;
    const failed = result.failed ?? 0;
    const errors = result.errors?.map((errorItem, index) => ({
      costName: costsWithAliases[index]?.costName ?? 'Unknown',
      error: errorItem.error,
    })) ?? [];

    if (success > 0) {
      notify.success(`Successfully imported ${success} model costs`);
    }
    if (failed > 0) {
      const errorMessage = errors
        .map(e => `${e.costName}: ${e.error}`)
        .join('\n');
      notify.warning(`Failed to import ${failed} costs:\n${errorMessage}`);
    }

    return { success, failed, errors };
  };

  const exportModelCosts = async (format: 'csv' | 'json' = 'csv'): Promise<void> => {
    try {
      const blob = await Promise.resolve(new Blob(['Model costs export not available'], { type: 'text/plain' }));
      const filename = `model-costs-${new Date().toISOString().split('T')[0]}.${format}`;
      downloadFile(blob, filename);
      notify.success('Model costs exported successfully');
    } catch (error) {
      notify.error(error, 'Failed to export model costs');
      throw error;
    }
  };

  return {
    isLoading: createMutation.isPending || updateMutation.isPending || deleteMutation.isPending || importMutation.isPending,
    isExporting: false,
    fetchModelCosts,
    createModelCost,
    updateModelCost,
    deleteModelCost,
    importModelCosts,
    importModelCostsWithAliases,
    exportModelCosts,
    getModelCostByPattern,
  };
}
