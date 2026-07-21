/**
 * Contract-backed Admin API transport extending the common BaseApiClient.
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
import createClient, { type Client } from 'openapi-fetch';
import { getRequestConstructor } from '@/lib/api-transport/request-constructor';
import type { paths } from '../generated/admin-api';
import type {
  ApiClientConfig,
  RequestConfig,
  RetryConfig,
  RequestConfigInfo,
  ResponseInfo
} from './types';
import { HTTP_HEADERS, CONTENT_TYPES, CLIENT_INFO } from '../constants';
import { HttpMethod, RequestOptions } from './HttpMethod';

interface ContractResult<TResponse = unknown> {
  data?: TResponse;
  error?: unknown;
  response: Response;
}

interface ContractReadOptions {
  [key: string]: unknown;
  headers: Record<string, string>;
  signal: AbortSignal;
}

type ContractOperation<TResponse> = (
  client: Client<paths>,
  options: ContractReadOptions,
) => Promise<ContractResult<TResponse>>;

/**
 * Admin contract transport extending the shared client lifecycle utilities.
 * Uses X-Master-Key authentication and fixed delay retry with caching support
 */
export abstract class FetchBaseApiClient extends BaseApiClient {
  private readonly contractClient: Client<paths>;
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
    this.contractClient = createClient<paths>({
      baseUrl: config.baseUrl,
      Request: getRequestConstructor(),
    });
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
   * Preserve the Admin API error mapping.
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
  // Preserve the local service interface while routing through openapi-fetch.
  // ============================================================================

  /**
   * Execute a request through the Admin OpenAPI transport.
   */
  protected override async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: RequestOptions<TRequest> & { method?: HttpMethod } = {}
  ): Promise<TResponse> {
    const method = options.method ?? HttpMethod.GET;
    return this.executeAdminRequest(url, { ...options, method }, (client, requestOptions) => {
      const parseAs = options.responseType === 'arraybuffer'
        ? 'arrayBuffer'
        : options.responseType ?? 'json';
      const request = client.request as unknown as (
        requestMethod: string,
        path: string,
        init: Record<string, unknown>,
      ) => Promise<ContractResult<TResponse>>;
      return request(method.toLowerCase(), url, {
        body: options.body,
        headers: requestOptions.headers,
        signal: requestOptions.signal,
        parseAs,
      });
    });
  }

  /**
   * Execute a generated, contract-native GET operation while preserving the
   * Admin client's authentication, timeout, callback, retry, and error lifecycle.
   * @internal Used by composed services as reads migrate off the URL transport.
   */
  protected async executeContractRead<TResponse>(
    resolvedPath: string,
    operation: ContractOperation<TResponse>,
    config?: RequestConfig,
  ): Promise<TResponse> {
    return this.executeAdminRequest(resolvedPath, {
      method: HttpMethod.GET,
      headers: config?.headers,
      signal: config?.signal,
      timeout: config?.timeout,
    }, operation);
  }

  /**
   * Apply the shared Admin request lifecycle to a contract operation.
   */
  private async executeAdminRequest<TResponse, TRequest = unknown>(
    url: string,
    options: RequestOptions<TRequest> & { method: HttpMethod },
    operation: ContractOperation<TResponse>,
  ): Promise<TResponse> {
    const fullUrl = this.adminBuildUrl(url);
    const controller = new AbortController();

    // Set up timeout
    const timeout = options.timeout ?? (this as unknown as { timeout: number }).timeout;
    const timeoutId = timeout
      ? setTimeout(() => controller.abort(), timeout)
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

      const externalAbort = () => controller.abort(options.signal?.reason);
      options.signal?.addEventListener('abort', externalAbort, { once: true });
      try {
        if (options.signal?.aborted) externalAbort();
        return await this.executeContractRequest(
          url,
          options.method,
          (client) => operation(client, {
            headers: requestInfo.headers,
            signal: controller.signal,
          }),
        );
      } finally {
        options.signal?.removeEventListener('abort', externalAbort);
      }
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    }
  }

  /**
   * Execute request with retry logic
   */
  private async executeContractRequest<TResponse>(
    url: string,
    method: HttpMethod,
    operation: (client: Client<paths>) => Promise<ContractResult<TResponse>>,
    attempt: number = 1
  ): Promise<TResponse> {
    try {
      const result = await operation(this.contractClient);
      const response = result.response;

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
          data: result.data,
          config: { url: this.adminBuildUrl(url), method } as RequestConfigInfo,
        };
        await this.onResponse(responseInfo);
      }

      if (result.error !== undefined || !response.ok) {
        // openapi-fetch has already consumed the error response. Recreate it so
        // the existing structured Admin error mapper keeps its observable shape.
        const errorResponse = {
          ...response,
          ok: false,
          status: response.status,
          statusText: response.statusText,
          headers: response.headers,
          url: response.url,
          text: async () => result.error === undefined ? '' : JSON.stringify(result.error),
          json: async () => result.error,
        } as Response;
        const apiError = await this.handleErrorResponse(errorResponse);
        throw apiError;
      }

      // Handle empty responses
      const contentLength = response.headers.get('content-length');

      if (contentLength === '0' || response.status === 204) {
        return undefined as TResponse;
      }

      return result.data as TResponse;
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
        return this.executeContractRequest(url, method, operation, attempt + 1);
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
   * @override Extended signature retained for local service compatibility.
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
