'use client';

import { useEffect } from 'react';
import { useSignalR } from './useSignalR';
import { useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { safeLog, safeWarn } from '@/lib/utils/logging';

interface ModelMappingEvent {
  mappingId: string;
  internalModelName: string;
  providerModelName: string;
  providerName: string;
  eventType: 'created' | 'updated' | 'deleted' | 'tested' | 'discoveryCompleted';
  timestamp: string;
  isEnabled?: boolean;
  priority?: number;
  testResult?: {
    success: boolean;
    responseTime?: number;
    error?: string;
  };
  discoveryResult?: {
    modelsDiscovered: number;
    newMappings: number;
    errors: number;
  };
  changes?: Record<string, unknown>;
}

export function useModelMappingHub() {
  const { connection, isConnected, error } = useSignalR({
    hubName: 'model-mappings',
    hubPath: '/hubs/model-mappings',
    enabled: process.env.NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES !== 'false',
  });
  
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!connection || !isConnected) return;

    // Handle model mapping created event
    const handleModelMappingCreated = (event: ModelMappingEvent) => {
      safeLog('Model mapping created event received', event);
      
      // Invalidate model mappings list
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      
      // Show notification
      notifications.show({
        title: 'Model Mapping Created',
        message: `New mapping created: ${event.internalModelName} → ${event.providerModelName}`,
        color: 'blue',
      });
    };

    // Handle model mapping updated event
    const handleModelMappingUpdated = (event: ModelMappingEvent) => {
      safeLog('Model mapping updated event received', event);
      
      // Invalidate model mappings queries
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      
      // Show notification for important changes
      if (event.changes?.isEnabled !== undefined) {
        notifications.show({
          title: 'Model Mapping Updated',
          message: `${event.internalModelName} mapping ${event.changes.isEnabled ? 'enabled' : 'disabled'}`,
          color: event.changes.isEnabled ? 'green' : 'orange',
        });
      } else {
        notifications.show({
          title: 'Model Mapping Updated',
          message: `${event.internalModelName} mapping configuration updated`,
          color: 'blue',
        });
      }
    };

    // Handle model mapping deleted event
    const handleModelMappingDeleted = (event: ModelMappingEvent) => {
      safeLog('Model mapping deleted event received', event);
      
      // Invalidate model mappings list
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      
      // Show notification
      notifications.show({
        title: 'Model Mapping Deleted',
        message: `Mapping for ${event.internalModelName} has been removed`,
        color: 'orange',
      });
    };

    // Handle model test result event
    const handleModelMappingTested = (event: ModelMappingEvent) => {
      safeLog('Model mapping test result received', event);
      
      // Update the model mapping cache with test result
      queryClient.setQueryData(adminApiKeys.modelMappings(), (oldData: unknown) => {
        if (!oldData || !Array.isArray(oldData)) return oldData;
        
        return oldData.map((mapping: unknown) => {
          if ((mapping as any).id === event.mappingId) {
            return {
              ...(mapping as any),
              lastTested: event.timestamp,
              lastTestSuccess: event.testResult?.success,
              lastResponseTime: event.testResult?.responseTime,
            };
          }
          return mapping;
        });
      });
      
      // Show notification based on test result
      if (event.testResult?.success) {
        notifications.show({
          title: 'Model Test Successful',
          message: `${event.internalModelName} is responding correctly (${event.testResult.responseTime}ms)`,
          color: 'green',
        });
      } else {
        notifications.show({
          title: 'Model Test Failed',
          message: `${event.internalModelName} test failed: ${event.testResult?.error || 'Unknown error'}`,
          color: 'red',
        });
      }
    };

    // Handle bulk discovery completed event
    const handleDiscoveryCompleted = (event: ModelMappingEvent) => {
      safeLog('Model discovery completed', event);
      
      // Invalidate model mappings to show new discoveries
      queryClient.invalidateQueries({ queryKey: adminApiKeys.modelMappings() });
      
      // Show notification with results
      if (event.discoveryResult) {
        const { modelsDiscovered, newMappings, errors } = event.discoveryResult;
        
        notifications.show({
          title: 'Model Discovery Complete',
          message: `Discovered ${modelsDiscovered} models, created ${newMappings} new mappings${errors > 0 ? ` (${errors} errors)` : ''}`,
          color: errors > 0 ? 'orange' : 'green',
        });
      }
    };

    // Handle usage update for model mappings
    const handleModelMappingUsageUpdate = (mappingId: string, requestCount: number, lastUsed: string) => {
      safeLog('Model mapping usage update', { mappingId, requestCount, lastUsed });
      
      // Update cache with new usage data
      queryClient.setQueryData(adminApiKeys.modelMappings(), (oldData: unknown) => {
        if (!oldData || !Array.isArray(oldData)) return oldData;
        
        return oldData.map((mapping: unknown) => {
          if (mapping.id === mappingId) {
            return {
              ...mapping,
              requestCount,
              lastUsed,
            };
          }
          return mapping;
        });
      });
    };

    // Handle batch usage update (for efficiency)
    const handleBatchUsageUpdate = (updates: Array<{ mappingId: string; requestCount: number; lastUsed: string }>) => {
      safeLog('Batch usage update received', { count: updates.length });
      
      // Update cache for all mappings at once
      queryClient.setQueryData(adminApiKeys.modelMappings(), (oldData: unknown) => {
        if (!oldData || !Array.isArray(oldData)) return oldData;
        
        const updateMap = new Map(updates.map(u => [u.mappingId, u]));
        
        return oldData.map((mapping: unknown) => {
          const update = updateMap.get(typeof mapping === 'object' && mapping !== null && 'id' in mapping ? (mapping as { id: string }).id : '');
          if (update) {
            return {
              ...(mapping as any),
              requestCount: update.requestCount,
              lastUsed: update.lastUsed,
            };
          }
          return mapping;
        });
      });
    };

    // Register event handlers
    connection.on('ModelMappingCreated', handleModelMappingCreated);
    connection.on('ModelMappingUpdated', handleModelMappingUpdated);
    connection.on('ModelMappingDeleted', handleModelMappingDeleted);
    connection.on('ModelMappingTested', handleModelMappingTested);
    connection.on('DiscoveryCompleted', handleDiscoveryCompleted);
    connection.on('ModelMappingUsageUpdate', handleModelMappingUsageUpdate);
    connection.on('BatchUsageUpdate', handleBatchUsageUpdate);

    // Subscribe to model mapping updates
    connection.invoke('SubscribeToModelMappingUpdates').catch((error) => {
      safeWarn('Failed to subscribe to model mapping updates', error);
    });

    // Cleanup
    return () => {
      connection.off('ModelMappingCreated', handleModelMappingCreated);
      connection.off('ModelMappingUpdated', handleModelMappingUpdated);
      connection.off('ModelMappingDeleted', handleModelMappingDeleted);
      connection.off('ModelMappingTested', handleModelMappingTested);
      connection.off('DiscoveryCompleted', handleDiscoveryCompleted);
      connection.off('ModelMappingUsageUpdate', handleModelMappingUsageUpdate);
      connection.off('BatchUsageUpdate', handleBatchUsageUpdate);
      
      // Unsubscribe when component unmounts
      connection.invoke('UnsubscribeFromModelMappingUpdates').catch((error) => {
        safeWarn('Failed to unsubscribe from model mapping updates', error);
      });
    };
  }, [connection, isConnected, queryClient]);

  return {
    isConnected,
    error,
  };
}