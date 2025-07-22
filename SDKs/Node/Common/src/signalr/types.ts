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
 * Base SignalR connection options
 */
export interface SignalRConnectionOptions {
  /**
   * Logging level
   */
  logLevel?: SignalRLogLevel;
  
  /**
   * Transport types to use
   */
  transport?: HttpTransportType;
  
  /**
   * Headers to include with requests
   */
  headers?: Record<string, string>;
  
  /**
   * Access token factory for authentication
   */
  accessTokenFactory?: () => string | Promise<string>;
  
  /**
   * Close timeout in milliseconds
   */
  closeTimeout?: number;
  
  /**
   * Reconnection delay intervals in milliseconds
   */
  reconnectionDelay?: number[];
  
  /**
   * Server timeout in milliseconds
   */
  serverTimeout?: number;
  
  /**
   * Keep-alive interval in milliseconds
   */
  keepAliveInterval?: number;
}

/**
 * Authentication configuration for SignalR connections
 */
export interface SignalRAuthConfig {
  /**
   * Authentication token or key
   */
  authToken: string;
  
  /**
   * Authentication type (e.g., 'master', 'virtual')
   */
  authType: 'master' | 'virtual';
  
  /**
   * Additional headers for authentication
   */
  additionalHeaders?: Record<string, string>;
}

/**
 * SignalR hub method argument types for type safety
 */
export type SignalRPrimitive = string | number | boolean | null | undefined;
export type SignalRValue = SignalRPrimitive | SignalRArgs | SignalRPrimitive[];
export interface SignalRArgs {
  [key: string]: SignalRValue;
}