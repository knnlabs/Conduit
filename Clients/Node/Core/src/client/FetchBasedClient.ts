import type { ClientConfig, RequestOptions, RetryConfig, RequestConfig, ResponseInfo } from './types';
import type { ErrorResponse } from '../models/common';
import { 
  ConduitError, 
  AuthError, 
  RateLimitError, 
  NetworkError,
  createErrorFromResponse
} from '../utils/errors';
import { HTTP_HEADERS, CONTENT_TYPES, CLIENT_INFO, ERROR_CODES } from '../constants';
import type { ExtendedRequestInit } from './FetchOptions';
import { ResponseParser } from './FetchOptions';
import { HttpMethod } from './HttpMethod';

/**
 * Type-safe base client using native fetch API
 * Provides all the functionality of HTTP with better type safety
 */
export abstract class FetchBasedClient {
  protected readonly config: Required<Omit<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>> & 
    Pick<ClientConfig, 'onError' | 'onRequest' | 'onResponse'>;
  protected readonly retryConfig: RetryConfig;
  protected readonly retryDelays: number[];

  constructor(config: ClientConfig) {
    this.config = {
      apiKey: config.apiKey,
      baseURL: config.baseURL || 'https://api.conduit.ai',
      timeout: config.timeout || 60000,
      maxRetries: config.maxRetries || 3,
      headers: config.headers || {},
      debug: config.debug || false,
      signalR: config.signalR || {},
      retryDelay: config.retryDelay || [1000, 2000, 4000, 8000, 16000],
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse,
    };

    this.retryConfig = {
      maxRetries: this.config.maxRetries,
      initialDelay: 1000,
      maxDelay: 30000,
      factor: 2,
    };

    this.retryDelays = this.config.retryDelay;
  }

