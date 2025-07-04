'use client';

import { useEffect } from 'react';
import { useSignalR } from './useSignalR';
import { useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { safeLog, safeWarn } from '@/lib/utils/logging';

interface ProviderEvent {
  providerId: string;
  providerName: string;
  providerType: string;
  eventType: 'created' | 'updated' | 'deleted' | 'healthChanged' | 'capabilitiesDiscovered';
  timestamp: string;
  isHealthy?: boolean;
  healthStatus?: 'healthy' | 'unhealthy' | 'unknown';
  modelsAvailable?: number;
  modelCapabilities?: unknown[];
  changes?: Record<string, unknown>;
}

export function useProviderHub() {
  const { connection, isConnected, error } = useSignalR({
    hubName: 'providers',
    hubPath: '/hubs/providers',
    enabled: process.env.NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES !== 'false',
  });
  
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!connection || !isConnected) return;

    // Handle provider created event
    const handleProviderCreated = (event: ProviderEvent) => {
      safeLog('Provider created event received', event);
      
      // Invalidate providers list
      queryClient.invalidateQueries({ queryKey: adminApiKeys.providers() });
      
      // Show notification
      notifications.show({
        title: 'Provider Added',
        message: `New provider "${event.providerName}" has been configured`,
        color: 'blue',
      });
    };

    // Handle provider updated event
    const handleProviderUpdated = (event: ProviderEvent) => {
      safeLog('Provider updated event received', event);
      
      // Invalidate providers queries
      queryClient.invalidateQueries({ queryKey: adminApiKeys.providers() });
      queryClient.invalidateQueries({ queryKey: adminApiKeys.provider(event.providerId) });
      
      // Show notification
      notifications.show({
        title: 'Provider Updated',
        message: `Provider "${event.providerName}" configuration has been updated`,
        color: 'blue',
      });
    };

    // Handle provider deleted event
    const handleProviderDeleted = (event: ProviderEvent) => {
      safeLog('Provider deleted event received', event);
      
      // Invalidate providers list
      queryClient.invalidateQueries({ queryKey: adminApiKeys.providers() });
      
      // Also invalidate model mappings as they might be affected
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      
      // Show notification
      notifications.show({
        title: 'Provider Deleted',
        message: `Provider "${event.providerName}" has been removed`,
        color: 'orange',
      });
    };

    // Handle provider health change event
    const handleProviderHealthChanged = (event: ProviderEvent) => {
      safeLog('Provider health changed', event);
      
      // Update provider cache with new health status
      queryClient.setQueryData(adminApiKeys.providers(), (oldData: unknown) => {
        if (!oldData || !Array.isArray(oldData)) return oldData;
        
        return oldData.map((provider: unknown) => {
          if (typeof provider === 'object' && provider !== null && 'id' in provider &&
              (provider as { id: string }).id === event.providerId) {
            return {
              ...provider,
              healthStatus: event.healthStatus,
              isHealthy: event.isHealthy,
              lastHealthCheck: event.timestamp,
            };
          }
          return provider;
        });
      });
      
      // Show notification for health issues
      if (!event.isHealthy) {
        notifications.show({
          title: 'Provider Health Alert',
          message: `Provider "${event.providerName}" is experiencing issues`,
          color: 'red',
        });
      } else if (event.healthStatus === 'healthy') {
        notifications.show({
          title: 'Provider Recovered',
          message: `Provider "${event.providerName}" is now healthy`,
          color: 'green',
          autoClose: 3000,
        });
      }
    };

    // Handle model capabilities discovered event
    const handleModelCapabilitiesDiscovered = (event: ProviderEvent) => {
      safeLog('Model capabilities discovered', event);
      
      // Update provider cache with new model count
      queryClient.setQueryData(adminApiKeys.providers(), (oldData: unknown) => {
        if (!oldData || !Array.isArray(oldData)) return oldData;
        
        return oldData.map((provider: unknown) => {
          if (typeof provider === 'object' && provider !== null && 'id' in provider &&
              (provider as { id: string }).id === event.providerId) {
            return {
              ...provider,
              modelsAvailable: event.modelsAvailable,
              lastCapabilityCheck: event.timestamp,
            };
          }
          return provider;
        });
      });
      
      // Invalidate model mappings as new models might be available
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      
      // Show notification
      if (event.modelsAvailable && event.modelsAvailable > 0) {
        notifications.show({
          title: 'Models Discovered',
          message: `Found ${event.modelsAvailable} models for provider "${event.providerName}"`,
          color: 'green',
        });
      }
    };

    // Handle batch provider update (for efficiency)
    const handleBatchProviderUpdate = (events: ProviderEvent[]) => {
      safeLog('Batch provider update received', { count: events.length });
      
      // Update cache for all providers at once
      queryClient.setQueryData(adminApiKeys.providers(), (oldData: unknown) => {
        if (!oldData || !Array.isArray(oldData)) return oldData;
        
        const updateMap = new Map(events.map(e => [e.providerId, e]));
        
        return oldData.map((provider: unknown) => {
          if (typeof provider !== 'object' || provider === null) return provider;
          
          const providerData = provider as { id: string; healthStatus?: string; isHealthy?: boolean; modelsAvailable?: number };
          const update = updateMap.get(providerData.id);
          if (update) {
            return {
              ...(provider as object),
              healthStatus: update.healthStatus ?? providerData.healthStatus,
              isHealthy: update.isHealthy ?? providerData.isHealthy,
              modelsAvailable: update.modelsAvailable ?? providerData.modelsAvailable,
              lastHealthCheck: update.timestamp,
            };
          }
          return provider;
        });
      });
    };

    // Register event handlers
    connection.on('ProviderCreated', handleProviderCreated);
    connection.on('ProviderUpdated', handleProviderUpdated);
    connection.on('ProviderDeleted', handleProviderDeleted);
    connection.on('ProviderHealthChanged', handleProviderHealthChanged);
    connection.on('ModelCapabilitiesDiscovered', handleModelCapabilitiesDiscovered);
    connection.on('BatchProviderUpdate', handleBatchProviderUpdate);

    // Subscribe to provider updates
    connection.invoke('SubscribeToProviderUpdates').catch((error) => {
      safeWarn('Failed to subscribe to provider updates', error);
    });

    // Cleanup
    return () => {
      connection.off('ProviderCreated', handleProviderCreated);
      connection.off('ProviderUpdated', handleProviderUpdated);
      connection.off('ProviderDeleted', handleProviderDeleted);
      connection.off('ProviderHealthChanged', handleProviderHealthChanged);
      connection.off('ModelCapabilitiesDiscovered', handleModelCapabilitiesDiscovered);
      connection.off('BatchProviderUpdate', handleBatchProviderUpdate);
      
      // Unsubscribe when component unmounts
      connection.invoke('UnsubscribeFromProviderUpdates').catch((error) => {
        safeWarn('Failed to unsubscribe from provider updates', error);
      });
    };
  }, [connection, isConnected, queryClient]);

  return {
    isConnected,
    error,
  };
}