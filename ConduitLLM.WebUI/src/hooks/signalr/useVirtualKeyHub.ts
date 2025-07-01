'use client';

import { useEffect } from 'react';
import { useSignalR } from './useSignalR';
import { useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';
import { safeLog, safeWarn } from '@/lib/utils/logging';

interface VirtualKeyEvent {
  keyId: string;
  keyHash: string;
  keyName: string;
  eventType: 'created' | 'updated' | 'deleted' | 'spendUpdated';
  timestamp: string;
  changes?: Record<string, any>;
  currentSpend?: number;
  spendAmount?: number;
}

export function useVirtualKeyHub() {
  const { connection, isConnected, error } = useSignalR({
    hubName: 'virtual-keys',
    hubPath: '/hubs/virtual-keys',
    enabled: process.env.NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES !== 'false',
  });
  
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!connection || !isConnected) return;

    // Handle virtual key created event
    const handleVirtualKeyCreated = (event: VirtualKeyEvent) => {
      safeLog('Virtual key created event received', event);
      
      // Invalidate virtual keys list to refetch with new key
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKeys() });
      
      // Show notification
      notifications.show({
        title: 'Virtual Key Created',
        message: `New virtual key "${event.keyName}" has been created`,
        color: 'blue',
      });
    };

    // Handle virtual key updated event
    const handleVirtualKeyUpdated = (event: VirtualKeyEvent) => {
      safeLog('Virtual key updated event received', event);
      
      // Invalidate both list and specific key queries
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKeys() });
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKey(event.keyId) });
      
      // Show notification
      notifications.show({
        title: 'Virtual Key Updated',
        message: `Virtual key "${event.keyName}" has been updated`,
        color: 'blue',
      });
    };

    // Handle virtual key deleted event
    const handleVirtualKeyDeleted = (event: VirtualKeyEvent) => {
      safeLog('Virtual key deleted event received', event);
      
      // Invalidate virtual keys list
      queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKeys() });
      
      // Show notification
      notifications.show({
        title: 'Virtual Key Deleted',
        message: `Virtual key "${event.keyName}" has been deleted`,
        color: 'orange',
      });
    };

    // Handle spend update event
    const handleSpendUpdated = (event: VirtualKeyEvent) => {
      safeLog('Virtual key spend updated', event);
      
      // Update the cache with new spend data without refetching
      queryClient.setQueryData(adminApiKeys.virtualKeys(), (oldData: any) => {
        if (!oldData) return oldData;
        
        return oldData.map((key: any) => {
          if (key.id === event.keyId) {
            return {
              ...key,
              currentSpend: event.currentSpend || key.currentSpend,
            };
          }
          return key;
        });
      });
      
      // Also update individual key cache
      queryClient.setQueryData(adminApiKeys.virtualKey(event.keyId), (oldData: any) => {
        if (!oldData) return oldData;
        
        return {
          ...oldData,
          currentSpend: event.currentSpend || oldData.currentSpend,
        };
      });
      
      // Show notification if spend is significant
      if (event.spendAmount && event.spendAmount > 0.01) {
        notifications.show({
          title: 'Spend Update',
          message: `Virtual key "${event.keyName}" spent $${event.spendAmount.toFixed(4)}`,
          color: 'yellow',
          autoClose: 3000,
        });
      }
    };

    // Handle batch spend update event (for efficiency)
    const handleBatchSpendUpdated = (events: VirtualKeyEvent[]) => {
      safeLog('Batch spend update received', { count: events.length });
      
      // Update cache for all keys at once
      queryClient.setQueryData(adminApiKeys.virtualKeys(), (oldData: any) => {
        if (!oldData) return oldData;
        
        const spendMap = new Map(events.map(e => [e.keyId, e.currentSpend]));
        
        return oldData.map((key: any) => {
          const newSpend = spendMap.get(key.id);
          if (newSpend !== undefined) {
            return { ...key, currentSpend: newSpend };
          }
          return key;
        });
      });
    };

    // Register event handlers
    connection.on('VirtualKeyCreated', handleVirtualKeyCreated);
    connection.on('VirtualKeyUpdated', handleVirtualKeyUpdated);
    connection.on('VirtualKeyDeleted', handleVirtualKeyDeleted);
    connection.on('SpendUpdated', handleSpendUpdated);
    connection.on('BatchSpendUpdated', handleBatchSpendUpdated);

    // Subscribe to virtual key updates
    connection.invoke('SubscribeToVirtualKeyUpdates').catch((error) => {
      safeWarn('Failed to subscribe to virtual key updates', error);
    });

    // Cleanup
    return () => {
      connection.off('VirtualKeyCreated', handleVirtualKeyCreated);
      connection.off('VirtualKeyUpdated', handleVirtualKeyUpdated);
      connection.off('VirtualKeyDeleted', handleVirtualKeyDeleted);
      connection.off('SpendUpdated', handleSpendUpdated);
      connection.off('BatchSpendUpdated', handleBatchSpendUpdated);
      
      // Unsubscribe when component unmounts
      connection.invoke('UnsubscribeFromVirtualKeyUpdates').catch((error) => {
        safeWarn('Failed to unsubscribe from virtual key updates', error);
      });
    };
  }, [connection, isConnected, queryClient]);

  return {
    isConnected,
    error,
  };
}