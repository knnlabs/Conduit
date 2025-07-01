import { create } from 'zustand';
import { ConnectionStatus } from '@/types/navigation';

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
      url: string,
      apiType: 'coreApi' | 'adminApi'
    ): Promise<void> => {
      try {
        get().setApiStatus(apiType, 'connecting');
        
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 5000);
        
        const response = await fetch(`${url}/health/ready`, {
          signal: controller.signal,
          mode: 'cors',
          headers: {
            'Accept': 'application/json',
          },
        });
        
        clearTimeout(timeoutId);
        
        if (response.ok) {
          get().setApiStatus(apiType, 'connected');
        } else {
          get().setApiStatus(apiType, 'error');
        }
      } catch (error: any) {
        console.warn(`${apiType} connection check failed:`, error.message);
        get().setApiStatus(apiType, 'error');
      }
    };

    // Check both APIs in parallel
    const coreApiUrl = process.env.NEXT_PUBLIC_CONDUIT_CORE_API_URL;
    const adminApiUrl = process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL;

    await Promise.allSettled([
      coreApiUrl ? checkApi(coreApiUrl, 'coreApi') : Promise.resolve(),
      adminApiUrl ? checkApi(adminApiUrl, 'adminApi') : Promise.resolve(),
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