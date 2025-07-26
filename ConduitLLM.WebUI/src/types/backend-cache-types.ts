// Backend cache configuration types (from Admin API)
export interface BackendCacheConfigurationDto {
  timestamp: string;
  cachePolicies: BackendCachePolicyDto[];
  cacheRegions: BackendCacheRegionDto[];
  statistics: BackendCacheStatisticsDto;
  configuration: BackendCacheGlobalConfigDto;
}

export interface BackendCachePolicyDto {
  id: string;
  name: string;
  type: string;
  ttl: number;
  maxSize: number;
  strategy: string;
  enabled: boolean;
  description: string;
}

export interface BackendCacheRegionDto {
  id: string;
  name: string;
  type: string;
  status: string;
  nodes: number;
  metrics: BackendCacheMetricsDto;
}

export interface BackendCacheMetricsDto {
  size: string;
  items: number;
  hitRate: number;
  missRate: number;
  evictionRate: number;
}

export interface BackendCacheStatisticsDto {
  totalHits: number;
  totalMisses: number;
  hitRate: number;
  avgResponseTime: BackendResponseTimeDto;
  memoryUsage: BackendMemoryUsageDto;
  topCachedItems: BackendTopCachedItemDto[];
}

export interface BackendResponseTimeDto {
  withCache: number;
  withoutCache: number;
}

export interface BackendMemoryUsageDto {
  current: string;
  peak: string;
  limit: string;
}

export interface BackendTopCachedItemDto {
  key: string;
  hits: number;
  size: string;
}

export interface BackendCacheGlobalConfigDto {
  defaultTTL: number;
  maxMemorySize: string;
  evictionPolicy: string;
  compressionEnabled: boolean;
  redisConnectionString?: string;
}