  /**
   * Type-safe request method with proper request/response typing
   */
  protected async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: RequestOptions & { 
      method?: HttpMethod; 
      body?: TRequest;
    } = {}
  ): Promise<TResponse> {
    const fullUrl = this.buildUrl(url);
    const controller = new AbortController();
    
    // Set up timeout
    const timeoutId = options.timeout || this.config.timeout
      ? setTimeout(() => controller.abort(), options.timeout || this.config.timeout)
      : undefined;

    try {
      const requestConfig: RequestConfig = {
        method: options.method || HttpMethod.GET,
        url: fullUrl,
        headers: this.buildHeaders(options.headers),
        data: options.body,
      };

      // Call onRequest callback if provided
      if (this.config.onRequest) {
        await this.config.onRequest(requestConfig);
      }


      if (this.config.debug) {
        console.debug(`[Conduit] ${requestConfig.method} ${requestConfig.url}`);
      }

      const response = await this.executeWithRetry<TResponse, TRequest>(
        fullUrl,
        {
          method: requestConfig.method,
          headers: requestConfig.headers as HeadersInit,
          body: options.body ? JSON.stringify(options.body) : undefined,
          signal: options.signal || controller.signal,
          responseType: options.responseType,
          timeout: options.timeout || this.config.timeout,
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
      body: data 
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
      body: data 
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
      body: data 
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

  private buildUrl(path: string): string {
    // If path is already a full URL, return it
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }
    
    // Ensure baseURL doesn't end with / and path starts with /
    const baseUrl = this.config.baseURL.replace(/\/$/, '');
    const cleanPath = path.startsWith('/') ? path : `/${path}`;
    
    return `${baseUrl}${cleanPath}`;
  }

  private buildHeaders(additionalHeaders?: Record<string, string>): Record<string, string> {
    return {
      [HTTP_HEADERS.AUTHORIZATION]: `Bearer ${this.config.apiKey}`,
      [HTTP_HEADERS.CONTENT_TYPE]: CONTENT_TYPES.JSON,
      [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
      ...this.config.headers,
      ...additionalHeaders,
    };
  }

  private async executeWithRetry<TResponse, TRequest = unknown>(
    url: string,
    init: ExtendedRequestInit,
    options: RequestOptions = {},
    attempt: number = 1
  ): Promise<TResponse> {
    try {
      const response = await fetch(url, ResponseParser.cleanRequestInit(init));
      
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
            method: init.method || 'GET',
            url,
            headers: init.headers as Record<string, string> || {},
            data: undefined,
          },
        };
        await this.config.onResponse(responseInfo);
      }

      if (this.config.debug) {
        console.debug(`[Conduit] Response: ${response.status} ${response.statusText}`);
      }

      if (!response.ok) {
        const error = await this.handleErrorResponse(response);
        throw error;
      }

      // Parse response using ResponseParser
      return await ResponseParser.parse<TResponse>(response, init.responseType || options.responseType);
    } catch (error) {
      
      // Check if we've exceeded max retries
      if (attempt > this.retryConfig.maxRetries) {
        throw this.handleError(error);
      }

      if (this.shouldRetry(error) && attempt <= this.retryConfig.maxRetries) {
        const delay = this.calculateDelay(attempt);
        if (this.config.debug) {
          console.debug(`[Conduit] Retrying request (attempt ${attempt + 1}) after ${delay}ms`);
        }
        await this.sleep(delay);
        return this.executeWithRetry<TResponse, TRequest>(url, init, options, attempt + 1);
      }

      throw this.handleError(error);
    }
  }

  private async handleErrorResponse(response: Response): Promise<Error> {
    let errorData: ErrorResponse | undefined;
    
    try {
      const contentType = response.headers.get('content-type');
      if (contentType?.includes('application/json')) {
        errorData = await response.json() as ErrorResponse;
      }
    } catch {
      // Ignore JSON parsing errors
    }

    const status = response.status;
    
    if (status === 401) {
      return new AuthError(
        errorData?.error?.message || 'Authentication failed',
        { code: errorData?.error?.code || 'auth_error' }
      );
    } else if (status === 429) {
      const retryAfter = response.headers.get('retry-after');
      return new RateLimitError(
        errorData?.error?.message || 'Rate limit exceeded',
        retryAfter ? parseInt(retryAfter, 10) : undefined
      );
    } else if (status === 400) {
      return new ConduitError(
        errorData?.error?.message || 'Bad request',
        status,
        errorData?.error?.code || 'bad_request'
      );
    } else if (errorData?.error) {
      return createErrorFromResponse(errorData, status);
    } else {
      return new ConduitError(
        `Request failed with status ${status}`,
        status,
        'http_error'
      );
    }
  }

  private shouldRetry(error: unknown): boolean {
    if (error instanceof ConduitError) {
      const status = error.statusCode;
      return status === 429 || status === 503 || status === 504;
    }
    
    if (error instanceof Error) {
      // Network errors are retryable
      return error.name === 'AbortError' || 
             error.message.includes('network') ||
             error.message.includes('fetch');
    }
    
    return false;
  }

  private calculateDelay(attempt: number): number {
    if (this.retryDelays && this.retryDelays.length > 0) {
      const index = Math.min(attempt - 1, this.retryDelays.length - 1);
      return this.retryDelays[index];
    }
    
    const delay = Math.min(
      this.retryConfig.initialDelay * Math.pow(this.retryConfig.factor, attempt - 1),
      this.retryConfig.maxDelay
    );
    return delay + Math.random() * 1000;
  }

  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  private handleError(error: unknown): Error {
    if (error instanceof Error) {
      if (error.name === 'AbortError') {
        const networkError = new NetworkError(
          'Request timeout',
          { code: ERROR_CODES.CONNECTION_ABORTED }
        );
        if (this.config.onError) {
          this.config.onError(networkError);
        }
        return networkError;
      }
      
      if (this.config.onError) {
        this.config.onError(error);
      }
      return error;
    }
    
    const unknownError = new Error(String(error));
    if (this.config.onError) {
      this.config.onError(unknownError);
    }
    return unknownError;
  }
}