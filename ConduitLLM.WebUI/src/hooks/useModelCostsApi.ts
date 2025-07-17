import { useState } from 'react';
import { notifications } from '@mantine/notifications';

export interface ModelCost {
  id: number;
  modelIdPattern: string;
  providerName: string;
  modelType: 'chat' | 'embedding' | 'image' | 'audio' | 'video';
  inputCostPerMillionTokens?: number;
  outputCostPerMillionTokens?: number;
  costPerRequest?: number;
  costPerSecond?: number;
  costPerImage?: number;
  isActive: boolean;
  priority: number;
  effectiveDate: string;
  expiryDate?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateModelCostDto {
  modelIdPattern: string;
  inputTokenCost: number;
  outputTokenCost: number;
  embeddingTokenCost?: number;
  imageCostPerImage?: number;
  audioCostPerMinute?: number;
  audioCostPerKCharacters?: number;
  audioInputCostPerMinute?: number;
  audioOutputCostPerMinute?: number;
  videoCostPerSecond?: number;
  videoResolutionMultipliers?: string;
  description?: string;
  priority?: number;
}

export interface UpdateModelCostDto extends Partial<CreateModelCostDto> {}

export interface ModelCostListResponse {
  items: ModelCost[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export function useModelCostsApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [isExporting, setIsExporting] = useState(false);

  const fetchModelCosts = async (page = 1, pageSize = 50, filters?: {
    provider?: string;
    isActive?: boolean;
  }): Promise<ModelCostListResponse> => {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });

    if (filters?.provider) {
      params.append('provider', filters.provider);
    }
    if (filters?.isActive !== undefined) {
      params.append('isActive', filters.isActive.toString());
    }

    const response = await fetch(`/api/model-costs?${params}`);
    if (!response.ok) {
      throw new Error('Failed to fetch model costs');
    }
    return response.json();
  };

  const createModelCost = async (data: CreateModelCostDto): Promise<ModelCost> => {
    setIsLoading(true);
    try {
      const response = await fetch('/api/model-costs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to create model cost');
      }

      const result = await response.json();
      
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
      const response = await fetch(`/api/model-costs/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to update model cost');
      }

      const result = await response.json();
      
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
      const response = await fetch(`/api/model-costs/${id}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to delete model cost');
      }

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

  const importModelCosts = async (costs: CreateModelCostDto[]): Promise<any> => {
    setIsLoading(true);
    try {
      const response = await fetch('/api/model-costs/import', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(costs),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to import model costs');
      }

      const result = await response.json();
      
      notifications.show({
        title: 'Success',
        message: `Successfully imported ${result.imported || costs.length} model costs`,
        color: 'green',
      });

      return result;
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
      const response = await fetch(`/api/model-costs/export?format=${format}`);
      
      if (!response.ok) {
        throw new Error('Failed to export model costs');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `model-costs-${new Date().toISOString().split('T')[0]}.${format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

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
      const response = await fetch(`/api/model-costs/pattern/${encodeURIComponent(pattern)}`);
      if (!response.ok) {
        if (response.status === 404) {
          return null;
        }
        throw new Error('Failed to fetch model cost by pattern');
      }
      return response.json();
    } catch (error) {
      console.error('Error fetching model cost by pattern:', error);
      return null;
    }
  };

  const getModelCostsByProvider = async (provider: string): Promise<ModelCost[]> => {
    try {
      const response = await fetch(`/api/model-costs/provider/${encodeURIComponent(provider)}`);
      if (!response.ok) {
        throw new Error('Failed to fetch model costs by provider');
      }
      return response.json();
    } catch (error) {
      console.error('Error fetching model costs by provider:', error);
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
    exportModelCosts,
    getModelCostByPattern,
    getModelCostsByProvider,
  };
}