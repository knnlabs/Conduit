'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { useQuery } from '@tanstack/react-query';

import type { 
  ProviderCredentialDto, 
  CreateProviderCredentialDto, 
  UpdateProviderCredentialDto,
  ProviderHealthStatusDto,
  ProviderKeyCredentialDto,
  CreateProviderKeyCredentialDto
} from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
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
      const result = await withAdminClient(client => 
        client.providers.list()
      );
      
      // Check if result is a paginated response or direct array
      if (Array.isArray(result)) {
        return result as ProviderCredentialDto[];
      } else if (result && typeof result === 'object' && 'items' in result) {
        interface PaginatedResponse {
          items: ProviderCredentialDto[];
        }
        return (result as PaginatedResponse).items;
      }
      return [] as ProviderCredentialDto[];
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch providers';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const getProvider = useCallback(async (id: number): Promise<ProviderCredentialDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.getById(id)
      );
      
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
      const result = await withAdminClient(client => 
        client.providers.create(provider)
      );

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

  const updateProvider = useCallback(async (id: number, updates: UpdateProviderCredentialDto): Promise<ProviderCredentialDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const result = await withAdminClient(client => 
        client.providers.update(id, updates)
      );

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

  const deleteProvider = useCallback(async (id: number): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      await withAdminClient(client => 
        client.providers.deleteById(id)
      );

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
      const result = await withAdminClient(client => 
        client.providers.testConfig({
          providerType: parseInt(request.type, 10),
          apiKey: request.apiKey,
          baseUrl: request.endpoint,
        })
      );

      const success = (result.result as string) === 'success';
      const message = result.message ?? (success ? 'Test successful' : 'Test failed');
      
      notifications.show({
        title: success ? 'Success' : 'Failed',
        message,
        color: success ? 'green' : 'red',
      });

      return { success, message };
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

  const getProviderHealth = useCallback(async (id: number): Promise<ProviderHealthStatusDto> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // First get the provider details to get its ProviderType
      const provider = await withAdminClient(client => 
        client.providers.getById(id)
      );
      
      if (!provider?.providerType) {
        throw new Error('Provider not found or missing provider type');
      }

      // Use getHealth method which is available in FetchProvidersService
      const healthResponse = await withAdminClient(client => 
        client.providers.getHealth(provider.providerType)
      );

      // Transform the response to match ProviderHealthStatusDto
      // The getHealth method returns ProviderHealthStatusResponse with providers array
      const healthData = healthResponse.providers?.[0];
      if (!healthData) {
        throw new Error('No health data found for provider');
      }

      // Map to ProviderHealthStatusDto format
      const result: ProviderHealthStatusDto = {
        providerType: provider.providerType,
        isHealthy: healthData.status === 'healthy',
        lastCheckTime: new Date().toISOString(), // getHealth doesn't provide this
        consecutiveFailures: healthData.status === 'healthy' ? 0 : 1,
        consecutiveSuccesses: healthData.status === 'healthy' ? 1 : 0,
        averageResponseTimeMs: healthData.responseTime,
        uptime: healthData.uptime,
      };

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch provider health';
      setError(message);
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
      const message = err instanceof Error ? err.message : 'Failed to fetch provider models';
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
      const message = err instanceof Error ? err.message : 'Failed to fetch provider keys';
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

      notifications.show({
        title: 'Success',
        message: 'API key created successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create provider key';
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

  const setPrimaryKey = useCallback(async (providerId: number, keyId: number): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      await withAdminClient(client => 
        client.providers.setPrimaryKey(providerId, keyId)
      );

      notifications.show({
        title: 'Success',
        message: 'Primary key updated successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to set primary key';
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

  const deleteProviderKey = useCallback(async (providerId: number, keyId: number): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      await withAdminClient(client => 
        client.providers.deleteKey(providerId, keyId)
      );

      notifications.show({
        title: 'Success',
        message: 'API key deleted successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete provider key';
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

  return {
    getProviders,
    getProvider,
    createProvider,
    updateProvider,
    deleteProvider,
    testProvider,
    getProviderHealth,
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
      return result.items as ProviderCredentialDto[];
    },
  });

  return {
    providers: providers ?? [],
    isLoading,
    error,
    refetch,
  };
}