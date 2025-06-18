import { FilterOptions, DateRange } from './common';

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