export interface Logger {
  debug(message: string, ...args: any[]): void;
  info(message: string, ...args: any[]): void;
  warn(message: string, ...args: any[]): void;
  error(message: string, ...args: any[]): void;
}

export interface CacheProvider {
  get<T>(key: string): Promise<T | null>;
  set<T>(key: string, value: T, ttl?: number): Promise<void>;
  delete(key: string): Promise<void>;
  clear(): Promise<void>;
}

export interface RetryConfig {
  maxRetries: number;
  retryDelay: number;
  retryCondition?: (error: any) => boolean;
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
  data?: any;
  params?: any;
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