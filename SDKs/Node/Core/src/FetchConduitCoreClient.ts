import { FetchBasedClient } from './client/FetchBasedClient';
import { FetchChatService } from './services/FetchChatService';
import { AudioService } from './services/AudioService';
import { AuthService } from './services/AuthService';
import { HealthService } from './services/HealthService';
import { ImagesService } from './services/ImagesService';
import { VideosService } from './services/VideosService';
import { DiscoveryService } from './services/DiscoveryService';
import { ProviderModelsService } from './services/ProviderModelsService';
import type { ClientConfig } from './client/types';
import { ConduitError } from './utils/errors';

/**
 * Type-safe Conduit Core Client using native fetch
 * 
 * Provides full type safety for all operations with TypeScript generics
 * and OpenAPI-generated types, without the complexity of HTTP.
 * 
 * @example
 * ```typescript
 * const client = new FetchConduitCoreClient({
 *   apiKey: 'your-api-key',
 *   baseURL: 'https://api.conduit.ai'
 * });
 * 
 * // All operations are fully typed
 * const response = await client.chat.create({
 *   model: 'gpt-4',
 *   messages: [{ role: 'user', content: 'Hello' }]
 * });
 * ```
 */
export class FetchConduitCoreClient extends FetchBasedClient {
  public readonly chat: FetchChatService;
  public readonly audio: AudioService;
  public readonly auth: AuthService;
  public readonly health: HealthService;
  public readonly images: ImagesService;
  public readonly videos: VideosService;
  public readonly discovery: DiscoveryService;
  public readonly providerModels: ProviderModelsService;

  constructor(config: ClientConfig) {
    super(config);

    // Initialize services
    this.chat = new FetchChatService(config);
    this.audio = new AudioService(this);
    this.auth = new AuthService(this);
    this.health = new HealthService(this);
    this.images = new ImagesService(this);
    this.videos = new VideosService(this);
    this.discovery = new DiscoveryService(this);
    this.providerModels = new ProviderModelsService(this);
  }

  /**
   * Type guard for checking if an error is a ConduitError
   */
  isConduitError(error: unknown): error is ConduitError {
    return error instanceof ConduitError;
  }

  /**
   * Type guard for checking if an error is an authentication error
   */
  isAuthError(error: unknown): error is ConduitError {
    return this.isConduitError(error) && error.statusCode === 401;
  }

  /**
   * Type guard for checking if an error is a rate limit error
   */
  isRateLimitError(error: unknown): error is ConduitError {
    return this.isConduitError(error) && error.statusCode === 429;
  }

  /**
   * Type guard for checking if an error is a validation error
   */
  isValidationError(error: unknown): error is ConduitError {
    return this.isConduitError(error) && error.statusCode === 400;
  }

  /**
   * Type guard for checking if an error is a not found error
   */
  isNotFoundError(error: unknown): error is ConduitError {
    return this.isConduitError(error) && error.statusCode === 404;
  }

  /**
   * Type guard for checking if an error is a server error
   */
  isServerError(error: unknown): error is ConduitError {
    return this.isConduitError(error) && 
           error.statusCode !== undefined && 
           error.statusCode >= 500;
  }

  /**
   * Type guard for checking if an error is a network error
   */
  isNetworkError(error: unknown): error is ConduitError {
    return this.isConduitError(error) && 
           (error.code === 'ECONNABORTED' || error.code === 'network_error');
  }
}

// Export the fetch-based client as the default
export default FetchConduitCoreClient;

// Re-export types for convenience
export type { 
  ClientConfig,
  RequestOptions,
  RetryConfig,
} from './client/types';

// Re-export generated types
export type { components, operations, paths } from './generated/core-api';

// Re-export error types
export { 
  ConduitError,
  AuthError,
  RateLimitError,
  NetworkError,
} from './utils/errors';