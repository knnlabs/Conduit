import { DateRange } from './common';

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
  metrics: {
    cost?: number;
    tokens?: number;
    latency?: number;
    errorRate?: number;
    [key: string]: number | undefined;
  };
  resolved: boolean;
}

export interface TimeSeriesDataPoint {
  timestamp: string;
  requests: number;
  cost: number;
  tokens: number;
}

export interface ProviderUsageBreakdown {
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
  percentage: number;
}

export interface ModelUsageBreakdown {
  model: string;
  provider: string;
  requests: number;
  cost: number;
  tokens: number;
}

export interface VirtualKeyUsageBreakdown {
  keyName: string;
  requests: number;
  cost: number;
  tokens: number;
  lastUsed: string;
}

export interface EndpointUsageBreakdown {
  endpoint: string;
  requests: number;
  avgDuration: number;
  errorRate: number;
}