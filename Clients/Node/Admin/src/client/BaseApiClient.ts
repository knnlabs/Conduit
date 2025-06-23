import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios';
import { ApiClientConfig, RequestConfig, RetryConfig, Logger, CacheProvider, AxiosError } from './types';
import { handleApiError } from '../utils/errors';

export abstract class BaseApiClient {
  protected readonly axios: AxiosInstance;
  protected readonly logger?: Logger;
  protected readonly cache?: CacheProvider;
  protected readonly retryConfig: RetryConfig;

  constructor(config: ApiClientConfig) {
    this.logger = config.logger;
    this.cache = config.cache;
    
    this.retryConfig = this.normalizeRetryConfig(config.retries);

    this.axios = axios.create({
      baseURL: config.baseUrl,
      timeout: config.timeout || 30000,
      headers: {
        'Content-Type': 'application/json',
        'X-API-Key': config.masterKey,
        ...config.defaultHeaders,
      },
    });

    this.setupInterceptors();
  }

  private normalizeRetryConfig(retries?: number | RetryConfig): RetryConfig {
    if (typeof retries === 'number') {
      return {
        maxRetries: retries,
        retryDelay: 1000,
        retryCondition: (error: AxiosError): boolean => {
          return (
            error.code === 'ECONNABORTED' ||
            error.code === 'ETIMEDOUT' ||
            (error.response?.status !== undefined && error.response.status >= 500)
          );
        },
      };
    }
    return retries || { maxRetries: 3, retryDelay: 1000 };
  }

  private setupInterceptors(): void {
    this.axios.interceptors.request.use(
      (config) => {
        this.logger?.debug(`[${config.method?.toUpperCase()}] ${config.url}`, {
          params: config.params,
          data: config.data,
        });
        return config;
      },
      (error) => {
        this.logger?.error('Request interceptor error:', error);
        return Promise.reject(error);
      }
    );

    this.axios.interceptors.response.use(
      (response) => {
        this.logger?.debug(`[${response.status}] ${response.config.url}`, {
          data: response.data,
        });
        return response;
      },
      async (error: AxiosError) => {
        const config = error.config;
        if (!config || typeof config._retry !== 'number') {
          if (config) {
            config._retry = 0;
          }
        }

        if (
          config &&
          typeof config._retry === 'number' &&
          config._retry < this.retryConfig.maxRetries &&
          this.retryConfig.retryCondition?.(error)
        ) {
          config._retry = (config._retry || 0) + 1;
          const delay = this.retryConfig.retryDelay * Math.pow(2, (config._retry || 1) - 1);
          
          this.logger?.warn(
            `Retrying request (attempt ${config._retry || 1}/${this.retryConfig.maxRetries})`,
            { url: config?.url, delay }
          );

          await new Promise((resolve) => setTimeout(resolve, delay));
          return this.axios(config);
        }

        this.logger?.error(`Request failed: ${error.message}`, {
          url: config?.url,
          status: error.response?.status,
          data: error.response?.data,
        });

        return Promise.reject(error);
      }
    );
  }

  protected async request<T>(config: RequestConfig & { method: string; url: string }): Promise<T> {
    try {
      const axiosConfig: AxiosRequestConfig = {
        method: config.method,
        url: config.url,
        data: config.data,
        params: config.params,
        headers: config.headers,
        timeout: config.timeout,
        responseType: config.responseType,
      };

      const response: AxiosResponse<T> = await this.axios.request(axiosConfig);
      return response.data;
    } catch (error) {
      handleApiError(error, config.url, config.method);
    }
  }

  protected async get<T>(url: string, params?: Record<string, unknown>, options?: RequestConfig): Promise<T> {
    return this.request<T>({
      method: 'GET',
      url,
      params,
      ...options,
    });
  }

  protected async post<T>(url: string, data?: unknown, options?: RequestConfig): Promise<T> {
    return this.request<T>({
      method: 'POST',
      url,
      data,
      ...options,
    });
  }

  protected async put<T>(url: string, data?: unknown, options?: RequestConfig): Promise<T> {
    return this.request<T>({
      method: 'PUT',
      url,
      data,
      ...options,
    });
  }

  protected async delete<T>(url: string, options?: RequestConfig): Promise<T> {
    return this.request<T>({
      method: 'DELETE',
      url,
      ...options,
    });
  }

  protected async patch<T>(url: string, data?: unknown, options?: RequestConfig): Promise<T> {
    return this.request<T>({
      method: 'PATCH',
      url,
      data,
      ...options,
    });
  }

  protected getCacheKey(prefix: string, ...parts: unknown[]): string {
    return [prefix, ...parts.map(p => JSON.stringify(p))].join(':');
  }

  protected async withCache<T>(
    key: string,
    fetcher: () => Promise<T>,
    ttl?: number
  ): Promise<T> {
    if (!this.cache) {
      return fetcher();
    }

    const cached = await this.cache.get<T>(key);
    if (cached) {
      this.logger?.debug(`Cache hit: ${key}`);
      return cached;
    }

    this.logger?.debug(`Cache miss: ${key}`);
    const result = await fetcher();
    await this.cache.set(key, result, ttl);
    return result;
  }
}