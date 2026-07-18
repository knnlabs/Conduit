import { FetchBaseApiClient } from './client/FetchBaseApiClient';
import { FetchAuthService } from './services/FetchAuthService';
import { FetchVirtualKeyService } from './services/FetchVirtualKeyService';
import { FetchVirtualKeyGroupService } from './services/FetchVirtualKeyGroupService';
import { FetchProvidersService } from './services/FetchProvidersService';
import { FetchSystemService } from './services/FetchSystemService';
import { FetchModelMappingsService } from './services/FetchModelMappingsService';
import { FetchSettingsService } from './services/FetchSettingsService';
import { FetchAnalyticsService } from './services/FetchAnalyticsService';
import { FetchConfigurationService } from './services/FetchConfigurationService';
import { FetchMonitoringService } from './services/FetchMonitoringService';
import { FetchIpFilterService } from './services/FetchIpFilterService';
import { FetchModelCostService } from './services/FetchModelCostService';
import { FetchMediaService } from './services/FetchMediaService';
import { FetchModelService } from './services/FetchModelService';
import { FetchModelSeriesService } from './services/FetchModelSeriesService';
import { FetchModelAuthorService } from './services/FetchModelAuthorService';
// import { FetchModelCapabilitiesService } from './services/FetchModelCapabilitiesService'; // Disabled - capabilities now embedded in Model
import { FetchProviderErrorsService } from './services/FetchProviderErrorsService';
import { ProviderToolsService } from './services/ProviderToolsService';
import { FetchMetricsService } from './services/FetchMetricsService';
import { FetchNotificationsService } from './services/FetchNotificationsService';
import {
  FetchFunctionConfigurationsService,
  FetchFunctionCredentialsService,
  FetchFunctionCostsService,
  FetchFunctionExecutionsService
} from './services/FetchFunctionsService';
import { FetchPricingService } from './services/FetchPricingService';
import type { ApiClientConfig } from './client/types';
import {
  isConduitError,
  isAuthError,
  isRateLimitError,
  isValidationError,
  isNotFoundError,
  isServerError
} from '@knn_labs/conduit-common';

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
  public readonly auth: FetchAuthService;
  public readonly virtualKeys: FetchVirtualKeyService;
  public readonly virtualKeyGroups: FetchVirtualKeyGroupService;
  public readonly providers: FetchProvidersService;
  public readonly system: FetchSystemService;
  public readonly modelMappings: FetchModelMappingsService;
  public readonly settings: FetchSettingsService;
  public readonly analytics: FetchAnalyticsService;
  public readonly configuration: FetchConfigurationService;
  public readonly monitoring: FetchMonitoringService;
  public readonly ipFilters: FetchIpFilterService;
  public readonly modelCosts: FetchModelCostService;
  public readonly media: FetchMediaService;
  public readonly models: FetchModelService;
  public readonly modelSeries: FetchModelSeriesService;
  public readonly modelAuthors: FetchModelAuthorService;
  // public readonly modelCapabilities: FetchModelCapabilitiesService; // Disabled - capabilities now embedded in Model
  public readonly providerErrors: FetchProviderErrorsService;
  public readonly providerTools: ProviderToolsService;
  public readonly metrics: FetchMetricsService;
  public readonly notifications: FetchNotificationsService;
  public readonly functionConfigurations: FetchFunctionConfigurationsService;
  public readonly functionCredentials: FetchFunctionCredentialsService;
  public readonly functionCosts: FetchFunctionCostsService;
  public readonly functionExecutions: FetchFunctionExecutionsService;
  public readonly pricing: FetchPricingService;

  constructor(config: ApiClientConfig) {
    super(config);

    // Initialize services
    this.auth = new FetchAuthService(this);
    this.virtualKeys = new FetchVirtualKeyService(this);
    this.virtualKeyGroups = new FetchVirtualKeyGroupService(this);
    this.providers = new FetchProvidersService(this);
    this.system = new FetchSystemService(this);
    this.modelMappings = new FetchModelMappingsService(this);
    this.settings = new FetchSettingsService(this);
    this.analytics = new FetchAnalyticsService(this);
    this.configuration = new FetchConfigurationService(this);
    this.monitoring = new FetchMonitoringService(this);
    this.ipFilters = new FetchIpFilterService(this);
    this.modelCosts = new FetchModelCostService(this);
    this.media = new FetchMediaService(this);
    this.models = new FetchModelService(this);
    this.modelSeries = new FetchModelSeriesService(this);
    this.modelAuthors = new FetchModelAuthorService(this);
    // this.modelCapabilities = new FetchModelCapabilitiesService(this); // Disabled - capabilities now embedded in Model
    this.providerErrors = new FetchProviderErrorsService(this);
    this.providerTools = new ProviderToolsService(this);
    this.metrics = new FetchMetricsService(this);
    this.notifications = new FetchNotificationsService(this);
    this.functionConfigurations = new FetchFunctionConfigurationsService(this);
    this.functionCredentials = new FetchFunctionCredentialsService(this);
    this.functionCosts = new FetchFunctionCostsService(this);
    this.functionExecutions = new FetchFunctionExecutionsService(this);
    this.pricing = new FetchPricingService(this);
  }

  /**
   * Type guard for checking if an error is a ConduitError
   * Re-exported from @knn_labs/conduit-common for convenience
   */
  isConduitError = isConduitError;

  /**
   * Type guard for checking if an error is an authentication error
   * Re-exported from @knn_labs/conduit-common for convenience
   */
  isAuthError = isAuthError;

  /**
   * Type guard for checking if an error is a rate limit error
   * Re-exported from @knn_labs/conduit-common for convenience
   */
  isRateLimitError = isRateLimitError;

  /**
   * Type guard for checking if an error is a validation error
   * Re-exported from @knn_labs/conduit-common for convenience
   */
  isValidationError = isValidationError;

  /**
   * Type guard for checking if an error is a not found error
   * Re-exported from @knn_labs/conduit-common for convenience
   */
  isNotFoundError = isNotFoundError;

  /**
   * Type guard for checking if an error is a server error
   * Re-exported from @knn_labs/conduit-common for convenience
   */
  isServerError = isServerError;
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
// NOTE: These types are available via components['schemas']['TypeName']
// They are not directly exported from the generated file