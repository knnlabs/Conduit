export interface CacheConfig {
  id: string;
  name: string;
  type: 'redis' | 'memory' | 'distributed';
  enabled: boolean;
  ttl: number;
  maxSize: number;
  evictionPolicy: 'lru' | 'lfu' | 'ttl' | 'random';
  compression: boolean;
  persistent: boolean;
}

export interface CacheStats {
  hits: number;
  misses: number;
  evictions: number;
  size: number;
  entries: number;
  hitRate: number;
  avgLatency: number;
}

export interface CacheDataResponse {
  configs: CacheConfig[];
  stats: Record<string, CacheStats>;
}

export interface CacheEntry {
  key: string;
  size: string;
  createdAt: string;
  lastAccessedAt: string;
  expiresAt: string;
  accessCount: number;
  priority: number;
}

export interface CacheEntriesResponse {
  regionId: string;
  entries: CacheEntry[];
  totalCount: number;
  skip: number;
  take: number;
  message?: string;
}

export interface MonitoringStatus {
  lastCheck: string;
  isHealthy: boolean;
  currentHitRate: number;
  currentMemoryUsagePercent: number;
  currentEvictionRate: number;
  currentResponseTimeMs: number;
  activeAlerts: number;
  details: Record<string, unknown>;
}

export interface AlertThresholds {
  minHitRate: number;
  maxMemoryUsage: number;
  maxEvictionRate: number;
  maxResponseTimeMs: number;
  minRequestsForHitRateAlert: number;
}

export interface CacheAlert {
  alertType: string;
  message: string;
  severity: 'info' | 'warning' | 'error' | 'critical';
  region?: string;
  details?: Record<string, unknown>;
  timestamp: string;
}

export interface AlertDefinition {
  type: string;
  name: string;
  defaultSeverity: string;
  description: string;
  recommendedActions: string[];
  notificationEnabled: boolean;
  cooldownPeriodMinutes: number;
}

export interface HealthSummary {
  overallHealth: string;
  hitRate: number;
  memoryUsagePercent: number;
  responseTimeMs: number;
  evictionRate: number;
  activeAlerts: number;
  totalCacheSize: number;
  totalEntries: number;
  lastCheck: string;
  recentAlerts: CacheAlert[];
}

export interface CacheClearResponse {
  message?: string;
  success: boolean;
  clearedCount?: number;
}