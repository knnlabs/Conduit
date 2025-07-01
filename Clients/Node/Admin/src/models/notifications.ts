import type {
  NavigationStateUpdateEvent,
  ModelDiscoveredEvent,
  ProviderHealthChangeEvent,
  VirtualKeyEvent,
  ConfigurationChangeEvent,
  AdminNotificationEvent
} from './signalr';

/**
 * Callback function types for event subscriptions
 */
export type NavigationStateUpdateCallback = (event: NavigationStateUpdateEvent) => void;
export type ModelDiscoveredCallback = (event: ModelDiscoveredEvent) => void;
export type ProviderHealthChangeCallback = (event: ProviderHealthChangeEvent) => void;
export type VirtualKeyEventCallback = (event: VirtualKeyEvent) => void;
export type ConfigurationChangeCallback = (event: ConfigurationChangeEvent) => void;
export type AdminNotificationCallback = (event: AdminNotificationEvent) => void;

/**
 * Subscription handle returned when subscribing to events
 */
export interface NotificationSubscription {
  /**
   * Unique identifier for this subscription
   */
  id: string;
  
  /**
   * The event type this subscription is for
   */
  eventType: 'navigationStateUpdate' | 'modelDiscovered' | 'providerHealthChange' | 
             'virtualKeyEvent' | 'configurationChange' | 'adminNotification';
  
  /**
   * Unsubscribe from this event
   */
  unsubscribe: () => void;
}

/**
 * Options for notification subscriptions
 */
export interface AdminNotificationOptions {
  /**
   * Whether to automatically reconnect on connection loss
   */
  autoReconnect?: boolean;
  
  /**
   * Filter events by specific criteria
   */
  filter?: {
    providers?: string[];
    virtualKeyIds?: number[];
    categories?: string[];
    severity?: ('info' | 'warning' | 'error' | 'success')[];
  };
  
  /**
   * Error handler for subscription errors
   */
  onError?: (error: Error) => void;
  
  /**
   * Handler for connection state changes
   */
  onConnectionStateChange?: (state: 'connected' | 'disconnected' | 'reconnecting') => void;
}

/**
 * Real-time notification service interface
 */
export interface IRealtimeNotificationService {
  /**
   * Subscribe to navigation state updates
   */
  onNavigationStateUpdate(
    callback: NavigationStateUpdateCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription>;
  
  /**
   * Subscribe to model discovered events
   */
  onModelDiscovered(
    callback: ModelDiscoveredCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription>;
  
  /**
   * Subscribe to provider health changes
   */
  onProviderHealthChange(
    callback: ProviderHealthChangeCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription>;
  
  /**
   * Subscribe to virtual key events
   */
  onVirtualKeyEvent(
    callback: VirtualKeyEventCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription>;
  
  /**
   * Subscribe to configuration changes
   */
  onConfigurationChange(
    callback: ConfigurationChangeCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription>;
  
  /**
   * Subscribe to admin notifications
   */
  onAdminNotification(
    callback: AdminNotificationCallback,
    options?: AdminNotificationOptions
  ): Promise<NotificationSubscription>;
  
  /**
   * Unsubscribe from all notifications
   */
  unsubscribeAll(): Promise<void>;
  
  /**
   * Connect to SignalR hubs
   */
  connect(): Promise<void>;
  
  /**
   * Disconnect from SignalR hubs
   */
  disconnect(): Promise<void>;
  
  /**
   * Check if connected to SignalR hubs
   */
  isConnected(): boolean;
}