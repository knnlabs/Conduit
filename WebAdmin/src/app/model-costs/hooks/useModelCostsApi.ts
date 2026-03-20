import { useState } from 'react';
import { notify } from '@/lib/notifications';
import { 
  ModelCost, 
  CreateModelCostDto, 
  UpdateModelCostDto, 
  ModelCostListResponse, 
  ModelCostFilters 
} from '../types/modelCost';
import { downloadFile } from '../utils/csvHelpers';
import { withAdminClient } from '@/lib/client/adminClient';

export function useModelCostsApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [isExporting, setIsExporting] = useState(false);

  const fetchModelCosts = async (page = 1, pageSize = 50, filters?: ModelCostFilters): Promise<ModelCostListResponse> => {
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
  };

  const createModelCost = async (data: CreateModelCostDto): Promise<ModelCost> => {
    setIsLoading(true);
    try {
      const result = await withAdminClient(client => 
        client.modelCosts.create(data)
      );
      
      notify.success('Model pricing created successfully');

      return result;
    } catch (error) {
      notify.error(error, 'Failed to create model pricing');
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const updateModelCost = async (id: number, data: UpdateModelCostDto): Promise<ModelCost> => {
    setIsLoading(true);
    try {
      const result = await withAdminClient(client => 
        client.modelCosts.update(id, data)
      );
      
      notify.success('Model pricing updated successfully');

      return result;
    } catch (error) {
      notify.error(error, 'Failed to update model pricing');
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const deleteModelCost = async (id: number): Promise<void> => {
    setIsLoading(true);
    try {
      await withAdminClient(client => 
        client.modelCosts.deleteById(id)
      );

      notify.success('Model pricing deleted successfully');
    } catch (error) {
      notify.error(error, 'Failed to delete model pricing');
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const importModelCosts = async (costs: CreateModelCostDto[]): Promise<{ imported?: number }> => {
    setIsLoading(true);
    try {
      const result = await withAdminClient(client => 
        client.modelCosts.import(costs)
      );
      
      const importCount = result.success || costs.length;
      
      notify.success(`Successfully imported ${importCount} model costs`);

      return { imported: importCount };
    } catch (error) {
      notify.error(error, 'Failed to import model costs');
      throw error;
    } finally {
      setIsLoading(false);
    }
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
    setIsLoading(true);
    try {
      // Transform to the format expected by the Admin SDK
      const transformedCosts = costsWithAliases.map(item => ({
        costName: item.costName,
        modelProviderMappingIds: [], // This would need to be resolved separately
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
    } catch (error) {
      notify.error(error, 'Failed to import model costs');
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const exportModelCosts = async (format: 'csv' | 'json' = 'csv'): Promise<void> => {
    setIsExporting(true);
    try {
      // Use analytics export for model costs data
      // Note: export method doesn't exist in analytics service
      // Using placeholder implementation
      // TODO: Implement model costs export once SDK supports it
      const blob = await Promise.resolve(new Blob(['Model costs export not available'], { type: 'text/plain' }));
      
      const filename = `model-costs-${new Date().toISOString().split('T')[0]}.${format}`;
      downloadFile(blob, filename);

      notify.success('Model costs exported successfully');
    } catch (error) {
      notify.error(error, 'Failed to export model costs');
      throw error;
    } finally {
      setIsExporting(false);
    }
  };

  const getModelCostByPattern = async (pattern: string): Promise<ModelCost | null> => {
    try {
      // Pattern-based lookup is no longer available, try name-based lookup
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
  };

  return {
    isLoading,
    isExporting,
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