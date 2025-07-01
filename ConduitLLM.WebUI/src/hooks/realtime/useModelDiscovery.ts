import { useEffect, useCallback, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { 
  getSDKSignalRManager, 
  ModelDiscoveryEvent,
  ProviderHealthEvent,
  ConfigurationEvent,
  VirtualKeyEvent,
} from '@/lib/signalr/SDKSignalRManager';
import { logger } from '@/lib/utils/logging';
import { notifications } from '@mantine/notifications';

export interface ProviderModelInfo {
  providerId: string;
  providerName: string;
  models: string[];
  health: 'healthy' | 'degraded' | 'unhealthy';
  latency?: number;
  lastDiscovered?: Date;
  lastHealthCheck?: Date;
}

export interface UseModelDiscoveryOptions {
  enabled?: boolean;
  providerIds?: string[];
  onModelDiscovered?: (event: ModelDiscoveryEvent) => void;
  onProviderHealthChange?: (event: ProviderHealthEvent) => void;
  onConfigurationChange?: (event: ConfigurationEvent) => void;
  onVirtualKeyUpdate?: (event: VirtualKeyEvent) => void;
  showNotifications?: boolean;
}

export function useModelDiscovery(options: UseModelDiscoveryOptions = {}) {
  const { 
    enabled = true,
    providerIds = [],
    onModelDiscovered,
    onProviderHealthChange,
    onConfigurationChange,
    onVirtualKeyUpdate,
    showNotifications = true,
  } = options;
  
  const queryClient = useQueryClient();
  const [providerModels, setProviderModels] = useState<Map<string, ProviderModelInfo>>(new Map());
  const [recentEvents, setRecentEvents] = useState<{
    type: 'discovery' | 'health' | 'config' | 'virtualKey';
    message: string;
    timestamp: Date;
    level: 'info' | 'warning' | 'error';
  }[]>([]);

  // Add event to recent events list
  const addEvent = useCallback((
    type: 'discovery' | 'health' | 'config' | 'virtualKey',
    message: string,
    level: 'info' | 'warning' | 'error' = 'info'
  ) => {
    setRecentEvents(prev => [
      { type, message, timestamp: new Date(), level },
      ...prev.slice(0, 19), // Keep last 20 events
    ]);
  }, []);

  // Handle model discovery events
  const handleModelDiscovered = useCallback((event: ModelDiscoveryEvent) => {
    logger.info('Models discovered:', event);

    // Filter by providerIds if specified
    if (providerIds.length > 0 && !providerIds.includes(event.providerId)) {
      return;
    }

    // Call custom handler
    onModelDiscovered?.(event);

    // Update provider models
    setProviderModels(prev => {
      const updated = new Map(prev);
      const existing = updated.get(event.providerId) || {
        providerId: event.providerId,
        providerName: event.providerName,
        models: [],
        health: 'healthy' as const,
      };

      existing.models = event.modelsDiscovered;
      existing.lastDiscovered = new Date(event.timestamp);
      updated.set(event.providerId, existing);
      return updated;
    });

    // Add event
    const modelCount = event.modelsDiscovered.length;
    addEvent(
      'discovery',
      `Discovered ${modelCount} models for ${event.providerName}`,
      'info'
    );

    // Show notification if enabled
    if (showNotifications) {
      notifications.show({
        title: 'Models Discovered',
        message: `Found ${modelCount} models for ${event.providerName}`,
        color: 'blue',
        autoClose: 5000,
      });
    }

    // Invalidate queries
    queryClient.invalidateQueries({ queryKey: ['admin', 'providers', event.providerId] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'model-mappings'] });
  }, [providerIds, onModelDiscovered, showNotifications, queryClient, addEvent]);

  // Handle provider health changes
  const handleProviderHealthChange = useCallback((event: ProviderHealthEvent) => {
    logger.info('Provider health changed:', event);

    // Filter by providerIds if specified
    if (providerIds.length > 0 && !providerIds.includes(event.providerId)) {
      return;
    }

    // Call custom handler
    onProviderHealthChange?.(event);

    // Update provider health
    setProviderModels(prev => {
      const updated = new Map(prev);
      const existing = updated.get(event.providerId) || {
        providerId: event.providerId,
        providerName: event.providerName,
        models: [],
        health: event.status,
      };

      existing.health = event.status;
      existing.latency = event.latency;
      existing.lastHealthCheck = new Date();
      updated.set(event.providerId, existing);
      return updated;
    });

    // Add event
    const level = event.status === 'unhealthy' ? 'error' : 
                  event.status === 'degraded' ? 'warning' : 'info';
    addEvent(
      'health',
      `${event.providerName} is ${event.status}${event.latency ? ` (${event.latency}ms)` : ''}`,
      level
    );

    // Show notification for unhealthy providers
    if (showNotifications && event.status !== 'healthy') {
      notifications.show({
        title: 'Provider Health Alert',
        message: `${event.providerName} is ${event.status}${event.error ? `: ${event.error}` : ''}`,
        color: event.status === 'unhealthy' ? 'red' : 'yellow',
        autoClose: event.status === 'unhealthy' ? false : 10000,
      });
    }

    // Invalidate queries
    queryClient.invalidateQueries({ queryKey: ['admin', 'providers', event.providerId] });
    queryClient.invalidateQueries({ queryKey: ['provider-health'] });
  }, [providerIds, onProviderHealthChange, showNotifications, queryClient, addEvent]);

  // Handle configuration changes
  const handleConfigurationChange = useCallback((event: ConfigurationEvent) => {
    logger.info('Configuration changed:', event);

    // Call custom handler
    onConfigurationChange?.(event);

    // Add event
    addEvent(
      'config',
      `${event.category}.${event.setting} changed`,
      'info'
    );

    // Show notification if enabled
    if (showNotifications) {
      notifications.show({
        title: 'Configuration Updated',
        message: `${event.category} setting "${event.setting}" has been updated`,
        color: 'teal',
        autoClose: 5000,
      });
    }

    // Invalidate relevant queries based on category
    if (event.category === 'providers' || event.category === 'models') {
      queryClient.invalidateQueries({ queryKey: ['admin', 'providers'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'model-mappings'] });
    } else if (event.category === 'security') {
      queryClient.invalidateQueries({ queryKey: ['admin', 'settings', 'security'] });
    } else {
      queryClient.invalidateQueries({ queryKey: ['admin', 'settings', event.category] });
    }
  }, [onConfigurationChange, showNotifications, queryClient, addEvent]);

  // Handle virtual key events
  const handleVirtualKeyUpdate = useCallback((event: VirtualKeyEvent) => {
    logger.info('Virtual key event:', event);

    // Call custom handler
    onVirtualKeyUpdate?.(event);

    // Add event
    const level = event.action === 'deleted' || event.action === 'disabled' ? 'warning' : 'info';
    addEvent(
      'virtualKey',
      `Virtual key ${event.keyId.substring(0, 8)}... was ${event.action}`,
      level
    );

    // Show notification for important events
    if (showNotifications && (event.action === 'deleted' || event.action === 'disabled')) {
      notifications.show({
        title: 'Virtual Key Update',
        message: `Virtual key ${event.keyId.substring(0, 8)}... has been ${event.action}`,
        color: 'orange',
        autoClose: 7000,
      });
    }

    // Invalidate queries
    queryClient.invalidateQueries({ queryKey: ['admin', 'virtual-keys', event.keyId] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'virtual-keys'] });
  }, [onVirtualKeyUpdate, showNotifications, queryClient, addEvent]);

  useEffect(() => {
    if (!enabled) return;

    try {
      // Get SignalR manager
      const signalRManager = getSDKSignalRManager();
      
      // Register event handlers
      signalRManager.on('onModelDiscovered', handleModelDiscovered);
      signalRManager.on('onProviderHealthChange', handleProviderHealthChange);
      signalRManager.on('onConfigurationChange', handleConfigurationChange);
      signalRManager.on('onVirtualKeyUpdate', handleVirtualKeyUpdate);

      logger.info('Model discovery hub listeners registered');

      // Cleanup
      return () => {
        signalRManager.off('onModelDiscovered');
        signalRManager.off('onProviderHealthChange');
        signalRManager.off('onConfigurationChange');
        signalRManager.off('onVirtualKeyUpdate');
        logger.info('Model discovery hub listeners unregistered');
      };
    } catch (error) {
      logger.error('Failed to setup model discovery hub:', error);
    }
  }, [
    enabled, 
    handleModelDiscovered, 
    handleProviderHealthChange,
    handleConfigurationChange,
    handleVirtualKeyUpdate,
  ]);

  // Get provider info
  const getProviderInfo = useCallback((providerId: string): ProviderModelInfo | undefined => {
    return providerModels.get(providerId);
  }, [providerModels]);

  // Get all providers
  const getAllProviders = useCallback((): ProviderModelInfo[] => {
    return Array.from(providerModels.values());
  }, [providerModels]);

  // Get healthy providers
  const getHealthyProviders = useCallback((): ProviderModelInfo[] => {
    return Array.from(providerModels.values()).filter(p => p.health === 'healthy');
  }, [providerModels]);

  // Get providers with issues
  const getProvidersWithIssues = useCallback((): ProviderModelInfo[] => {
    return Array.from(providerModels.values()).filter(
      p => p.health === 'degraded' || p.health === 'unhealthy'
    );
  }, [providerModels]);

  // Clear events
  const clearEvents = useCallback(() => {
    setRecentEvents([]);
  }, []);

  return {
    providers: getAllProviders(),
    healthyProviders: getHealthyProviders(),
    providersWithIssues: getProvidersWithIssues(),
    recentEvents,
    getProviderInfo,
    clearEvents,
    hasIssues: getProvidersWithIssues().length > 0,
    isConnected: enabled,
  };
}

