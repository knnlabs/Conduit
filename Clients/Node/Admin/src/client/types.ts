// Import common client configuration types
import {
  Logger,
  CacheProvider,
  HttpError,
  RetryConfig as CommonRetryConfig,
  SignalRConfig,
  RequestConfigInfo,
  ResponseInfo as CommonResponseInfo
} from '@knn_labs/conduit-common';

// Re-export for backward compatibility
export { Logger, CacheProvider, HttpError, SignalRConfig, RequestConfigInfo };

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