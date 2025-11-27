// Import common SignalR types from Common package
import {
  HubConnectionState,
  SignalRLogLevel,
  HttpTransportType,
  DefaultTransports
} from '@knn_labs/conduit-common';

import type { ConfigValue, ExtendedMetadata } from './common-types';

// Re-export for backward compatibility
export {
  HubConnectionState,
  SignalRLogLevel,
  HttpTransportType,
  DefaultTransports
};

// Define SignalRConnectionOptions locally to avoid bundler issues
export interface SignalRConnectionOptions {
  hubUrl: string;
  masterKey?: string;
  virtualKey?: string;
  accessToken?: string | (() => string | Promise<string>);
  transport?: HttpTransportType;
  logLevel?: SignalRLogLevel;
  withCredentials?: boolean;
  headers?: Record<string, string>;
  skipNegotiation?: boolean;
  reconnectDelay?: number[];
  connectionTimeout?: number;
  onConnectionStateChanged?: (state: HubConnectionState) => void;
  onReconnecting?: (error?: Error) => void;
  onReconnected?: (connectionId?: string) => void;
  onClose?: (error?: Error) => void;
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
    oldValue: ConfigValue;
    newValue: ConfigValue;
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
  oldValue: ConfigValue;
  newValue: ConfigValue;
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
  details?: ExtendedMetadata;
  actionRequired?: boolean;
  actions?: {
    label: string;
    action: string;
    data?: ExtendedMetadata;
  }[];
  timestamp: string;
}