// Import common client configuration types
import {
  RetryConfig as CommonRetryConfig,
  ResponseInfo as CommonResponseInfo
} from '@knn_labs/conduit-common';

// Define types locally to avoid bundler issues with type-only exports
/**
 * Logger interface for client logging
 */
export interface Logger {
  debug(message: string, ...args: unknown[]): void;
  info(message: string, ...args: unknown[]): void;
  warn(message: string, ...args: unknown[]): void;
  error(message: string, ...args: unknown[]): void;
}

/**
 * Cache provider interface for client-side caching
 */
export interface CacheProvider {
  get<T>(key: string): Promise<T | null>;
  set<T>(key: string, value: T, ttl?: number): Promise<void>;
  delete(key: string): Promise<void>;
  clear(): Promise<void>;
}

/**
 * HTTP error class
 */
export class HttpError extends Error {
  public code?: string;
  public response?: {
    status: number;
    data: unknown;
    headers: Record<string, string>;
  };
  public request?: unknown;
  public config?: {
    url?: string;
    method?: string;
    _retry?: number;
  };

  constructor(message: string, code?: string) {
    super(message);
    this.name = 'HttpError';
    this.code = code;
  }
}

/**
 * SignalR client configuration
 */
export interface SignalRConfig {
  enabled?: boolean;
  autoConnect?: boolean;
  reconnectDelay?: number[];
  logLevel?: any; // SignalRLogLevel from Common
  transport?: any; // HttpTransportType from Common
  headers?: Record<string, string>;
  connectionTimeout?: number;
}

/**
 * Request configuration info for callbacks
 */
export interface RequestConfigInfo {
  method: string;
  url: string;
  headers: Record<string, string>;
  data?: unknown;
  params?: Record<string, unknown>;
}

// Admin SDK specific RetryConfig (uses fixed delay)
export interface RetryConfig extends CommonRetryConfig {
  maxRetries: number;
  retryDelay: number;  // Fixed delay between retries
  retryCondition?: (error: unknown) => boolean;
}

// SignalRConfig now imported from Common package above

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
  signal?: AbortSignal;
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

// Extend ResponseInfo to maintain compatibility
export interface ResponseInfo extends CommonResponseInfo {
  status: number;
  statusText: string;
  headers: Record<string, string>;
  data: unknown;
  config: RequestConfigInfo;
}