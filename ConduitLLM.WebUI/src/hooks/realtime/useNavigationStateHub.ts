import { useEffect, useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getSDKSignalRManager, NavigationStateUpdate } from '@/lib/signalr/SDKSignalRManager';
import { useNavigationStore } from '@/stores/useNavigationStore';
import { logger } from '@/lib/utils/logging';

export interface UseNavigationStateHubOptions {
  enabled?: boolean;
  onUpdate?: (update: NavigationStateUpdate) => void;
}

export function useNavigationStateHub(options: UseNavigationStateHubOptions = {}) {
  const { enabled = true, onUpdate } = options;
  const queryClient = useQueryClient();
  const navigationStore = useNavigationStore();

  // Handle navigation state updates
  const handleNavigationStateUpdate = useCallback((update: NavigationStateUpdate) => {
    logger.info('Navigation state update received:', update);

    // Call custom handler if provided
    onUpdate?.(update);

    // Update query cache based on the type of update
    switch (update.type) {
      case 'model_mapping':
        // Invalidate model mappings queries
        queryClient.invalidateQueries({ queryKey: ['admin', 'model-mappings'] });
        
        // Update navigation store
        navigationStore.updateNavigationState({ lastUpdated: new Date() });
        break;

      case 'provider':
        // Invalidate provider queries
        queryClient.invalidateQueries({ queryKey: ['admin', 'providers'] });
        queryClient.invalidateQueries({ queryKey: ['provider-health'] });
        
        // Update navigation store
        navigationStore.updateNavigationState({ lastUpdated: new Date() });
        break;

      case 'virtual_key':
        // Invalidate virtual key queries
        queryClient.invalidateQueries({ queryKey: ['admin', 'virtual-keys'] });
        
        // Update navigation store
        navigationStore.updateNavigationState({ lastUpdated: new Date() });
        break;
    }

    // Last sync time already updated above
  }, [queryClient, navigationStore, onUpdate]);

  useEffect(() => {
    if (!enabled) return;

    try {
      // Get SignalR manager
      const signalRManager = getSDKSignalRManager();
      
      // Register navigation state update handler
      signalRManager.on('onNavigationStateUpdate', handleNavigationStateUpdate);

      logger.info('Navigation state hub listener registered');

      // Cleanup
      return () => {
        signalRManager.off('onNavigationStateUpdate');
        logger.info('Navigation state hub listener unregistered');
      };
    } catch (error) {
      logger.error('Failed to setup navigation state hub:', error);
    }
  }, [enabled, handleNavigationStateUpdate]);

  // Manual refresh function
  const refresh = useCallback(async () => {
    // Invalidate all navigation-related queries
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['admin', 'model-mappings'] }),
      queryClient.invalidateQueries({ queryKey: ['admin', 'providers'] }),
      queryClient.invalidateQueries({ queryKey: ['admin', 'virtual-keys'] }),
      queryClient.invalidateQueries({ queryKey: ['provider-health'] }),
    ]);
    
    navigationStore.updateNavigationState({ lastUpdated: new Date() });
  }, [queryClient, navigationStore]);

  return {
    refresh,
    lastSync: navigationStore.lastUpdated,
    isConnected: true, // TODO: Get actual SignalR connection state
  };
}