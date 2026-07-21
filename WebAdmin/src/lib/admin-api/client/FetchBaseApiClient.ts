/**
 * Admin SDK HTTP client extending the common BaseApiClient
 *
 * Features:
 * - X-Master-Key authentication
 * - Fixed delay retry strategy
 * - Built-in caching support
 * - Structured logging support
 * - Extended GET with query parameter support
 */

import {
  BaseApiClient,
  type RetryStrategy,
  RetryStrategyType,
  handleApiError,
} from '@/lib/conduit-common';
import type {
  ApiClientConfig,
  RetryConfig,
  RequestConfigInfo,
  ResponseInfo
} from './types';
import { HTTP_HEADERS, CONTENT_TYPES, CLIENT_INFO } from '../constants';
import { ExtendedRequestInit, ResponseParser } from './FetchOptions';
import { HttpMethod, RequestOptions } from './HttpMethod';

/**
 * Admin SDK client extending the common BaseApiClient
 * Uses X-Master-Key authentication and fixed delay retry with caching support
 */
export abstract class FetchBaseApiClient extends BaseApiClient {
  /**
   * Master key for authentication
   */
  protected readonly masterKey: string;

  /**
   * Legacy retry config for backward compatibility
   */
  protected readonly retryConfig: RetryConfig;

  /**
   * Custom retry delays array
   */
  protected readonly retryDelays?: number[];

  /**
   * Legacy callback references for backward compatibility
   */
  protected override readonly onError?: (error: Error) => void;
  protected override readonly onRequest?: (config: RequestConfigInfo) => void | Promise<void>;
  protected override readonly onResponse?: (response: ResponseInfo) => void | Promise<void>;

