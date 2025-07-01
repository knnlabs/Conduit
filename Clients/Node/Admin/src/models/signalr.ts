/**
 * SignalR hub connection states
 */
export enum HubConnectionState {
  Disconnected = 'Disconnected',
  Connecting = 'Connecting',
  Connected = 'Connected',
  Disconnecting = 'Disconnecting',
  Reconnecting = 'Reconnecting',
}

/**
 * SignalR logging levels
 */
export enum SignalRLogLevel {
  Trace = 0,
  Debug = 1,
  Information = 2,
  Warning = 3,
  Error = 4,
  Critical = 5,
  None = 6,
}

/**
 * HTTP transport types for SignalR
 */
export enum HttpTransportType {
  None = 0,
  WebSockets = 1,
  ServerSentEvents = 2,
  LongPolling = 4,
}

/**
 * Default transport configuration
 */
export const DefaultTransports = 
  HttpTransportType.WebSockets | 
  HttpTransportType.ServerSentEvents | 
  HttpTransportType.LongPolling;

/**
 * SignalR endpoints for Admin API
 */
export const SignalREndpoints = {
  NavigationState: '/hubs/navigation-state',
  AdminNotifications: '/hubs/admin-notifications',
} as const;

/**
 * SignalR connection options
 */
export interface SignalRConnectionOptions {
  logLevel?: SignalRLogLevel;
  transport?: HttpTransportType;
  headers?: Record<string, string>;
  reconnectionDelay?: number[];
}

/**
 * Navigation state update event
 */
export interface NavigationStateUpdateEvent {
  timestamp: string;
  changedEntities: {
    modelMappings?: boolean;
    providers?: boolean;
    virtualKeys?: boolean;
    settings?: boolean;
  };
  summary: {
    totalProviders: number;
    enabledProviders: number;
    totalMappings: number;
    activeMappings: number;
    totalVirtualKeys: number;
    activeVirtualKeys: number;
  };
}

/**
 * Model discovered event
 */
export interface ModelDiscoveredEvent {
  providerId: number;
  providerName: string;
  model: {
    id: string;
    name: string;
    capabilities: string[];
    contextWindow?: number;
    maxOutput?: number;
  };
  timestamp: string;
}

/**
 * Provider health change event
 */
export interface ProviderHealthChangeEvent {
  providerId: number;
  providerName: string;
  previousStatus: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  currentStatus: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  healthScore: number;
  issues?: string[];
  timestamp: string;
}

/**
 * Virtual key event types
 */
export type VirtualKeyEventType = 'created' | 'updated' | 'deleted' | 'enabled' | 'disabled' | 'spend_updated';

/**
 * Virtual key event
 */
export interface VirtualKeyEvent {
  eventType: VirtualKeyEventType;
  virtualKeyId: number;
  virtualKeyHash: string;
  virtualKeyName?: string;
  changes?: {
    field: string;
    oldValue: any;
    newValue: any;
  }[];
  metadata?: {
    currentSpend?: number;
    spendLimit?: number;
    isEnabled?: boolean;
  };
  timestamp: string;
}

/**
 * Configuration change event
 */
export interface ConfigurationChangeEvent {
  category: string;
  setting: string;
  oldValue: any;
  newValue: any;
  changedBy?: string;
  timestamp: string;
}

/**
 * Admin notification event
 */
export interface AdminNotificationEvent {
  id: string;
  type: 'info' | 'warning' | 'error' | 'success';
  title: string;
  message: string;
  details?: any;
  actionRequired?: boolean;
  actions?: {
    label: string;
    action: string;
    data?: any;
  }[];
  timestamp: string;
}

/**
 * Hub server interfaces
 */
export interface INavigationStateHubServer {
  SubscribeToUpdates(groupName?: string): Promise<void>;
  UnsubscribeFromUpdates(groupName?: string): Promise<void>;
}

export interface IAdminNotificationHubServer {
  Subscribe(): Promise<void>;
  Unsubscribe(): Promise<void>;
  AcknowledgeNotification(notificationId: string): Promise<void>;
}

/**
 * Hub client interfaces
 */
export interface INavigationStateHubClient {
  onNavigationStateUpdate(callback: (event: NavigationStateUpdateEvent) => void): void;
  onModelDiscovered(callback: (event: ModelDiscoveredEvent) => void): void;
  onProviderHealthChange(callback: (event: ProviderHealthChangeEvent) => void): void;
}

export interface IAdminNotificationHubClient {
  onVirtualKeyEvent(callback: (event: VirtualKeyEvent) => void): void;
  onConfigurationChange(callback: (event: ConfigurationChangeEvent) => void): void;
  onAdminNotification(callback: (event: AdminNotificationEvent) => void): void;
}