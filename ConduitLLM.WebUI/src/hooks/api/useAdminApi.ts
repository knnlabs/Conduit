'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notifications } from '@mantine/notifications';
import { BackendErrorHandler, type BackendError } from '@/lib/errors/BackendErrorHandler';
import { getAdminClient } from '@/lib/clients/conduit';
import { apiFetch } from '@/lib/utils/fetch-wrapper';
import type { 
  CreateVirtualKeyRequest, 
  UpdateVirtualKeyRequest,
  CreateProviderCredentialDto,
  UpdateProviderCredentialDto,
  CreateModelProviderMappingDto,
  UpdateModelProviderMappingDto
} from '@knn_labs/conduit-admin-client';

// Query key factory
export const adminApiKeys = {
  all: ['admin-api'] as const,
  virtualKeys: () => [...adminApiKeys.all, 'virtual-keys'] as const,
  virtualKey: (id: string) => [...adminApiKeys.virtualKeys(), id] as const,
  providers: () => [...adminApiKeys.all, 'providers'] as const,
  provider: (id: string) => [...adminApiKeys.providers(), id] as const,
  modelMappings: () => [...adminApiKeys.all, 'model-mappings'] as const,
  systemInfo: () => [...adminApiKeys.all, 'system-info'] as const,
  systemSettings: () => [...adminApiKeys.all, 'system-settings'] as const,
  systemHealth: () => [...adminApiKeys.all, 'system-health'] as const,
  securityMetrics: () => [...adminApiKeys.all, 'security-metrics'] as const,
  securityEvents: () => [...adminApiKeys.all, 'security-events'] as const,
  threatDetections: () => [...adminApiKeys.all, 'threat-detections'] as const,
  settings: () => [...adminApiKeys.all, 'settings'] as const,
  audioUsage: () => [...adminApiKeys.all, 'audio-usage'] as const,
} as const;

// Virtual Keys API
export function useVirtualKeys() {
  return useQuery({
    queryKey: adminApiKeys.virtualKeys(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const keys = await client.virtualKeys.list({
          // Get both enabled and disabled keys by not filtering
        });
        // Transform the data to ensure IDs are strings
        return keys.map(key => ({
          ...key,
          id: key.id.toString(), // Convert number to string
        }));
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 30 * 1000, // 30 seconds
    retry: (failureCount, error: unknown) => {
      return BackendErrorHandler.shouldRetry(error as BackendError, failureCount);
    },
    retryDelay: (attemptIndex, error: unknown) => {
      return BackendErrorHandler.getRetryDelay(error as BackendError, attemptIndex);
    },
  });
}

export function useVirtualKey(id: string) {
  return useQuery({
    queryKey: adminApiKeys.virtualKey(id),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const key = await client.virtualKeys.getById(parseInt(id));
        return {
          ...key,
          id: key.id.toString(), // Convert number to string
        };
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    enabled: !!id,
  });
}

export function useCreateVirtualKey() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (data: CreateVirtualKeyRequest) => {
      try {
        const client = getAdminClient();
        return await client.virtualKeys.create(data);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKeys() });
      notifications.show({
        title: 'Success',
        message: 'Virtual key created successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const classifiedError = BackendErrorHandler.classifyError(error);
      notifications.show({
        title: 'Error',
        message: BackendErrorHandler.getUserFriendlyMessage(classifiedError),
        color: 'red',
      });
    },
  });
}

