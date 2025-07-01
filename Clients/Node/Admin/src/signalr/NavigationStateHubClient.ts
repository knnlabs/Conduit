import { HubConnection } from '@microsoft/signalr';
import { BaseSignalRConnection } from './BaseSignalRConnection';
import { 
  SignalREndpoints, 
  NavigationStateUpdateEvent,
  ModelDiscoveredEvent,
  ProviderHealthChangeEvent,
  INavigationStateHubClient
} from '../models/signalr';

/**
 * SignalR client for navigation state updates
 */
export class NavigationStateHubClient extends BaseSignalRConnection implements INavigationStateHubClient {
  protected get hubPath(): string {
    return SignalREndpoints.NavigationState;
  }

  private navigationStateCallbacks: ((event: NavigationStateUpdateEvent) => void)[] = [];
  private modelDiscoveredCallbacks: ((event: ModelDiscoveredEvent) => void)[] = [];
  private providerHealthCallbacks: ((event: ProviderHealthChangeEvent) => void)[] = [];

  /**
   * Configures event handlers for the navigation state hub
   */
  protected configureHubHandlers(connection: HubConnection): void {
    // Navigation state update handler
    connection.on('NavigationStateUpdated', (event: NavigationStateUpdateEvent) => {
      this.navigationStateCallbacks.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Error in navigation state update callback:', error);
        }
      });
    });

    // Model discovered handler
    connection.on('ModelDiscovered', (event: ModelDiscoveredEvent) => {
      this.modelDiscoveredCallbacks.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Error in model discovered callback:', error);
        }
      });
    });

    // Provider health change handler
    connection.on('ProviderHealthChanged', (event: ProviderHealthChangeEvent) => {
      this.providerHealthCallbacks.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Error in provider health change callback:', error);
        }
      });
    });
  }

  /**
   * Subscribe to navigation state updates
   */
  onNavigationStateUpdate(callback: (event: NavigationStateUpdateEvent) => void): void {
    this.navigationStateCallbacks.push(callback);
  }

  /**
   * Subscribe to model discovered events
   */
  onModelDiscovered(callback: (event: ModelDiscoveredEvent) => void): void {
    this.modelDiscoveredCallbacks.push(callback);
  }

  /**
   * Subscribe to provider health changes
   */
  onProviderHealthChange(callback: (event: ProviderHealthChangeEvent) => void): void {
    this.providerHealthCallbacks.push(callback);
  }

  /**
   * Subscribe to updates for a specific group
   */
  async subscribeToUpdates(groupName?: string): Promise<void> {
    await this.invoke('SubscribeToUpdates', groupName);
  }

  /**
   * Unsubscribe from updates for a specific group
   */
  async unsubscribeFromUpdates(groupName?: string): Promise<void> {
    await this.invoke('UnsubscribeFromUpdates', groupName);
  }

  /**
   * Clear all callbacks
   */
  clearCallbacks(): void {
    this.navigationStateCallbacks = [];
    this.modelDiscoveredCallbacks = [];
    this.providerHealthCallbacks = [];
  }

  /**
   * Remove a specific callback
   */
  removeNavigationStateCallback(callback: (event: NavigationStateUpdateEvent) => void): void {
    const index = this.navigationStateCallbacks.indexOf(callback);
    if (index > -1) {
      this.navigationStateCallbacks.splice(index, 1);
    }
  }

  /**
   * Remove a specific model discovered callback
   */
  removeModelDiscoveredCallback(callback: (event: ModelDiscoveredEvent) => void): void {
    const index = this.modelDiscoveredCallbacks.indexOf(callback);
    if (index > -1) {
      this.modelDiscoveredCallbacks.splice(index, 1);
    }
  }

  /**
   * Remove a specific provider health callback
   */
  removeProviderHealthCallback(callback: (event: ProviderHealthChangeEvent) => void): void {
    const index = this.providerHealthCallbacks.indexOf(callback);
    if (index > -1) {
      this.providerHealthCallbacks.splice(index, 1);
    }
  }

  /**
   * Get the number of active callbacks
   */
  getActiveCallbackCount(): number {
    return this.navigationStateCallbacks.length + 
           this.modelDiscoveredCallbacks.length + 
           this.providerHealthCallbacks.length;
  }

  /**
   * Dispose the client
   */
  async dispose(): Promise<void> {
    this.clearCallbacks();
    await super.dispose();
  }
}