'use client';

import { useEffect, useRef, useState } from 'react';
import { HubConnection } from '@microsoft/signalr';
import { SignalRManager } from '@/lib/signalr/SignalRManager';
import { config } from '@/config';

interface UseSignalROptions {
  hubName: string;
  hubPath: string;
  enabled?: boolean;
}

interface UseSignalRReturn {
  connection: HubConnection | null;
  isConnected: boolean;
  isConnecting: boolean;
  error: string | null;
  reconnect: () => Promise<void>;
  disconnect: () => Promise<void>;
}

// Global SignalR manager instance
let globalSignalRManager: SignalRManager | null = null;

function getSignalRManager(): SignalRManager {
  if (!globalSignalRManager) {
    // Connect directly to Conduit Core API but with session-based auth
    const baseUrl = config.api.public.coreUrl;
    globalSignalRManager = new SignalRManager({
      baseUrl,
      automaticReconnect: config.signalr.autoReconnect,
      reconnectInterval: config.signalr.reconnectInterval,
    });
  }
  return globalSignalRManager;
}

export function useSignalR({
  hubName,
  hubPath,
  enabled = true,
}: UseSignalROptions): UseSignalRReturn {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const managerRef = useRef<SignalRManager | null>(null);
  const unsubscribeRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    if (!enabled) return;

    managerRef.current = getSignalRManager();

    const connectToHub = async () => {
      if (!managerRef.current) return;

      try {
        setIsConnecting(true);
        setError(null);
        
        const hubConnection = await managerRef.current.connectHub(hubName, hubPath);
        setConnection(hubConnection);
        
        // Subscribe to connection state changes
        unsubscribeRef.current = managerRef.current.onConnectionChange(
          hubName,
          (connected) => {
            setIsConnected(connected);
            if (!connected) {
              setError('Connection lost');
            } else {
              setError(null);
            }
          }
        );
        
        setIsConnected(hubConnection.state === 'Connected');
      } catch (err: unknown) {
        console.error(`Failed to connect to ${hubName} hub:`, err);
        setError(err instanceof Error ? err.message : 'Connection failed');
        setIsConnected(false);
      } finally {
        setIsConnecting(false);
      }
    };

    connectToHub();

    return () => {
      if (unsubscribeRef.current) {
        unsubscribeRef.current();
        unsubscribeRef.current = null;
      }
    };
  }, [hubName, hubPath, enabled]);

  const reconnect = async () => {
    if (!managerRef.current) return;

    try {
      setIsConnecting(true);
      setError(null);
      
      await managerRef.current.disconnectHub(hubName);
      const hubConnection = await managerRef.current.connectHub(hubName, hubPath);
      setConnection(hubConnection);
      setIsConnected(hubConnection.state === 'Connected');
    } catch (err: unknown) {
      console.error(`Failed to reconnect to ${hubName} hub:`, err);
      setError(err instanceof Error ? err.message : 'Reconnection failed');
      setIsConnected(false);
    } finally {
      setIsConnecting(false);
    }
  };

  const disconnect = async () => {
    if (!managerRef.current) return;

    try {
      await managerRef.current.disconnectHub(hubName);
      setConnection(null);
      setIsConnected(false);
      setError(null);
    } catch (err: unknown) {
      console.error(`Failed to disconnect from ${hubName} hub:`, err);
      setError(err instanceof Error ? err.message : 'Disconnection failed');
    }
  };

  return {
    connection,
    isConnected,
    isConnecting,
    error,
    reconnect,
    disconnect,
  };
}

// Cleanup on page unload
if (typeof window !== 'undefined') {
  window.addEventListener('beforeunload', () => {
    if (globalSignalRManager) {
      globalSignalRManager.disconnectAll();
    }
  });
}