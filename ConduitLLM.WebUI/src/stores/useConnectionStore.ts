import { create } from 'zustand';
import { ConnectionStatus } from '@/types/navigation';
import { ConduitCoreClient } from '@knn_labs/conduit-core-client';
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';

interface ConnectionState {
  status: ConnectionStatus;
  
  // Actions
  updateConnectionStatus: (updates: Partial<ConnectionStatus>) => void;
  setApiStatus: (api: 'coreApi' | 'adminApi', status: ConnectionStatus['coreApi']) => void;
  setSignalRStatus: (status: ConnectionStatus['signalR']) => void;
  checkConnections: () => Promise<void>;
}

export const useConnectionStore = create<ConnectionState>((set, get) => ({
  status: {
    coreApi: 'disconnected',
    adminApi: 'disconnected',
    signalR: 'disconnected',
    lastCheck: null,
  },

  updateConnectionStatus: (updates: Partial<ConnectionStatus>) => {
    set((state) => ({
      status: {
        ...state.status,
        ...updates,
        lastCheck: new Date(),
      },
    }));
  },

  setApiStatus: (api: 'coreApi' | 'adminApi', status: ConnectionStatus['coreApi']) => {
    set((state) => ({
      status: {
        ...state.status,
        [api]: status,
        lastCheck: new Date(),
      },
    }));
  },

  setSignalRStatus: (status: ConnectionStatus['signalR']) => {
    set((state) => ({
      status: {
        ...state.status,
        signalR: status,
        lastCheck: new Date(),
      },
    }));
  },

  checkConnections: async () => {
    const checkApi = async (
      apiType: 'coreApi' | 'adminApi'
    ): Promise<void> => {
      try {
        get().setApiStatus(apiType, 'connecting');
        
        let isConnected = false;
        
        if (apiType === 'coreApi') {
          const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL;
          if (coreApiUrl) {
            // Create a temporary Core client for connection check
            const coreClient = new ConduitCoreClient({
              baseURL: coreApiUrl,
              apiKey: 'ping-check', // Dummy key for ping
              timeout: 5000,
            });
            
            // Use the new connection.ping method from local SDK
            isConnected = await coreClient.connection.pingWithTimeout(5000);
          }
        } else {
          const adminApiUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL;
          if (adminApiUrl) {
            // Create a temporary Admin client for connection check
            const adminClient = new ConduitAdminClient({
              adminApiUrl: adminApiUrl,
              masterKey: 'ping-check', // Dummy key for ping
              options: {
                timeout: 5000,
              }
            });
            
            // Use the new connection.ping method from local SDK
            isConnected = await adminClient.connection.pingWithTimeout(5000);
          }
        }
        
        get().setApiStatus(apiType, isConnected ? 'connected' : 'error');
      } catch (error: any) {
        console.warn(`${apiType} connection check failed:`, error.message);
        get().setApiStatus(apiType, 'error');
      }
    };

    // Check both APIs in parallel
    await Promise.allSettled([
      checkApi('coreApi'),
      checkApi('adminApi'),
    ]);
  },
}));

// Initialize connection checks
if (typeof window !== 'undefined') {
  useConnectionStore.getState().checkConnections();
  
  // Check connections every 30 seconds
  setInterval(() => {
    useConnectionStore.getState().checkConnections();
  }, 30000);
}