import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { ENDPOINTS } from '../constants';
import type {
  GlobalSettingDto,
  CreateGlobalSettingDto,
  UpdateGlobalSettingDto,
  SettingCategory,
} from '../models/settings';

// Define the batch update types that match the issue requirements
export interface SettingUpdate {
  key: string;
  value: any;
}

export interface SettingsListResponseDto {
  items: GlobalSettingDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SettingsDto {
  settings: GlobalSettingDto[];
  categories: string[];
  lastModified: string;
}

/**
 * Type-safe Settings service using native fetch
 */
export class FetchSettingsService {
  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Get all global settings
   */
  async getGlobalSettings(config?: RequestConfig): Promise<SettingsDto> {
    // Get all settings
    const settings = await this.client['get']<GlobalSettingDto[]>(
      ENDPOINTS.SETTINGS.GLOBAL,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );

    // Extract unique categories
    const categories = [...new Set(settings.map(s => s.category).filter(Boolean))] as string[];

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

  /**
   * Get all global settings with pagination
   */
  async listGlobalSettings(
    page: number = 1,
    pageSize: number = 100,
    config?: RequestConfig
  ): Promise<SettingsListResponseDto> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });

    return this.client['get']<SettingsListResponseDto>(
      `${ENDPOINTS.SETTINGS.GLOBAL}?${params.toString()}`,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Get a specific setting by key
   */
  async getGlobalSetting(key: string, config?: RequestConfig): Promise<GlobalSettingDto> {
    return this.client['get']<GlobalSettingDto>(
      ENDPOINTS.SETTINGS.GLOBAL_BY_KEY(key),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Create a new global setting
   */
  async createGlobalSetting(
    data: CreateGlobalSettingDto,
    config?: RequestConfig
  ): Promise<GlobalSettingDto> {
    return this.client['post']<GlobalSettingDto, CreateGlobalSettingDto>(
      ENDPOINTS.SETTINGS.GLOBAL,
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Update a specific setting
   */
  async updateGlobalSetting(
    key: string,
    data: UpdateGlobalSettingDto,
    config?: RequestConfig
  ): Promise<void> {
    return this.client['put']<void, UpdateGlobalSettingDto>(
      ENDPOINTS.SETTINGS.GLOBAL_BY_KEY(key),
      data,
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Delete a global setting
   */
  async deleteGlobalSetting(key: string, config?: RequestConfig): Promise<void> {
    return this.client['delete']<void>(
      ENDPOINTS.SETTINGS.GLOBAL_BY_KEY(key),
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
    );
  }

  /**
   * Batch update multiple settings
   */
  async batchUpdateSettings(
    settings: SettingUpdate[],
    config?: RequestConfig
  ): Promise<void> {
    return this.client['post']<void, { settings: SettingUpdate[] }>(
      ENDPOINTS.SETTINGS.BATCH_UPDATE,
      { settings },
      {
        signal: config?.signal,
        timeout: config?.timeout,
        headers: config?.headers,
      }
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
      const category = setting.category || 'General';
      if (!categoryMap.has(category)) {
        categoryMap.set(category, []);
      }
      categoryMap.get(category)!.push(setting);
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
  async getTypedSettingValue<T = any>(key: string, config?: RequestConfig): Promise<T> {
    const setting = await this.getGlobalSetting(key, config);
    
    switch (setting.dataType) {
      case 'number':
        return parseFloat(setting.value) as T;
      case 'boolean':
        return (setting.value.toLowerCase() === 'true') as T;
      case 'json':
        return JSON.parse(setting.value) as T;
      default:
        return setting.value as T;
    }
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
      { value: stringValue, description },
      config
    );
  }

  /**
   * Helper method to get all secret settings (with values hidden)
   */
  async getSecretSettings(config?: RequestConfig): Promise<GlobalSettingDto[]> {
    const allSettings = await this.getGlobalSettings(config);
    return allSettings.settings.filter(s => s.isSecret);
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
    if (setting.isSecret) {
      return '********';
    }
    
    switch (setting.dataType) {
      case 'json':
        try {
          return JSON.stringify(JSON.parse(setting.value), null, 2);
        } catch {
          return setting.value;
        }
      default:
        return setting.value;
    }
  }
}