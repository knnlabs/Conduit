'use client';

import { useEffect } from 'react';
import { useSignalR } from './useSignalR';
import { useNavigationStore } from '@/stores/useNavigationStore';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { safeLog, safeWarn } from '@/lib/utils/logging';

interface NavigationStateUpdate {
  timestamp: string;
  changes: {
    providersAvailable: number;
    modelsDiscovered: number;
    healthyProviders: number;
  };
}

export function useNavigationStateHub() {
  const { connection, isConnected, error } = useSignalR({
    hubName: 'navigation-state',
    hubPath: '/hubs/navigation-state',
    enabled: process.env.NEXT_PUBLIC_ENABLE_REAL_TIME_UPDATES !== 'false',
  });
  
  const { updateNavigationState } = useNavigationStore();
  const { setSignalRStatus } = useConnectionStore();

  useEffect(() => {
    if (!connection) return;

    // Update connection status based on SignalR state
    setSignalRStatus(isConnected ? 'connected' : 'disconnected');

    if (!isConnected) return;

    // Subscribe to navigation state updates
    const handleNavigationStateUpdate = (update: NavigationStateUpdate) => {
      safeLog('Navigation state updated', update);
      
      // Update navigation store with new state
      updateNavigationState({
        lastUpdated: new Date(update.timestamp),
        sections: useNavigationStore.getState().sections.map(section => {
          if (section.id === 'configuration') {
            return {
              ...section,
              items: section.items.map(item => {
                // Update provider-related items with live counts
                if (item.id === 'llm-providers') {
                  return {
                    ...item,
                    badge: update.changes.providersAvailable > 0 ? update.changes.providersAvailable.toString() : undefined,
                    disabled: update.changes.providersAvailable === 0,
                  };
                }
                if (item.id === 'model-mappings') {
                  return {
                    ...item,
                    badge: update.changes.modelsDiscovered > 0 ? update.changes.modelsDiscovered.toString() : undefined,
                  };
                }
                return item;
              }),
            };
          }
          
          if (section.id === 'system') {
            return {
              ...section,
              items: section.items.map(item => {
                if (item.id === 'provider-health') {
                  return {
                    ...item,
                    badge: update.changes.healthyProviders.toString(),
                    color: update.changes.healthyProviders === update.changes.providersAvailable ? 'green' : 'orange',
                  };
                }
                return item;
              }),
            };
          }
          
          return section;
        }),
      });
    };

    const handleProviderHealthChange = (providerName: string, isHealthy: boolean) => {
      safeLog(`Provider ${providerName} health changed`, { isHealthy });
      
      // Update connection store with provider-specific health info
      setSignalRStatus(isHealthy ? 'connected' : 'error');
      
      // Trigger a refresh of the navigation state to get updated counts
      connection.invoke('RequestNavigationState').catch(safeWarn);
    };

    const handleModelCapabilitiesDiscovered = (providerName: string, modelCount: number) => {
      safeLog(`Models discovered for ${providerName}`, { modelCount });
      
      // Update navigation with new model discovery info
      updateNavigationState({
        lastUpdated: new Date(),
        sections: useNavigationStore.getState().sections.map(section => {
          if (section.id === 'configuration') {
            return {
              ...section,
              items: section.items.map(item => {
                if (item.id === 'model-mappings') {
                  return {
                    ...item,
                    badge: modelCount > 0 ? modelCount.toString() : undefined,
                    color: modelCount > 0 ? 'green' : 'gray',
                  };
                }
                return item;
              }),
            };
          }
          return section;
        }),
      });
    };

    // Register event handlers
    connection.on('NavigationStateUpdated', handleNavigationStateUpdate);
    connection.on('ProviderHealthChanged', handleProviderHealthChange);
    connection.on('ModelCapabilitiesDiscovered', handleModelCapabilitiesDiscovered);

    // Request initial navigation state
    connection.invoke('RequestNavigationState').catch((error) => {
      safeWarn('Failed to request initial navigation state', error);
    });

    // Cleanup event handlers
    return () => {
      connection.off('NavigationStateUpdated', handleNavigationStateUpdate);
      connection.off('ProviderHealthChanged', handleProviderHealthChange);
      connection.off('ModelCapabilitiesDiscovered', handleModelCapabilitiesDiscovered);
    };
  }, [connection, isConnected, updateNavigationState, setSignalRStatus]);

  useEffect(() => {
    if (error) {
      setSignalRStatus('error');
    }
  }, [error, setSignalRStatus]);

  return {
    isConnected,
    error,
  };
}