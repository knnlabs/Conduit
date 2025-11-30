// Import common client configuration types
import type {
  SignalRConfig,
  RetryConfig as CommonRetryConfig,
  RequestConfigInfo as CommonRequestConfig,
  ResponseInfo as CommonResponseInfo
} from '@knn_labs/conduit-common';

// Re-export for backward compatibility
export type { SignalRConfig };

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

// Core SDK specific RetryConfig (uses exponential backoff)
export interface RetryConfig extends CommonRetryConfig {
  maxRetries: number;
  initialDelay: number;  // Initial delay for exponential backoff
  maxDelay: number;      // Maximum delay between retries
  factor: number;        // Backoff multiplication factor
}

// Use common types with Core SDK naming
export interface RequestConfig extends CommonRequestConfig {
  method: string;
  url: string;
  headers: Record<string, string>;
  data?: unknown;
}

export interface ResponseInfo extends CommonResponseInfo {
  status: number;
  statusText: string;
  headers: Record<string, string>;
  data: unknown;
  config: RequestConfig;
}