// Hook for monitoring a specific provider
export function useProviderModels(providerId: string | null) {
  const [providerInfo, setProviderInfo] = useState<ProviderModelInfo | null>(null);

  const handleModelDiscovered = useCallback((event: ModelDiscoveryEvent) => {
    if (event.providerId !== providerId) return;

    setProviderInfo(prev => ({
      providerId: event.providerId,
      providerName: event.providerName,
      models: event.modelsDiscovered,
      health: prev?.health || 'healthy',
      latency: prev?.latency,
      lastDiscovered: new Date(event.timestamp),
      lastHealthCheck: prev?.lastHealthCheck,
    }));
  }, [providerId]);

  const handleHealthChange = useCallback((event: ProviderHealthEvent) => {
    if (event.providerId !== providerId) return;

    setProviderInfo(prev => prev ? {
      ...prev,
      health: event.status,
      latency: event.latency,
      lastHealthCheck: new Date(),
    } : null);
  }, [providerId]);

  const { providers } = useModelDiscovery({
    enabled: !!providerId,
    providerIds: providerId ? [providerId] : [],
    onModelDiscovered: handleModelDiscovered,
    onProviderHealthChange: handleHealthChange,
    showNotifications: false,
  });

  // Initialize from providers if available
  useEffect(() => {
    if (providerId && !providerInfo) {
      const provider = providers.find(p => p.providerId === providerId);
      if (provider) {
        setProviderInfo(provider);
      }
    }
  }, [providerId, providerInfo, providers]);

  return {
    providerName: providerInfo?.providerName || '',
    models: providerInfo?.models || [],
    modelCount: providerInfo?.models.length || 0,
    health: providerInfo?.health || 'healthy',
    latency: providerInfo?.latency,
    isHealthy: providerInfo?.health === 'healthy',
    lastDiscovered: providerInfo?.lastDiscovered,
    lastHealthCheck: providerInfo?.lastHealthCheck,
  };
}