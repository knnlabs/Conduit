import type { 
  ApiClientConfig, 
  RetryConfig, 
  Logger, 
  CacheProvider, 
  RequestConfigInfo, 
  ResponseInfo 
} from './types';
import { handleApiError } from '../utils/errors';
import { HTTP_HEADERS, CONTENT_TYPES, CLIENT_INFO } from '../constants';
import { ExtendedRequestInit, ResponseParser } from './FetchOptions';
import { HttpMethod, RequestOptions } from './HttpMethod';

/**
 * Type-safe base API client for Conduit Admin using native fetch
 * Provides all functionality without HTTP complexity
 */
export abstract class FetchBaseApiClient {
  protected readonly logger?: Logger;
  protected readonly cache?: CacheProvider;
  protected readonly retryConfig: RetryConfig;
  protected readonly retryDelays?: number[];
  protected readonly onError?: (error: Error) => void;
  protected readonly onRequest?: (config: RequestConfigInfo) => void | Promise<void>;
  protected readonly onResponse?: (response: ResponseInfo) => void | Promise<void>;
  protected readonly baseUrl: string;
  protected readonly masterKey: string;
  protected readonly timeout: number;
  protected readonly defaultHeaders: Record<string, string>;

  constructor(config: ApiClientConfig) {
    this.logger = config.logger;
    this.cache = config.cache;
    this.retryDelays = config.retryDelay;
    this.onError = config.onError;
    this.onRequest = config.onRequest;
    this.onResponse = config.onResponse;
    this.baseUrl = config.baseUrl.replace(/\/$/, ''); // Remove trailing slash
    this.masterKey = config.masterKey;
    this.timeout = config.timeout ?? 30000;
    this.defaultHeaders = config.defaultHeaders ?? {};
    
    this.retryConfig = this.normalizeRetryConfig(config.retries);
  }

  private normalizeRetryConfig(retries?: number | RetryConfig): RetryConfig {
    if (typeof retries === 'number') {
      return {
        maxRetries: retries,
        retryDelay: 1000,
        retryCondition: (error: unknown): boolean => {
          if (error instanceof Error) {
            return error.name === 'AbortError' || 
                   error.message.includes('network') ||
                   error.message.includes('fetch');
          }
          return false;
        },
      };
    }
    return retries ?? { maxRetries: 3, retryDelay: 1000 };
  }

