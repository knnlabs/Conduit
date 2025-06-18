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
  };
}

export interface RequestConfig {
  method: string;
  url: string;
  data?: unknown;
  params?: Record<string, unknown>;
  headers?: Record<string, string>;
  timeout?: number;
}

export interface ApiClientConfig {
  baseUrl: string;
  masterKey: string;
  timeout?: number;
  retries?: number | RetryConfig;
  logger?: Logger;
  cache?: CacheProvider;
  defaultHeaders?: Record<string, string>;
}