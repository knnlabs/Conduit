'use client';

import { useEffect, useCallback } from 'react';
import { useSignalR } from './useSignalR';
import { useQueryClient } from '@tanstack/react-query';
import { adminApiKeys } from '@/hooks/api/useAdminApi';

interface SpendUpdateEvent {
  virtualKeyId: string;
  keyHash: string;
  previousSpend: number;
  newSpend: number;
  deltaAmount: number;
  timestamp: string;
  requestId?: string;
}

interface BudgetAlertEvent {
  virtualKeyId: string;
  keyHash: string;
  keyName: string;
  currentSpend: number;
  maxBudget: number;
  percentage: number;
  alertType: 'warning' | 'critical' | 'exceeded';
  timestamp: string;
}

export function useSpendTracking() {
  const { connection, isConnected } = useSignalR({
    hubName: 'spend-tracking',
    hubPath: '/hubs/spend-tracking',
    enabled: process.env.NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES !== 'false',
  });
  
  const queryClient = useQueryClient();

  const handleSpendUpdate = useCallback((event: SpendUpdateEvent) => {
    console.log('Spend update received:', event);

    // Update the virtual keys query cache
    queryClient.setQueryData(adminApiKeys.virtualKeys(), (oldData: unknown) => {
      if (!oldData) return oldData;
      
      return (oldData as unknown[]).map((key: unknown) => {
        if (key.id === event.virtualKeyId || key.keyHash === event.keyHash) {
          return {
            ...key,
            currentSpend: event.newSpend,
            lastUsed: event.timestamp,
            // Increment request count if this is a new request
            requestCount: event.requestId ? key.requestCount + 1 : key.requestCount,
          };
        }
        return key;
      });
    });

    // Update individual virtual key cache if it exists
    queryClient.setQueryData(adminApiKeys.virtualKey(event.virtualKeyId), (oldData: unknown) => {
      if (!oldData) return oldData;
      
      return {
        ...oldData,
        currentSpend: event.newSpend,
        lastUsed: event.timestamp,
        requestCount: event.requestId ? (oldData as any).requestCount + 1 : (oldData as any).requestCount,
      };
    });

  }, [queryClient]);

  const handleBudgetAlert = useCallback((event: BudgetAlertEvent) => {
    console.log('Budget alert received:', event);

    // Show notification based on alert type
    import('@mantine/notifications').then(({ notifications }) => {
      const alertConfig = {
      warning: {
        color: 'orange',
        title: 'Budget Warning',
        message: `Virtual key "${event.keyName}" has reached ${event.percentage}% of its budget`,
      },
      critical: {
        color: 'red',
        title: 'Budget Critical',
        message: `Virtual key "${event.keyName}" has reached ${event.percentage}% of its budget`,
      },
      exceeded: {
        color: 'red',
        title: 'Budget Exceeded',
        message: `Virtual key "${event.keyName}" has exceeded its budget limit`,
      },
    };

    const config = alertConfig[event.alertType];
    
      notifications.show({
        id: `budget-alert-${event.virtualKeyId}`,
        ...config,
        autoClose: 10000,
      });
    });

  }, []);

  const handleBatchSpendUpdate = useCallback((events: SpendUpdateEvent[]) => {
    console.log('Batch spend update received:', events);

    // Update multiple keys at once for efficiency
    queryClient.setQueryData(adminApiKeys.virtualKeys(), (oldData: unknown) => {
      if (!oldData) return oldData;
      
      const updates = new Map(events.map(event => [event.virtualKeyId, event]));
      
      return (oldData as unknown[]).map((key: unknown) => {
        const k = key as { id: string; requestCount: number; [key: string]: unknown };
        const update = updates.get(k.id);
        if (update) {
          return {
            ...k,
            currentSpend: update.newSpend,
            lastUsed: update.timestamp,
            requestCount: update.requestId ? k.requestCount + 1 : k.requestCount,
          };
        }
        return k;
      });
    });

  }, [queryClient]);

  useEffect(() => {
    if (!connection || !isConnected) return;

    // Subscribe to spend tracking events
    connection.on('SpendUpdated', handleSpendUpdate);
    connection.on('BudgetAlert', handleBudgetAlert);
    connection.on('BatchSpendUpdate', handleBatchSpendUpdate);

    // Request to join spend tracking group
    connection.invoke('JoinSpendTracking').catch((error) => {
      console.warn('Failed to join spend tracking group:', error);
    });

    // Cleanup event handlers
    return () => {
      connection.off('SpendUpdated', handleSpendUpdate);
      connection.off('BudgetAlert', handleBudgetAlert);
      connection.off('BatchSpendUpdate', handleBatchSpendUpdate);
      
      connection.invoke('LeaveSpendTracking').catch((error) => {
        console.warn('Failed to leave spend tracking group:', error);
      });
    };
  }, [connection, isConnected, handleSpendUpdate, handleBudgetAlert, handleBatchSpendUpdate]);

  // Manual refresh function
  const refreshSpendData = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: adminApiKeys.virtualKeys() });
  }, [queryClient]);

  return {
    isConnected,
    refreshSpendData,
  };
}