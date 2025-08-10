import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { components } from '../generated/admin-api';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type { 
  SystemInfoDto, 
  HealthStatusDto,
  SystemHealthDto,
  SystemMetricsDto,
  ServiceStatusDto,
  HealthEventsResponseDto,
  HealthEventSubscriptionOptions,
  HealthEventSubscription
} from '../models/system';
import type { 
  BackendSystemInfoResponse,
  MetricsParams,
  PerformanceMetrics,
  ExportParams,
  ExportResult,
  ISystemService
} from './types/system-service.types';
import { FetchSystemHelpers } from './FetchSystemHelpers';
import { FetchSystemHealthService } from './FetchSystemHealthService';
import { FetchSystemMetricsService } from './FetchSystemMetricsService';

/**
 * Type-safe System service using native fetch
 */
export class FetchSystemService implements ISystemService {
  private helpers: FetchSystemHelpers;
  private healthService: FetchSystemHealthService;
  private metricsService: FetchSystemMetricsService;

  constructor(private readonly client: FetchBaseApiClient) {
    this.helpers = new FetchSystemHelpers();
    this.healthService = new FetchSystemHealthService(client);
    this.metricsService = new FetchSystemMetricsService(client);
  }

  /**
   * Get system information
   */
  async getSystemInfo(config?: RequestConfig): Promise<SystemInfoDto> {
    const response = await this.client['get']<BackendSystemInfoResponse>(
      ENDPOINTS.SYSTEM.INFO,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
    
    return this.helpers.transformSystemInfoResponse(response);
  }

  /**
   * Get system health status
   */
  async getHealth(config?: RequestConfig): Promise<HealthStatusDto> {
    return this.client['get']<HealthStatusDto>(
      ENDPOINTS.SYSTEM.HEALTH,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }


  /**
   * Get performance metrics (optional)
   * Delegates to FetchSystemMetricsService
   */
  async getPerformanceMetrics(
    params?: MetricsParams,
    config?: RequestConfig
  ): Promise<PerformanceMetrics> {
    return this.metricsService.getPerformanceMetrics(params, config);
  }

  /**
   * Export performance data (optional)
   * Delegates to FetchSystemMetricsService
   */
  async exportPerformanceData(
    params: ExportParams,
    config?: RequestConfig
  ): Promise<ExportResult> {
    return this.metricsService.exportPerformanceData(params, config);
  }

  /**
   * Get comprehensive system health status and metrics.
   * Delegates to FetchSystemHealthService
   */
  async getSystemHealth(config?: RequestConfig): Promise<SystemHealthDto> {
    return this.healthService.getSystemHealth(config);
  }

  /**
   * Get detailed system resource metrics.
   * Delegates to FetchSystemMetricsService
   */
  async getSystemMetrics(config?: RequestConfig): Promise<SystemMetricsDto> {
    return this.metricsService.getSystemMetrics(config);
  }

  /**
   * Get health status of individual services.
   * Delegates to FetchSystemHealthService
   */
  async getServiceStatus(config?: RequestConfig): Promise<ServiceStatusDto> {
    return this.healthService.getServiceStatus(config);
  }

  /**
   * Get system uptime in seconds.
   * Delegates to FetchSystemMetricsService
   */
  async getUptime(config?: RequestConfig): Promise<number> {
    return this.metricsService.getUptime(config);
  }

  /**
   * Get the number of active connections to the system.
   * Delegates to FetchSystemMetricsService
   */
  async getActiveConnections(config?: RequestConfig): Promise<number> {
    return this.metricsService.getActiveConnections(config);
  }

  /**
   * Get recent health events for the system.
   * Delegates to FetchSystemHealthService
   */
  async getHealthEvents(limit?: number, config?: RequestConfig): Promise<HealthEventsResponseDto> {
    return this.healthService.getHealthEvents(limit, config);
  }

  /**
   * Subscribe to real-time health event updates.
   * Delegates to FetchSystemHealthService
   */
  async subscribeToHealthEvents(
    options?: HealthEventSubscriptionOptions,
    config?: RequestConfig
  ): Promise<HealthEventSubscription> {
    return this.healthService.subscribeToHealthEvents(options, config);
  }

  /**
   * Helper method to check if system is healthy
   * Delegates to FetchSystemHelpers
   */
  isSystemHealthy(health: HealthStatusDto): boolean {
    return this.helpers.isSystemHealthy(health);
  }

  /**
   * Helper method to get unhealthy services
   * Delegates to FetchSystemHelpers
   */
  getUnhealthyServices(health: HealthStatusDto): string[] {
    return this.helpers.getUnhealthyServices(health);
  }

  /**
   * Helper method to format uptime
   * Delegates to FetchSystemHelpers
   */
  formatUptime(uptimeSeconds: number): string {
    return this.helpers.formatUptime(uptimeSeconds);
  }

  /**
   * Helper method to check if a feature is enabled
   * Delegates to FetchSystemHelpers
   */
  isFeatureEnabled(systemInfo: SystemInfoDto, feature: keyof SystemInfoDto['features']): boolean {
    return this.helpers.isFeatureEnabled(systemInfo, feature);
  }

  /**
   * Gets or creates the special WebUI virtual key.
   * This key is stored unencrypted in GlobalSettings for WebUI/TUI access.
   * @returns The actual (unhashed) virtual key value
   */
  async getWebUIVirtualKey(config?: RequestConfig): Promise<string> {
    // Import services we need
    const { FetchSettingsService } = await import('./FetchSettingsService');
    const { FetchVirtualKeyService } = await import('./FetchVirtualKeyService');
    const { FetchVirtualKeyGroupService } = await import('./FetchVirtualKeyGroupService');
    
    const settingsService = new FetchSettingsService(this.client);
    const virtualKeyService = new FetchVirtualKeyService(this.client);
    
    let existingKey: string | null = null;
    
    try {
      // First try to get existing key from GlobalSettings
      const setting = await settingsService.getGlobalSetting('WebUI_VirtualKey', config);
      if (setting?.value) {
        existingKey = setting.value;
        console.warn('[SDK] Found WebUI virtual key in GlobalSettings, validating...');
        
        // Validate that the key exists in VirtualKeys table
        try {
          // Try to validate the key by checking if it works
          const validationResult = await virtualKeyService.validate(existingKey, config);
          
          if (!validationResult?.isValid) {
            console.warn('[SDK] WebUI virtual key from GlobalSettings is not valid');
            existingKey = null;
          } else {
            console.warn('[SDK] WebUI virtual key validated successfully');
            return existingKey;
          }
        } catch (validationError) {
          console.error('[SDK] Failed to validate WebUI virtual key', validationError);
          existingKey = null;
        }
      }
    } catch {
      // Key doesn't exist in GlobalSettings
      console.warn('[SDK] WebUI virtual key not found in GlobalSettings');
    }

    // If we don't have a valid key, create a new one
    console.warn('[SDK] Creating new WebUI virtual key with group and $1000 balance');
    
    // First, create a virtual key group with $1000 initial balance
    const virtualKeyGroupService = new FetchVirtualKeyGroupService(this.client);
    const group = await virtualKeyGroupService.create({
      groupName: 'WebUI Internal Group',
      externalGroupId: 'webui-internal',
      initialBalance: 1000.00
    }, config);
    
    console.warn(`[SDK] Created WebUI virtual key group with ID ${group.id} and $1000 balance`);
    
    // Create metadata
    const metadata = {
      visibility: 'hidden',
      created: new Date().toISOString(),
      originator: 'Admin SDK',
      groupId: group.id
    };

    // Create the virtual key and associate it with the group
    const virtualKeyRequest = {
      keyName: 'WebUI Internal Key',
      metadata: JSON.stringify(metadata),
      virtualKeyGroupId: group.id
    } as components['schemas']['ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyRequestDto'];
    
    const response = await virtualKeyService.create(virtualKeyRequest, config);
    
    if (!response.virtualKey) {
      throw new Error('Failed to create virtual key: No key returned');
    }

    // Store the unhashed key in GlobalSettings
    await settingsService.createGlobalSetting({
      key: 'WebUI_VirtualKey',
      value: response.virtualKey,
      isSecret: true,
      category: 'WebUI',
      description: 'Virtual key for WebUI Core API access'
    }, config);
    
    console.warn('[SDK] Created new WebUI virtual key and stored in GlobalSettings');
    return response.virtualKey;
  }
}