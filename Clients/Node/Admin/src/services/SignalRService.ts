import { NavigationStateHubClient } from '../signalr/NavigationStateHubClient';
import { AdminNotificationHubClient } from '../signalr/AdminNotificationHubClient';
import { SignalRConnectionOptions, HubConnectionState } from '../models/signalr';

/**
 * Service for managing SignalR connections to the Admin API
 */
export class SignalRService {
  private readonly baseUrl: string;
  private readonly masterKey: string;
  
  private navigationStateHub?: NavigationStateHubClient;
  private adminNotificationHub?: AdminNotificationHubClient;
  
  private disposed = false;

  constructor(baseUrl: string, masterKey: string, _options?: SignalRConnectionOptions) {
    this.baseUrl = baseUrl;
    this.masterKey = masterKey;
  }

  /**
   * Gets or creates the navigation state hub connection
   */
  async getOrCreateNavigationStateHub(): Promise<NavigationStateHubClient> {
    if (!this.navigationStateHub) {
      this.navigationStateHub = new NavigationStateHubClient(this.baseUrl, this.masterKey);
      
      // Set up connection event handlers
      this.navigationStateHub.onConnected = async () => {
        console.log('Navigation state hub connected');
      };
      
      this.navigationStateHub.onDisconnected = async (error) => {
        console.warn('Navigation state hub disconnected:', error?.message);
      };
      
      this.navigationStateHub.onReconnecting = async (error) => {
        console.log('Navigation state hub reconnecting:', error?.message);
      };
      
      this.navigationStateHub.onReconnected = async () => {
        console.log('Navigation state hub reconnected');
      };
    }
    
    return this.navigationStateHub;
  }

  /**
   * Gets or creates the admin notification hub connection
   */
  async getOrCreateAdminNotificationHub(): Promise<AdminNotificationHubClient> {
    if (!this.adminNotificationHub) {
      this.adminNotificationHub = new AdminNotificationHubClient(this.baseUrl, this.masterKey);
      
      // Set up connection event handlers
      this.adminNotificationHub.onConnected = async () => {
        console.log('Admin notification hub connected');
        // Auto-subscribe to notifications when connected
        await this.adminNotificationHub!.subscribe();
      };
      
      this.adminNotificationHub.onDisconnected = async (error) => {
        console.warn('Admin notification hub disconnected:', error?.message);
      };
      
      this.adminNotificationHub.onReconnecting = async (error) => {
        console.log('Admin notification hub reconnecting:', error?.message);
      };
      
      this.adminNotificationHub.onReconnected = async () => {
        console.log('Admin notification hub reconnected');
        // Re-subscribe after reconnection
        await this.adminNotificationHub!.subscribe();
      };
    }
    
    return this.adminNotificationHub;
  }

  /**
   * Gets a connection by type
   */
  async getOrCreateConnection(type: 'navigation' | 'notifications'): Promise<NavigationStateHubClient | AdminNotificationHubClient> {
    switch (type) {
      case 'navigation':
        return this.getOrCreateNavigationStateHub();
      case 'notifications':
        return this.getOrCreateAdminNotificationHub();
      default:
        throw new Error(`Unknown connection type: ${type}`);
    }
  }

  /**
   * Connects all hubs
   */
  async connectAll(): Promise<void> {
    const promises: Promise<void>[] = [];
    
    // Create and start navigation state hub
    const navigationHub = await this.getOrCreateNavigationStateHub();
    if (!navigationHub.isConnected) {
      promises.push(navigationHub.start());
    }
    
    // Create and start admin notification hub
    const notificationHub = await this.getOrCreateAdminNotificationHub();
    if (!notificationHub.isConnected) {
      promises.push(notificationHub.start());
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
    
    if (this.adminNotificationHub) {
      promises.push(this.adminNotificationHub.stop());
    }
    
    await Promise.all(promises);
  }

  /**
   * Checks if any hub is connected
   */
  isAnyConnected(): boolean {
    return (this.navigationStateHub?.isConnected || false) ||
           (this.adminNotificationHub?.isConnected || false);
  }

  /**
   * Gets the state of all connections
   */
  getConnectionStates(): Record<string, HubConnectionState> {
    return {
      navigationState: this.navigationStateHub?.state || HubConnectionState.Disconnected,
      adminNotifications: this.adminNotificationHub?.state || HubConnectionState.Disconnected,
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
      
      if (this.adminNotificationHub) {
        promises.push(this.adminNotificationHub.dispose());
      }
      
      await Promise.all(promises);
      
      this.navigationStateHub = undefined;
      this.adminNotificationHub = undefined;
      this.disposed = true;
    }
  }
}