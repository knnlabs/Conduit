import { useEffect, useRef, useState, useCallback } from 'react';
import { HubConnectionState } from '@microsoft/signalr';
import { BaseSignalRConnection } from '../../../signalr/BaseSignalRConnection';
import { useConduit } from '../../ConduitProvider';

export interface UseSignalRConnectionOptions {
  enabled?: boolean;
  onConnected?: () => void;
  onDisconnected?: (error?: Error) => void;
  onReconnecting?: (error?: Error) => void;
  onReconnected?: () => void;
}

export interface SignalRConnectionState {
  connectionState: HubConnectionState;
  isConnected: boolean;
  isConnecting: boolean;
  isReconnecting: boolean;
  isDisconnected: boolean;
  error?: Error;
}

export function useSignalRConnection<T extends BaseSignalRConnection>(
  createConnection: (baseUrl: string, apiKey: string) => T,
  hubPath: string,
  options: UseSignalRConnectionOptions = {}
): {
  connection: T | null;
  state: SignalRConnectionState;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
} {
  const { virtualKey } = useConduit();
  const [connection, setConnection] = useState<T | null>(null);
  const [state, setState] = useState<SignalRConnectionState>({
    connectionState: HubConnectionState.Disconnected,
    isConnected: false,
    isConnecting: false,
    isReconnecting: false,
    isDisconnected: true,
  });
  const connectionRef = useRef<T | null>(null);
  const mountedRef = useRef(true);
  
  // Get base URL from environment or default
  const baseUrl = typeof window !== 'undefined' ? window.location.origin + '/api/core' : 'http://localhost:5005';

  const updateState = useCallback((updates: Partial<SignalRConnectionState>) => {
    if (mountedRef.current) {
      setState(prev => ({ ...prev, ...updates }));
    }
  }, []);

  const connect = useCallback(async () => {
    if (!virtualKey) {
      updateState({ error: new Error('Missing virtual key') });
      return;
    }

    if (connectionRef.current && (connectionRef.current as any).connection?.state === HubConnectionState.Connected) {
      return;
    }

    try {
      updateState({ isConnecting: true, isDisconnected: false, error: undefined });

      if (!connectionRef.current) {
        const newConnection = createConnection(baseUrl, virtualKey);
        connectionRef.current = newConnection;
        setConnection(newConnection);

        // Set up event handlers
        newConnection.onReconnecting = async (error?: Error) => {
          updateState({ 
            connectionState: HubConnectionState.Reconnecting,
            isReconnecting: true,
            isConnected: false,
            error 
          });
          options.onReconnecting?.(error);
        };

        newConnection.onReconnected = async () => {
          updateState({ 
            connectionState: HubConnectionState.Connected,
            isReconnecting: false,
            isConnected: true,
            isDisconnected: false,
            error: undefined 
          });
          options.onReconnected?.();
        };

        newConnection.onDisconnected = async (error?: Error) => {
          updateState({ 
            connectionState: HubConnectionState.Disconnected,
            isConnected: false,
            isConnecting: false,
            isReconnecting: false,
            isDisconnected: true,
            error 
          });
          options.onDisconnected?.(error);
        };
      }

      await connectionRef.current.start();
      
      updateState({ 
        connectionState: HubConnectionState.Connected,
        isConnected: true,
        isConnecting: false,
        isDisconnected: false,
        error: undefined 
      });
      
      options.onConnected?.();
    } catch (error) {
      updateState({ 
        connectionState: HubConnectionState.Disconnected,
        isConnected: false,
        isConnecting: false,
        isDisconnected: true,
        error: error as Error 
      });
      throw error;
    }
  }, [virtualKey, baseUrl, createConnection, options, updateState]);

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      try {
        await connectionRef.current.stop();
        connectionRef.current = null;
        setConnection(null);
        updateState({ 
          connectionState: HubConnectionState.Disconnected,
          isConnected: false,
          isDisconnected: true,
          error: undefined 
        });
      } catch (error) {
        updateState({ error: error as Error });
      }
    }
  }, [updateState]);

  // Auto-connect on mount if enabled
  useEffect(() => {
    mountedRef.current = true;
    
    if (options.enabled !== false) {
      connect().catch(error => {
        console.error(`Failed to connect to ${hubPath}:`, error);
      });
    }

    return () => {
      mountedRef.current = false;
      if (connectionRef.current) {
        connectionRef.current.stop().catch(error => {
          console.error(`Error stopping ${hubPath} connection:`, error);
        });
      }
    };
  }, [options.enabled]); // Only reconnect if enabled changes

  return {
    connection,
    state,
    connect,
    disconnect,
  };
}