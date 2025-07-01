import { SignalRService } from './SignalRService';
import { NavigationStateHubClient } from '../signalr/NavigationStateHubClient';
import { AdminNotificationHubClient } from '../signalr/AdminNotificationHubClient';
import type {
  NavigationStateUpdateCallback,
  ModelDiscoveredCallback,
  ProviderHealthChangeCallback,
  VirtualKeyEventCallback,
  ConfigurationChangeCallback,
  AdminNotificationCallback,
  NotificationSubscription,
  AdminNotificationOptions,
  IRealtimeNotificationService
} from '../models/notifications';
import type {
  NavigationStateUpdateEvent,
  ModelDiscoveredEvent,
  ProviderHealthChangeEvent,
  VirtualKeyEvent,
  ConfigurationChangeEvent,
  AdminNotificationEvent
} from '../models/signalr';

/**
 * Service for managing real-time notifications through SignalR
 */
export class RealtimeNotificationsService implements IRealtimeNotificationService {
  private signalRService: SignalRService;
  private subscriptions: Map<string, NotificationSubscription> = new Map();
  private navigationStateHub?: NavigationStateHubClient;
  private adminNotificationHub?: AdminNotificationHubClient;
  private connectionStateCallbacks: Set<(state: 'connected' | 'disconnected' | 'reconnecting') => void> = new Set();

  constructor(signalRService: SignalRService) {
    this.signalRService = signalRService;
  }

  /**
   * Subscribe to navigation state updates
   */
  async onNavigationStateUpdate(
    callback: NavigationStateUpdateCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription> {
    // Ensure navigation state hub is initialized
    if (!this.navigationStateHub) {
      this.navigationStateHub = await this.signalRService.getOrCreateNavigationStateHub();
    }

    const subscriptionId = this.generateSubscriptionId();

    // Subscribe to navigation state updates
    this.navigationStateHub.onNavigationStateUpdate((event: NavigationStateUpdateEvent) => {
      // Apply filters if specified
      if (options?.filter?.providers) {
        const hasProvider = event.summary.enabledProviders > 0; // Simplified filter
        if (!hasProvider) return;
      }

      callback(event);
    });

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'navigationStateUpdate',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    // Handle connection state changes
    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    // Subscribe to updates on the hub
    await this.navigationStateHub.subscribeToUpdates();

    return subscription;
  }

  /**
   * Subscribe to model discovered events
   */
  async onModelDiscovered(
    callback: ModelDiscoveredCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription> {
    if (!this.navigationStateHub) {
      this.navigationStateHub = await this.signalRService.getOrCreateNavigationStateHub();
    }

    const subscriptionId = this.generateSubscriptionId();

    this.navigationStateHub.onModelDiscovered((event: ModelDiscoveredEvent) => {
      // Apply filters
      if (options?.filter?.providers && !options.filter.providers.includes(event.providerName)) {
        return;
      }

      callback(event);
    });

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'modelDiscovered',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to provider health changes
   */
  async onProviderHealthChange(
    callback: ProviderHealthChangeCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription> {
    if (!this.navigationStateHub) {
      this.navigationStateHub = await this.signalRService.getOrCreateNavigationStateHub();
    }

    const subscriptionId = this.generateSubscriptionId();

    this.navigationStateHub.onProviderHealthChange((event: ProviderHealthChangeEvent) => {
      // Apply filters
      if (options?.filter?.providers && !options.filter.providers.includes(event.providerName)) {
        return;
      }

      callback(event);
    });

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'providerHealthChange',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to virtual key events
   */
  async onVirtualKeyEvent(
    callback: VirtualKeyEventCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription> {
    if (!this.adminNotificationHub) {
      this.adminNotificationHub = await this.signalRService.getOrCreateAdminNotificationHub();
    }

    const subscriptionId = this.generateSubscriptionId();

    this.adminNotificationHub.onVirtualKeyEvent((event: VirtualKeyEvent) => {
      // Apply filters
      if (options?.filter?.virtualKeyIds && !options.filter.virtualKeyIds.includes(event.virtualKeyId)) {
        return;
      }

      callback(event);
    });

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'virtualKeyEvent',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to configuration changes
   */
  async onConfigurationChange(
    callback: ConfigurationChangeCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription> {
    if (!this.adminNotificationHub) {
      this.adminNotificationHub = await this.signalRService.getOrCreateAdminNotificationHub();
    }

    const subscriptionId = this.generateSubscriptionId();

    this.adminNotificationHub.onConfigurationChange((event: ConfigurationChangeEvent) => {
      // Apply filters
      if (options?.filter?.categories && !options.filter.categories.includes(event.category)) {
        return;
      }

      callback(event);
    });

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'configurationChange',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Subscribe to admin notifications
   */
  async onAdminNotification(
    callback: AdminNotificationCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription> {
    if (!this.adminNotificationHub) {
      this.adminNotificationHub = await this.signalRService.getOrCreateAdminNotificationHub();
    }

    const subscriptionId = this.generateSubscriptionId();

    this.adminNotificationHub.onAdminNotification((event: AdminNotificationEvent) => {
      // Apply filters
      if (options?.filter?.severity && !options.filter.severity.includes(event.type)) {
        return;
      }

      callback(event);
    });

    const subscription: NotificationSubscription = {
      id: subscriptionId,
      eventType: 'adminNotification',
      unsubscribe: () => this.unsubscribe(subscriptionId),
    };

    this.subscriptions.set(subscriptionId, subscription);

    if (options?.onConnectionStateChange) {
      this.connectionStateCallbacks.add(options.onConnectionStateChange);
    }

    return subscription;
  }

  /**
   * Unsubscribe from all notifications
   */
  async unsubscribeAll(): Promise<void> {
    const subscriptionIds = Array.from(this.subscriptions.keys());
    subscriptionIds.forEach(id => this.unsubscribe(id));
    this.connectionStateCallbacks.clear();
  }

  /**
   * Get all active subscriptions
   */
  getActiveSubscriptions(): NotificationSubscription[] {
    return Array.from(this.subscriptions.values());
  }

  /**
   * Connect to SignalR hubs
   */
  async connect(): Promise<void> {
    await this.signalRService.connectAll();
  }

  /**
   * Disconnect from SignalR hubs
   */
  async disconnect(): Promise<void> {
    await this.signalRService.disconnectAll();
  }

  /**
   * Check if connected to SignalR hubs
   */
  isConnected(): boolean {
    return this.signalRService.isAnyConnected();
  }

  /**
   * Acknowledge an admin notification
   */
  async acknowledgeNotification(notificationId: string): Promise<void> {
    if (!this.adminNotificationHub) {
      this.adminNotificationHub = await this.signalRService.getOrCreateAdminNotificationHub();
    }
    
    await this.adminNotificationHub.acknowledgeNotification(notificationId);
  }

  private unsubscribe(subscriptionId: string): void {
    this.subscriptions.delete(subscriptionId);
    
    // If no more subscriptions, we could disconnect, but we'll keep connections alive
    // for better performance in case of new subscriptions
  }

  private generateSubscriptionId(): string {
    return `sub_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }
}