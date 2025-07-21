import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

export interface ConnectionState {
  // 'unknown' here is a string literal status value, not the TypeScript unknown type
  coreApi: 'connected' | 'connecting' | 'disconnected' | 'error' | 'unknown';
  adminApi: 'connected' | 'connecting' | 'disconnected' | 'error' | 'unknown';
  signalR: 'connected' | 'connecting' | 'disconnected' | 'error' | 'unknown';
}

interface ConnectionStore {
  status: ConnectionState;
  updateStatus: (type: keyof ConnectionState, status: ConnectionState[keyof ConnectionState]) => void;
  setStatus: (status: Partial<ConnectionState>) => void;
  reset: () => void;
}

const initialState: ConnectionState = {
  coreApi: 'unknown', // String literal status, not TypeScript unknown type
  adminApi: 'unknown', // String literal status, not TypeScript unknown type
  signalR: 'unknown', // String literal status, not TypeScript unknown type
};

export const useConnectionStore = create<ConnectionStore>()(
  devtools(
    (set) => ({
      status: initialState,
      
      updateStatus: (type, status) =>
        set((state) => ({
          status: {
            ...state.status,
            [type]: status,
          },
        })),
      
      setStatus: (status) =>
        set((state) => ({
          status: {
            ...state.status,
            ...status,
          },
        })),
      
      reset: () =>
        set({
          status: initialState,
        }),
    }),
    {
      name: 'connection-store',
    }
  )
);