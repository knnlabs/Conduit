import { FetchBaseApiClient } from './client/FetchBaseApiClient';
import { FetchVirtualKeyService } from './services/FetchVirtualKeyService';
import { FetchDashboardService } from './services/FetchDashboardService';
import { FetchProvidersService } from './services/FetchProvidersService';
import { FetchSystemService } from './services/FetchSystemService';
import { FetchModelMappingsService } from './services/FetchModelMappingsService';
import { FetchProviderModelsService } from './services/FetchProviderModelsService';
import type { ApiClientConfig } from './client/types';
import { ConduitError } from './utils/errors';

/**
 * Type-safe Conduit Admin Client using native fetch
 * 
 * Provides full type safety for all admin operations without HTTP complexity
 * 
 * @example
 * ```typescript
 * const client = new FetchConduitAdminClient({
 *   baseUrl: 'https://admin.conduit.ai',
 *   masterKey: 'your-master-key'
 * });
 * 
 * // All operations are fully typed
 * const keys = await client.virtualKeys.list();
 * const metrics = await client.dashboard.getMetrics();
 * ```
 */
export class FetchConduitAdminClient extends FetchBaseApiClient {
  public readonly virtualKeys: FetchVirtualKeyService;
  public readonly dashboard: FetchDashboardService;
  public readonly providers: FetchProvidersService;
  public readonly system: FetchSystemService;
  public readonly modelMappings: FetchModelMappingsService;
  public readonly providerModels: FetchProviderModelsService;

  constructor(config: ApiClientConfig) {
    super(config);

    // Initialize services
    this.virtualKeys = new FetchVirtualKeyService(this);
    this.dashboard = new FetchDashboardService(this);
    this.providers = new FetchProvidersService(this);
    this.system = new FetchSystemService(this);
    this.modelMappings = new FetchModelMappingsService(this);
    this.providerModels = new FetchProviderModelsService(this);
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
}

// Export the fetch-based client as the default
export default FetchConduitAdminClient;

// Re-export types for convenience
export type { 
  ApiClientConfig,
  RequestConfig,
  RetryConfig,
  Logger,
  CacheProvider,
} from './client/types';

// Re-export generated types
export type { components, operations, paths } from './generated/admin-api';

// Re-export specific schema types for convenience
export type {
  VirtualKeyDto,
  CreateVirtualKeyRequestDto,
  UpdateVirtualKeyRequestDto,
  VirtualKeyValidationResult,
  GlobalSettingDto,
  CreateGlobalSettingDto,
  UpdateGlobalSettingDto,
  ModelProviderMappingDto,
  CreateModelProviderMappingDto,
  ProviderCredentialDto,
  CreateProviderCredentialDto,
  UpdateProviderCredentialDto,
} from './generated/admin-api';