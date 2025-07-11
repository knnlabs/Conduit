import type { SignalRLogLevel, HttpTransportType } from '../models/signalr';

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

export interface ClientConfig {
  apiKey: string;
  baseURL?: string;
  timeout?: number;
  maxRetries?: number;
  headers?: Record<string, string>;
  debug?: boolean;
  signalR?: SignalRConfig;
  /**
   * Custom retry delays in milliseconds
   * @default [1000, 2000, 4000, 8000, 16000]
   */
  retryDelay?: number[];
  /**
   * Callback invoked on any error
   */
  onError?: (error: Error) => void;
  /**
   * Callback invoked before each request
   */
  onRequest?: (config: RequestConfig) => void | Promise<void>;
  /**
   * Callback invoked after each response
   */
  onResponse?: (response: ResponseInfo) => void | Promise<void>;
}

export interface RequestOptions {
  signal?: AbortSignal;
  headers?: Record<string, string>;
  timeout?: number;
  correlationId?: string;
  responseType?: 'json' | 'text' | 'arraybuffer' | 'blob';
}

export interface RetryConfig {
  maxRetries: number;
  initialDelay: number;
  maxDelay: number;
  factor: number;
}

export interface RequestConfig {
  method: string;
  url: string;
  headers: Record<string, string>;
  data?: unknown;
}

export interface ResponseInfo {
  status: number;
  statusText: string;
  headers: Record<string, string>;
  data: unknown;
  config: RequestConfig;
}