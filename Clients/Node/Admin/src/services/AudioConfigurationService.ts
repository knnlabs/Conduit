import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { AudioProviderSettings } from '../models/common-types';
import type {
  AudioProviderConfigRequest,
  AudioProviderConfigDto,
  AudioCostConfigRequest,
  AudioCostConfigDto,
  AudioUsageDto,
  AudioUsageSummaryDto,
  AudioUsageFilters,
  AudioUsageSummaryFilters,
  RealtimeSessionDto,
  AudioProviderTestResult,
} from '../models/audioConfiguration';
import type { PagedResponse } from '../models/common';
import type { ExportResult } from '../models/analyticsExport';
import {
  validateAudioProviderRequest,
  validateAudioCostConfigRequest,
  validateAudioUsageFilters,
} from '../models/audioConfiguration';
import { ENDPOINTS } from '../constants';

/**
 * Service for managing audio provider configurations, cost settings, and usage analytics
 */
export class AudioConfigurationService extends FetchBaseApiClient {
  private static readonly PROVIDERS_ENDPOINT = '/admin/audio/providers';
  private static readonly COSTS_ENDPOINT = '/admin/audio/costs';
  private static readonly USAGE_ENDPOINT = '/admin/audio/usage';
  private static readonly SESSIONS_ENDPOINT = '/admin/audio/sessions';

  // #region Provider Configuration

  /**
   * Creates a new audio provider configuration
   */
  async createProvider(request: AudioProviderConfigRequest): Promise<AudioProviderConfigDto> {
    validateAudioProviderRequest(request);
    
    return this.post<AudioProviderConfigDto>(
      AudioConfigurationService.PROVIDERS_ENDPOINT,
      request
    );
  }

  /**
   * Gets all audio provider configurations
   */
  async getProviders(): Promise<AudioProviderConfigDto[]> {
    return this.get<AudioProviderConfigDto[]>(
      AudioConfigurationService.PROVIDERS_ENDPOINT
    );
  }

  /**
   * Gets enabled audio providers for a specific operation type
   */
  async getEnabledProviders(operationType: string): Promise<AudioProviderConfigDto[]> {
    if (!operationType || operationType.trim().length === 0) {
      throw new Error('Operation type is required');
    }

    const endpoint = `${AudioConfigurationService.PROVIDERS_ENDPOINT}/enabled/${encodeURIComponent(operationType)}`;
    
    return this.get<AudioProviderConfigDto[]>(endpoint);
  }

  /**
   * Gets a specific audio provider configuration by ID
   */
  async getProvider(providerId: string): Promise<AudioProviderConfigDto> {
    if (!providerId || providerId.trim().length === 0) {
      throw new Error('Provider ID is required');
    }

    const endpoint = `${AudioConfigurationService.PROVIDERS_ENDPOINT}/${encodeURIComponent(providerId)}`;
    
    return this.get<AudioProviderConfigDto>(endpoint);
  }

  /**
   * Updates an existing audio provider configuration
   */
  async updateProvider(
    providerId: string,
    request: AudioProviderConfigRequest
  ): Promise<AudioProviderConfigDto> {
    if (!providerId || providerId.trim().length === 0) {
      throw new Error('Provider ID is required');
    }

    validateAudioProviderRequest(request);

    const endpoint = `${AudioConfigurationService.PROVIDERS_ENDPOINT}/${encodeURIComponent(providerId)}`;
    
    return this.put<AudioProviderConfigDto>(
      endpoint,
      request
    );
  }

  /**
   * Deletes an audio provider configuration
   */
  async deleteProvider(providerId: string): Promise<void> {
    if (!providerId || providerId.trim().length === 0) {
      throw new Error('Provider ID is required');
    }

    const endpoint = `${AudioConfigurationService.PROVIDERS_ENDPOINT}/${encodeURIComponent(providerId)}`;
    
    await this.delete<void>(endpoint);
  }

  /**
   * Tests the connectivity and configuration of an audio provider
   */
  async testProvider(providerId: string): Promise<AudioProviderTestResult> {
    if (!providerId || providerId.trim().length === 0) {
      throw new Error('Provider ID is required');
    }

    const endpoint = `${AudioConfigurationService.PROVIDERS_ENDPOINT}/${encodeURIComponent(providerId)}/test`;
    
    return this.post<AudioProviderTestResult>(
      endpoint,
      {}
    );
  }

  // #endregion

  // #region Cost Configuration

  /**
   * Creates a new audio cost configuration
   */
  async createCostConfig(request: AudioCostConfigRequest): Promise<AudioCostConfigDto> {
    validateAudioCostConfigRequest(request);
    
    return this.post<AudioCostConfigDto>(
      AudioConfigurationService.COSTS_ENDPOINT,
      request
    );
  }

  /**
   * Gets all audio cost configurations
   */
  async getCostConfigs(): Promise<AudioCostConfigDto[]> {
    return this.get<AudioCostConfigDto[]>(
      AudioConfigurationService.COSTS_ENDPOINT
    );
  }

