import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useConnectionStore } from '@/stores/useConnectionStore';
import { debugLog } from '@/lib/utils/logging';

export interface SignalRConfig {
  baseUrl: string;
  automaticReconnect?: boolean;
  reconnectInterval?: number;
  logLevel?: LogLevel;
}

export class SignalRManager {
  private connections = new Map<string, HubConnection>();
  private config: SignalRConfig;
  private connectionCallbacks = new Map<string, Set<(connected: boolean) => void>>();

  constructor(config: SignalRConfig) {
    this.config = {
      automaticReconnect: true,
      reconnectInterval: 5000,
      logLevel: LogLevel.Warning,
      ...config,
    };
  }

  async connectHub(hubName: string, hubPath: string): Promise<HubConnection> {
    const connectionKey = `${hubName}-${this.config.baseUrl}`;
    
    if (this.connections.has(connectionKey)) {
      const existingConnection = this.connections.get(connectionKey)!;
      if (existingConnection.state === 'Connected') {
        return existingConnection;
      }
    }

    // Get session token for authentication
    const getSessionToken = async (): Promise<string> => {
      try {
        const response = await fetch('/api/auth/session-token', {
          method: 'GET',
          credentials: 'include',
        });
        if (response.ok) {
          const data = await response.json();
          return data.token || '';
        }
      } catch (error) {
        console.warn('Failed to get session token for SignalR:', error);
      }
      return '';
    };

    const builder = new HubConnectionBuilder()
      .withUrl(`${this.config.baseUrl}${hubPath}`, {
        accessTokenFactory: getSessionToken,
      });
      
    if (this.config.automaticReconnect) {
      builder.withAutomaticReconnect([0, 2000, 10000, 30000]);
    }
    
    const connection = builder
      .configureLogging(this.config.logLevel!)
      .build();

    this.setupConnectionEvents(connection, hubName);
    this.connections.set(connectionKey, connection);

    try {
      useConnectionStore.getState().setSignalRStatus('connecting');
      await connection.start();
      useConnectionStore.getState().setSignalRStatus('connected');
      debugLog(`Connected to ${hubName} hub`);
      this.notifyConnectionChange(hubName, true);
    } catch (error) {
      console.error(`Failed to connect to ${hubName} hub:`, error);
      useConnectionStore.getState().setSignalRStatus('error');
      this.notifyConnectionChange(hubName, false);
    }

    return connection;
  }

  private setupConnectionEvents(connection: HubConnection, hubName: string) {
    connection.onreconnecting(() => {
      debugLog(`${hubName} hub reconnecting...`);
      useConnectionStore.getState().setSignalRStatus('reconnecting');
      this.notifyConnectionChange(hubName, false);
    });

    connection.onreconnected(() => {
      debugLog(`${hubName} hub reconnected`);
      useConnectionStore.getState().setSignalRStatus('connected');
      this.notifyConnectionChange(hubName, true);
    });

    connection.onclose((error) => {
      debugLog(`${hubName} hub disconnected:`, error);
      useConnectionStore.getState().setSignalRStatus('disconnected');
      this.notifyConnectionChange(hubName, false);
    });
  }

  private notifyConnectionChange(hubName: string, connected: boolean) {
    const callbacks = this.connectionCallbacks.get(hubName);
    if (callbacks) {
      callbacks.forEach(callback => callback(connected));
    }
  }

  onConnectionChange(hubName: string, callback: (connected: boolean) => void) {
    if (!this.connectionCallbacks.has(hubName)) {
      this.connectionCallbacks.set(hubName, new Set());
    }
    this.connectionCallbacks.get(hubName)!.add(callback);

    // Return unsubscribe function
    return () => {
      this.connectionCallbacks.get(hubName)?.delete(callback);
    };
  }

  getConnection(hubName: string): HubConnection | undefined {
    const connectionKey = `${hubName}-${this.config.baseUrl}`;
    return this.connections.get(connectionKey);
  }

  async disconnectHub(hubName: string): Promise<void> {
    const connectionKey = `${hubName}-${this.config.baseUrl}`;
    const connection = this.connections.get(connectionKey);
    
    if (connection) {
      await connection.stop();
      this.connections.delete(connectionKey);
      this.connectionCallbacks.delete(hubName);
    }
  }

  async disconnectAll(): Promise<void> {
    const disconnectPromises = Array.from(this.connections.entries()).map(
      async ([key, connection]) => {
        try {
          await connection.stop();
        } catch (error) {
          console.warn(`Error disconnecting ${key}:`, error);
        }
      }
    );

    await Promise.allSettled(disconnectPromises);
    this.connections.clear();
    this.connectionCallbacks.clear();
    useConnectionStore.getState().setSignalRStatus('disconnected');
  }

  isConnected(hubName: string): boolean {
    const connection = this.getConnection(hubName);
    return connection?.state === 'Connected';
  }

  getConnectionState(hubName: string): string {
    const connection = this.getConnection(hubName);
    return connection?.state || 'Disconnected';
  }
}