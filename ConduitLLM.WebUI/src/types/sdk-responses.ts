/**
 * TypeScript type definitions for Conduit SDK responses
 * These types ensure type safety when working with SDK data
 */

// Base response types
export interface PaginatedResponse<T> {
  data: T[];
  page: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
}

export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
  errors?: string[];
}

// Virtual Key types
export interface VirtualKey {
  id: number;
  name: string;
  key: string;
  isActive: boolean;
  budget: number;
  currentSpend: number;
  budgetPeriod: 'daily' | 'monthly' | 'total';
  allowedModels: string[] | null;
  allowedProviders: string[] | null;
  expirationDate: string | null;
  createdDate: string;
  modifiedDate: string;
  lastUsedDate: string | null;
  metadata: Record<string, any> | null;
}

export interface VirtualKeyValidationResult {
  isValid: boolean;
  virtualKeyId?: number;
  reason?: string;
  remainingBudget?: number;
  isModelAllowed?: boolean;
}

export interface CreateVirtualKeyRequest {
  name: string;
  budget?: number;
  budgetPeriod?: 'daily' | 'monthly' | 'total';
  allowedModels?: string[];
  allowedProviders?: string[];
  expirationDate?: string;
  metadata?: Record<string, any>;
}

export interface CreateVirtualKeyResponse {
  virtualKey: VirtualKey;
  key: string;
}

// Provider types
export interface Provider {
  id: string;
  name: string;
  type: string;
  isEnabled: boolean;
  endpoint?: string;
  supportedModels: string[];
  configuration: Record<string, any>;
  createdDate: string;
  modifiedDate: string;
}

export interface ProviderHealth {
  providerId: string;
  providerName: string;
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown';
  responseTime: number;
  uptime: number;
  errorRate: number;
  lastChecked: string;
  lastError?: string;
  incidents: ProviderIncident[];
}

export interface ProviderIncident {
  id: string;
  providerId: string;
  startTime: string;
  endTime?: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  description: string;
  affectedModels: string[];
  status: 'active' | 'resolved';
}

export interface ProviderHealthSummary {
  totalProviders: number;
  healthyProviders: number;
  degradedProviders: number;
  unhealthyProviders: number;
  averageResponseTime: number;
  averageUptime: number;
  providers: ProviderHealth[];
}

// System types
export interface SystemHealth {
  status: 'healthy' | 'degraded' | 'unhealthy';
  version: string;
  uptime: number;
  services: ServiceHealth[];
  dependencies: DependencyHealth[];
  timestamp: string;
}

export interface ServiceHealth {
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  version?: string;
  uptime?: number;
  lastCheck: string;
  message?: string;
}

export interface DependencyHealth {
  name: string;
  type: 'database' | 'cache' | 'queue' | 'external';
  status: 'connected' | 'disconnected' | 'error';
  latency?: number;
  lastCheck: string;
  error?: string;
}

export interface SystemInfo {
  version: string;
  environment: string;
  buildDate: string;
  gitCommit?: string;
  features: SystemFeature[];
  configuration: SystemConfiguration;
}

export interface SystemFeature {
  name: string;
  enabled: boolean;
  version?: string;
}

export interface SystemConfiguration {
  maxRequestSize: number;
  requestTimeout: number;
  rateLimiting: {
    enabled: boolean;
    requestsPerMinute?: number;
  };
  caching: {
    enabled: boolean;
    provider?: string;
  };
}

// Analytics types
export interface UsageAnalytics {
  startDate: string;
  endDate: string;
  totalRequests: number;
  totalCost: number;
  totalTokens: {
    input: number;
    output: number;
    total: number;
  };
  successRate: number;
  averageLatency: number;
  requestsByModel: ModelUsage[];
  requestsByProvider: ProviderUsage[];
  dailyUsage: DailyUsage[];
}

export interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  cost: number;
  tokens: {
    input: number;
    output: number;
  };
  averageLatency: number;
  errorRate: number;
}

export interface ProviderUsage {
  provider: string;
  requests: number;
  cost: number;
  successRate: number;
  averageLatency: number;
  models: string[];
}

export interface DailyUsage {
  date: string;
  requests: number;
  cost: number;
  tokens: number;
  successRate: number;
}

// Cost Dashboard types
export interface CostDashboard {
  timeframe: string;
  startDate: string;
  endDate: string;
  totalCost: number;
  totalRequests: number;
  averageCostPerRequest: number;
  costByProvider: ProviderCost[];
  costByModel: ModelCost[];
  costByVirtualKey: VirtualKeyCost[];
  costTrend: CostTrendPoint[];
}

