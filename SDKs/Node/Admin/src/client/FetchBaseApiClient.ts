import { BaseApiClient, BaseClientConfig } from '@knn_labs/conduit-common';
import type { ApiClientConfig } from './types';
import { handleApiError } from '../utils/errors';
import { HTTP_HEADERS } from '../constants';

/**
 * Type-safe base API client for Conduit Admin using native fetch
 * Extends the common base client with Admin-specific authentication
 */
export abstract class FetchBaseApiClient extends BaseApiClient {
  private readonly masterKey: string;

  constructor(config: ApiClientConfig) {
    const baseConfig: BaseClientConfig = {
      baseURL: config.baseUrl.replace(/\/$/, ''), // Remove trailing slash
      timeout: config.timeout ?? 30000,
      retries: config.retries,
      headers: config.defaultHeaders ?? {},
      debug: false, // Admin SDK doesn't have debug flag in config
      retryDelay: config.retryDelay,
      logger: config.logger,
      cache: config.cache,
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse,
    };
    super(baseConfig);
    
    this.masterKey = config.masterKey;
  }

  /**
   * Provide Admin SDK authentication headers
   */
  protected getAuthHeaders(): Record<string, string> {
    return {
      [HTTP_HEADERS.X_API_KEY]: this.masterKey
    };
  }

  /**
   * Override request to add console.warn for SDK requests
   */
  protected async request<TResponse = unknown, TRequest = unknown>(
    url: string,
    options: Parameters<BaseApiClient['request']>[1] = {}
  ): Promise<TResponse> {
    console.warn('[SDK] API Request:', options.method ?? 'GET', this.buildFullUrl(url));
    return super.request<TResponse, TRequest>(url, options as any);
  }

  /**
   * Build URL from path using base class method
   */
  private buildFullUrl(path: string): string {
    // Access protected method through type assertion
    return (this as any).buildUrl(path);
  }
  
  /**
   * Get master key for services that need direct access
   */
  public getMasterKey(): string {
    return this.masterKey;
  }
  
  /**
   * Get base URL for services that need direct access
   */
  public getBaseUrl(): string {
    return super.getBaseURL();
  }
  
  /**
   * Get timeout for services that need direct access
   */
  public getTimeout(): number {
    return super.getTimeout();
  }

  /**
   * Handle error responses specific to Admin API
   */
  protected async handleErrorResponse(response: Response): Promise<Error> {
    const headers: Record<string, string> = {};
    response.headers.forEach((value, key) => {
      headers[key] = value;
    });
    
    console.error('[SDK] API Error Response:', {
      url: response.url,
      status: response.status,
      statusText: response.statusText,
      method: 'N/A'
    });
    
    const errorData = await this.parseErrorResponse(response);
    
    const apiError = handleApiError({
      response: {
        status: response.status,
        data: errorData,
        headers,
      },
      config: { url: response.url, method: 'N/A' },
      isHttpError: false,
      message: `HTTP ${response.status}: ${response.statusText}`,
    });
    
    return apiError;
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
}