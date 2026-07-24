import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { HttpMethod } from '../client/HttpMethod';
import type {
  GlobalSettingDto,
  CreateGlobalSettingDto,
  UpdateGlobalSettingByKeyDto,
  GlobalSettingCacheStats,
  GlobalSettingDefinitionDto,
  GlobalSettingsReloadAcceptedResponse,
  SettingCategory,
} from '../models/settings';

// Define the batch update types that match the issue requirements
export interface SettingUpdate {
  key: string;
  value: unknown;
}

export interface SettingsDto {
  settings: GlobalSettingDto[];
  categories: string[];
  lastModified: string;
}

/**
 * Type-safe Settings service using the generated Admin contract.
 */
export class FetchSettingsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all global settings
   */
  async getGlobalSettings(config?: RequestConfig): Promise<SettingsDto> {
    const readPage = (page: number) => this.client['executeContractRead'](
      '/v1/admin/global-settings',
      (contractClient, options) => contractClient.GET('/v1/admin/global-settings', {
        ...options,
        params: { query: { page, pageSize: 100 } },
      }),
      config,
    );
    const firstPage = await readPage(1);
    const settings = [...firstPage.data];
    for (let page = 2; page <= firstPage.pagination.totalPages; page++) {
      settings.push(...(await readPage(page)).data);
    }

    // The API does not return category metadata, so no categories can be derived.
    const categories: string[] = [];

    // Find the most recent update
    const lastModified = settings
      .map(s => s.updatedAt)
      .sort((a, b) => new Date(b).getTime() - new Date(a).getTime())[0] || new Date().toISOString();

    return {
      settings,
      categories,
      lastModified,
    };
  }

  /** Gets the server-owned registry used to render and validate typed settings. */
  async getDefinitions(config?: RequestConfig): Promise<GlobalSettingDefinitionDto[]> {
    const result = await this.client['executeContractRead'](
      '/v1/admin/global-settings/definitions',
      (contractClient, options) => contractClient.GET('/v1/admin/global-settings/definitions', {
        ...options,
        params: { query: { page: 1, pageSize: 100 } },
      }),
      config,
    );
    return result.data as GlobalSettingDefinitionDto[];
  }

  /**
   * Get a specific setting by key
   */
  async getGlobalSetting(key: string, config?: RequestConfig): Promise<GlobalSettingDto> {
    return this.client['executeContractRead'](
      `/v1/admin/global-settings/by-key/${encodeURIComponent(key)}`,
      (contractClient, options) => contractClient.GET('/v1/admin/global-settings/by-key/{key}', {
        ...options,
        params: { path: { key } },
      }),
      config,
    );
  }

  /**
   * Create a new global setting
   */
  async createGlobalSetting(
    data: CreateGlobalSettingDto,
    config?: RequestConfig
  ): Promise<GlobalSettingDto> {
    return this.client['executeContractOperation']<GlobalSettingDto, CreateGlobalSettingDto>(
      '/v1/admin/global-settings',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/v1/admin/global-settings', {
        ...options,
        body: data,
      }),
      config,
      data,
    );
  }

  /**
   * Update a specific setting by key
   */
  async updateGlobalSetting(
    key: string,
    value: string,
    description?: string,
    config?: RequestConfig
  ): Promise<void> {
    const data: UpdateGlobalSettingByKeyDto = {
      key,
      value,
      description,
    };

    return this.client['executeContractOperation']<void, UpdateGlobalSettingByKeyDto>(
      '/v1/admin/global-settings/by-key',
      HttpMethod.PUT,
      (contractClient, options) => contractClient.PUT('/v1/admin/global-settings/by-key', {
        ...options,
        body: data,
      }),
      config,
      data,
    );
  }

  /**
   * Delete a global setting
   */
  async deleteGlobalSetting(key: string, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation']<void>(
      `/v1/admin/global-settings/by-key/${encodeURIComponent(key)}`,
      HttpMethod.DELETE,
      (contractClient, options) => contractClient.DELETE('/v1/admin/global-settings/by-key/{key}', {
        ...options,
        params: { path: { key } },
      }),
      config,
    );
  }


  /**
   * Get settings grouped by category
   */
  async getSettingsByCategory(config?: RequestConfig): Promise<SettingCategory[]> {
    const allSettings = await this.getGlobalSettings(config);

    // Group settings by category
    const categoryMap = new Map<string, GlobalSettingDto[]>();

    for (const setting of allSettings.settings) {
      // The API does not return category metadata, so all settings are ungrouped.
      const category = 'General';
      if (!categoryMap.has(category)) {
        categoryMap.set(category, []);
      }
      const categorySettings = categoryMap.get(category);
      if (categorySettings) {
        categorySettings.push(setting);
      }
    }

    // Convert to array of SettingCategory
    const categories: SettingCategory[] = [];
    for (const [name, settings] of categoryMap) {
      categories.push({
        name,
        description: `${name} settings`,
        settings,
      });
    }

    return categories;
  }

  /**
   * Helper method to check if a setting exists
   */
  async settingExists(key: string, config?: RequestConfig): Promise<boolean> {
    try {
      await this.getGlobalSetting(key, config);
      return true;
    } catch (error) {
      if (error && typeof error === 'object' && 'statusCode' in error && error.statusCode === 404) {
        return false;
      }
      throw error;
    }
  }

  /**
   * Helper method to get typed setting value
   */
  async getTypedSettingValue<T = unknown>(key: string, config?: RequestConfig): Promise<T> {
    const setting = await this.getGlobalSetting(key, config);

    // The API stores values as opaque strings with no data-type metadata. Callers that
    // know the expected shape should parse the returned string themselves.
    return setting.value as T;
  }

  /**
   * Helper method to update setting with type conversion
   */
  async updateTypedSetting<T>(
    key: string,
    value: T,
    description?: string,
    config?: RequestConfig
  ): Promise<void> {
    let stringValue: string;

    if (typeof value === 'object') {
      stringValue = JSON.stringify(value);
    } else {
      stringValue = String(value);
    }

    await this.updateGlobalSetting(
      key,
      stringValue,
      description,
      config
    );
  }

  /**
   * Helper method to get all secret settings (with values hidden)
   */
  async getSecretSettings(config?: RequestConfig): Promise<GlobalSettingDto[]> {
    void config;
    // The API does not flag settings as secret, so none can be identified as such.
    return Promise.resolve([]);
  }

  /**
   * Helper method to validate setting value based on data type
   */
  validateSettingValue(value: string, dataType: string): boolean {
    switch (dataType) {
      case 'number':
        return !isNaN(parseFloat(value));
      case 'boolean':
        return value.toLowerCase() === 'true' || value.toLowerCase() === 'false';
      case 'json':
        try {
          JSON.parse(value);
          return true;
        } catch {
          return false;
        }
      default:
        return true;
    }
  }

  /**
   * Helper method to format setting value for display
   */
  formatSettingValue(setting: GlobalSettingDto): string {
    // The API provides no data-type metadata; pretty-print values that happen to be JSON.
    const trimmed = setting.value.trim();
    if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
      try {
        return JSON.stringify(JSON.parse(setting.value), null, 2);
      } catch {
        return setting.value;
      }
    }
    return setting.value;
  }

  /**
   * Get global settings cache statistics
   */
  async getCacheStats(config?: RequestConfig): Promise<GlobalSettingCacheStats> {
    return this.client['executeContractRead'](
      '/v1/admin/global-settings/cache/stats',
      (contractClient, options) => contractClient.GET('/v1/admin/global-settings/cache/stats', options),
      config,
    );
  }

  /**
   * Reload all global settings from database into cache
   */
  async reloadCache(config?: RequestConfig): Promise<GlobalSettingsReloadAcceptedResponse> {
    return this.client['executeContractOperation']<GlobalSettingsReloadAcceptedResponse>(
      '/v1/admin/global-settings/cache/reload',
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/v1/admin/global-settings/cache/reload', options),
      config,
    );
  }

  /**
   * Invalidate a specific cached setting
   */
  async invalidateSetting(key: string, config?: RequestConfig): Promise<void> {
    return this.client['executeContractOperation']<void>(
      `/v1/admin/global-settings/cache/invalidate/${encodeURIComponent(key)}`,
      HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/v1/admin/global-settings/cache/invalidate/{key}', {
        ...options,
        params: { path: { key } },
      }),
      config,
    );
  }
}
