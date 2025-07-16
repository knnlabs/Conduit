// Import common SignalR types from Common package
import {
  HubConnectionState,
  SignalRLogLevel,
  HttpTransportType,
  DefaultTransports,
  SignalRConnectionOptions
} from '@knn_labs/conduit-common';

// Re-export for backward compatibility
export {
  HubConnectionState,
  SignalRLogLevel,
  HttpTransportType,
  DefaultTransports,
  SignalRConnectionOptions
};

/**
 * SignalR endpoints for Admin API
 */
export const SignalREndpoints = {
  NavigationState: '/hubs/navigation-state',
  AdminNotifications: '/hubs/admin-notifications',
} as const;

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
  SubscribeToVirtualKey(virtualKeyId: number): Promise<void>;
  UnsubscribeFromVirtualKey(virtualKeyId: number): Promise<void>;
  SubscribeToProvider(providerName: string): Promise<void>;
  UnsubscribeFromProvider(providerName: string): Promise<void>;
  RefreshProviderHealth(): Promise<void>;
  AcknowledgeNotification?(notificationId: string): Promise<void>;
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