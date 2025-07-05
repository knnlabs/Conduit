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
    try {
      // Set both APIs to connecting state
      get().setApiStatus('coreApi', 'connecting');
      get().setApiStatus('adminApi', 'connecting');
      
      // Call the server-side health check API
      const response = await fetch('/api/health/connections');
      
      if (response.ok) {
        const data = await response.json();
        get().setApiStatus('coreApi', data.coreApi);
        get().setApiStatus('adminApi', data.adminApi);
      } else {
        // If health check fails, set both to error
        get().setApiStatus('coreApi', 'error');
        get().setApiStatus('adminApi', 'error');
      }
    } catch (error) {
      console.warn('Connection health check failed:', error);
      get().setApiStatus('coreApi', 'error');
      get().setApiStatus('adminApi', 'error');
    }
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