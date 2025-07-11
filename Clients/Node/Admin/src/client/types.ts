export interface Logger {
  debug(message: string, ...args: unknown[]): void;
  info(message: string, ...args: unknown[]): void;
  warn(message: string, ...args: unknown[]): void;
  error(message: string, ...args: unknown[]): void;
}

export interface CacheProvider {
  get<T>(key: string): Promise<T | null>;
  set<T>(key: string, value: T, ttl?: number): Promise<void>;
  delete(key: string): Promise<void>;
  clear(): Promise<void>;
}

// Type definition for axios errors
export interface AxiosError {
  code?: string;
  message: string;
  response?: {
    status: number;
    data: unknown;
    headers: Record<string, string>;
  };
  request?: unknown;
  config?: {
    url?: string;
    method?: string;
    _retry?: number;
  };
}

export interface RetryConfig {
  maxRetries: number;
  retryDelay: number;
  retryCondition?: (error: AxiosError) => boolean;
}

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

export interface ConduitConfig {
  masterKey: string;
  adminApiUrl: string;
  conduitApiUrl?: string;
  options?: {
    timeout?: number;
    retries?: number | RetryConfig;
    logger?: Logger;
    cache?: CacheProvider;
    headers?: Record<string, string>;
    validateStatus?: (status: number) => boolean;
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
    onRequest?: (config: RequestConfigInfo) => void | Promise<void>;
    /**
     * Callback invoked after each response
     */
    onResponse?: (response: ResponseInfo) => void | Promise<void>;
  };
}

export interface RequestConfig {
  method?: string;
  url?: string;
  data?: unknown;
  params?: Record<string, unknown>;
  headers?: Record<string, string>;
  timeout?: number;
  responseType?: 'json' | 'text' | 'blob' | 'arraybuffer' | 'document' | 'stream';
}

export interface ApiClientConfig {
  baseUrl: string;
  masterKey: string;
  timeout?: number;
  retries?: number | RetryConfig;
  logger?: Logger;
  cache?: CacheProvider;
  defaultHeaders?: Record<string, string>;
  retryDelay?: number[];
  onError?: (error: Error) => void;
  onRequest?: (config: RequestConfigInfo) => void | Promise<void>;
  onResponse?: (response: ResponseInfo) => void | Promise<void>;
}

export interface RequestConfigInfo {
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
  config: RequestConfigInfo;
}