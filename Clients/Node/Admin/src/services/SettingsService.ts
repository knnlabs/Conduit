import { BaseApiClient } from '../client/BaseApiClient';
import { ENDPOINTS, CACHE_TTL } from '../constants';
import {
  GlobalSettingDto,
  CreateGlobalSettingDto,
  UpdateGlobalSettingDto,
  AudioConfigurationDto,
  CreateAudioConfigurationDto,
  UpdateAudioConfigurationDto,
  RouterConfigurationDto,
  UpdateRouterConfigurationDto,
  SystemConfiguration,
  SettingFilters,
} from '../models/settings';
import { ValidationError, NotImplementedError } from '../utils/errors';
import { z } from 'zod';

const createSettingSchema = z.object({
  key: z.string().min(1).regex(/^[A-Z_][A-Z0-9_]*$/, 'Key must be uppercase with underscores'),
  value: z.string(),
  description: z.string().optional(),
  dataType: z.enum(['string', 'number', 'boolean', 'json']).optional(),
  category: z.string().optional(),
  isSecret: z.boolean().optional(),
});

const audioConfigSchema = z.object({
  provider: z.string().min(1),
  isEnabled: z.boolean().optional(),
  apiKey: z.string().optional(),
  apiEndpoint: z.string().url().optional(),
  defaultVoice: z.string().optional(),
  defaultModel: z.string().optional(),
  maxDuration: z.number().positive().optional(),
  allowedVoices: z.array(z.string()).optional(),
  customSettings: z.record(z.any()).optional(),
});

export class SettingsService extends BaseApiClient {
  // Global Settings
  async getGlobalSettings(filters?: SettingFilters): Promise<GlobalSettingDto[]> {
    const params = filters
      ? {
          category: filters.category,
          dataType: filters.dataType,
          isSecret: filters.isSecret,
          search: filters.searchKey,
        }
      : undefined;

    const cacheKey = this.getCacheKey('global-settings', params);
    return this.withCache(
      cacheKey,
      () => super.get<GlobalSettingDto[]>(ENDPOINTS.SETTINGS.GLOBAL, params),
      CACHE_TTL.MEDIUM
    );
  }

  async getGlobalSetting(key: string): Promise<GlobalSettingDto> {
    const cacheKey = this.getCacheKey('global-setting', key);
    return this.withCache(
      cacheKey,
      () => super.get<GlobalSettingDto>(ENDPOINTS.SETTINGS.GLOBAL_BY_KEY(key)),
      CACHE_TTL.MEDIUM
    );
  }

