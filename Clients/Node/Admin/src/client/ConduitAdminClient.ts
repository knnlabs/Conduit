import { ConduitConfig, ApiClientConfig } from './types';
import { VirtualKeyService } from '../services/VirtualKeyService';
import { ProviderService } from '../services/ProviderService';
import { ModelMappingService } from '../services/ModelMappingService';
import { SettingsService } from '../services/SettingsService';
import { IpFilterService } from '../services/IpFilterService';
import { ModelCostService } from '../services/ModelCostService';
import { AnalyticsService } from '../services/AnalyticsService';
import { SystemService } from '../services/SystemService';
import { DiscoveryService } from '../services/DiscoveryService';
import { ProviderModelsService } from '../services/ProviderModelsService';
import { AudioConfigurationService } from '../services/AudioConfigurationService';
import { MetricsService } from '../services/MetricsService';
import { ProviderHealthService } from '../services/ProviderHealthService';
import { NotificationsService } from '../services/NotificationsService';
import { DatabaseBackupService } from '../services/DatabaseBackupService';
import { SignalRService } from '../services/SignalRService';
import { RealtimeNotificationsService } from '../services/RealtimeNotificationsService';
import { ValidationError } from '../utils/errors';
import { z } from 'zod';

const configSchema = z.object({
  masterKey: z.string().min(1, 'Master key is required'),
  adminApiUrl: z.string().url('Admin API URL must be a valid URL'),
  conduitApiUrl: z.string().url().optional(),
  options: z.object({
    timeout: z.number().positive().optional(),
    retries: z.union([
      z.number().nonnegative(),
      z.object({
        maxRetries: z.number().nonnegative(),
        retryDelay: z.number().positive(),
        retryCondition: z.function().optional(),
      })
    ]).optional(),
    logger: z.any().optional(),
    cache: z.any().optional(),
    headers: z.record(z.string()).optional(),
    validateStatus: z.function().optional(),
  }).optional(),
});

export class ConduitAdminClient {
  public readonly virtualKeys: VirtualKeyService;
  public readonly providers: ProviderService;
  public readonly modelMappings: ModelMappingService;
  public readonly settings: SettingsService;
  public readonly ipFilters: IpFilterService;
  public readonly modelCosts: ModelCostService;
  public readonly analytics: AnalyticsService;
  public readonly system: SystemService;
  public readonly discovery: DiscoveryService;
  public readonly providerModels: ProviderModelsService;
  public readonly audioConfiguration: AudioConfigurationService;
  public readonly metrics: MetricsService;
  public readonly providerHealth: ProviderHealthService;
  public readonly notifications: NotificationsService;
  public readonly databaseBackup: DatabaseBackupService;
  public readonly signalr: SignalRService;
  public readonly realtimeNotifications: RealtimeNotificationsService;

  private readonly config: ConduitConfig;

  constructor(config: ConduitConfig) {
    try {
      configSchema.parse(config);
    } catch (error) {
      throw new ValidationError('Invalid Conduit client configuration', error);
    }

    this.config = config;
    
    const baseConfig: ApiClientConfig = {
      baseUrl: this.normalizeUrl(config.adminApiUrl),
      masterKey: config.masterKey,
      timeout: config.options?.timeout,
      retries: config.options?.retries,
      logger: config.options?.logger,
      cache: config.options?.cache,
      defaultHeaders: config.options?.headers,
      retryDelay: config.options?.retryDelay,
      onError: config.options?.onError,
      onRequest: config.options?.onRequest,
      onResponse: config.options?.onResponse,
    };

    this.virtualKeys = new VirtualKeyService(baseConfig);
    this.providers = new ProviderService(baseConfig);
    this.modelMappings = new ModelMappingService(baseConfig);
    this.settings = new SettingsService(baseConfig);
    this.ipFilters = new IpFilterService(baseConfig);
    this.modelCosts = new ModelCostService(baseConfig);
    this.analytics = new AnalyticsService(baseConfig);
    this.system = new SystemService(baseConfig);
    this.discovery = new DiscoveryService(baseConfig);
    this.providerModels = new ProviderModelsService(baseConfig);
    this.audioConfiguration = new AudioConfigurationService(baseConfig);
    this.metrics = new MetricsService(baseConfig);
    this.providerHealth = new ProviderHealthService(baseConfig);
    this.notifications = new NotificationsService(baseConfig);
    this.databaseBackup = new DatabaseBackupService(baseConfig);
    
    // Initialize SignalR services
    const signalRConfig = config.options?.signalR ?? {};
    this.signalr = new SignalRService(
      config.adminApiUrl.replace(/\/api$/, ''), // Remove /api suffix for SignalR
      config.masterKey,
      {
        logLevel: signalRConfig.logLevel,
        transport: signalRConfig.transport,
        headers: signalRConfig.headers,
        reconnectionDelay: signalRConfig.reconnectDelay,
      }
    );
    
    this.realtimeNotifications = new RealtimeNotificationsService(this.signalr);
    
    // Auto-connect SignalR if enabled
    if (signalRConfig.enabled !== false && signalRConfig.autoConnect !== false) {
      this.signalr.connectAll().catch(error => {
        if (config.options?.logger) {
          config.options.logger.error('Failed to connect to SignalR:', error);
        }
      });
    }
  }

  static fromEnvironment(env?: {
    CONDUIT_MASTER_KEY?: string;
    CONDUIT_ADMIN_API_URL?: string;
    CONDUIT_ADMIN_API_BASE_URL?: string;
    CONDUIT_API_URL?: string;
  }): ConduitAdminClient {
    const environment = env || process.env;
    
    const masterKey = environment.CONDUIT_MASTER_KEY;
    // Support both environment variable names for backward compatibility
    const adminApiUrl = environment.CONDUIT_ADMIN_API_URL || environment.CONDUIT_ADMIN_API_BASE_URL;
    const conduitApiUrl = environment.CONDUIT_API_URL;

    if (!masterKey) {
      throw new ValidationError('CONDUIT_MASTER_KEY environment variable is required');
    }

    if (!adminApiUrl) {
      throw new ValidationError('Either CONDUIT_ADMIN_API_URL or CONDUIT_ADMIN_API_BASE_URL environment variable is required');
    }

    return new ConduitAdminClient({
      masterKey,
      adminApiUrl,
      conduitApiUrl,
    });
  }

  getConfig(): Readonly<ConduitConfig> {
    return { ...this.config };
  }

  private normalizeUrl(url: string): string {
    // Remove trailing slash
    const normalized = url.endsWith('/') ? url.slice(0, -1) : url;
    
    // Auto-append /api if not already present
    if (!normalized.endsWith('/api')) {
      return `${normalized}/api`;
    }
    
    return normalized;
  }
}