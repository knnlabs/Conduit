import { FilterOptions, DateRange } from './common';

// Additional types for comprehensive analytics service
export interface RequestLogParams {
  page?: number;
  pageSize?: number;
  startDate?: string;
  endDate?: string;
  virtualKeyId?: string;
  provider?: string;
  model?: string;
  statusCode?: number;
  minLatency?: number;
  maxLatency?: number;
  sortBy?: 'timestamp' | 'latency' | 'cost' | 'tokens';
  sortOrder?: 'asc' | 'desc';
}

export interface RequestLogPage {
  items: RequestLogDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface UsageParams {
  startDate?: string;
  endDate?: string;
  groupBy?: 'hour' | 'day' | 'week' | 'month';
  virtualKeyIds?: string[];
  providers?: string[];
  models?: string[];
}

export interface UsageAnalytics {
  summary: {
    totalRequests: number;
    totalTokens: number;
    totalCost: number;
    averageLatency: number;
    successRate: number;
  };
  byProvider: Record<string, ProviderUsage>;
  byVirtualKey: Record<string, VirtualKeyUsage>;
  byModel: Record<string, ModelUsage>;
  timeSeries: TimeSeriesData[];
  timeRange: {
    start: string;
    end: string;
  };
}

export interface ProviderUsage {
  provider: string;
  requests: number;
  tokens: number;
  cost: number;
  averageLatency: number;
  successRate: number;
}

export interface VirtualKeyUsage {
  keyId: string;
  keyName: string;
  requests: number;
  tokens: number;
  cost: number;
  averageLatency: number;
}

export interface ModelUsage {
  model: string;
  provider: string;
  requests: number;
  tokens: number;
  cost: number;
  averageLatency: number;
}

export interface TimeSeriesData {
  timestamp: string;
  requests: number;
  tokens: number;
  cost: number;
  averageLatency: number;
  successRate: number;
}

export interface VirtualKeyParams {
  startDate?: string;
  endDate?: string;
  virtualKeyIds?: string[];
  groupBy?: 'hour' | 'day' | 'week' | 'month';
}

export interface VirtualKeyAnalytics {
  virtualKeys: VirtualKeyUsageSummary[];
  topUsers: {
    byRequests: VirtualKeyRanking[];
    byCost: VirtualKeyRanking[];
    byTokens: VirtualKeyRanking[];
  };
  trends: {
    daily: TrendData[];
    weekly: TrendData[];
    monthly: TrendData[];
  };
}

export interface VirtualKeyUsageSummary {
  keyId: string;
  keyName: string;
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  averageRequestsPerDay: number;
  budgetUsed: number;
  budgetRemaining: number;
  lastUsed: string;
}

export interface VirtualKeyRanking {
  keyId: string;
  keyName: string;
  value: number;
  percentage: number;
}

export interface TrendData {
  period: string;
  value: number;
  change: number;
  changePercentage: number;
}

export interface ModelUsageParams {
  startDate?: string;
  endDate?: string;
  models?: string[];
  providers?: string[];
}

export interface ModelUsageAnalytics {
  models: ModelUsageSummary[];
  capabilities: CapabilityUsage[];
  performance: ModelPerformanceMetrics[];
}

export interface ModelUsageSummary {
  model: string;
  provider: string;
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  averageLatency: number;
  successRate: number;
  popularEndpoints: string[];
}

export interface CapabilityUsage {
  capability: string;
  requests: number;
  percentage: number;
  models: string[];
}

export interface ModelPerformanceMetrics {
  model: string;
  provider: string;
  averageLatency: number;
  p50Latency: number;
  p95Latency: number;
  p99Latency: number;
  successRate: number;
  errorRate: number;
  timeoutRate: number;
}

export interface CostParams {
  startDate?: string;
  endDate?: string;
  groupBy?: 'hour' | 'day' | 'week' | 'month';
}

export interface CostAnalytics {
  totalCost: number;
  breakdown: {
    byProvider: CostBreakdown[];
    byModel: CostBreakdown[];
    byVirtualKey: CostBreakdown[];
  };
  projections: {
    daily: number;
    weekly: number;
    monthly: number;
  };
  trends: CostTrend[];
}

export interface CostBreakdown {
  name: string;
  cost: number;
  percentage: number;
  tokens: number;
  requests: number;
}

export interface CostTrend {
  period: string;
  cost: number;
  change: number;
  changePercentage: number;
  projectedCost: number;
}

export interface ExportParams {
  format: 'csv' | 'json' | 'excel';
  startDate?: string;
  endDate?: string;
  filters?: Record<string, any>;
}

export interface ExportResult {
  url: string;
  expiresAt: string;
  size: number;
  recordCount: number;
}

export interface CostSummaryDto {
  totalCost: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  currency: string;
  period: DateRange;
  costByModel: {
    modelId: string;
    cost: number;
    inputTokens: number;
    outputTokens: number;
    requestCount: number;
  }[];
  costByKey: {
    keyId: number;
    keyName: string;
    cost: number;
    requestCount: number;
  }[];
  costByProvider: {
    providerId: string;
    providerName: string;
    cost: number;
    requestCount: number;
  }[];
}

export interface CostByPeriodDto {
  periods: {
    period: string;
    startDate: string;
    endDate: string;
    totalCost: number;
    inputTokens: number;
    outputTokens: number;
    requestCount: number;
  }[];
  totalCost: number;
  averageCostPerPeriod: number;
  trend: 'increasing' | 'decreasing' | 'stable';
  trendPercentage: number;
}

export interface RequestLogDto {
  id: string;
  timestamp: string;
  virtualKeyId?: number;
  virtualKeyName?: string;
  model: string;
  provider: string;
  inputTokens: number;
  outputTokens: number;
  cost: number;
  currency: string;
  duration: number;
  status: 'success' | 'error' | 'timeout';
  errorMessage?: string;
  ipAddress?: string;
  userAgent?: string;
  requestHeaders?: Record<string, string>;
  responseHeaders?: Record<string, string>;
  metadata?: Record<string, any>;
}

export interface RequestLogFilters extends FilterOptions {
  startDate?: string;
  endDate?: string;
  virtualKeyId?: number;
  model?: string;
  provider?: string;
  status?: 'success' | 'error' | 'timeout';
  minCost?: number;
  maxCost?: number;
  minDuration?: number;
  maxDuration?: number;
  ipAddress?: string;
}

export interface UsageMetricsDto {
  period: DateRange;
  totalRequests: number;
  successfulRequests: number;
  failedRequests: number;
  averageLatency: number;
  p95Latency: number;
  p99Latency: number;
  requestsPerMinute: number;
  peakRequestsPerMinute: number;
  uniqueKeys: number;
  uniqueModels: number;
  errorRate: number;
}

export interface ModelUsageDto {
  modelId: string;
  totalRequests: number;
  totalTokens: number;
  totalCost: number;
  averageTokensPerRequest: number;
  averageCostPerRequest: number;
  successRate: number;
  averageLatency: number;
  popularKeys: {
    keyId: number;
    keyName: string;
    requestCount: number;
  }[];
}

export interface KeyUsageDto {
  keyId: number;
  keyName: string;
  totalRequests: number;
  totalCost: number;
  budgetUsed: number;
  budgetRemaining: number;
  averageCostPerRequest: number;
  requestsPerDay: number;
  popularModels: {
    modelId: string;
    requestCount: number;
    totalCost: number;
  }[];
  lastUsed: string;
}

export interface AnalyticsFilters {
  startDate: string;
  endDate: string;
  virtualKeyIds?: number[];
  models?: string[];
  providers?: string[];
  groupBy?: 'hour' | 'day' | 'week' | 'month';
  includeMetadata?: boolean;
}

export interface CostForecastDto {
  forecastPeriod: DateRange;
  predictedCost: number;
  confidence: number;
  basedOnPeriod: DateRange;
  factors: {
    name: string;
    impact: number;
    description: string;
  }[];
  recommendations: string[];
}

export interface AnomalyDto {
  id: string;
  detectedAt: string;
  type: 'cost_spike' | 'usage_spike' | 'error_rate' | 'latency';
  severity: 'low' | 'medium' | 'high';
  description: string;
  affectedResources: {
    type: 'key' | 'model' | 'provider';
    id: string;
    name: string;
  }[];
  metrics: Record<string, any>;
  resolved: boolean;
}