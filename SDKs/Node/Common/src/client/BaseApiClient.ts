import type { 
  BaseClientOptions,
  RetryConfig,
  Logger,
  CacheProvider,
  RequestConfigInfo,
  ResponseInfo
} from './types';
import { HttpMethod } from '../http';

export interface BaseClientConfig extends BaseClientOptions {
  baseURL: string;
}

export interface BaseRequestOptions {
  headers?: Record<string, string>;
  signal?: AbortSignal;
  timeout?: number;
  responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
}

/**
 * Abstract base API client for Conduit SDKs
 * Provides common HTTP functionality with authentication handled by subclasses
 */
export abstract class BaseApiClient {
  protected readonly config: Required<Omit<BaseClientConfig, 'logger' | 'cache' | 'onError' | 'onRequest' | 'onResponse'>> & 
    Pick<BaseClientConfig, 'logger' | 'cache' | 'onError' | 'onRequest' | 'onResponse'>;
  protected readonly retryConfig: RetryConfig;
  protected readonly logger?: Logger;
  protected readonly cache?: CacheProvider;

  constructor(config: BaseClientConfig) {
    this.config = {
      baseURL: config.baseURL.replace(/\/$/, ''), // Remove trailing slash
      timeout: config.timeout ?? 30000,
      retries: config.retries ?? 3,
      headers: config.headers ?? {},
      debug: config.debug ?? false,
      retryDelay: config.retryDelay ?? [1000, 2000, 4000, 8000, 16000],
      validateStatus: config.validateStatus ?? ((status) => status >= 200 && status < 300),
      logger: config.logger,
      cache: config.cache,
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse,
    };

    this.logger = config.logger;
    this.cache = config.cache;
    this.retryConfig = this.normalizeRetryConfig(config.retries);
  }

  /**
   * Abstract method for SDK-specific authentication headers
   * Must be implemented by Core and Admin SDK clients
   */
  protected abstract getAuthHeaders(): Record<string, string>;
  
  /**
   * Get base URL for services that need direct access
   */
  public getBaseURL(): string {
    return this.config.baseURL;
  }
  
  /**
   * Get timeout for services that need direct access
   */
  public getTimeout(): number {
    return this.config.timeout;
  }