  constructor(config: ApiClientConfig) {
    // Build retry strategy from config
    const retryStrategy = config.retryDelay
      ? ({
          type: RetryStrategyType.CUSTOM_DELAYS,
          delays: config.retryDelay,
        } as RetryStrategy)
      : FetchBaseApiClient.normalizeRetryConfig(config.retries);

    super({
      baseUrl: config.baseUrl,
      timeout: config.timeout ?? 30000,
      defaultHeaders: {
        [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
        ...config.defaultHeaders,
      },
      retryStrategy,
      debug: false,
      onError: config.onError,
      onRequest: config.onRequest as ((config: { method: string; url: string; headers: Record<string, string>; data?: unknown }) => void | Promise<void>) | undefined,
      onResponse: config.onResponse as ((response: { status: number; statusText: string; headers: Record<string, string>; data: unknown; config: { method: string; url: string; headers: Record<string, string>; data?: unknown } }) => void | Promise<void>) | undefined,
      logger: config.logger,
      cache: config.cache,
    });

    this.masterKey = config.masterKey;
    this.retryDelays = config.retryDelay;

    // Store callbacks for backward compatibility
    this.onError = config.onError;
    this.onRequest = config.onRequest;
    this.onResponse = config.onResponse;

    // Legacy retry config for backward compatibility
    this.retryConfig = this.normalizeRetryConfigInstance(config.retries);
  }

  /**
   * Static method to normalize retry config for super() call
   */
  private static normalizeRetryConfig(retries?: number | RetryConfig): RetryStrategy {
    if (typeof retries === 'number') {
      return {
        type: RetryStrategyType.FIXED_DELAY,
        maxRetries: retries,
        delayMs: 1000,
        retryCondition: (error: unknown): boolean => {
          if (error instanceof Error) {
            return (
              error.name === 'AbortError' ||
              error.message.includes('network') ||
              error.message.includes('fetch')
            );
          }
          return false;
        },
      };
    }
    if (retries) {
      return {
        type: RetryStrategyType.FIXED_DELAY,
        maxRetries: retries.maxRetries,
        delayMs: retries.retryDelay ?? 1000,
        retryCondition: retries.retryCondition,
      };
    }
    // Default
    return {
      type: RetryStrategyType.FIXED_DELAY,
      maxRetries: 3,
      delayMs: 1000,
    };
  }

  /**
   * Instance method to normalize retry config
   */
  private normalizeRetryConfigInstance(retries?: number | RetryConfig): RetryConfig {
    if (typeof retries === 'number') {
      return {
        maxRetries: retries,
        retryDelay: 1000,
        retryCondition: (error: unknown): boolean => {
          if (error instanceof Error) {
            return (
              error.name === 'AbortError' ||
              error.message.includes('network') ||
              error.message.includes('fetch')
            );
          }
          return false;
        },
      };
    }
    return retries ?? { maxRetries: 3, retryDelay: 1000 };
  }

  /**
   * Returns X-Master-Key authentication headers
   */
  protected getAuthHeaders(): Record<string, string> {
    return {
      'X-Master-Key': this.masterKey,
    };
  }

  /**
   * Returns default fixed delay retry strategy
   */
  protected getDefaultRetryStrategy(): RetryStrategy {
    return {
      type: RetryStrategyType.FIXED_DELAY,
      maxRetries: 3,
      delayMs: 1000,
      retryCondition: (error: unknown): boolean => {
        if (error instanceof Error) {
          return (
            error.name === 'AbortError' ||
            error.message.includes('network') ||
            error.message.includes('fetch')
          );
        }
        return false;
      },
    };
  }

  /**
   * Override to use Admin SDK error handling pattern
   */
  protected override async handleErrorResponse(response: Response): Promise<Error> {
    const headers: Record<string, string> = {};
    response.headers.forEach((value, key) => {
      headers[key] = value;
    });

    let data: unknown;
    try {
      data = await this.parseErrorResponseBody(response);
    } catch {
      data = null;
    }

    // Use handleApiError which throws the appropriate error type
    try {
      handleApiError({
        response: {
          status: response.status,
          data,
          headers,
        },
        config: { url: response.url, method: 'unknown' },
        isHttpError: false,
        message: `HTTP ${response.status}: ${response.statusText}`,
      });
    } catch (error) {
      return error as Error;
    }

  }

  /**
   * Parse error response body
   */
  private async parseErrorResponseBody(response: Response): Promise<unknown> {
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

  // ============================================================================
  // Override HTTP methods to maintain Admin SDK's RequestOptions interface
  // ============================================================================

  /**
   * Type-safe request method with Admin SDK options
   */
  protected override async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: RequestOptions<TRequest> & { method?: HttpMethod } = {}
  ): Promise<TResponse> {
    const fullUrl = this.adminBuildUrl(url);
    const controller = new AbortController();

    // Set up timeout
    const timeoutId = options.timeout ?? (this as unknown as { timeout: number }).timeout
      ? setTimeout(() => controller.abort(), options.timeout ?? (this as unknown as { timeout: number }).timeout)
      : undefined;

    try {
      const requestInfo: RequestConfigInfo = {
        method: options.method ?? 'GET',
        url: fullUrl,
        headers: this.adminBuildHeaders(options.headers),
        data: options.body,
      };

      // Call onRequest hook if provided
      if (this.onRequest) {
        await this.onRequest(requestInfo);
      }

      this.log('debug', `API Request: ${requestInfo.method} ${requestInfo.url}`);

      const response = await this.executeRequestWithRetry<TResponse, TRequest>(
        fullUrl,
        {
          method: requestInfo.method,
          headers: requestInfo.headers,
          body: options.body ? JSON.stringify(options.body) : undefined,
          signal: options.signal ?? controller.signal,
          responseType: options.responseType,
          timeout: options.timeout ?? (this as unknown as { timeout: number }).timeout,
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
   * Execute request with retry logic
   */
  private async executeRequestWithRetry<TResponse, TRequest = unknown>(
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
        const apiError = await this.handleErrorResponse(response);
        throw apiError;
      }

      // Handle empty responses
      const contentLength = response.headers.get('content-length');

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

      const shouldRetry =
        this.retryConfig.retryCondition &&
        error instanceof Error &&
        this.retryConfig.retryCondition(error as unknown as Error);

      if (shouldRetry) {
        const delay = this.calculateRetryDelay(attempt);
        this.log('debug', `Retrying request (attempt ${attempt + 1}) after ${delay}ms`);

        await this.adminSleep(delay);
        return this.executeRequestWithRetry<TResponse, TRequest>(url, init, attempt + 1);
      }

      if (this.onError && error instanceof Error) {
        this.onError(error);
      }
      throw error;
    }
  }

  /**
   * Calculate retry delay
   */
  private calculateRetryDelay(attempt: number): number {
    if (this.retryDelays && this.retryDelays.length > 0) {
      const index = Math.min(attempt - 1, this.retryDelays.length - 1);
      return this.retryDelays[index];
    }

    const baseDelay = this.retryConfig.retryDelay ?? 1000;
    return baseDelay * Math.pow(2, attempt - 1);
  }

  /**
   * Sleep for a specified duration
   */
  private adminSleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  /**
   * Build full URL from path
   */
  private adminBuildUrl(path: string): string {
    // If path is already a full URL, return it
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }

    // Ensure path starts with /
    const cleanPath = path.startsWith('/') ? path : `/${path}`;

    return `${(this as unknown as { baseUrl: string }).baseUrl}${cleanPath}`;
  }

  /**
   * Build headers including auth, defaults, and additional headers
   */
  private adminBuildHeaders(additionalHeaders?: Record<string, string>): Record<string, string> {
    return {
      [HTTP_HEADERS.CONTENT_TYPE]: CONTENT_TYPES.JSON,
      'X-Master-Key': this.masterKey,
      [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
      ...(this as unknown as { defaultHeaders: Record<string, string> }).defaultHeaders,
      ...additionalHeaders,
    };
  }

  /**
   * Type-safe GET request with query parameter support
   * Supports both 2-argument and 3-argument patterns for backward compatibility
   */
  protected override async get<TResponse = unknown>(
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
      const urlWithParams = optionsOrParams
        ? this.buildUrlWithParams(url, optionsOrParams as Record<string, unknown>)
        : url;
      return this.request<TResponse>(urlWithParams, { ...extraOptions, method: HttpMethod.GET });
    }

    // Check if it's options (has headers/signal/timeout/responseType) or params
    const isOptions =
      optionsOrParams &&
      ('headers' in optionsOrParams ||
        'signal' in optionsOrParams ||
        'timeout' in optionsOrParams ||
        'responseType' in optionsOrParams);

    if (isOptions) {
      return this.request<TResponse>(url, {
        ...(optionsOrParams as {
          headers?: Record<string, string>;
          signal?: AbortSignal;
          timeout?: number;
          responseType?: 'json' | 'text' | 'blob' | 'arraybuffer';
        }),
        method: HttpMethod.GET,
      });
    } else {
      // It's params - add them to the URL
      const urlWithParams = optionsOrParams
        ? this.buildUrlWithParams(url, optionsOrParams)
        : url;
      return this.request<TResponse>(urlWithParams, { method: HttpMethod.GET });
    }
  }

  /**
   * Type-safe POST request
   */
  protected override async post<TResponse = unknown, TRequest = unknown>(
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
      body: data,
    });
  }

