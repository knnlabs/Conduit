'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getAdminClient } from '@/lib/clients/conduit';
import { reportError } from '@/lib/utils/logging';

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
        const client = await getAdminClient();
        const response = await fetch('/api/modelcosts', {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch model costs: ${response.statusText}`);
        }

        return response.json() as Promise<ModelCost[]>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch model costs');
        throw error;
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
        const client = await getAdminClient();
        const response = await fetch(`/api/modelcosts/${id}`, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch model cost: ${response.statusText}`);
        }

        return response.json() as Promise<ModelCost>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch model cost');
        throw error;
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
        const client = await getAdminClient();
        const response = await fetch(`/api/modelcosts/provider/${providerName}`, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch model costs: ${response.statusText}`);
        }

        return response.json() as Promise<ModelCost[]>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch model costs by provider');
        throw error;
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
        const client = await getAdminClient();
        const response = await fetch(`/api/modelcosts/overview?startDate=${startDate}&endDate=${endDate}`, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to fetch model cost overview: ${response.statusText}`);
        }

        return response.json() as Promise<ModelCostOverview[]>;
      } catch (error) {
        reportError(error as Error, 'Failed to fetch model cost overview');
        throw error;
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
        const client = await getAdminClient();
        const response = await fetch('/api/modelcosts', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(data),
        });

        if (!response.ok) {
          throw new Error(`Failed to create model cost: ${response.statusText}`);
        }

        return response.json() as Promise<ModelCost>;
      } catch (error) {
        reportError(error as Error, 'Failed to create model cost');
        throw error;
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
        const client = await getAdminClient();
        const response = await fetch(`/api/modelcosts/${data.id}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(data),
        });

        if (!response.ok) {
          throw new Error(`Failed to update model cost: ${response.statusText}`);
        }

        return response.json();
      } catch (error) {
        reportError(error as Error, 'Failed to update model cost');
        throw error;
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
        const client = await getAdminClient();
        const response = await fetch(`/api/modelcosts/${id}`, {
          method: 'DELETE',
          headers: { 'Content-Type': 'application/json' },
        });

        if (!response.ok) {
          throw new Error(`Failed to delete model cost: ${response.statusText}`);
        }

        return response.json();
      } catch (error) {
        reportError(error as Error, 'Failed to delete model cost');
        throw error;
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
        const client = await getAdminClient();
        const response = await fetch('/api/modelcosts/import', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(modelCosts),
        });

        if (!response.ok) {
          throw new Error(`Failed to import model costs: ${response.statusText}`);
        }

        return response.json() as Promise<number>; // Returns count of imported items
      } catch (error) {
        reportError(error as Error, 'Failed to import model costs');
        throw error;
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