  /**
   * Gets a specific audio cost configuration by ID
   */
  async getCostConfig(configId: string): Promise<AudioCostConfigDto> {
    if (!configId || configId.trim().length === 0) {
      throw new Error('Cost configuration ID is required');
    }

    const endpoint = `${AudioConfigurationService.COSTS_ENDPOINT}/${encodeURIComponent(configId)}`;
    
    return this.get<AudioCostConfigDto>(endpoint);
  }

  /**
   * Updates an existing audio cost configuration
   */
  async updateCostConfig(
    configId: string,
    request: AudioCostConfigRequest
  ): Promise<AudioCostConfigDto> {
    if (!configId || configId.trim().length === 0) {
      throw new Error('Cost configuration ID is required');
    }

    validateAudioCostConfigRequest(request);

    const endpoint = `${AudioConfigurationService.COSTS_ENDPOINT}/${encodeURIComponent(configId)}`;
    
    return this.put<AudioCostConfigDto>(
      endpoint,
      request
    );
  }

  /**
   * Deletes an audio cost configuration
   */
  async deleteCostConfig(configId: string): Promise<void> {
    if (!configId || configId.trim().length === 0) {
      throw new Error('Cost configuration ID is required');
    }

    const endpoint = `${AudioConfigurationService.COSTS_ENDPOINT}/${encodeURIComponent(configId)}`;
    
    await this.delete<void>(endpoint);
  }

  // #endregion

  // #region Usage Analytics

  /**
   * Gets audio usage data with optional filtering
   */
  async getUsage(filters?: AudioUsageFilters): Promise<PagedResponse<AudioUsageDto>> {
    if (filters) {
      validateAudioUsageFilters(filters);
    }

    const queryParams: string[] = [];
    
    if (filters?.startDate) {
      queryParams.push(`startDate=${encodeURIComponent(filters.startDate)}`);
    }
    
    if (filters?.endDate) {
      queryParams.push(`endDate=${encodeURIComponent(filters.endDate)}`);
    }
    
    if (filters?.virtualKey) {
      queryParams.push(`virtualKey=${encodeURIComponent(filters.virtualKey)}`);
    }
    
    if (filters?.provider) {
      queryParams.push(`provider=${encodeURIComponent(filters.provider)}`);
    }
    
    if (filters?.operationType) {
      queryParams.push(`operationType=${encodeURIComponent(filters.operationType)}`);
    }
    
    if (filters?.page !== undefined) {
      queryParams.push(`page=${filters.page}`);
    }
    
    if (filters?.pageSize !== undefined) {
      queryParams.push(`pageSize=${filters.pageSize}`);
    }

    const endpoint = queryParams.length > 0 
      ? `${AudioConfigurationService.USAGE_ENDPOINT}?${queryParams.join('&')}`
      : AudioConfigurationService.USAGE_ENDPOINT;
    
    return this.get<PagedResponse<AudioUsageDto>>(endpoint);
  }

  /**
   * Gets audio usage summary for a date range
   */
  async getUsageSummary(filters: AudioUsageSummaryFilters): Promise<AudioUsageSummaryDto> {
    if (!filters.startDate || !filters.endDate) {
      throw new Error('Start date and end date are required for usage summary');
    }

    if (new Date(filters.startDate) >= new Date(filters.endDate)) {
      throw new Error('Start date must be before end date');
    }

    const queryParams: string[] = [
      `startDate=${encodeURIComponent(filters.startDate)}`,
      `endDate=${encodeURIComponent(filters.endDate)}`,
    ];
    
    if (filters.virtualKey) {
      queryParams.push(`virtualKey=${encodeURIComponent(filters.virtualKey)}`);
    }
    
    if (filters.provider) {
      queryParams.push(`provider=${encodeURIComponent(filters.provider)}`);
    }
    
    if (filters.operationType) {
      queryParams.push(`operationType=${encodeURIComponent(filters.operationType)}`);
    }

    const endpoint = `${AudioConfigurationService.USAGE_ENDPOINT}/summary?${queryParams.join('&')}`;
    
    return this.get<AudioUsageSummaryDto>(endpoint);
  }

  // #endregion

  // #region Real-time Sessions

  /**
   * Gets all active real-time audio sessions
   */
  async getActiveSessions(): Promise<RealtimeSessionDto[]> {
    return this.get<RealtimeSessionDto[]>(
      AudioConfigurationService.SESSIONS_ENDPOINT
    );
  }

  /**
   * Gets a specific real-time session by ID
   */
  async getSession(sessionId: string): Promise<RealtimeSessionDto> {
    if (!sessionId || sessionId.trim().length === 0) {
      throw new Error('Session ID is required');
    }

    const endpoint = `${AudioConfigurationService.SESSIONS_ENDPOINT}/${encodeURIComponent(sessionId)}`;
    
    return this.get<RealtimeSessionDto>(endpoint);
  }

