import type { AxiosInstance, AxiosRequestConfig } from 'axios';
import axios, { AxiosError } from 'axios';
import type { ClientConfig, RequestOptions, RetryConfig } from './types';
import type { ErrorResponse } from '../models/common';
import { 
  ConduitError, 
  AuthenticationError, 
  RateLimitError, 
  NetworkError 
} from '../utils/errors';
import { HTTP_HEADERS, CONTENT_TYPES, CLIENT_INFO, ERROR_CODES } from '../constants';

export abstract class BaseClient {
  protected readonly client: AxiosInstance;
  protected readonly config: Required<ClientConfig>;
  protected readonly retryConfig: RetryConfig;

  constructor(config: ClientConfig) {
    this.config = {
      apiKey: config.apiKey,
      baseURL: config.baseURL || 'https://api.conduit.ai',
      timeout: config.timeout || 60000,
      maxRetries: config.maxRetries || 3,
      headers: config.headers || {},
      debug: config.debug || false,
    };

    this.retryConfig = {
      maxRetries: this.config.maxRetries,
      initialDelay: 1000,
      maxDelay: 30000,
      factor: 2,
    };

    this.client = axios.create({
      baseURL: this.config.baseURL,
      timeout: this.config.timeout,
      headers: {
        [HTTP_HEADERS.AUTHORIZATION]: `Bearer ${this.config.apiKey}`,
        [HTTP_HEADERS.CONTENT_TYPE]: CONTENT_TYPES.JSON,
        [HTTP_HEADERS.USER_AGENT]: CLIENT_INFO.USER_AGENT,
        ...this.config.headers,
      },
    });

    this.setupInterceptors();
  }

  private setupInterceptors(): void {
    this.client.interceptors.request.use(
      (config) => {
        if (this.config.debug) {
          console.debug(`[Conduit] ${config.method?.toUpperCase()} ${config.url}`);
        }
        return config;
      },
      (error) => {
        if (this.config.debug) {
          console.error('[Conduit] Request error:', error);
        }
        return Promise.reject(error);
      }
    );

    this.client.interceptors.response.use(
      (response) => {
        if (this.config.debug) {
          console.debug(`[Conduit] Response ${response.status} from ${response.config.url}`);
        }
        return response;
      },
      (error) => {
        if (this.config.debug) {
          console.error('[Conduit] Response error:', error);
        }
        return Promise.reject(error);
      }
    );
  }

  protected async request<T>(
    config: AxiosRequestConfig,
    options?: RequestOptions
  ): Promise<T> {
    const requestConfig: AxiosRequestConfig = {
      ...config,
      signal: options?.signal,
      timeout: options?.timeout || this.config.timeout,
      headers: {
        ...config.headers,
        ...options?.headers,
        ...(options?.correlationId && { 'X-Correlation-Id': options.correlationId }),
      },
    };

    return this.executeWithRetry<T>(requestConfig);
  }

  private async executeWithRetry<T>(
    config: AxiosRequestConfig,
    attempt: number = 1
  ): Promise<T> {
    try {
      const response = await this.client.request<T>(config);
      return response.data;
    } catch (error) {
      if (attempt >= this.retryConfig.maxRetries) {
        throw this.handleError(error);
      }

      if (this.shouldRetry(error)) {
        const delay = this.calculateDelay(attempt);
        if (this.config.debug) {
          console.debug(`[Conduit] Retrying request (attempt ${attempt + 1}) after ${delay}ms`);
        }
        await this.sleep(delay);
        return this.executeWithRetry<T>(config, attempt + 1);
      }

      throw this.handleError(error);
    }
  }

  private shouldRetry(error: unknown): boolean {
    if (error instanceof AxiosError) {
      const status = error.response?.status;
      if (status === 429 || status === 503 || status === 504) {
        return true;
      }
      if (!error.response && error.code === ERROR_CODES.CONNECTION_ABORTED) {
        return true;
      }
    }
    return false;
  }

  private calculateDelay(attempt: number): number {
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
    if (error instanceof AxiosError) {
      const status = error.response?.status;
      const data = error.response?.data as unknown;

      if (data && this.isErrorResponse(data)) {
        const errorData = data;
        if (status === 401) {
          return new AuthenticationError(errorData.error.message);
        }
        if (status === 429) {
          const retryAfter = error.response?.headers['retry-after'] as string | undefined;
          return new RateLimitError(
            errorData.error.message,
            retryAfter ? parseInt(retryAfter, 10) : undefined
          );
        }
        return ConduitError.fromErrorResponse(errorData, status);
      }

      if (!error.response) {
        return new NetworkError(error.message || 'Network request failed');
      }

      return new ConduitError(
        error.message || 'Request failed',
        status,
        error.code
      );
    }

    if (error instanceof Error) {
      return error;
    }

    return new ConduitError('An unknown error occurred');
  }

  private isErrorResponse(data: unknown): data is ErrorResponse {
    return !!data && typeof data === 'object' && 'error' in data;
  }
}