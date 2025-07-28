import { BaseApiClient, BaseClientConfig } from '@knn_labs/conduit-common';
import type { ClientConfig } from './types';
import type { ErrorResponse } from '../models/common';
import { 
  ConduitError, 
  AuthError, 
  RateLimitError, 
  NetworkError,
} from '../utils/errors';
import { ERROR_CODES } from '../constants';

/**
 * Type-safe base client for Core SDK using native fetch API
 * Extends the common base client with Core-specific authentication
 */
export abstract class FetchBasedClient extends BaseApiClient {
  private readonly apiKey: string;
  private readonly signalRConfig: ClientConfig['signalR'];

  constructor(config: ClientConfig) {
    const baseConfig: BaseClientConfig = {
      baseURL: config.baseURL ?? 'https://api.conduit.ai',
      timeout: config.timeout ?? 60000,
      retries: config.maxRetries ?? 3,
      headers: config.headers ?? {},
      debug: config.debug ?? false,
      retryDelay: config.retryDelay ?? [1000, 2000, 4000, 8000, 16000],
      onError: config.onError,
      onRequest: config.onRequest,
      onResponse: config.onResponse,
    };
    super(baseConfig);
    
    this.apiKey = config.apiKey;
    this.signalRConfig = config.signalR ?? {};
  }

  /**
   * Provide Core SDK authentication headers
   */
  protected getAuthHeaders(): Record<string, string> {
    return {
      'Authorization': `Bearer ${this.apiKey}`
    };
  }
  
  /**
   * Get SignalR configuration
   */
  public getSignalRConfig(): ClientConfig['signalR'] {
    return this.signalRConfig;
  }
  
  /**
   * Get API key for services that need direct access
   */
  public getApiKey(): string {
    return this.apiKey;
  }

  protected async handleErrorResponse(response: Response): Promise<Error> {
    let errorData: ErrorResponse | undefined;
    
    try {
      const contentType = response.headers.get('content-type');
      if (contentType?.includes('application/json')) {
        errorData = await response.json() as unknown as ErrorResponse;
      }
    } catch {
      // Ignore JSON parsing errors
    }

    const status = response.status;
    
    if (status === 401) {
      return new AuthError(
        errorData?.error?.message ?? 'Authentication failed',
        { code: errorData?.error?.code ?? 'auth_error' }
      );
    } else if (status === 429) {
      const retryAfter = response.headers.get('retry-after');
      return new RateLimitError(
        errorData?.error?.message ?? 'Rate limit exceeded',
        retryAfter ? parseInt(retryAfter, 10) : undefined
      );
    } else if (status === 400) {
      return new ConduitError(
        errorData?.error?.message ?? 'Bad request',
        status,
        errorData?.error?.code ?? 'bad_request'
      );
    } else if (errorData?.error) {
      return new ConduitError(
        errorData.error.message,
        status,
        errorData.error.code ?? undefined
      );
    } else {
      return new ConduitError(
        `Request failed with status ${status}`,
        status,
        'http_error'
      );
    }
  }

  /**
   * Override base class to add Core SDK specific retry logic
   */
  protected shouldRetry(error: unknown): boolean {
    if (error instanceof ConduitError) {
      const status = error.statusCode;
      return status === 429 || status === 503 || status === 504;
    }
    
    // Fall back to base class implementation
    return super.shouldRetry(error);
  }

  protected handleError(error: unknown): Error {
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