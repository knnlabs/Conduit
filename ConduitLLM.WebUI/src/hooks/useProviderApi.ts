'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { useQuery } from '@tanstack/react-query';

import type { 
  ProviderCredentialDto, 
  CreateProviderCredentialDto, 
  UpdateProviderCredentialDto,
  ProviderHealthStatusDto
} from '@knn_labs/conduit-admin-client';
import type { ErrorResponse } from '@knn_labs/conduit-common';
// Error utilities are handled inline with proper typing

interface ProviderModel {
  id: string;
  name: string;
  capabilities: string[];
  contextWindow?: number;
  maxTokens?: number;
  pricing?: {
    prompt: number;
    completion: number;
    currency: string;
  };
}

interface TestProviderRequest {
  endpoint: string;
  apiKey: string;
  type: string;
  model?: string;
}

export function useProviderApi() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getProviders = useCallback(async (): Promise<ProviderCredentialDto[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/providers', {
        method: 'GET',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch providers');
      }

      const result = await response.json() as ProviderCredentialDto[];
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch providers';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getProvider = useCallback(async (id: string): Promise<ProviderCredentialDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/providers/${id}`, {
        method: 'GET',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch provider');
      }

      const result = await response.json() as ProviderCredentialDto;
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch provider';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createProvider = useCallback(async (provider: CreateProviderCredentialDto): Promise<ProviderCredentialDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/providers', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(provider),
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to create provider');
      }

      const result = await response.json() as ProviderCredentialDto;

      notifications.show({
        title: 'Success',
        message: 'Provider created successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create provider';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateProvider = useCallback(async (id: string, updates: UpdateProviderCredentialDto): Promise<ProviderCredentialDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/providers/${id}`, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(updates),
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to update provider');
      }

      const result = await response.json() as ProviderCredentialDto;

      notifications.show({
        title: 'Success',
        message: 'Provider updated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update provider';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const deleteProvider = useCallback(async (id: string): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/providers/${id}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to delete provider');
      }

      notifications.show({
        title: 'Success',
        message: 'Provider deleted successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete provider';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const testProvider = useCallback(async (request: TestProviderRequest): Promise<{ success: boolean; message: string }> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/providers/test', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      const result = await response.json() as { success: boolean; message: string; error?: string };

      if (!response.ok) {
        throw new Error(result.error ?? 'Provider test failed');
      }

      notifications.show({
        title: result.success ? 'Success' : 'Failed',
        message: result.message,
        color: result.success ? 'green' : 'red',
      });

      return { success: result.success, message: result.message };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Provider test failed';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getProviderHealth = useCallback(async (id: string): Promise<ProviderHealthStatusDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/health/providers/${id}`, {
        method: 'GET',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch provider health');
      }

      const result = await response.json() as ProviderHealthStatusDto;

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch provider health';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getProviderModels = useCallback(async (id: string): Promise<ProviderModel[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/provider-models/${id}`, {
        method: 'GET',
      });

      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch provider models');
      }

      const result = await response.json() as ProviderModel[];

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch provider models';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    getProviders,
    getProvider,
    createProvider,
    updateProvider,
    deleteProvider,
    testProvider,
    getProviderHealth,
    getProviderModels,
    isLoading,
    error,
  };
}

// React Query hook for fetching providers
export function useProviders() {
  const { data: providers, isLoading, error, refetch } = useQuery({
    queryKey: ['providers'],
    queryFn: async () => {
      const response = await fetch('/api/providers');
      if (!response.ok) {
        const errorResult = await response.json() as ErrorResponse;
        throw new Error(errorResult.error ?? errorResult.message ?? 'Failed to fetch providers');
      }
      const data = await response.json() as ProviderCredentialDto[];
      // Use SDK types directly
      return data;
    },
  });

  return {
    providers: providers ?? [],
    isLoading,
    error,
    refetch,
  };
}