  /**
   * Type-safe request method with proper request/response typing
   */
  protected async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: RequestOptions<TRequest> & { method?: HttpMethod } = {}
  ): Promise<TResponse> {
    const fullUrl = this.buildUrl(url);
    const controller = new AbortController();
    
    // Set up timeout
    const timeoutId = options.timeout ?? this.timeout
      ? setTimeout(() => controller.abort(), options.timeout ?? this.timeout)
      : undefined;

    try {
      const requestInfo: RequestConfigInfo = {
        method: options.method ?? 'GET',
        url: fullUrl,
        headers: this.buildHeaders(options.headers),
        data: options.body,
      };

      // Call onRequest hook if provided
      if (this.onRequest) {
        await this.onRequest(requestInfo);
      }

      console.warn('[SDK] API Request:', requestInfo.method, requestInfo.url);
      this.log('debug', `API Request: ${requestInfo.method} ${requestInfo.url}`);

      const response = await this.executeWithRetry<TResponse, TRequest>(
        fullUrl,
        {
          method: requestInfo.method,
          headers: requestInfo.headers,
          body: options.body ? JSON.stringify(options.body) : undefined,
          signal: options.signal ?? controller.signal,
          responseType: options.responseType,
          timeout: options.timeout ?? this.timeout,
        }
      );

      return response;
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    }
  }

  /**
   * Type-safe GET request
   */
  protected async get<TResponse = unknown>(
    url: string,
    optionsOrParams?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
      responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
    } | Record<string, unknown>,
    extraOptions?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
      responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
    }
  ): Promise<TResponse> {
    // Handle 3-argument case (url, params, options)
    if (extraOptions) {
      const urlWithParams = optionsOrParams ? this.buildUrlWithParams(url, optionsOrParams as Record<string, unknown>) : url;
      return this.request<TResponse>(urlWithParams, { ...extraOptions, method: HttpMethod.GET });
    }
    
    // Check if it's options (has headers/signal/timeout/responseType) or params
    const isOptions = optionsOrParams && 
      ('headers' in optionsOrParams || 'signal' in optionsOrParams || 
       'timeout' in optionsOrParams || 'responseType' in optionsOrParams);
    
    if (isOptions) {
      return this.request<TResponse>(url, { 
        ...(optionsOrParams as { headers?: Record<string, string>; signal?: AbortSignal; timeout?: number; responseType?: 'json' | 'text' | 'blob' | 'arraybuffer'; }), 
        method: HttpMethod.GET 
      });
    } else {
      // It's params - add them to the URL
      const urlWithParams = optionsOrParams ? this.buildUrlWithParams(url, optionsOrParams) : url;
      return this.request<TResponse>(urlWithParams, { method: HttpMethod.GET });
    }
  }

  /**
   * Type-safe POST request
   */
  protected async post<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
    }
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
    options?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
    }
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
    options?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
    }
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
    options?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
    }
  ): Promise<TResponse> {
    return this.request<TResponse>(url, { ...options, method: HttpMethod.DELETE });
  }

  private buildUrl(path: string): string {
    // If path is already a full URL, return it
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }
    
    // Ensure path starts with /
    const cleanPath = path.startsWith('/') ? path : `/${path}`;
    
    return `${this.baseUrl}${cleanPath}`;
  }

  private buildHeaders(additionalHeaders?: Record<string, string>): Record<string, string> {
    return {
      [HTTP_HEADERS.CONTENT_TYPE]: CONTENT_TYPES.JSON,
      [HTTP_HEADERS.X_API_KEY]: this.masterKey,
      [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
      ...this.defaultHeaders,
      ...additionalHeaders,
    };
  }

  private async executeWithRetry<TResponse, TRequest = unknown>(
    url: string,
    init: ExtendedRequestInit,
    attempt: number = 1
  ): Promise<TResponse> {
    try {
      const response = await fetch(url, ResponseParser.cleanRequestInit(init));
      
      this.log('debug', `API Response: ${response.status} ${response.statusText}`);
      
      // Convert headers to object (needed for both onResponse and error handling)
      const headers: Record<string, string> = {};
      response.headers.forEach((value, key) => {
        headers[key] = value;
      });
      
      // Call onResponse hook if provided
      if (this.onResponse) {
        const responseInfo: ResponseInfo = {
          status: response.status,
          statusText: response.statusText,
          headers,
          data: undefined, // Will be populated after parsing
          config: { url, method: init?.method ?? HttpMethod.GET } as RequestConfigInfo,
        };
        await this.onResponse(responseInfo);
      }

      if (!response.ok) {
        console.error('[SDK] API Error Response:', {
          url,
          status: response.status,
          statusText: response.statusText,
          method: init.method ?? HttpMethod.GET
        });
        
        const apiError = handleApiError({
          response: {
            status: response.status,
            data: await this.parseErrorResponse(response),
            headers,
          },
          config: { url, method: init.method ?? HttpMethod.GET },
          isHttpError: false,
          message: `HTTP ${response.status}: ${response.statusText}`,
        });
        
        throw apiError;
      }

      // Handle empty responses
      const contentLength = response.headers.get('content-length');
      // Content type checked but not used for empty responses
      
      if (contentLength === '0' || response.status === 204) {
        return undefined as TResponse;
      }

      // Parse response using ResponseParser
      return await ResponseParser.parse<TResponse>(response, init.responseType);
    } catch (error) {
      if (attempt > this.retryConfig.maxRetries) {
        if (this.onError && error instanceof Error) {
          this.onError(error);
        }
        throw error;
      }

      const shouldRetry = this.retryConfig.retryCondition && 
                         error instanceof Error &&
                         this.retryConfig.retryCondition(error as unknown as Error);

      if (shouldRetry) {
        const delay = this.calculateRetryDelay(attempt);
        this.log('debug', `Retrying request (attempt ${attempt + 1}) after ${delay}ms`);
        
        await this.sleep(delay);
        return this.executeWithRetry<TResponse, TRequest>(url, init, attempt + 1);
      }

      if (this.onError && error instanceof Error) {
        this.onError(error);
      }
      throw error;
    }
  }

  private async parseErrorResponse(response: Response): Promise<unknown> {
    try {
      const contentType = response.headers.get('content-type');
      if (contentType?.includes('application/json')) {
        return await response.json() as unknown;
      }
      return await response.text();
    } catch {
      return null;
    }
  }

  private calculateRetryDelay(attempt: number): number {
    if (this.retryDelays && this.retryDelays.length > 0) {
      const index = Math.min(attempt - 1, this.retryDelays.length - 1);
      return this.retryDelays[index];
    }
    
    const baseDelay = this.retryConfig.retryDelay ?? 1000;
    return baseDelay * Math.pow(2, attempt - 1);
  }

  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  protected log(level: 'debug' | 'info' | 'warn' | 'error', message: string, ...args: unknown[]): void {
    if (this.logger?.[level]) {
      this.logger[level](message, ...args);
    }
  }

  protected getCacheKey(
    methodOrResource: string, 
    urlOrId?: unknown, 
    paramsOrId2?: Record<string, unknown> | string
  ): string {
    // Handle different signatures
    if (typeof urlOrId === 'string' && typeof paramsOrId2 === 'string') {
      // Three string signature: resource, id1, id2
      return `${methodOrResource}:${urlOrId}:${paramsOrId2}`;
    } else if (typeof urlOrId === 'string' && paramsOrId2 && typeof paramsOrId2 === 'object') {
      // Old signature: method, url, params
      const paramStr = JSON.stringify(paramsOrId2);
      return `${methodOrResource}:${urlOrId}:${paramStr}`;
    } else {
      // New signature: resource, id/filters
      const idStr = urlOrId ? JSON.stringify(urlOrId) : '';
      return `${methodOrResource}:${idStr}`;
    }
  }

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
   * Execute a function with caching
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

  private buildUrlWithParams(url: string, params: Record<string, unknown>): string {
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
}