export interface ProviderCost {
  provider: string;
  totalCost: number;
  requestCount: number;
  percentageOfTotal: number;
}

export interface ModelCost {
  model: string;
  provider: string;
  totalCost: number;
  requestCount: number;
  averageCostPerRequest: number;
}

export interface VirtualKeyCost {
  virtualKeyId: number;
  virtualKeyName: string;
  totalCost: number;
  requestCount: number;
  budgetUsed: number;
  remainingBudget: number;
}

export interface CostTrendPoint {
  date: string;
  totalCost: number;
  requestCount: number;
  providers: Record<string, number>;
}

// Audio Usage types
export interface AudioUsageSummary {
  startDate: string;
  endDate: string;
  totalRequests: number;
  totalCost: number;
  totalDuration: number; // in seconds
  averageLatency: number; // in ms
  transcriptionGrowth?: number;
  ttsGrowth?: number;
  costGrowth?: number;
  topModels: AudioModelUsage[];
  dailyUsage: AudioDailyUsage[];
  modelUsage?: AudioModelUsage[];
  languageDistribution?: LanguageUsage[];
  modelPerformance?: AudioModelPerformance[];
}

export interface AudioModelUsage {
  model: string;
  requests: number;
  cost: number;
  duration?: number;
  percentage?: number;
}

export interface AudioDailyUsage {
  date: string;
  requests: number;
  cost: number;
  transcriptions?: number;
  ttsGenerations?: number;
  totalMinutes?: number;
}

export interface LanguageUsage {
  language: string;
  count: number;
  percentage: number;
}

export interface AudioModelPerformance {
  model: string;
  requests: number;
  minutesProcessed: number;
  avgProcessingTime?: string;
  successRate?: number;
  totalCost: number;
  costPerMinute: number;
}

// Media types
export interface MediaRecord {
  id: string;
  virtualKeyId: number;
  mediaType: string;
  mediaUrl: string;
  thumbnailUrl?: string;
  cdnUrl?: string;
  storageKey: string;
  size: number;
  createdDate: string;
  expirationDate?: string;
  metadata?: {
    model?: string;
    prompt?: string;
    width?: number;
    height?: number;
    duration?: number; // for videos
    format?: string;
  };
}

export interface MediaStorageStats {
  virtualKeyId: number;
  totalSize: number;
  totalCount: number;
  imageCount: number;
  videoCount: number;
  oldestMedia?: string;
  newestMedia?: string;
}

export interface OverallMediaStorageStats extends MediaStorageStats {
  byVirtualKey: MediaStorageStats[];
  byProvider: Record<string, number>;
  byType: Record<string, number>;
}

// Security types
export interface SecurityEvent {
  timestamp: string;
  type: 'auth_failure' | 'rate_limit' | 'blocked_ip' | 'suspicious_activity';
  severity: 'low' | 'medium' | 'high' | 'critical';
  source: string;
  virtualKeyId?: string;
  details: string;
  statusCode?: number;
}

export interface SecurityEventsSummary {
  timestamp: string;
  timeRange: {
    start: string;
    end: string;
  };
  totalEvents: number;
  eventsByType: Array<{
    type: string;
    count: number;
  }>;
  eventsBySeverity: Array<{
    severity: string;
    count: number;
  }>;
  events: SecurityEvent[];
}

export interface ThreatAnalytics {
  timestamp: string;
  metrics: {
    totalThreatsToday: number;
    uniqueThreatsToday: number;
    blockedIPs: number;
    complianceScore: number;
  };
  topThreats: Array<{
    ipAddress: string;
    totalFailures: number;
    daysActive: number;
    lastSeen: string;
    riskScore: number;
  }>;
  threatDistribution: Array<{
    type: string;
    count: number;
    uniqueIPs: number;
  }>;
  threatTrend: Array<{
    date: string;
    threats: number;
  }>;
}

// Request Log types
export interface RequestLog {
  id: string;
  timestamp: string;
  virtualKeyId: number;
  virtualKeyName?: string;
  provider: string;
  model: string;
  endpoint: string;
  method: string;
  statusCode: number;
  latency: number;
  inputTokens?: number;
  outputTokens?: number;
  cost?: number;
  error?: string;
  clientIp?: string;
  userAgent?: string;
}

// Model Mapping types
export interface ModelMapping {
  id: string;
  sourceModel: string;
  targetProvider: string;
  targetModel: string;
  isActive: boolean;
  priority: number;
  metadata?: Record<string, any>;
  createdDate: string;
  modifiedDate: string;
}

// Export all types
export type * from './sdk-responses';