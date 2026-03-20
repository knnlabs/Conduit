'use client';

import { useState, useCallback } from 'react';
import { notify } from '@/lib/notifications';
import { useQuery } from '@tanstack/react-query';

import type {
  ProviderDto,
  CreateProviderDto,
  UpdateProviderDto,
  ProviderKeyCredentialDto,
  CreateProviderKeyCredentialDto
} from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { getErrorMessage } from '@/lib/utils/error-utils';

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

  const getProviders = useCallback(async (): Promise<ProviderDto[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.list()
      );
      
      // Check if result is a paginated response or direct array
      if (Array.isArray(result)) {
        return result as ProviderDto[];
      } else if (result && typeof result === 'object' && 'items' in result) {
        interface PaginatedResponse {
          items: ProviderDto[];
        }
        return (result as PaginatedResponse).items;
      }
      return [] as ProviderDto[];
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getProvider = useCallback(async (id: number): Promise<ProviderDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.getById(id)
      );
      
      return result;
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createProvider = useCallback(async (provider: CreateProviderDto): Promise<ProviderDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.create(provider)
      );

      notify.success('Provider created successfully');

      return result;
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateProvider = useCallback(async (id: number, updates: UpdateProviderDto): Promise<ProviderDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.update(id, updates)
      );

      notify.success('Provider updated successfully');

      return result;
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const deleteProvider = useCallback(async (id: number): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      await withAdminClient(client => 
        client.providers.deleteById(id)
      );

      notify.success('Provider deleted successfully');
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const testProvider = useCallback(async (request: TestProviderRequest): Promise<{ success: boolean; message: string }> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.testConfig({
          providerType: parseInt(request.type, 10),
          apiKey: request.apiKey,
          baseUrl: request.endpoint,
        })
      );

      const success = (result.result as string) === 'success';
      const message = result.message ?? (success ? 'Test successful' : 'Test failed');
      
      if (success) {
        notify.success(message);
      } else {
        notify.error(message);
      }

      return { success, message };
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getProviderModels = useCallback(async (): Promise<ProviderModel[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: providerModels service is deprecated and getByProviderId doesn't exist
      // Using placeholder implementation
      // TODO: Implement provider models retrieval once SDK supports it
      return Promise.resolve([]);
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  // API Key management functions
  const getProviderKeys = useCallback(async (): Promise<ProviderKeyCredentialDto[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // Note: getKeys method doesn't exist in providers service
      // Using placeholder implementation
      // TODO: Implement provider keys retrieval once SDK supports it
      return Promise.resolve([]);
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createProviderKey = useCallback(async (providerId: number, keyData: { keyName: string; apiKey: string; organization?: string }): Promise<ProviderKeyCredentialDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.createKey(providerId, keyData as CreateProviderKeyCredentialDto)
      );

      notify.success('API key created successfully');

      return result;
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const setPrimaryKey = useCallback(async (providerId: number, keyId: number): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      await withAdminClient(client => 
        client.providers.setPrimaryKey(providerId, keyId)
      );

      notify.success('Primary key updated successfully');
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      notify.error(err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const deleteProviderKey = useCallback(async (providerId: number, keyId: number): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      await withAdminClient(client => 
        client.providers.deleteKey(providerId, keyId)
      );

      notify.success('API key deleted successfully');
    } catch (err) {
      const message = getErrorMessage(err);
      setError(message);
      notify.error(err);
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
    getProviderModels,
    // API Key management
    getProviderKeys,
    createProviderKey,
    setPrimaryKey,
    deleteProviderKey,
    isLoading,
    error,
  };
}

// React Query hook for fetching providers
export function useProviders() {
  const { data: providers, isLoading, error, refetch } = useQuery({
    queryKey: ['providers'],
    queryFn: async () => {
      const result = await withAdminClient(client => 
        client.providers.list(1, 1000)
      );
      return result.items;
    },
  });

  return {
    providers: providers ?? [],
    isLoading,
    error,
    refetch,
  };
}