export function useUpdateVirtualKey() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async ({ id, data }: { id: string; data: UpdateVirtualKeyRequest }) => {
      try {
        const client = getAdminClient();
        return await client.virtualKeys.update(parseInt(id), data);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKeys() });
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKey(variables.id) });
      notifications.show({
        title: 'Success',
        message: 'Virtual key updated successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to update virtual key';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function useDeleteVirtualKey() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (id: string) => {
      try {
        const client = getAdminClient();
        await client.virtualKeys.deleteById(parseInt(id));
        return { success: true };
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKeys() });
      notifications.show({
        title: 'Success',
        message: 'Virtual key deleted successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to delete virtual key';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

// Providers API
export function useProviders() {
  return useQuery({
    queryKey: adminApiKeys.providers(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const providers = await client.providers.list();
        
        // Transform the data to match the expected Provider interface
        // Since the SDK doesn't provide health status and models count,
        // we'll set default values for now
        return providers.map(provider => ({
          ...provider,
          id: provider.id.toString(), // Convert number to string
          providerType: provider.providerName, // Use providerName as type for now
          healthStatus: 'unknown' as const, // Default to unknown
          modelsAvailable: 0, // Default to 0
          lastHealthCheck: undefined,
        }));
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 30 * 1000,
  });
}

export function useCreateProvider() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (data: CreateProviderCredentialDto) => {
      try {
        const client = getAdminClient();
        return await client.providers.create(data);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.providers() });
      notifications.show({
        title: 'Success',
        message: 'Provider created successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to create provider';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function useUpdateProvider() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async ({ id, data }: { id: string; data: UpdateProviderCredentialDto }) => {
      try {
        const client = getAdminClient();
        return await client.providers.update(parseInt(id), data);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.providers() });
      notifications.show({
        title: 'Success',
        message: 'Provider updated successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to update provider';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function useDeleteProvider() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (providerId: string) => {
      try {
        const client = getAdminClient();
        await client.providers.deleteById(parseInt(providerId));
        return { success: true };
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.providers() });
      notifications.show({
        title: 'Success',
        message: 'Provider deleted successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to delete provider';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

// Model Mappings API
export function useModelMappings() {
  return useQuery({
    queryKey: adminApiKeys.modelMappings(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const mappings = await client.modelMappings.list({
          // Get all mappings
        });
        // Transform the data to match expected interface
        return mappings.map(mapping => ({
          ...mapping,
          id: mapping.id.toString(), // Convert number to string
          internalModelName: mapping.modelId,
          providerModelName: mapping.providerModelId,
          providerName: mapping.providerId,
          capabilities: [], // Default empty for now
          priority: mapping.priority || 100,
          createdAt: new Date().toISOString(), // Default for now
          requestCount: 0, // Default for now
        }));
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 30 * 1000,
  });
}

export function useCreateModelMapping() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (data: CreateModelProviderMappingDto) => {
      try {
        const client = getAdminClient();
        return await client.modelMappings.create(data);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      notifications.show({
        title: 'Success',
        message: 'Model mapping created successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to create model mapping';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

export function useUpdateModelMapping() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async ({ id, data }: { id: string; data: UpdateModelProviderMappingDto }) => {
      try {
        const client = getAdminClient();
        return await client.modelMappings.update(parseInt(id), data);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      notifications.show({
        title: 'Success',
        message: 'Model mapping updated successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to update model mapping';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

// Test model mapping - Not available in SDK yet
export function useTestModelMapping() {
  return useMutation({
    mutationFn: async (_mappingId: string) => {
      // SDK doesn't have test method yet
      throw new Error('Model mapping test is not yet implemented');
    },
    onSuccess: () => {
      notifications.show({
        title: 'Model Test Successful',
        message: 'Model is responding correctly',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Model Test Failed',
        message: error.message || 'Failed to test model',
        color: 'red',
      });
    },
  });
}

// Delete model mapping
export function useDeleteModelMapping() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (mappingId: string) => {
      try {
        const client = getAdminClient();
        await client.modelMappings.deleteById(parseInt(mappingId));
        return { success: true };
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      notifications.show({
        title: 'Success',
        message: 'Model mapping deleted successfully',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Error',
        message: error.message || 'Failed to delete model mapping',
        color: 'red',
      });
    },
  });
}

// Bulk discover model mappings - Not available in SDK yet
export function useBulkDiscoverModelMappings() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async () => {
      // SDK doesn't have discoverModels method yet
      throw new Error('Model discovery is not yet implemented');
    },
    onSuccess: (data: { modelsDiscovered?: number }) => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      const modelsFound = data.modelsDiscovered || 0;
      notifications.show({
        title: 'Discovery Complete',
        message: `Model discovery completed successfully. Found ${modelsFound} new models.`,
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Discovery Failed',
        message: error.message || 'Failed to discover models',
        color: 'red',
      });
    },
  });
}

// System Information API
export function useSystemInfo() {
  return useQuery({
    queryKey: adminApiKeys.systemInfo(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        return await client.system.getSystemInfo();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

// System Settings API
export function useSystemSettings() {
  return useQuery({
    queryKey: adminApiKeys.systemSettings(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        const settings = await client.settings.getGlobalSettings();
        // Transform array of settings into object
        const settingsObject: Record<string, unknown> = {};
        settings.forEach(setting => {
          settingsObject[setting.key] = setting.value;
        });
        return settingsObject;
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 60 * 1000, // 1 minute
  });
}

export function useUpdateSystemSettings() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (settings: Record<string, string>) => {
      try {
        const client = getAdminClient();
        // Update settings one by one as SDK doesn't have bulk update
        const promises = Object.entries(settings).map(([key, value]) => 
          client.settings.updateGlobalSetting(key, { value })
        );
        const results = await Promise.all(promises);
        return results;
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: adminApiKeys.systemSettings() });
      notifications.show({
        title: 'Settings Saved',
        message: 'System settings have been updated successfully',
        color: 'green',
      });
    },
    onError: (error: unknown) => {
      const errorMessage = error instanceof Error ? error.message : 'Failed to save settings';
      notifications.show({
        title: 'Save Failed',
        message: errorMessage,
        color: 'red',
      });
    },
  });
}

// System Health API
export function useSystemHealth() {
  return useQuery({
    queryKey: adminApiKeys.systemHealth(),
    queryFn: async () => {
      try {
        const client = getAdminClient();
        return await client.system.getHealth();
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    staleTime: 60 * 1000, // 1 minute
    refetchInterval: 60 * 1000, // Auto-refresh every minute
  });
}

// Security APIs - Temporary simulated data until real security endpoints exist
export function useSecurityMetrics() {
  return useQuery({
    queryKey: adminApiKeys.securityMetrics(),
    queryFn: async () => {
      // Simulate security metrics until real endpoint exists
      return {
        threatLevel: ['low', 'medium', 'high', 'critical'][Math.floor(Math.random() * 4)] as 'low' | 'medium' | 'high' | 'critical',
        blockedRequests: Math.floor(Math.random() * 500) + 100,
        suspiciousActivity: Math.floor(Math.random() * 20) + 5,
        rateLimitHits: Math.floor(Math.random() * 150) + 50,
        failedAuthentications: Math.floor(Math.random() * 50) + 10,
        activeThreats: Math.floor(Math.random() * 5),
        vulnerabilityScore: Math.floor(Math.random() * 20) + 75, // 75-95
        uptime: 99.5 + Math.random() * 0.5, // 99.5-100%
      };
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchInterval: 30 * 1000, // Auto-refresh every 30 seconds
  });
}

export function useSecurityEvents(filters?: {
  startDate?: string;
  endDate?: string;
  severity?: string;
  type?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: [...adminApiKeys.securityEvents(), filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      if (filters?.severity) params.append('severity', filters.severity);
      if (filters?.type) params.append('type', filters.type);
      params.append('page', (filters?.page || 1).toString());
      params.append('pageSize', (filters?.pageSize || 20).toString());

      const response = await apiFetch(`/api/admin/security/events?${params}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to fetch security events');
      }

      return response.json();
    },
    staleTime: 60 * 1000, // 1 minute
    refetchInterval: 60 * 1000, // Auto-refresh every minute
  });
}

export function useThreatDetections(filters?: {
  status?: string;
  severity?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: [...adminApiKeys.threatDetections(), filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters?.status) params.append('status', filters.status);
      if (filters?.severity) params.append('severity', filters.severity);
      params.append('page', (filters?.page || 1).toString());
      params.append('pageSize', (filters?.pageSize || 20).toString());

      const response = await apiFetch(`/api/admin/security/threats?${params}`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to fetch threat detections');
      }

      return response.json();
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

// Test provider connection
export function useTestProvider() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: async (providerId: string) => {
      try {
        const client = getAdminClient();
        return await client.providers.testConnectionById(parseInt(providerId));
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      notifications.show({
        title: 'Connection Test Successful',
        message: 'Provider is responding correctly',
        color: 'green',
      });
      
      // Refresh provider data to update health status
      queryClient.invalidateQueries({ queryKey: adminApiKeys.providers() });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Connection Test Failed',
        message: error.message || 'Failed to connect to provider',
        color: 'red',
      });
    },
  });
}

// Test provider connection before creation
export function useTestProviderConnection() {
  return useMutation({
    mutationFn: async (providerConfig: CreateProviderCredentialDto) => {
      try {
        const client = getAdminClient();
        return await client.providers.testConnection(providerConfig);
      } catch (error: unknown) {
        throw BackendErrorHandler.classifyError(error);
      }
    },
    onSuccess: () => {
      notifications.show({
        title: 'Connection Test Successful',
        message: 'Provider configuration is valid',
        color: 'green',
      });
    },
    onError: (error: Error) => {
      notifications.show({
        title: 'Connection Test Failed',
        message: error.message || 'Please check your configuration',
        color: 'red',
      });
    },
  });
}