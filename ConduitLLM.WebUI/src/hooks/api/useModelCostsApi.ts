'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';
import { BackendErrorHandler } from '@/lib/errors/BackendErrorHandler';

// Query key factory for Model Costs API
export const modelCostsApiKeys = {
  all: ['model-costs-api'] as const,
  list: () => [...modelCostsApiKeys.all, 'list'] as const,
  byId: (id: number) => [...modelCostsApiKeys.all, 'byId', id] as const,
  byProvider: (provider: string) => [...modelCostsApiKeys.all, 'byProvider', provider] as const,
  overview: (startDate: string, endDate: string) => [...modelCostsApiKeys.all, 'overview', startDate, endDate] as const,
} as const;

export interface ModelCost {
  id: number;
  modelIdPattern: string;
  providerName: string;
  inputCostPerMillionTokens: number;
  outputCostPerMillionTokens: number;
  isActive: boolean;
  priority: number;
  effectiveDate: string;
  description?: string;
  modelCategory?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateModelCost {
  modelIdPattern: string;
  providerName: string;
  inputCostPerMillionTokens: number;
  outputCostPerMillionTokens: number;
  isActive?: boolean;
  priority?: number;
  effectiveDate?: string;
  description?: string;
  modelCategory?: string;
}

export interface UpdateModelCost extends CreateModelCost {
  id: number;
}

export interface ModelCostOverview {
  modelName: string;
  providerName: string;
  totalRequests: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalCost: number;
  averageCostPerRequest: number;
  costTrend: 'up' | 'down' | 'stable';
  trendPercentage: number;
}

/**
 * Hook to fetch all model costs
 */
export function useModelCosts() {
  return useQuery({
    queryKey: modelCostsApiKeys.list(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const result = await client.modelCosts.list();
        
        // Transform SDK response to match expected format
        return result.items.map(cost => ({
          id: cost.id,
          modelIdPattern: cost.modelIdPattern,
          providerName: cost.providerName,
          inputCostPerMillionTokens: cost.inputCostPerMillionTokens || 0,
          outputCostPerMillionTokens: cost.outputCostPerMillionTokens || 0,
          isActive: cost.isActive,
          priority: cost.priority || 0,
          effectiveDate: cost.effectiveDate || new Date().toISOString(),
          description: cost.metadata?.description,
          modelCategory: cost.metadata?.category || 'text',
          createdAt: cost.createdAt,
          updatedAt: cost.updatedAt,
        }));
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch model costs');
        throw backendError;
      }
    },
    staleTime: 300000, // 5 minutes
  });
}

/**
 * Hook to fetch a specific model cost by ID
 */
export function useModelCost(id: number) {
  return useQuery({
    queryKey: modelCostsApiKeys.byId(id),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const cost = await client.modelCosts.getById(id);
        
        // Transform SDK response to match expected format
        return {
          id: cost.id,
          modelIdPattern: cost.modelIdPattern,
          providerName: cost.providerName,
          inputCostPerMillionTokens: cost.inputCostPerMillionTokens || 0,
          outputCostPerMillionTokens: cost.outputCostPerMillionTokens || 0,
          isActive: cost.isActive,
          priority: cost.priority || 0,
          effectiveDate: cost.effectiveDate || new Date().toISOString(),
          description: cost.metadata?.description,
          modelCategory: cost.metadata?.category || 'text',
          createdAt: cost.createdAt,
          updatedAt: cost.updatedAt,
        };
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch model cost');
        throw backendError;
      }
    },
    enabled: !!id,
    staleTime: 300000, // 5 minutes
  });
}

/**
 * Hook to fetch model costs by provider
 */
export function useModelCostsByProvider(providerName: string) {
  return useQuery({
    queryKey: modelCostsApiKeys.byProvider(providerName),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const costs = await client.modelCosts.getByProvider(providerName);
        
        // Transform SDK response to match expected format
        return costs.map(cost => ({
          id: cost.id,
          modelIdPattern: cost.modelIdPattern,
          providerName: cost.providerName,
          inputCostPerMillionTokens: cost.inputCostPerMillionTokens || 0,
          outputCostPerMillionTokens: cost.outputCostPerMillionTokens || 0,
          isActive: cost.isActive,
          priority: cost.priority || 0,
          effectiveDate: cost.effectiveDate || new Date().toISOString(),
          description: cost.metadata?.description,
          modelCategory: cost.metadata?.category || 'text',
          createdAt: cost.createdAt,
          updatedAt: cost.updatedAt,
        }));
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch model costs by provider');
        throw backendError;
      }
    },
    enabled: !!providerName,
    staleTime: 300000, // 5 minutes
  });
}

/**
 * Hook to fetch model cost overview
 */
