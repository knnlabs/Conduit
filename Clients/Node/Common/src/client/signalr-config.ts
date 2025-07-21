import type { SignalRLogLevel, HttpTransportType } from '../signalr';

/**
 * SignalR client configuration
 */
export interface SignalRConfig {
  /**
   * Whether SignalR is enabled
   * @default true
   */
  enabled?: boolean;
  
  /**
   * Whether to automatically connect on client initialization
   * @default true
   */
  autoConnect?: boolean;
  
  /**
   * Reconnection delays in milliseconds (exponential backoff)
   * @default [0, 2000, 10000, 30000]
   */
  reconnectDelay?: number[];
  
  /**
   * SignalR logging level
   * @default SignalRLogLevel.Information
   */
  logLevel?: SignalRLogLevel;
  
  /**
   * HTTP transport type
   * @default HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling
   */
  transport?: HttpTransportType;
  
  /**
   * Custom headers for SignalR connections
   */
  headers?: Record<string, string>;
  
  /**
   * Connection timeout in milliseconds
   * @default 30000
   */
  connectionTimeout?: number;
}