import type { ConfigValue, ExtendedMetadata } from './common-types';

// Feature Flag types
export interface FeatureFlag {
  key: string;
  name: string;
  description?: string;
  enabled: boolean;
  rolloutPercentage?: number;
  conditions?: FeatureFlagCondition[];
  metadata?: ExtendedMetadata;
  lastModified: string;
}

export interface FeatureFlagCondition {
  type: 'user' | 'key' | 'environment' | 'custom';
  field: string;
  operator: 'in' | 'not_in' | 'equals' | 'regex';
  values: ConfigValue[];
}

export interface UpdateFeatureFlagDto {
  name?: string;
  description?: string;
  enabled?: boolean;
  rolloutPercentage?: number;
  conditions?: FeatureFlagCondition[];
  metadata?: ExtendedMetadata;
}