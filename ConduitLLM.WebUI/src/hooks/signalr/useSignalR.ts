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
let signalRConfigPromise: Promise<{ coreUrl: string; adminUrl: string }> | null = null;

async function getSignalRConfig() {
  if (!signalRConfigPromise) {
    signalRConfigPromise = fetch('/api/signalr/config')
      .then(res => res.json())
      .catch(err => {
        console.error('Failed to fetch SignalR config:', err);
        throw err;
      });
  }
  return signalRConfigPromise;
}

async function getSignalRManager(): Promise<SignalRManager> {
  if (!globalSignalRManager) {
    const { coreUrl } = await getSignalRConfig();
    globalSignalRManager = new SignalRManager({
      baseUrl: coreUrl,
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

    const connectToHub = async () => {
      try {
        setIsConnecting(true);
        setError(null);
        
        managerRef.current = await getSignalRManager();
        
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