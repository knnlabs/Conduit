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
 * Base retry configuration interface
 * 
 * Note: The Admin and Core SDKs have different retry strategies:
 * - Admin SDK uses simple fixed delay retry
 * - Core SDK uses exponential backoff
 * 
 * This base interface supports both patterns.
 */
export interface RetryConfig {
  /**
   * Maximum number of retry attempts
   */
  maxRetries: number;
  
  /**
   * For Admin SDK: Fixed delay between retries in milliseconds
   * For Core SDK: Initial delay for exponential backoff
   */
  retryDelay?: number;
  
  /**
   * For Core SDK: Initial delay for exponential backoff
   */
  initialDelay?: number;
  
  /**
   * For Core SDK: Maximum delay between retries
   */
  maxDelay?: number;
  
  /**
   * For Core SDK: Backoff multiplication factor
   */
  factor?: number;
  
  /**
   * Custom retry condition function
   */
  retryCondition?: (error: unknown) => boolean;
}

/**
 * HTTP error interface
 */
export interface HttpError {
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

/**
 * Request configuration information
 */
export interface RequestConfigInfo {
  method: string;
  url: string;
  headers: Record<string, string>;
  data?: unknown;
  params?: Record<string, unknown>;
}

/**
 * Response information
 */
export interface ResponseInfo {
  status: number;
  statusText: string;
  headers: Record<string, string>;
  data: unknown;
  config: RequestConfigInfo;
}

/**
 * Base client lifecycle callbacks
 */
export interface ClientLifecycleCallbacks {
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
}

/**
 * Base client configuration options
 */
export interface BaseClientOptions extends ClientLifecycleCallbacks {
  /**
   * Request timeout in milliseconds
   */
  timeout?: number;
  
  /**
   * Retry configuration
   */
  retries?: number | RetryConfig;
  
  /**
   * Logger instance for client logging
   */
  logger?: Logger;
  
  /**
   * Cache provider for response caching
   */
  cache?: CacheProvider;
  
  /**
   * Custom headers to include with all requests
   */
  headers?: Record<string, string>;
  
  /**
   * Custom retry delays in milliseconds (overrides retry config)
   * @default [1000, 2000, 4000, 8000, 16000]
   */
  retryDelay?: number[];
  
  /**
   * Custom function to validate response status
   */
  validateStatus?: (status: number) => boolean;
  
  /**
   * Enable debug mode
   */
  debug?: boolean;
}