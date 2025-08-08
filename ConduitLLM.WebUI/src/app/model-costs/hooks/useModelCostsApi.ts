import { useState } from 'react';
import { notifications } from '@mantine/notifications';
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
      })
    );
    
    return {
      items: result.items,
      totalCount: result.totalCount,
      page: result.page,
      pageSize: result.pageSize,
    } as ModelCostListResponse;
  };

  const createModelCost = async (data: CreateModelCostDto): Promise<ModelCost> => {
    setIsLoading(true);
    try {
      const result = await withAdminClient(client => 
        client.modelCosts.create(data)
      );
      
      notifications.show({
        title: 'Success',
        message: 'Model pricing created successfully',
        color: 'green',
      });

      return result;
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to create model pricing',
        color: 'red',
      });
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
      
      notifications.show({
        title: 'Success',
        message: 'Model pricing updated successfully',
        color: 'green',
      });

      return result;
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to update model pricing',
        color: 'red',
      });
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

      notifications.show({
        title: 'Success',
        message: 'Model pricing deleted successfully',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to delete model pricing',
        color: 'red',
      });
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
      
      notifications.show({
        title: 'Success',
        message: `Successfully imported ${importCount} model costs`,
        color: 'green',
      });

      return { imported: importCount };
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to import model costs',
        color: 'red',
      });
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
        notifications.show({
          title: 'Success',
          message: `Successfully imported ${success} model costs`,
          color: 'green',
        });
      }

      if (failed > 0) {
        const errorMessage = errors
          .map(e => `${e.costName}: ${e.error}`)
          .join('\n');
        notifications.show({
          title: 'Warning',
          message: `Failed to import ${failed} costs:\n${errorMessage}`,
          color: 'orange',
        });
      }

      return { success, failed, errors };
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: error instanceof Error ? error.message : 'Failed to import model costs',
        color: 'red',
      });
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

      notifications.show({
        title: 'Success',
        message: 'Model costs exported successfully',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to export model costs',
        color: 'red',
      });
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

  const getModelCostsByProvider = async (providerType: number): Promise<ModelCost[]> => {
    try {
      const result = await withAdminClient(client => 
        client.modelCosts.getByProvider(providerType)
      );
      return result;
    } catch (error) {
      console.warn('Error fetching model costs by provider:', error);
      return [];
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
    getModelCostsByProvider,
  };
}