  async createGlobalSetting(request: CreateGlobalSettingDto): Promise<GlobalSettingDto> {
    try {
      createSettingSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid global setting request', error);
    }

    const response = await this.post<GlobalSettingDto>(
      ENDPOINTS.SETTINGS.GLOBAL,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async updateGlobalSetting(key: string, request: UpdateGlobalSettingDto): Promise<void> {
    await this.put(ENDPOINTS.SETTINGS.GLOBAL_BY_KEY(key), request);
    await this.invalidateCache();
  }

  async deleteGlobalSetting(key: string): Promise<void> {
    await this.delete(ENDPOINTS.SETTINGS.GLOBAL_BY_KEY(key));
    await this.invalidateCache();
  }

  // Audio Configuration
  async getAudioConfigurations(): Promise<AudioConfigurationDto[]> {
    const cacheKey = 'audio-configurations';
    return this.withCache(
      cacheKey,
      () => super.get<AudioConfigurationDto[]>(ENDPOINTS.SETTINGS.AUDIO),
      CACHE_TTL.MEDIUM
    );
  }

  async getAudioConfiguration(provider: string): Promise<AudioConfigurationDto> {
    const cacheKey = this.getCacheKey('audio-config', provider);
    return this.withCache(
      cacheKey,
      () => super.get<AudioConfigurationDto>(ENDPOINTS.SETTINGS.AUDIO_BY_PROVIDER(provider)),
      CACHE_TTL.MEDIUM
    );
  }

  async createAudioConfiguration(
    request: CreateAudioConfigurationDto
  ): Promise<AudioConfigurationDto> {
    try {
      audioConfigSchema.parse(request);
    } catch (error) {
      throw new ValidationError('Invalid audio configuration request', error);
    }

    const response = await this.post<AudioConfigurationDto>(
      ENDPOINTS.SETTINGS.AUDIO,
      request
    );

    await this.invalidateCache();
    return response;
  }

  async updateAudioConfiguration(
    provider: string,
    request: UpdateAudioConfigurationDto
  ): Promise<void> {
    await this.put(ENDPOINTS.SETTINGS.AUDIO_BY_PROVIDER(provider), request);
    await this.invalidateCache();
  }

  async deleteAudioConfiguration(provider: string): Promise<void> {
    await this.delete(ENDPOINTS.SETTINGS.AUDIO_BY_PROVIDER(provider));
    await this.invalidateCache();
  }

  // Router Configuration
  async getRouterConfiguration(): Promise<RouterConfigurationDto> {
    const cacheKey = 'router-configuration';
    return this.withCache(
      cacheKey,
      () => super.get<RouterConfigurationDto>(ENDPOINTS.SETTINGS.ROUTER),
      CACHE_TTL.SHORT
    );
  }

  async updateRouterConfiguration(request: UpdateRouterConfigurationDto): Promise<void> {
    await this.put(ENDPOINTS.SETTINGS.ROUTER, request);
    await this.invalidateCache();
  }

  // Convenience methods
  async getSetting(key: string): Promise<string> {
    const setting = await this.getGlobalSetting(key);
    return setting.value;
  }

  async setSetting(key: string, value: string, options?: {
    description?: string;
    dataType?: 'string' | 'number' | 'boolean' | 'json';
    category?: string;
    isSecret?: boolean;
  }): Promise<void> {
    try {
      await this.getGlobalSetting(key);
      // Setting exists, update it
      await this.updateGlobalSetting(key, { value });
    } catch (error) {
      // Setting doesn't exist, create it
      await this.createGlobalSetting({
        key,
        value,
        ...options,
      });
    }
  }

  async getSettingsByCategory(category: string): Promise<GlobalSettingDto[]> {
    const settings = await this.getGlobalSettings({ category });
    return settings;
  }

  async updateCategory(category: string, updates: Record<string, string>): Promise<void> {
    // Get all settings in the category
    const settings = await this.getSettingsByCategory(category);
    
    // Update each setting that has a new value
    const updatePromises = settings
      .filter(setting => updates.hasOwnProperty(setting.key))
      .map(setting => this.updateGlobalSetting(setting.key, { value: updates[setting.key] }));
    
    await Promise.all(updatePromises);
  }

  async update(key: string, value: string): Promise<void> {
    await this.updateGlobalSetting(key, { value });
  }

  async set(key: string, value: string, options?: {
    description?: string;
    dataType?: 'string' | 'number' | 'boolean' | 'json';
    category?: string;
    isSecret?: boolean;
  }): Promise<void> {
    await this.setSetting(key, value, options);
  }

  // Stub methods
  async getSystemConfiguration(): Promise<SystemConfiguration> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'getSystemConfiguration requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/settings/system-configuration'
    );
  }

  async exportSettings(_format: 'json' | 'env'): Promise<Blob> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'exportSettings requires Admin API endpoint implementation. ' +
        'Consider implementing GET /api/settings/export'
    );
  }

  async importSettings(_file: File | Blob, _format: 'json' | 'env'): Promise<{
    imported: number;
    skipped: number;
    errors: string[];
  }> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'importSettings requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/settings/import'
    );
  }

  async validateConfiguration(): Promise<{
    isValid: boolean;
    errors: string[];
    warnings: string[];
  }> {
    // STUB: This endpoint needs to be implemented in the Admin API
    throw new NotImplementedError(
      'validateConfiguration requires Admin API endpoint implementation. ' +
        'Consider implementing POST /api/settings/validate'
    );
  }

  private async invalidateCache(): Promise<void> {
    if (!this.cache) return;
    await this.cache.clear();
  }
}