  /**
   * Terminates an active real-time audio session
   */
  async terminateSession(sessionId: string): Promise<{ success: boolean; sessionId: string; message?: string }> {
    if (!sessionId || sessionId.trim().length === 0) {
      throw new Error('Session ID is required');
    }

    const endpoint = `${AudioConfigurationService.SESSIONS_ENDPOINT}/${encodeURIComponent(sessionId)}/terminate`;
    
    try {
      const response = await this.post<{ success: boolean; message?: string }>(endpoint, {});
      return {
        success: response.success,
        sessionId,
        message: response.message,
      };
    } catch (error) {
      // If the session is already terminated or doesn't exist, handle gracefully
      if (error && typeof error === 'object' && 'status' in error) {
        if (error.status === 404) {
          throw new Error('Session not found or already terminated');
        } else if (error.status === 409) {
          throw new Error('Session is already terminated');
        }
      }
      throw error;
    }
  }

  // #endregion

  // #region Export

  /**
   * Exports audio usage data in the specified format
   */
  async exportAudioUsage(params: {
    startDate: string;
    endDate: string;
    format: 'csv' | 'json';
    virtualKey?: string;
    provider?: string;
    operationType?: string;
  }): Promise<ExportResult> {
    if (!params.startDate || !params.endDate) {
      throw new Error('Start date and end date are required for audio usage export');
    }

    if (!params.format) {
      throw new Error('Export format is required');
    }

    if (new Date(params.startDate) >= new Date(params.endDate)) {
      throw new Error('Start date must be before end date');
    }

    const requestBody = {
      format: params.format,
      dateRange: {
        startDate: params.startDate,
        endDate: params.endDate,
      },
      filters: {
        virtualKey: params.virtualKey,
        provider: params.provider,
        operationType: params.operationType,
      },
    };

    return this.post<ExportResult>(
      ENDPOINTS.ANALYTICS.EXPORT_AUDIO_USAGE,
      requestBody
    );
  }

  // #endregion
}

/**
 * Helper functions for audio configuration management
 */
export const AudioConfigurationHelpers = {
  /**
   * Creates a basic audio provider configuration request
   */
  createProviderRequest(
    name: string,
    baseUrl: string,
    apiKey: string,
    options?: {
      isEnabled?: boolean;
      supportedOperations?: string[];
      priority?: number;
      timeoutSeconds?: number;
      settings?: AudioProviderSettings;
    }
  ): AudioProviderConfigRequest {
    return {
      name,
      baseUrl,
      apiKey,
      isEnabled: options?.isEnabled ?? true,
      supportedOperations: options?.supportedOperations ?? [],
      priority: options?.priority ?? 1,
      timeoutSeconds: options?.timeoutSeconds ?? 30,
      settings: options?.settings,
    };
  },

  /**
   * Creates a basic audio cost configuration request
   */
  createCostConfigRequest(
    providerId: string,
    operationType: string,
    costPerUnit: number,
    unitType: string,
    options?: {
      modelName?: string;
      currency?: string;
      isActive?: boolean;
      effectiveFrom?: string;
      effectiveTo?: string;
    }
  ): AudioCostConfigRequest {
    return {
      providerId,
      operationType,
      costPerUnit,
      unitType,
      modelName: options?.modelName,
      currency: options?.currency ?? 'USD',
      isActive: options?.isActive ?? true,
      effectiveFrom: options?.effectiveFrom,
      effectiveTo: options?.effectiveTo,
    };
  },

  /**
   * Creates audio usage filters with sensible defaults
   */
  createUsageFilters(options?: {
    startDate?: string;
    endDate?: string;
    virtualKey?: string;
    provider?: string;
    operationType?: string;
    page?: number;
    pageSize?: number;
  }): AudioUsageFilters {
    return {
      startDate: options?.startDate,
      endDate: options?.endDate,
      virtualKey: options?.virtualKey,
      provider: options?.provider,
      operationType: options?.operationType,
      page: options?.page ?? 1,
      pageSize: options?.pageSize ?? 50,
    };
  },

  /**
   * Creates date range for common periods
   */
  createDateRange(period: 'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'): {
    startDate: string;
    endDate: string;
  } {
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    
    switch (period) {
      case 'today':
        return {
          startDate: today.toISOString(),
          endDate: now.toISOString(),
        };
      
      case 'yesterday': {
        const yesterday = new Date(today);
        yesterday.setDate(yesterday.getDate() - 1);
        return {
          startDate: yesterday.toISOString(),
          endDate: today.toISOString(),
        };
      }
      
      case 'last7days': {
        const sevenDaysAgo = new Date(today);
        sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);
        return {
          startDate: sevenDaysAgo.toISOString(),
          endDate: now.toISOString(),
        };
      }
      
      case 'last30days': {
        const thirtyDaysAgo = new Date(today);
        thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
        return {
          startDate: thirtyDaysAgo.toISOString(),
          endDate: now.toISOString(),
        };
      }
      
      case 'thisMonth': {
        const monthStart = new Date(now.getFullYear(), now.getMonth(), 1);
        return {
          startDate: monthStart.toISOString(),
          endDate: now.toISOString(),
        };
      }
      
      default:
        throw new Error(`Unknown period: ${period}`);
    }
  },
};