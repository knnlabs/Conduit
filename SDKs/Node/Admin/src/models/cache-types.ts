import type { ConfigValue } from './common-types';

// Extended Cache types
export interface CacheConfigDto {
  enabled: boolean;
  strategy: 'lru' | 'lfu' | 'ttl' | 'adaptive';
  maxSizeBytes: number;
  defaultTtlSeconds: number;
  rules: CacheRule[];
  redis?: {
    enabled: boolean;
    endpoint: string;
    cluster: boolean;
  };
}

export interface UpdateCacheConfigDto {
  enabled?: boolean;
  strategy?: 'lru' | 'lfu' | 'ttl' | 'adaptive';
  maxSizeBytes?: number;
  defaultTtlSeconds?: number;
  rules?: CacheRule[];
  redis?: {
    enabled?: boolean;
    endpoint?: string;
    cluster?: boolean;
  };
}

export interface CacheRule {
  id: string;
  pattern: string;
  ttlSeconds: number;
  maxSizeBytes?: number;
  conditions?: CacheCondition[];
}

export interface CacheCondition {
  type: 'header' | 'query' | 'body' | 'time';
  field: string;
  operator: 'equals' | 'contains' | 'regex' | 'exists';
  value?: ConfigValue;
}

export interface CacheClearParams {
  pattern?: string;
  region?: string;
  type?: 'all' | 'expired' | 'pattern';
  force?: boolean;
}

export interface CacheClearResult {
  success: boolean;
  clearedCount: number;
  clearedSizeBytes: number;
  errors?: string[];
}

export interface CacheStatsDto {
  hitRate: number;
  missRate: number;
  evictionRate: number;
  totalRequests: number;
  totalHits: number;
  totalMisses: number;
  currentSizeBytes: number;
  maxSizeBytes: number;
  itemCount: number;
  topKeys: CacheKeyStats[];
}

export interface CacheKeyStats {
  key: string;
  hits: number;
  misses: number;
  sizeBytes: number;
  ttlSeconds: number;
  lastAccessed: string;
}