  /**
   * Type-safe PUT request
   */
  protected override async put<TResponse = unknown, TRequest = unknown>(
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
      body: data,
    });
  }

  /**
   * Type-safe PATCH request
   */
  protected override async patch<TResponse = unknown, TRequest = unknown>(
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
      body: data,
    });
  }

  /**
   * Type-safe DELETE request
   */
  protected override async delete<TResponse = unknown>(
    url: string,
    options?: {
      headers?: Record<string, string>;
      signal?: AbortSignal;
      timeout?: number;
    }
  ): Promise<TResponse> {
    return this.request<TResponse>(url, { ...options, method: HttpMethod.DELETE });
  }

  /**
   * Build URL with query parameters
   */
  private buildUrlWithParams(url: string, params: Record<string, unknown>): string {
    const searchParams = new URLSearchParams();
    const serialize = (value: unknown): string => {
      if (typeof value === 'string') return value;
      if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
        return value.toString();
      }
      if (typeof value === 'symbol') return value.description ?? '';
      if (typeof value === 'function') return value.name;
      return JSON.stringify(value) ?? '';
    };

    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        if (Array.isArray(value)) {
          value.forEach((item) => searchParams.append(key, serialize(item)));
        } else {
          searchParams.append(key, serialize(value));
        }
      }
    });

    const queryString = searchParams.toString();
    return queryString ? `${url}?${queryString}` : url;
  }

  // ============================================================================
  // Caching Utilities (inherited from BaseApiClient, re-exposed for compatibility)
  // ============================================================================

  /**
   * Generate a cache key from resource and identifiers
   * @override Extended signature for Admin SDK compatibility
   */
  protected override getCacheKey(
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
}
