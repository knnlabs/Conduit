import { useEffect, useState, useCallback } from 'react';
import { getSDKSignalRManager, SDKSignalRManager, SDKSignalRConfig } from '@/lib/signalr/SDKSignalRManager';
import { useAuthStore } from '@/stores/useAuthStore';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { logger } from '@/lib/utils/logging';

export interface UseRealTimeFeaturesOptions {
  enabled?: boolean;
  autoConnect?: boolean;
  reconnectInterval?: number[];
}

export interface RealTimeConnectionStatus {
  core: {
    connected: boolean;
    status: 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'error';
  };
  admin: {
    connected: boolean;
    status: 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'error';
  };
  overall: boolean;
}

/**
 * Master hook for managing all real-time SignalR connections
 * This hook should be used at the app level to initialize SignalR
 */
export function useRealTimeFeatures(options: UseRealTimeFeaturesOptions = {}) {
  const { enabled = true, autoConnect = true, reconnectInterval } = options;
  
  const { user } = useAuthStore();
  const isAuthenticated = user?.isAuthenticated || false;
  const connectionStore = useConnectionStore();
  const [manager, setManager] = useState<SDKSignalRManager | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<RealTimeConnectionStatus>({
    core: { connected: false, status: 'disconnected' },
    admin: { connected: false, status: 'disconnected' },
    overall: false,
  });

  // Initialize SignalR manager
  const initializeManager = useCallback(async () => {
    if (!enabled || !isAuthenticated) {
      logger.info('Real-time features disabled or user not authenticated');
      return;
    }

    try {
      // Get API URLs from environment or use defaults
      const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_API_EXTERNAL_URL || 
                        process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL ||
                        'http://localhost:5000';
      
      const adminApiUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_EXTERNAL_URL ||
                         process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL ||
                         'http://localhost:5002';

      // Create SignalR manager config
      const config: SDKSignalRConfig = {
        coreApiUrl,
        adminApiUrl,
        autoConnect,
        reconnectInterval,
      };

      // Get or create manager
      const signalRManager = getSDKSignalRManager(config);
      setManager(signalRManager);

      // Initialize Core client if virtual key is available
      // TODO: Add support for virtual key initialization when needed
      // if (virtualKey) {
      //   logger.info('Initializing Core API SignalR with virtual key');
      //   await signalRManager.initializeCoreClient(virtualKey);
      //   
      //   // Update connection status
      //   setConnectionStatus(prev => ({
      //     ...prev,
      //     core: { connected: true, status: 'connected' },
      //     overall: true,
      //   }));
      // }

      // Initialize Admin client if master key is available
      if (user?.masterKey) {
        logger.info('Initializing Admin API SignalR with master key');
        const masterKey = user.masterKey || process.env.NEXT_PUBLIC_CONDUIT_MASTER_KEY || '';
        await signalRManager.initializeAdminClient(masterKey);
        
        // Update connection status
        setConnectionStatus(prev => ({
          ...prev,
          admin: { connected: true, status: 'connected' },
          overall: true,
        }));
      }

      // Update global connection store
      connectionStore.setSignalRStatus('connected');
      logger.info('Real-time features initialized successfully');

    } catch (error) {
      logger.error('Failed to initialize real-time features:', error);
      connectionStore.setSignalRStatus('error');
      setConnectionStatus({
        core: { connected: false, status: 'error' },
        admin: { connected: false, status: 'error' },
        overall: false,
      });
    }
  }, [enabled, isAuthenticated, user, autoConnect, reconnectInterval, connectionStore]);

  // Connect manually
  const connect = useCallback(async () => {
    if (!manager) {
      await initializeManager();
      return;
    }
    
    try {
      await manager.connect();
      connectionStore.setSignalRStatus('connected');
      logger.info('SignalR connections established');
    } catch (error) {
      logger.error('Failed to connect SignalR:', error);
      connectionStore.setSignalRStatus('error');
    }
  }, [manager, initializeManager, connectionStore]);

  // Disconnect manually
  const disconnect = useCallback(async () => {
    if (!manager) return;
    
    try {
      await manager.disconnect();
      connectionStore.setSignalRStatus('disconnected');
      logger.info('SignalR connections closed');
    } catch (error) {
      logger.error('Failed to disconnect SignalR:', error);
    }
  }, [manager, connectionStore]);

  // Initialize on mount or when authentication changes
  useEffect(() => {
    if (enabled && isAuthenticated) {
      initializeManager();
    }

    // Cleanup on unmount
    return () => {
      if (manager) {
        logger.info('Cleaning up SignalR connections');
        manager.cleanup().catch(error => {
          logger.error('Error during SignalR cleanup:', error);
        });
      }
    };
  }, [enabled, isAuthenticated]); // Don't include initializeManager to avoid loops

  // Monitor connection changes
  useEffect(() => {
    if (!manager) return;

    const checkStatus = () => {
      const isConnected = manager.isConnected();
      setConnectionStatus(prev => ({
        ...prev,
        overall: isConnected,
      }));
    };

    // Check status periodically
    const interval = setInterval(checkStatus, 5000);
    checkStatus(); // Initial check

    return () => clearInterval(interval);
  }, [manager]);

  return {
    isConnected: connectionStatus.overall,
    connectionStatus,
    connect,
    disconnect,
    manager,
  };
}

/**
 * Hook to check if real-time features are available and connected
 * Use this in components that depend on real-time updates
 */
export function useRealTimeStatus() {
  const signalRStatus = useConnectionStore(state => state.status.signalR);
  const isConnected = signalRStatus === 'connected';
  const isConnecting = signalRStatus === 'connecting' || signalRStatus === 'reconnecting';
  const hasError = signalRStatus === 'error';

  return {
    isConnected,
    isConnecting,
    hasError,
    status: signalRStatus,
  };
}

/**
 * Initialize real-time features at the app level
 * Call this in your main App component or layout
 */
export async function initializeRealTimeFeatures(options?: UseRealTimeFeaturesOptions) {
  const { user } = useAuthStore.getState();
  
  if (!user) {
    logger.warn('Cannot initialize real-time features without authenticated user');
    return;
  }

  const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_API_EXTERNAL_URL || 
                    process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL ||
                    'http://localhost:5000';
  
  const adminApiUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_EXTERNAL_URL ||
                     process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL ||
                     'http://localhost:5002';

  const config: SDKSignalRConfig = {
    coreApiUrl,
    adminApiUrl,
    autoConnect: options?.autoConnect ?? true,
    reconnectInterval: options?.reconnectInterval,
  };

  try {
    const signalRManager = getSDKSignalRManager(config);
    
    // TODO: Add support for virtual key initialization when needed
    // if (virtualKey) {
    //   await signalRManager.initializeCoreClient(virtualKey);
    // }
    
    if (user?.masterKey) {
      const masterKey = user.masterKey || process.env.NEXT_PUBLIC_CONDUIT_MASTER_KEY || '';
      await signalRManager.initializeAdminClient(masterKey);
    }
    
    logger.info('Real-time features initialized');
    useConnectionStore.getState().setSignalRStatus('connected');
    
  } catch (error) {
    logger.error('Failed to initialize real-time features:', error);
    useConnectionStore.getState().setSignalRStatus('error');
  }
}