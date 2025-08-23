import { FilterOptions } from './common';
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