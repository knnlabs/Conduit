/**
 * Gateway SDK HTTP client extending the common BaseApiClient
 *
 * Features:
 * - Bearer token authentication
 * - Exponential backoff retry with jitter
 * - Sophisticated error handling with OpenAI-compatible error types
 * - SignalR configuration support
 */

import {
  BaseApiClient,
  type BaseRequestOptions,
  type RetryStrategy,
  RetryStrategyType,
  ConduitError,
} from '@knn_labs/conduit-common';
import type { ClientConfig, RequestOptions, RetryConfig } from './types';
import { parseErrorResponse, shouldRetry as shouldRetryError, getRetryDelay } from '../utils/errorParser';
import { HTTP_HEADERS, CLIENT_INFO } from '../constants';
import { HttpMethod } from './HttpMethod';

/**
 * Gateway SDK client extending the common BaseApiClient
 * Uses Bearer token authentication and exponential backoff retry
 */
export abstract class FetchBasedClient extends BaseApiClient {
  /**
   * API key for authentication
   */
  protected readonly apiKey: string;

  /**
   * Full configuration including SDK-specific settings
   */
  protected readonly config: Required<Omit<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>> &
    Pick<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>;

  /**
   * Legacy retry config for backward compatibility
   */
  protected readonly retryConfig: RetryConfig;

  /**
   * Custom retry delays array
   */
  protected readonly retryDelays: number[];

  constructor(config: ClientConfig) {
    // Build retry strategy from config
    const retryStrategy: RetryStrategy = config.retryDelay
      ? {
          type: RetryStrategyType.CUSTOM_DELAYS,
          delays: config.retryDelay,
        }
      : {
          type: RetryStrategyType.EXPONENTIAL_BACKOFF,
          maxRetries: config.maxRetries ?? 3,
          initialDelayMs: 1000,
          maxDelayMs: 30000,
          factor: 2,
          jitter: true,
        };

    super({
      baseUrl: config.baseURL ?? 'https://api.conduit.ai',
      timeout: config.timeout ?? 60000,
      defaultHeaders: {
        [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
        ...config.headers,
      },
      retryStrategy,
      debug: config.debug ?? false,
      onError: config.onError,
      onRequest: config.onRequest as ((config: { method: string; url: string; headers: Record<string, string>; data?: unknown }) => void | Promise<void>) | undefined,
      onResponse: config.onResponse as ((response: { status: number; statusText: string; headers: Record<string, string>; data: unknown; config: { method: string; url: string; headers: Record<string, string>; data?: unknown } }) => void | Promise<void>) | undefined,
    });

    this.apiKey = config.apiKey;

    // Store full config for SDK-specific features
    this.config = {
      apiKey: config.apiKey,
      baseURL: config.baseURL ?? 'https://api.conduit.ai',
      timeout: config.timeout ?? 60000,
      maxRetries: config.maxRetries ?? 3,
      headers: config.headers ?? {},
      debug: config.debug ?? false,
      signalR: config.signalR ?? {},
      retryDelay: config.retryDelay ?? [1000, 2000, 4000, 8000, 16000],
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse,
    };

    // Legacy retry config for backward compatibility
    this.retryConfig = {
      maxRetries: this.config.maxRetries,
      initialDelay: 1000,
      maxDelay: 30000,
      factor: 2,
    };

    this.retryDelays = this.config.retryDelay;
  }

  /**
   * Returns Bearer token authentication headers
   */
  protected getAuthHeaders(): Record<string, string> {
    return {
      [HTTP_HEADERS.AUTHORIZATION]: `Bearer ${this.apiKey}`,
    };
  }

  /**
   * Returns default exponential backoff retry strategy
   */
  protected getDefaultRetryStrategy(): RetryStrategy {
    return {
      type: RetryStrategyType.EXPONENTIAL_BACKOFF,
      maxRetries: 3,
      initialDelayMs: 1000,
      maxDelayMs: 30000,
      factor: 2,
      jitter: true,
    };
  }

  /**
   * Override to use Gateway-specific error parsing with OpenAI format support
   */
  protected async handleErrorResponse(response: Response): Promise<Error> {
    let errorData: unknown;

    try {
      const contentType = response.headers.get('content-type');
      if (contentType?.includes('application/json')) {
        errorData = await response.json();
      }
    } catch {
      // If JSON parsing fails, use empty object
      errorData = {};
    }

    // Use the Gateway SDK's parseErrorResponse for OpenAI-compatible errors
    return parseErrorResponse(response, errorData);
  }

  /**
   * Override to use Gateway-specific retry logic
   */
  protected shouldRetry(error: unknown, attempt: number): boolean {
    // Use parent's max retry check
    if (!super.shouldRetry(error, attempt)) {
      return false;
    }

    // Use Gateway-specific error-aware retry logic
    if (error instanceof ConduitError) {
      return shouldRetryError(error);
    }

    if (error instanceof Error) {
      // Network errors are retryable
      return (
        error.name === 'AbortError' ||
        error.message.includes('network') ||
        error.message.includes('fetch')
      );
    }

    return false;
  }

  /**
   * Override to use Gateway-specific retry delay with error-aware delays
   * (e.g., respecting Retry-After headers from rate limits)
   */
  protected getRetryDelay(error: unknown, attempt: number): number {
    // Use error-specific delay if available (e.g., from rate limit headers)
    if (error instanceof ConduitError) {
      return getRetryDelay(error, attempt);
    }

    // Fall back to custom delays or exponential backoff
    if (this.retryDelays && this.retryDelays.length > 0) {
      const index = Math.min(attempt - 1, this.retryDelays.length - 1);
      return this.retryDelays[index];
    }

    return super.getRetryDelay(error, attempt);
  }

  // ============================================================================
  // Override HTTP methods to maintain Gateway SDK's RequestOptions interface
  // ============================================================================

  /**
   * Type-safe request method with Gateway SDK options
   */
  protected async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: RequestOptions & {
      method?: HttpMethod;
      body?: TRequest;
    } = {}
  ): Promise<TResponse> {
    // Convert Gateway SDK options to base options
    const baseOptions: BaseRequestOptions & { method?: HttpMethod; body?: TRequest } = {
      headers: options.headers,
      signal: options.signal,
      timeout: options.timeout,
      responseType: options.responseType,
      method: options.method,
      body: options.body,
    };

    return super.request<TResponse, TRequest>(url, baseOptions);
  }

  /**
   * Type-safe GET request
   */
  protected async get<TResponse = unknown>(
    url: string,
    options?: RequestOptions
  ): Promise<TResponse> {
    return this.request<TResponse>(url, { ...options, method: HttpMethod.GET });
  }

  /**
   * Type-safe POST request
   */
  protected async post<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
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
  protected async put<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
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
  protected async patch<TResponse = unknown, TRequest = unknown>(
    url: string,
    data?: TRequest,
    options?: RequestOptions
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
  protected async delete<TResponse = unknown>(
    url: string,
    options?: RequestOptions
  ): Promise<TResponse> {
    return this.request<TResponse>(url, { ...options, method: HttpMethod.DELETE });
  }
}