export function useModelCostOverview(startDate: string, endDate: string) {
  return useQuery({
    queryKey: modelCostsApiKeys.overview(startDate, endDate),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const overviews = await client.modelCosts.getOverview({
          startDate,
          endDate,
          groupBy: 'model',
        });
        
        // Transform SDK response to match expected format
        return overviews.map(overview => ({
          modelName: overview.modelName,
          providerName: overview.providerName,
          totalRequests: overview.totalRequests,
          totalInputTokens: overview.totalTokens / 2, // Estimate input tokens as half
          totalOutputTokens: overview.totalTokens / 2, // Estimate output tokens as half
          totalCost: overview.totalCost,
          averageCostPerRequest: overview.averageCostPerRequest,
          costTrend: (overview.costTrend === 'increasing' ? 'up' : overview.costTrend === 'decreasing' ? 'down' : 'stable') as 'up' | 'down' | 'stable',
          trendPercentage: overview.trendPercentage || 0,
        }));
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to fetch model cost overview');
        throw backendError;
      }
    },
    enabled: !!startDate && !!endDate,
    staleTime: 60000, // 1 minute
  });
}

/**
 * Hook to create a new model cost
 */
export function useCreateModelCost() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: CreateModelCost) => {
      try {
        const client = getAdminClient();
        const result = await client.modelCosts.create({
          modelId: data.modelIdPattern,
          inputTokenCost: data.inputCostPerMillionTokens / 1000,
          outputTokenCost: data.outputCostPerMillionTokens / 1000,
          isActive: data.isActive,
          effectiveDate: data.effectiveDate,
          providerId: data.providerName,
          description: data.description,
        });
        
        return {
          id: result.id,
          modelIdPattern: result.modelIdPattern,
          providerName: result.providerName,
          inputCostPerMillionTokens: result.inputCostPerMillionTokens || 0,
          outputCostPerMillionTokens: result.outputCostPerMillionTokens || 0,
          isActive: result.isActive,
          priority: result.priority || 0,
          effectiveDate: result.effectiveDate || new Date().toISOString(),
          description: result.metadata?.description,
          modelCategory: result.metadata?.category || 'text',
          createdAt: result.createdAt,
          updatedAt: result.updatedAt,
        };
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to create model cost');
        throw backendError;
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.all });
    },
  });
}

/**
 * Hook to update a model cost
 */
export function useUpdateModelCost() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: UpdateModelCost) => {
      try {
        const client = getAdminClient();
        const result = await client.modelCosts.update(data.id, {
          inputTokenCost: data.inputCostPerMillionTokens / 1000,
          outputTokenCost: data.outputCostPerMillionTokens / 1000,
          isActive: data.isActive,
          effectiveDate: data.effectiveDate,
          description: data.description,
        });
        
        return result;
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to update model cost');
        throw backendError;
      }
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.all });
      queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.byId(variables.id) });
    },
  });
}

/**
 * Hook to delete a model cost
 */
export function useDeleteModelCost() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number) => {
      try {
        const client = getAdminClient();
        await client.modelCosts.deleteById(id);
        return { success: true };
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to delete model cost');
        throw backendError;
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.all });
    },
  });
}

/**
 * Hook to import model costs
 */
export function useImportModelCosts() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (modelCosts: CreateModelCost[]) => {
      try {
        const client = getAdminClient();
        const sdkModelCosts = modelCosts.map(cost => ({
          modelId: cost.modelIdPattern,
          inputTokenCost: cost.inputCostPerMillionTokens / 1000,
          outputTokenCost: cost.outputCostPerMillionTokens / 1000,
          isActive: cost.isActive,
          effectiveDate: cost.effectiveDate,
          description: cost.description,
          providerId: cost.providerName,
        }));
        
        const result = await client.modelCosts.import(sdkModelCosts);
        return result.success; // Returns count of imported items
      } catch (error) {
        const backendError = BackendErrorHandler.classifyError(error);
        reportError(new Error(BackendErrorHandler.getUserFriendlyMessage(backendError)), 'Failed to import model costs');
        throw backendError;
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.all });
    },
  });
}

/**
 * Hook to invalidate model costs queries
 */
export function useInvalidateModelCosts() {
  const queryClient = useQueryClient();
  
  return {
    invalidateAll: () => queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.all }),
    invalidateList: () => queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.list() }),
    invalidateById: (id: number) => queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.byId(id) }),
    invalidateByProvider: (provider: string) => queryClient.invalidateQueries({ queryKey: modelCostsApiKeys.byProvider(provider) }),
    invalidateOverview: () => queryClient.invalidateQueries({ 
      predicate: (query) => 
        query.queryKey[0] === 'model-costs-api' && 
        query.queryKey[1] === 'overview'
    }),
  };
}