  /**
   * Type-safe request method with proper request/response typing
   */
  protected async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: BaseRequestOptions & { 
      method?: HttpMethod; 
      body?: TRequest;
    } = {}
  ): Promise<TResponse> {
    const fullUrl = this.buildUrl(url);
    const controller = new AbortController();
    
    // Set up timeout
    const timeoutId = options.timeout ?? this.config.timeout
      ? setTimeout(() => controller.abort(), options.timeout ?? this.config.timeout)
      : undefined;

    try {
      const requestConfig: RequestConfigInfo = {
        method: options.method ?? HttpMethod.GET,
        url: fullUrl,
        headers: this.buildHeaders(options.headers),
        data: options.body,
      };

      // Call onRequest callback if provided
      if (this.config.onRequest) {
        await this.config.onRequest(requestConfig);
      }

      if (this.config.debug) {
        this.log('debug', `[Conduit] ${requestConfig.method} ${requestConfig.url}`);
      }

      const response = await this.executeWithRetry<TResponse, TRequest>(
        fullUrl,
        {
          method: requestConfig.method,
          headers: requestConfig.headers,
          body: options.body ? JSON.stringify(options.body) : undefined,
          signal: options.signal ?? controller.signal,
        },
        options
      );

      return response;
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    }
  }

  /**
   * Type-safe GET request with support for query parameters
   */
  protected async get<TResponse = unknown>(
    url: string,
    paramsOrOptions?: Record<string, unknown> | BaseRequestOptions,
    options?: BaseRequestOptions
  ): Promise<TResponse> {
    // If third argument is provided, second arg is params
    if (options) {
      const urlWithParams = this.buildUrlWithParams(url, paramsOrOptions as Record<string, unknown>);
      return this.request<TResponse>(urlWithParams, { ...options, method: HttpMethod.GET });
    }
    
    // Check if it's options (has headers/signal/timeout/responseType) or params
    const isOptions = paramsOrOptions && 
      ('headers' in paramsOrOptions || 'signal' in paramsOrOptions || 
       'timeout' in paramsOrOptions || 'responseType' in paramsOrOptions);
    
    if (isOptions) {
      return this.request<TResponse>(url, { 
        ...(paramsOrOptions as BaseRequestOptions), 
        method: HttpMethod.GET 
      });
    } else if (paramsOrOptions) {
      // It's params - add them to the URL
      const urlWithParams = this.buildUrlWithParams(url, paramsOrOptions as Record<string, unknown>);
      return this.request<TResponse>(urlWithParams, { method: HttpMethod.GET });
    } else {
      // No params or options
      return this.request<TResponse>(url, { method: HttpMethod.GET });
    }
  }

  /**
   * Type-safe POST request
   */
  protected async post<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: BaseRequestOptions
  ): Promise<TResponse> {
    return this.request<TResponse, TRequest>(url, { 
      ...options, 
      method: HttpMethod.POST, 
      body: data 
    });
  }

  /**
   * Type-safe PUT request
   */
  protected async put<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: BaseRequestOptions
  ): Promise<TResponse> {
    return this.request<TResponse, TRequest>(url, { 
      ...options, 
      method: HttpMethod.PUT, 
      body: data 
    });
  }

  /**
   * Type-safe PATCH request
   */
  protected async patch<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: BaseRequestOptions
  ): Promise<TResponse> {
    return this.request<TResponse, TRequest>(url, { 
      ...options, 
      method: HttpMethod.PATCH, 
      body: data 
    });
  }

  /**
   * Type-safe DELETE request
   */
  protected async delete<TResponse = unknown>(
    url: string,
    options?: BaseRequestOptions
  ): Promise<TResponse> {
    return this.request<TResponse>(url, { ...options, method: HttpMethod.DELETE });
  }

  /**
   * Build full URL from path
   */
  private buildUrl(path: string): string {
    // If path is already a full URL, return it
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }
    
    // Ensure path starts with /
    const cleanPath = path.startsWith('/') ? path : `/${path}`;
    
    return `${this.config.baseURL}${cleanPath}`;
  }

  /**
   * Build headers with authentication and defaults
   */
  private buildHeaders(additionalHeaders?: Record<string, string>): Record<string, string> {
    return {
      'Content-Type': 'application/json',
      'User-Agent': '@knn_labs/conduit-sdk',
      ...this.config.headers,
      ...this.getAuthHeaders(), // SDK-specific auth headers
      ...additionalHeaders,
    };
  }

  /**
   * Execute request with retry logic
   */
  private async executeWithRetry<TResponse, TRequest = unknown>(
    url: string,
    init: RequestInit,
    options: BaseRequestOptions,
    attempt: number = 1
  ): Promise<TResponse> {
    try {
      const response = await fetch(url, init);
      
      // Call onResponse callback if provided
      if (this.config.onResponse) {
        const headers: Record<string, string> = {};
        response.headers.forEach((value, key) => {
          headers[key] = value;
        });
        
        const responseInfo: ResponseInfo = {
          status: response.status,
          statusText: response.statusText,
          headers,
          data: undefined, // Will be populated after parsing
          config: {
            method: init.method ?? 'GET',
            url,
            headers: init.headers as Record<string, string> ?? {},
            data: undefined,
          },
        };
        await this.config.onResponse(responseInfo);
      }

      if (this.config.debug) {
        this.log('debug', `[Conduit] Response: ${response.status} ${response.statusText}`);
      }

      // Validate response status
      if (!this.config.validateStatus(response.status)) {
        const error = await this.handleErrorResponse(response);
        throw error;
      }

      // Parse response based on type
      return await this.parseResponse<TResponse>(response, options.responseType);
    } catch (error) {
      // Check if we should retry
      if (attempt <= this.retryConfig.maxRetries && this.shouldRetry(error)) {
        const delay = this.calculateDelay(attempt);
        if (this.config.debug) {
          this.log('debug', `[Conduit] Retrying request (attempt ${attempt + 1}) after ${delay}ms`);
        }
        await this.sleep(delay);
        return this.executeWithRetry<TResponse, TRequest>(url, init, options, attempt + 1);
      }

      // Handle error
      const handledError = this.handleError(error);
      if (this.config.onError) {
        this.config.onError(handledError);
      }
      throw handledError;
    }
  }

  /**
   * Parse response based on content type
   */
  private async parseResponse<T>(response: Response, responseType?: string): Promise<T> {
    // Handle empty responses
    const contentLength = response.headers.get('content-length');
    if (contentLength === '0' || response.status === 204) {
      return undefined as T;
    }

    // Parse based on requested type or content-type header
    const contentType = response.headers.get('content-type') ?? '';
    
    if (responseType === 'blob' || contentType.includes('image/') || contentType.includes('application/octet-stream')) {
      return await response.blob() as T;
    } else if (responseType === 'arraybuffer') {
      return await response.arrayBuffer() as T;
    } else if (responseType === 'text' || contentType.includes('text/')) {
      return await response.text() as T;
    } else {
      // Default to JSON
      return await response.json() as T;
    }
  }

  /**
   * Handle error responses
   */
  protected abstract handleErrorResponse(response: Response): Promise<Error>;

  /**
   * Determine if error should trigger retry
   */
  protected shouldRetry(error: unknown): boolean {
    // Use custom retry condition if provided
    if (this.retryConfig.retryCondition) {
      return this.retryConfig.retryCondition(error);
    }

    // Default retry logic
    if (error instanceof Error) {
      // Network errors are retryable
      if (error.name === 'AbortError' || 
          error.message.includes('network') ||
          error.message.includes('fetch')) {
        return true;
      }
    }

    return false;
  }

  /**
   * Calculate retry delay
   */
  private calculateDelay(attempt: number): number {
    // Use custom delays if provided
    if (this.config.retryDelay && this.config.retryDelay.length > 0) {
      const index = Math.min(attempt - 1, this.config.retryDelay.length - 1);
      return this.config.retryDelay[index];
    }
    
    // Use exponential backoff
    const initialDelay = this.retryConfig.initialDelay ?? 1000;
    const maxDelay = this.retryConfig.maxDelay ?? 30000;
    const factor = this.retryConfig.factor ?? 2;
    
    const delay = Math.min(
      initialDelay * Math.pow(factor, attempt - 1),
      maxDelay
    );
    
    // Add jitter
    return delay + Math.random() * 1000;
  }

  /**
   * Sleep for specified milliseconds
   */
  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  /**
   * Handle and transform errors
   */
  protected handleError(error: unknown): Error {
    if (error instanceof Error) {
      return error;
    }
    return new Error(String(error));
  }

  /**
   * Normalize retry configuration
   */
  private normalizeRetryConfig(retries?: number | RetryConfig): RetryConfig {
    if (typeof retries === 'number') {
      return {
        maxRetries: retries,
        initialDelay: 1000,
        maxDelay: 30000,
        factor: 2,
      };
    }
    return retries ?? { maxRetries: 3, initialDelay: 1000, maxDelay: 30000, factor: 2 };
  }

  /**
   * Log message using logger if available
   */
  protected log(level: 'debug' | 'info' | 'warn' | 'error', message: string, ...args: unknown[]): void {
    if (this.logger?.[level]) {
      this.logger[level](message, ...args);
    } else if (this.config.debug && level === 'debug') {
      console.warn(message, ...args);
    }
  }

  /**
   * Build URL with query parameters
   */
  protected buildUrlWithParams(url: string, params: Record<string, unknown>): string {
    const searchParams = new URLSearchParams();
    
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        if (Array.isArray(value)) {
          value.forEach(v => searchParams.append(key, String(v)));
        } else {
          searchParams.append(key, String(value));
        }
      }
    });
    
    const queryString = searchParams.toString();
    return queryString ? `${url}?${queryString}` : url;
  }

  /**
   * Get cache key for a request
   */
  protected getCacheKey(resource: string, id?: unknown, params?: Record<string, unknown>): string {
    const parts = [resource];
    if (id !== undefined) {
      parts.push(JSON.stringify(id));
    }
    if (params) {
      parts.push(JSON.stringify(params));
    }
    return parts.join(':');
  }

  /**
   * Get from cache
   */
  protected async getFromCache<T>(key: string): Promise<T | null> {
    if (!this.cache) return null;
    
    try {
      const cached = await this.cache.get<T>(key);
      if (cached) {
        this.log('debug', `Cache hit for key: ${key}`);
        return cached;
      }
    } catch (error) {
      this.log('error', 'Cache get error:', error);
    }
    
    return null;
  }

  /**
   * Set cache value
   */
  protected async setCache(key: string, value: unknown, ttl?: number): Promise<void> {
    if (!this.cache) return;
    
    try {
      await this.cache.set(key, value, ttl);
      this.log('debug', `Cache set for key: ${key}`);
    } catch (error) {
      this.log('error', 'Cache set error:', error);
    }
  }

  /**
   * Execute function with caching
   */
  protected async withCache<T>(
    cacheKey: string,
    fn: () => Promise<T>,
    ttl?: number
  ): Promise<T> {
    // Try to get from cache first
    const cached = await this.getFromCache<T>(cacheKey);
    if (cached !== null) {
      return cached;
    }

    // Execute the function
    const result = await fn();

    // Cache the result
    await this.setCache(cacheKey, result, ttl);

    return result;
  }
}