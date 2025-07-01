import { BaseClient } from '../client/BaseClient';
import { SignalRService } from './SignalRService';
import { HubConnectionState } from '../models/signalr';
import type { SignalRConfig } from '../client/types';
import axios from 'axios';

/**
 * Service for managing connections and health checks.
 * Provides methods to connect, disconnect, and monitor SignalR hub connections,
 * as well as lightweight health checks for API connectivity.
 */
export class ConnectionService {
  private readonly client: BaseClient;
  private signalRService: SignalRService | null = null;

  constructor(client: BaseClient) {
    this.client = client;
  }

  /**
   * Initializes the SignalR service if not already initialized.
   * @internal
   */
  initializeSignalR(signalRService: SignalRService): void {
    this.signalRService = signalRService;
  }

  /**
   * Connects all SignalR hubs.
   * @returns Promise that resolves when all connections are established
   * @throws Error if SignalR is not initialized
   */
  async connect(): Promise<void> {
    this.ensureSignalRInitialized();
    await this.signalRService!.startAllConnections();
  }

  /**
   * Disconnects all SignalR hubs.
   * @returns Promise that resolves when all connections are closed
   * @throws Error if SignalR is not initialized
   */
  async disconnect(): Promise<void> {
    this.ensureSignalRInitialized();
    await this.signalRService!.stopAllConnections();
  }

  /**
   * Gets the current connection status of all SignalR hubs.
   * @returns Object with hub names as keys and their connection states as values
   * @throws Error if SignalR is not initialized
   */
  getStatus(): Record<string, HubConnectionState> {
    this.ensureSignalRInitialized();
    return this.signalRService!.getConnectionStatus();
  }

  /**
   * Checks if all SignalR connections are established.
   * @returns true if all connections are established, false otherwise
   * @throws Error if SignalR is not initialized
   */
  isConnected(): boolean {
    this.ensureSignalRInitialized();
    return this.signalRService!.areAllConnectionsEstablished();
  }

  /**
   * Waits for all SignalR connections to be established.
   * @param timeoutMs - Maximum time to wait in milliseconds (default: 30000)
   * @returns Promise that resolves to true if all connections are established, false if timeout
   * @throws Error if SignalR is not initialized
   */
  async waitForConnection(timeoutMs = 30000): Promise<boolean> {
    this.ensureSignalRInitialized();
    return this.signalRService!.waitForAllConnections(timeoutMs);
  }

  /**
   * Reconnects all SignalR hubs by disconnecting and then connecting again.
   * @returns Promise that resolves when reconnection is complete
   * @throws Error if SignalR is not initialized
   */
  async reconnect(): Promise<void> {
    await this.disconnect();
    await this.connect();
  }

  /**
   * Updates SignalR configuration dynamically.
   * Note: This requires reconnecting to apply the new configuration.
   * @param config - New SignalR configuration
   * @returns Promise that resolves when configuration is updated and reconnected
   */
  async updateConfiguration(config: SignalRConfig): Promise<void> {
    this.ensureSignalRInitialized();
    
    // Store current connection state
    const wasConnected = this.isConnected();
    
    // Disconnect if currently connected
    if (wasConnected) {
      await this.disconnect();
    }
    
    // Update the configuration on the client
    const currentConfig = this.client.getConfig();
    currentConfig.signalR = { ...currentConfig.signalR, ...config };
    
    // Reconnect if was previously connected and auto-connect is not disabled
    if (wasConnected && config.enabled !== false && config.autoConnect !== false) {
      await this.connect();
    }
  }

  /**
   * Gets detailed connection information for each hub.
   * @returns Array of connection details including hub name, state, and connection info
   */
  getDetailedStatus(): Array<{
    hub: string;
    state: HubConnectionState;
    stateDescription: string;
    isConnected: boolean;
  }> {
    this.ensureSignalRInitialized();
    
    const status = this.getStatus();
    const stateDescriptions: Record<HubConnectionState, string> = {
      [HubConnectionState.Disconnected]: 'Not connected',
      [HubConnectionState.Connecting]: 'Establishing connection',
      [HubConnectionState.Connected]: 'Connected and ready',
      [HubConnectionState.Disconnecting]: 'Closing connection',
      [HubConnectionState.Reconnecting]: 'Connection lost, attempting to reconnect',
    };
    
    return Object.entries(status).map(([hub, state]) => ({
      hub,
      state,
      stateDescription: stateDescriptions[state] || 'Unknown',
      isConnected: state === HubConnectionState.Connected,
    }));
  }

  /**
   * Subscribes to connection state changes.
   * @param callback - Function to call when connection state changes
   * @returns Unsubscribe function
   */
  onConnectionStateChange(_callback: (hub: string, state: HubConnectionState) => void): () => void {
    // This would require extending the SignalR service to support state change callbacks
    // For now, we'll return a no-op unsubscribe function
    console.warn('Connection state change subscriptions are not yet implemented');
    return () => {};
  }

  /**
   * Ensures SignalR service is initialized.
   * @throws Error if SignalR is not initialized
   */
  private ensureSignalRInitialized(): void {
    if (!this.signalRService) {
      throw new Error('SignalR service is not initialized. Make sure the client is properly configured.');
    }
  }

  /**
   * Performs a lightweight health check to verify the API is reachable.
   * This method does not require authentication and is ideal for connection monitoring.
   * @returns Promise<boolean> True if the API is reachable, false otherwise
   */
  async ping(): Promise<boolean> {
    try {
      // Create a separate axios instance without authentication for ping
      const pingClient = axios.create({
        baseURL: this.client['config'].baseURL,
        timeout: 5000,
        headers: {
          'Accept': 'application/json',
        }
      });
      
      const response = await pingClient.get('/health/ready');
      return response.status === 200;
    } catch {
      return false;
    }
  }

  /**
   * Performs a lightweight health check with a custom timeout.
   * This method does not require authentication and is ideal for connection monitoring.
   * @param timeoutMs - Custom timeout in milliseconds
   * @returns Promise<boolean> True if the API is reachable within the timeout, false otherwise
   */
  async pingWithTimeout(timeoutMs: number): Promise<boolean> {
    if (timeoutMs <= 0) {
      throw new Error('Timeout must be greater than 0');
    }

    try {
      // Create a separate axios instance without authentication for ping
      const pingClient = axios.create({
        baseURL: this.client['config'].baseURL,
        timeout: timeoutMs,
        headers: {
          'Accept': 'application/json',
        }
      });
      
      const response = await pingClient.get('/health/ready');
      return response.status === 200;
    } catch {
      return false;
    }
  }
}