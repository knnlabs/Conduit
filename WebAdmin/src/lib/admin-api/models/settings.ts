import { FilterOptions } from './common';
import type { CustomSettings, ConfigValue } from './common-types';

// Matches the wire `GlobalSettingDto`. The API returns no category/dataType/isSecret
// metadata, so those client-only fields were removed in issue #1038.
export interface GlobalSettingDto {
  id: number;
  key: string;
  value: string;
  description?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface GlobalSettingCacheStats {
  cacheSize: number;
  cacheHits: number;
  cacheMisses: number;
  invalidations: number;
  hitRate: number;
  lastLoadTime: string;
  cachedKeys: string[];
}

export interface GlobalSettingDefinitionDto {
  key: string;
  displayName: string;
  description: string;
  type: 'string' | 'boolean' | 'integer' | 'number' | 'json';
  category: string;
  defaultValue: string;
  minimum?: number | null;
  maximum?: number | null;
  maxLength?: number | null;
  featureRoute?: string | null;
  isFeatureOwned: boolean;
}

export interface GlobalSettingsReloadAcceptedResponse {
  message: string;
  requestId: string;
  acceptedAt: string;
}

export interface CreateGlobalSettingDto {
  key: string;
  value: string;
  description?: string | null;
}

export interface UpdateGlobalSettingDto {
  id: number;
  value: string;
  description?: string | null;
}

export interface UpdateGlobalSettingByKeyDto {
  key: string;
  value: string;
  description?: string;
}

export interface SettingCategory {
  name: string;
  description: string;
  settings: GlobalSettingDto[];
}

export interface AudioConfigurationDto {
  provider: string;
  isEnabled: boolean;
  apiKey?: string;
  apiEndpoint?: string;
  defaultVoice?: string;
  defaultModel?: string;
  maxDuration?: number;
  allowedVoices?: string[];
  customSettings?: CustomSettings;
  createdAt: string;
  updatedAt: string;
}

export interface CreateAudioConfigurationDto {
  provider: string;
  isEnabled?: boolean;
  apiKey?: string;
  apiEndpoint?: string;
  defaultVoice?: string;
  defaultModel?: string;
  maxDuration?: number;
  allowedVoices?: string[];
  customSettings?: CustomSettings;
}

export interface UpdateAudioConfigurationDto {
  isEnabled?: boolean;
  apiKey?: string;
  apiEndpoint?: string;
  defaultVoice?: string;
  defaultModel?: string;
  maxDuration?: number;
  allowedVoices?: string[];
  customSettings?: CustomSettings;
}

export interface RouterConfigurationDto {
  routingStrategy: 'priority' | 'round-robin' | 'least-cost' | 'fastest' | 'random';
  fallbackEnabled: boolean;
  maxRetries: number;
  retryDelay: number;
  loadBalancingEnabled: boolean;
  healthCheckEnabled: boolean;
  healthCheckInterval: number;
  circuitBreakerEnabled: boolean;
  circuitBreakerThreshold: number;
  circuitBreakerDuration: number;
  customRules?: RouterRule[];
  createdAt: string;
  updatedAt: string;
}

export interface RouterRule {
  id?: number;
  name: string;
  condition: RouterCondition;
  action: RouterAction;
  priority: number;
  isEnabled: boolean;
}

export interface RouterCondition {
  type: 'model' | 'key' | 'metadata' | 'time' | 'cost';
  operator: 'equals' | 'contains' | 'greater_than' | 'less_than' | 'between';
  value: ConfigValue;
}

export interface RouterAction {
  type: 'route_to_provider' | 'block' | 'rate_limit' | 'add_metadata';
  value: ConfigValue;
}

export interface UpdateRouterConfigurationDto {
  routingStrategy?: 'priority' | 'round-robin' | 'least-cost' | 'fastest' | 'random';
  fallbackEnabled?: boolean;
  maxRetries?: number;
  retryDelay?: number;
  loadBalancingEnabled?: boolean;
  healthCheckEnabled?: boolean;
  healthCheckInterval?: number;
  circuitBreakerEnabled?: boolean;
  circuitBreakerThreshold?: number;
  circuitBreakerDuration?: number;
  customRules?: RouterRule[];
}

export interface SystemConfiguration {
  general: GlobalSettingDto[];
  audio: AudioConfigurationDto[];
  router: RouterConfigurationDto;
  categories: SettingCategory[];
}

export interface SettingFilters extends FilterOptions {
  category?: string;
  dataType?: string;
  isSecret?: boolean;
  searchKey?: string;
}
