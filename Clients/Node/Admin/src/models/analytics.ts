import { FilterOptions, DateRange } from './common';
import { AnalyticsMetadata } from './metadata';

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


export interface ExportParams {
  format: 'csv' | 'json' | 'excel';
  startDate?: string;
  endDate?: string;
  filters?: {
    providers?: string[];
    models?: string[];
    virtualKeyIds?: string[];
    status?: string[];
    [key: string]: string[] | undefined;
  };
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
  metadata?: AnalyticsMetadata;
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

export interface RequestLogStatisticsParams {
  startDate?: string;
  endDate?: string;
  virtualKeyId?: string;
  provider?: string;
  model?: string;
}

export interface RequestLogStatistics {
  totalRequests: number;
  successfulRequests: number;
  failedRequests: number;
  averageLatency: number;
  p95Latency: number;
  p99Latency: number;
  totalCost: number;
  totalTokens: number;
  errorRate: number;
  byStatusCode: Record<number, number>;
  byProvider: Record<string, { requests: number; cost: number; errors: number }>;
  byModel: Record<string, { requests: number; cost: number; tokens: number }>;
  hourlyDistribution: Array<{ hour: number; requests: number }>;
}


export interface ServiceHealthMetrics {
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: number; // percentage
  responseTime: number; // ms
  errorRate: number;
  lastChecked: string;
}

export interface QueueMetrics {
  name: string;
  size: number;
  processing: number;
  failed: number;
  throughput: number; // messages per second
}

export interface DatabaseMetrics {
  connections: {
    active: number;
    idle: number;
    total: number;
  };
  queryPerformance: {
    averageTime: number;
    slowQueries: number;
  };
  size: number; // bytes
}

export interface SystemAlert {
  id: string;
  severity: 'info' | 'warning' | 'error' | 'critical';
  message: string;
  timestamp: string;
  service?: string;
}


export interface ProviderHealthDetails {
  provider: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: number; // percentage
  averageLatency: number; // ms
  errorRate: number; // percentage
  lastChecked: string;
  endpoints: EndpointHealth[];
  history?: HealthHistoryPoint[];
}

export interface EndpointHealth {
  endpoint: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  responseTime: number;
  successRate: number;
  lastError?: string;
}

export interface HealthHistoryPoint {
  timestamp: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  uptime: number;
  errorRate: number;
}

export interface ProviderIncident {
  id: string;
  provider: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  status: 'active' | 'resolved';
  startTime: string;
  endTime?: string;
  description: string;
  impact: string;
}


export interface VirtualKeyDetail {
  keyId: string;
  keyName: string;
  status: 'active' | 'expired' | 'disabled';
  usage: {
    requests: number;
    requestsChange: number; // NEW: percentage change from previous period
    tokens: number;
    tokensChange: number;   // NEW: percentage change from previous period
    cost: number;
    costChange: number;     // NEW: percentage change from previous period
    lastUsed: string;
    errorRate: number;      // NEW: error rate percentage
  };
  quota: {
    limit: number;
    used: number;
    remaining: number;
    percentage: number;
    resetDate?: string;
  };
  performance: {
    averageLatency: number;
    errorRate: number;
    successRate: number;
  };
  trends: {
    dailyChange: number; // percentage
    weeklyChange: number; // percentage
  };
  endpointBreakdown: {      // NEW: endpoint usage data
    path: string;
    requests: number;
    avgDuration: number;
    errorRate: number;
  }[];
  timeSeries?: {            // NEW: per-key time series
    timestamp: string;
    requests: number;
    tokens: number;
    cost: number;
    errorRate: number;
  }[];
  tokenLimit?: number;      // NEW: token consumption limit (from metadata)
  tokenPeriod?: string;     // NEW: token limit period (from metadata)
}

export interface QuotaAlert {
  keyId: string;
  keyName: string;
  type: 'approaching_limit' | 'exceeded_limit' | 'unusual_activity';
  severity: 'info' | 'warning' | 'critical';
  message: string;
  threshold?: number;
  currentUsage?: number;
}