import { NavigationStateHubClient } from '../signalr/NavigationStateHubClient';
import { SignalRConnectionOptions, HubConnectionState } from '../models/signalr';

/**
 * Service for managing SignalR connections to the Admin API
 */
export class SignalRService {
  private readonly baseUrl: string;
  private readonly masterKey: string;
  
  private navigationStateHub?: NavigationStateHubClient;
  
  private disposed = false;

  constructor(baseUrl: string, masterKey: string, _options?: SignalRConnectionOptions) {
    this.baseUrl = baseUrl;
    this.masterKey = masterKey;
  }

  /**
   * Gets or creates the navigation state hub connection
   */
  getOrCreateNavigationStateHub(): NavigationStateHubClient {
    if (!this.navigationStateHub) {
      this.navigationStateHub = new NavigationStateHubClient(this.baseUrl, this.masterKey);
      
      // Set up connection event handlers
      this.navigationStateHub.onConnected = async () => {
        console.warn('Navigation state hub connected');
      };
      
      this.navigationStateHub.onDisconnected = async (error) => {
        console.warn('Navigation state hub disconnected:', error?.message);
      };
      
      this.navigationStateHub.onReconnecting = async (error) => {
        console.warn('Navigation state hub reconnecting:', error?.message);
      };
      
      this.navigationStateHub.onReconnected = async () => {
        console.warn('Navigation state hub reconnected');
      };
    }
    
    return this.navigationStateHub;
  }


  /**
   * Gets a connection by type
   */
  getOrCreateConnection(type: 'navigation'): NavigationStateHubClient {
    switch (type) {
      case 'navigation':
        return this.getOrCreateNavigationStateHub();
      default:
        throw new Error(`Unknown connection type: ${type as string}`);
    }
  }

  /**
   * Connects all hubs
   */
  async connectAll(): Promise<void> {
    const promises: Promise<void>[] = [];
    
    // Create and start navigation state hub
    const navigationHub = this.getOrCreateNavigationStateHub();
    if (!navigationHub.isConnected) {
      promises.push(navigationHub.start());
    }
    
    await Promise.all(promises);
  }

  /**
   * Disconnects all hubs
   */
  async disconnectAll(): Promise<void> {
    const promises: Promise<void>[] = [];
    
    if (this.navigationStateHub) {
      promises.push(this.navigationStateHub.stop());
    }
    
    await Promise.all(promises);
  }

  /**
   * Checks if any hub is connected
   */
  isAnyConnected(): boolean {
    return (this.navigationStateHub?.isConnected ?? false);
  }

  /**
   * Gets the state of all connections
   */
  getConnectionStates(): Record<string, HubConnectionState> {
    return {
      navigationState: this.navigationStateHub?.state ?? HubConnectionState.Disconnected,
    };
  }

  /**
   * Disposes all SignalR connections
   */
  async dispose(): Promise<void> {
    if (!this.disposed) {
      const promises: Promise<void>[] = [];
      
      if (this.navigationStateHub) {
        promises.push(this.navigationStateHub.dispose());
      }
      
      await Promise.all(promises);
      
      this.navigationStateHub = undefined;
      this.disposed = true;
    }
  }
}